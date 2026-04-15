using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asp.net_10_oidc_codex_visualcode.Pages;

public class IndexModel : PageModel
{
    public bool IsAuthenticated { get; private set; }

    public IReadOnlyList<string> ProtectedData { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<ClaimItem> DebugClaims { get; private set; } = Array.Empty<ClaimItem>();

    public void OnGet()
    {
        IsAuthenticated = User.Identity?.IsAuthenticated == true;

        if (!IsAuthenticated)
        {
            return;
        }

        var subject = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "unknown";
        var email = User.FindFirst("email")?.Value ?? "not present";
        var roles = User.FindAll("role").Select(claim => claim.Value).ToArray();

        ProtectedData =
        [
            $"Subject: {subject}",
            $"Email: {email}",
            $"Roles: {(roles.Length > 0 ? string.Join(", ", roles) : "none assigned")}",
            $"Protected data rendered at (UTC): {DateTimeOffset.UtcNow:O}"
        ];

        DebugClaims = User.Claims
            .OrderBy(claim => claim.Type, StringComparer.Ordinal)
            .ThenBy(claim => claim.Value, StringComparer.Ordinal)
            .Select(claim => new ClaimItem(claim.Type, claim.Value))
            .ToArray();
    }

    public sealed record ClaimItem(string Type, string Value);
}
