using System.Reflection;
using System.Text.Json.Serialization;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.Mappings;
using InfectionSolutions.Application;
using InfectionSolutions.Domain.Identity;
using InfectionSolutions.Infrastructure;
using InfectionSolutions.Infrastructure.Persistence;
using InfectionSolutions.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

const string PoliticaCors = "Clientes";

var builder = WebApplication.CreateBuilder(args);

// Mismas capas que el panel: misma base de datos, mismas reglas.
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutoMapper(
    options => options.LicenseKey = ApplicationServiceCollectionExtensions.AutoMapperLicenseKey(builder.Configuration),
    typeof(ApiMappingProfile).Assembly);

var carpetaRecibos = builder.Configuration["Receipts:Path"] ?? "App_Data/recibos";
builder.Services.AddFileReceiptStorage(Path.IsPathRooted(carpetaRecibos)
    ? carpetaRecibos
    : Path.Combine(builder.Environment.ContentRootPath, carpetaRecibos));

// La clave de firma se valida al arrancar: sin ella la API no sirve tokens.
builder.Services.AddOptions<JwtOptions>().ValidateDataAnnotations().ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        // Claims con sus nombres cortos ("sub", "role"), sin el mapeo a URIs de WS-Federation.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenService.SigningKey(jwt.Value),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtTokenService.NameClaim,
            RoleClaimType = JwtTokenService.RoleClaim
        };
    });

// Todo exige token salvo lo marcado con [AllowAnonymous] (login, registro, salud).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(ApplicationRoles.Administrator))
    .AddPolicy(AuthorizationPolicies.CustomerOnly, policy => policy.RequireRole(ApplicationRoles.Customer))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services
    .AddControllers(options =>
    {
        // Los errores de validacion usan el nombre JSON del campo ("email", no "Email").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problema = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Uno o más campos no son válidos.",
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
    };
});

// 401/403 sin cuerpo tambien salen como ProblemDetails, con un mensaje util para el SPA.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    switch (context.ProblemDetails.Status)
    {
        case StatusCodes.Status401Unauthorized:
            context.ProblemDetails.Title = "No autenticado";
            context.ProblemDetails.Detail ??= "Falta el token o ya venció. Inicia sesión de nuevo.";
            break;
        case StatusCodes.Status403Forbidden:
            context.ProblemDetails.Title = "Acceso denegado";
            context.ProblemDetails.Detail ??= "Tu rol no tiene permiso para esta operación.";
            break;
    }
});
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PoliticaCors, policy => policy
    .WithOrigins(origenes)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // Sin exponerlo, el navegador no deja leer el nombre del PDF descargado.
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InfectionSolutions API",
        Version = "v1",
        Description = "Productos, clientes y ventas de InfectionSolutions. Inicia sesión en POST /api/auth/login, "
            + "copia el accessToken y pégalo en el botón Authorize."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega solo el token (sin la palabra Bearer)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    options.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "InfectionSolutions API v1");
        options.DocumentTitle = "InfectionSolutions API";
        options.EnablePersistAuthorization();
    });

    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();
}

if (app.Configuration.GetValue("UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous()
    .WithTags("Salud");

// Comparte la base con el panel: migraciones y roles son idempotentes, da igual cual arranque primero.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DatabaseInitializer.InitializeAsync(app.Services);
}

app.Run();

/// <summary>Visible para las pruebas.</summary>
public partial class Program;
