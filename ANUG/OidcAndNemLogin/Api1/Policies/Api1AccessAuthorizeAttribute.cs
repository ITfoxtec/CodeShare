using ITfoxtec.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Api1.Policies
{
    public class Api1AccessAuthorizeAttribute : AuthorizeAttribute
    {
        public const string Name = nameof(Api1AccessAuthorizeAttribute);

        public Api1AccessAuthorizeAttribute() : base(Name)
        { }

        public static void AddPolicy(AuthorizationOptions options)
        {
            options.AddPolicy(Name, policy =>
            {
                policy.RequireScope("api1:read", "api1:update");
                policy.RequireRole("api1.read");
            });
        }
    }
}
