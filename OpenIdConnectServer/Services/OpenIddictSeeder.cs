using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdConnectServer.Services
{
    public class OpenIddictSeeder: IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public OpenIddictSeeder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

            // ── Register custom scopes ────────────────────────────────────────────
            if (await scopeManager.FindByNameAsync("api") is null)
            {
                await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    Name = "api",
                    DisplayName = "API access",
                    Resources = { "resource_server" }  // the audience this scope grants access to
                });
            }

            // ── Register a SPA / public client (uses PKCE, no secret) ────────────
            if (await appManager.FindByClientIdAsync("spa-client") is null)
            {
                await appManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "spa-client",
                    DisplayName = "My SPA",
                    ClientType = ClientTypes.Public,   // no client_secret
                    RedirectUris =
                {
                    new Uri("https://localhost:5173/callback"),   // Vite dev
                    new Uri("https://myapp.example.com/callback")
                },
                    PostLogoutRedirectUris =
                {
                    new Uri("https://localhost:5173/"),
                    new Uri("https://myapp.example.com/")
                },
                    Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    //Permissions.Endpoints.Logout,

                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,

                    Permissions.ResponseTypes.Code,

                    //Permissions.Scopes.OpenId,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Email,
                    // Permissions.Scopes.OfflineAccess,
                    Permissions.Prefixes.Scope + "api"
                },
                    Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange  // enforce PKCE
                }
                });
            }

            // ── Register a confidential server-side client ────────────────────────
            if (await appManager.FindByClientIdAsync("mvc-client") is null)
            {
                await appManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "mvc-client",
                    ClientSecret = "mvc-client-secret-change-in-production",
                    DisplayName = "MVC Web App",
                    ClientType = ClientTypes.Confidential,
                    RedirectUris =
                {
                    new Uri("https://localhost:7001/signin-oidc")
                },
                    PostLogoutRedirectUris =
                {
                    new Uri("https://localhost:7001/signout-callback-oidc")
                },
                    Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    // Permissions.Endpoints.Logout,

                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,

                    Permissions.ResponseTypes.Code,

                    //Permissions.Scopes.OpenId,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Email,
                    // Permissions.Scopes.OfflineAccess,
                    Permissions.Prefixes.Scope + "api"
                }
                });
            }

            // ── Register a machine-to-machine client (client credentials) ─────────
            if (await appManager.FindByClientIdAsync("worker-service") is null)
            {
                await appManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "worker-service",
                    ClientSecret = "worker-secret-change-in-production",
                    DisplayName = "Background Worker",
                    ClientType = ClientTypes.Confidential,
                    Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + "api"
                }
                });
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
