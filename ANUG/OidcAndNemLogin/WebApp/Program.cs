using ITfoxtec.Identity;
using ITfoxtec.Identity.Discovery;
using ITfoxtec.Identity.Helpers;
using ITfoxtec.Identity.Util;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Globalization;
using WebApp.Identity;
using WebApp.Models;

var builder = WebApplication.CreateBuilder(args);

var identitySettings = builder.Services.BindConfig<IdentitySettings>(builder.Configuration, nameof(IdentitySettings));
builder.Services.BindConfig<AppSettings>(builder.Configuration, nameof(AppSettings));

IdentityModelEventSource.ShowPII = true; //To show detail of error and see the problem

builder.Services.AddSingleton((serviceProvider) =>
{
    var settings = serviceProvider.GetService<IdentitySettings>();
    var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

    return new OidcDiscoveryHandler(httpClientFactory, UrlCombine.Combine(settings.FoxIDsAuthority, IdentityConstants.OidcDiscovery.Path));
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.Events.OnValidatePrincipal = async (context) =>
        {
            var logoutMemoryCache = context.HttpContext.RequestServices.GetService<LogoutMemoryCache>();
            var sessionId = context.Principal.Claims.Where(c => c.Type == JwtClaimTypes.SessionId).Select(c => c.Value).FirstOrDefault();
            foreach (var item in logoutMemoryCache.List)
            {
                if (sessionId == item)
                {
                    logoutMemoryCache.Remove(item);
                    // Handle Front-Channel Logout
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }
            }

            try
            {
                var expiresUtc = DateTimeOffset.Parse(context.Properties.GetTokenValue("expires_at"));

                // Tokens expires 30 seconds before actual expiration time.
                if (expiresUtc < DateTimeOffset.UtcNow.AddSeconds(30))
                {
                    var tokenResponse = await RefreshTokenHandler.ResolveRefreshToken(context, identitySettings);

                    context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.AccessToken, tokenResponse.AccessToken);
                    context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.IdToken, tokenResponse.IdToken);
                    if (!tokenResponse.RefreshToken.IsNullOrEmpty())
                    {
                        context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.RefreshToken, tokenResponse.RefreshToken);
                    }
                    else
                    {
                        context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.RefreshToken, context.Properties.GetTokenValue(OpenIdConnectParameterNames.RefreshToken));
                    }
                    context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.TokenType, tokenResponse.TokenType);

                    var newExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn.HasValue ? tokenResponse.ExpiresIn.Value : 30);
                    context.Properties.UpdateTokenValue("expires_at", newExpiresUtc.ToString("o", CultureInfo.InvariantCulture));

                    // Cookie should be renewed.
                    context.ShouldRenew = true;
                }
            }
            catch
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        };
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = identitySettings.FoxIDsAuthority;
        options.ClientId = identitySettings.ClientId;
        options.ClientSecret = identitySettings.ClientSecret;

        options.ResponseType = OpenIdConnectResponseType.Code;

        options.SaveTokens = true;
        // False to support refresh token renewal.
        options.UseTokenLifetime = false;

        options.Scope.Add("offline_access");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("role");
        options.Scope.Add("nemlogin");
        options.Scope.Add("api1:read");
        options.Scope.Add("api1:update");

        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = JwtClaimTypes.Subject;
        options.TokenValidationParameters.RoleClaimType = JwtClaimTypes.Role;

        options.Events.OnTokenResponseReceived = async (context) =>
        {
            if (!context.TokenEndpointResponse.Error.IsNullOrEmpty())
            {
                throw new Exception($"Token response error. {context.TokenEndpointResponse.Error}, {context.TokenEndpointResponse.ErrorDescription} ");
            }
            await Task.FromResult(string.Empty);
        };
        options.Events.OnRemoteFailure = async (context) =>
        {
            if (context.Failure != null)
            {
                throw new Exception("Remote failure.", context.Failure);
            }
            await Task.FromResult(string.Empty);
        };
    });

builder.Services.AddTransient<TokenExecuteHelper>();
builder.Services.AddSingleton<LogoutMemoryCache>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
