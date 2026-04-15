using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asp.net_10_oidc_codex_visualcode.Pages.Auth;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }

    public IActionResult OnPost(string? returnUrl = null)
    {
        var localReturnUrl = ResolveReturnUrl(returnUrl);

        return SignOut(
            new AuthenticationProperties { RedirectUri = localReturnUrl },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return Url.Content("~/");
        }

        return returnUrl;
    }
}