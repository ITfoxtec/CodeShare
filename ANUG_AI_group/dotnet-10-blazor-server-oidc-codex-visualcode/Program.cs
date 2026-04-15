using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.IdentityModel.Tokens;
using asp.net_oidc_codex.Data;

var builder = WebApplication.CreateBuilder(args);
var foxIdsSection = builder.Configuration.GetSection("Authentication:FoxIDs");
var authority = foxIdsSection["Authority"] ?? string.Empty;
var clientId = foxIdsSection["ClientId"] ?? string.Empty;
var clientSecret = foxIdsSection["ClientSecret"] ?? string.Empty;

// Add services to the container.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-FoxIdsAuth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = "code";
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.SaveTokens = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.ClaimActions.MapJsonKey("role", "role");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/account/login", (string? returnUrl) =>
{
    var authenticationProperties = new AuthenticationProperties
    {
        RedirectUri = GetLocalReturnUrl(returnUrl)
    };

    return Results.Challenge(authenticationProperties, [OpenIdConnectDefaults.AuthenticationScheme]);
});

app.MapGet("/account/logout", (string? returnUrl) =>
{
    var authenticationProperties = new AuthenticationProperties
    {
        RedirectUri = GetLocalReturnUrl(returnUrl)
    };

    return Results.SignOut(
        authenticationProperties,
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static string GetLocalReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    return returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        ? returnUrl
        : "/";
}
