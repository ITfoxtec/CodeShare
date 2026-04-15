using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asp.net_10_oidc_codex_visualcode.Pages.Auth;

public class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        var localReturnUrl = ResolveReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(localReturnUrl);
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = localReturnUrl },
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