using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIdConnectServer.Entities;

namespace OpenIdConnectServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUserEntity>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // OpenIddict adds its own tables automatically via UseEntityFrameworkCore()
        }
    }
}
