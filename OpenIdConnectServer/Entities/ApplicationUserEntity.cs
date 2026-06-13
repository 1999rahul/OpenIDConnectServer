using Microsoft.AspNetCore.Identity;

namespace OpenIdConnectServer.Entities
{
    public class ApplicationUserEntity: IdentityUser
    {
        public string? FullName { get; set; }
    }
}
