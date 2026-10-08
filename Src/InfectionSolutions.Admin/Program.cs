using System.Globalization;
using InfectionSolutions.Admin.Common;
using InfectionSolutions.Application;
using InfectionSolutions.Domain.Identity;
using InfectionSolutions.Infrastructure;
using InfectionSolutions.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Capas compartidas con la API: casos de uso, persistencia e Identity.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// El panel autentica con la cookie de Identity.
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "InfectionSolutions.Admin";
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Todo el panel exige el rol Administrador salvo lo marcado con
// [AllowAnonymous] (login y error). Un Cliente autenticado recibe 403.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(ApplicationRoles.Administrator))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(ApplicationRoles.Administrator)
        .Build());

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Cultura es-CO para fechas y moneda, pero con punto decimal: los
// <input type="number"> y jQuery Validation siempre mandan punto, y con la
// coma de es-CO el model binding leeria "1500.50" como 150050.
var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();
cultura.NumberFormat.NumberDecimalSeparator = ".";
cultura.NumberFormat.NumberGroupSeparator = ",";
cultura.NumberFormat.CurrencyDecimalSeparator = ".";
cultura.NumberFormat.CurrencyGroupSeparator = ",";

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = [cultura],
    SupportedUICultures = [cultura],
    // Siempre la misma cultura, sin importar el idioma del navegador.
    RequestCultureProviders = []
});

if (app.Configuration.GetValue("UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Migraciones + roles + administrador inicial antes de atender peticiones.
await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();
