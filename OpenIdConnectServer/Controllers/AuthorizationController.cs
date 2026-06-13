using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIdConnectServer.Entities;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdConnectServer.Controllers
{
    public class AuthorizationController : Controller
    {
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly IOpenIddictAuthorizationManager _authorizationManager;
        private readonly IOpenIddictScopeManager _scopeManager;
        private readonly UserManager<ApplicationUserEntity> _userManager;

        public AuthorizationController(IOpenIddictApplicationManager applicationManager, IOpenIddictAuthorizationManager authorizationManager, IOpenIddictScopeManager scopeManager,
                                       UserManager<ApplicationUserEntity> userManager)
        {
            _applicationManager = applicationManager;
            _authorizationManager = authorizationManager;
            _scopeManager = scopeManager;
            _userManager = userManager;
        }

        // ── GET /connect/authorize ────────────────────────────────────────────────
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("OpenIddict request not found.");

            // Check if the user is already logged in
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            if (!result.Succeeded)
            {
                // Not logged in — redirect to login page, come back after
                return Challenge(
                    authenticationSchemes: IdentityConstants.ApplicationScheme,
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + Request.QueryString.Value
                    });
            }

            var user = await _userManager.GetUserAsync(result.Principal)
                ?? throw new InvalidOperationException("User not found.");

            // Build the claims identity for the tokens
            var identity = new ClaimsIdentity(authenticationType: TokenValidationParameters.DefaultAuthenticationType, nameType: Claims.Name, roleType: Claims.Role);

            // 'sub' is REQUIRED by OIDC spec
            identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user))
                    .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user))
                    .SetClaim(Claims.Name, user.FullName ?? user.UserName)
                    .SetClaim(Claims.PreferredUsername, user.UserName);

            // Add roles as claims
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                identity.AddClaim(Claims.Role, role);

            // Control which claims go into which token (access_token vs id_token)
            identity.SetDestinations(claim => claim.Type switch
            {
                // email only goes into id_token if openid + email scope was requested
                Claims.Email when identity.HasScope(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],

                Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],

                // roles go into the access token only
                Claims.Role => [Destinations.AccessToken],

                _ => [Destinations.AccessToken]
            });

            var principal = new ClaimsPrincipal(identity);

            // Store the approved scopes on the principal
            principal.SetScopes(request.GetScopes());

            // Resources = which APIs this token is valid for
            principal.SetResources(await _scopeManager.ListResourcesAsync(principal.GetScopes()).ToListAsync());

            // Sign in with the OpenIddict scheme — this completes the OIDC flow
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // ── POST /connect/token ───────────────────────────────────────────────────
        [HttpPost("~/connect/token")]
        // [IgnoreAntiForgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("OpenIddict request not found.");

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // OpenIddict has already validated the code/refresh token.
                // Retrieve the principal it stored.
                var result = await HttpContext.AuthenticateAsync(
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                var userId = result.Principal?.GetClaim(Claims.Subject)
                    ?? throw new InvalidOperationException("Sub claim missing.");

                var user = await _userManager.FindByIdAsync(userId);

                if (user is null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                                "The user no longer exists."
                        }));
                }

                // Refresh the claims (e.g. roles may have changed since the code was issued)
                var identity = new ClaimsIdentity(result.Principal!.Claims,
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    Claims.Name, Claims.Role);

                identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user))
                        .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user))
                        .SetClaim(Claims.Name, user.FullName ?? user.UserName);

                identity.SetDestinations(GetDestinations);

                return SignIn(new ClaimsPrincipal(identity),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (request.IsClientCredentialsGrantType())
            {
                // Machine-to-machine — no user involved
                var application = await _applicationManager.FindByClientIdAsync(request.ClientId!)
                    ?? throw new InvalidOperationException("Client not found.");

                var identity = new ClaimsIdentity(
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    Claims.Name, Claims.Role);

                identity.SetClaim(Claims.Subject, await _applicationManager.GetClientIdAsync(application))
                        .SetClaim(Claims.Name, await _applicationManager.GetDisplayNameAsync(application));

                identity.SetScopes(request.GetScopes());
                identity.SetDestinations(_ => [Destinations.AccessToken]);

                return SignIn(new ClaimsPrincipal(identity),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("Unsupported grant type.");
        }

        // ── GET /connect/userinfo ─────────────────────────────────────────────────
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        public async Task<IActionResult> Userinfo()
        {
            var user = await _userManager.FindByIdAsync(
                User.GetClaim(Claims.Subject) ?? string.Empty);

            if (user is null)
                return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [Claims.Subject] = await _userManager.GetUserIdAsync(user)
            };

            if (User.HasScope(Scopes.Email))
                claims[Claims.Email] = await _userManager.GetEmailAsync(user) ?? "";

            if (User.HasScope(Scopes.Profile))
            {
                claims[Claims.Name] = user.FullName ?? user.UserName ?? "";
                claims[Claims.PreferredUsername] = user.UserName ?? "";
            }

            return Ok(claims);
        }

        // ── GET /connect/logout ───────────────────────────────────────────────────
        [HttpGet("~/connect/logout")]
        public IActionResult Logout() => View();

        [HttpPost("~/connect/logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return SignOut(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties { RedirectUri = "/" });
        }

        // ── Helper ────────────────────────────────────────────────────────────────
        private static IEnumerable<string> GetDestinations(Claim claim)
        {
            return claim.Type switch
            {
                Claims.Name or Claims.Subject
                    => [Destinations.AccessToken, Destinations.IdentityToken],

                Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true
                    => [Destinations.AccessToken, Destinations.IdentityToken],

                Claims.Role
                    => [Destinations.AccessToken],

                _ => [Destinations.AccessToken]
            };
        }
    }
}
