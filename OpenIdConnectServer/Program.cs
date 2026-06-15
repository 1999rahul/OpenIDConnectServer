using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIdConnectServer.Data;
using OpenIdConnectServer.Entities;
using OpenIdConnectServer.Services;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    // Tell EF Core that OpenIddict will use this DbContext
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUserEntity, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddOpenIddict()

    // 4a. Core — wire up the EF Core stores
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })

    // 4b. Server — configure the OIDC/OAuth endpoints
    .AddServer(options =>
    {
        // Endpoints
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            //.SetUserinfoEndpointUris("/connect/userinfo")
            .SetIntrospectionEndpointUris("/connect/introspect")
            .SetRevocationEndpointUris("/connect/revoke");
            //.SetLogoutEndpointUris("/connect/logout");

        // Grant types
        options
            .AllowAuthorizationCodeFlow()   // for web apps / SPAs
            .AllowClientCredentialsFlow()   // for machine-to-machine
            .AllowRefreshTokenFlow();       // for silent renewal

        // Enforce PKCE for all auth code requests
        options.RequireProofKeyForCodeExchange();

        // Scopes this server can issue
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.OfflineAccess,  // enables refresh tokens
            "api"                                       // your custom scope
        );

        // Token lifetimes
        options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
        options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5));

        // Signing/encryption keys — USE REAL CERTS IN PRODUCTION
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Wire up to ASP.NET Core pipeline
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()  // your controller handles /connect/authorize
               .EnableTokenEndpointPassthrough();          // your controller handles /connect/token
               //.EnableUserinfoEndpointPassthrough()       // your controller handles /connect/userinfo
               //.EnableLogoutEndpointPassthrough();
    })

    // 4c. Validation — for when this app also acts as a resource server
    .AddValidation(options =>
    {
        options.UseLocalServer();    // validates JWTs using this server's own keys
        options.UseAspNetCore();
    });

builder.Services.AddHostedService<OpenIddictSeeder>();

// Add services to the container.
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
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
