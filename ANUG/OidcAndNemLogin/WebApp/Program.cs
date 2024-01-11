using ITfoxtec.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WebApp.Models;

var builder = WebApplication.CreateBuilder(args);

var identitySettings = builder.Services.BindConfig<IdentitySettings>(builder.Configuration, nameof(IdentitySettings));
builder.Services.BindConfig<AppSettings>(builder.Configuration, nameof(AppSettings));

IdentityModelEventSource.ShowPII = true; //To show detail of error and see the problem

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddCookie()
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
