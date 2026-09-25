using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Application.Aperturas;
using CapitalPos.Tcg.Api.Application.Caja;
using CapitalPos.Tcg.Api.Application.Catalogo;
using CapitalPos.Tcg.Api.Application.Clientes;
using CapitalPos.Tcg.Api.Application.Dashboard;
using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Application.Ecosistema;
using CapitalPos.Tcg.Api.Application.Entregas;
using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Application.Izipay;
using CapitalPos.Tcg.Api.Application.Pagos;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Application.Productos;
using CapitalPos.Tcg.Api.Application.Proveedores;
using CapitalPos.Tcg.Api.Application.Reportes;
using CapitalPos.Tcg.Api.Application.Sedes;
using CapitalPos.Tcg.Api.Application.Subastas;
using CapitalPos.Tcg.Api.Application.Usuarios;
using CapitalPos.Tcg.Api.Application.WooCommerce;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Infrastructure;
using CapitalPos.Tcg.Api.Infrastructure.Auth;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Cpe;
using CapitalPos.Tcg.Api.Infrastructure.Izipay;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using CapitalPos.Tcg.Api.Infrastructure.Subastas;
using CapitalPos.Tcg.Api.Infrastructure.WooCommerce;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusinessRuleExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<KardexWriter>();
builder.Services.AddScoped<WooStockPublishQueue>();
builder.Services.AddScoped<WooStockPublisher>();
builder.Services.AddScoped<ProductosTcgService>();
builder.Services.AddScoped<CatalogoTcgService>();
builder.Services.AddScoped<SedesService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<AperturasTcgService>();
builder.Services.AddScoped<SubastasTcgService>();
builder.Services.AddScoped<PedidosDigitalesService>();
builder.Services.AddScoped<ClientesService>();
builder.Services.AddScoped<ProveedoresService>();
builder.Services.AddScoped<PagosService>();
builder.Services.AddSingleton<IzipayCredentialProtector>();
builder.Services.AddSingleton<IzipayHmacValidator>();
builder.Services.AddScoped<IzipayIpnService>();
builder.Services.AddScoped<EntregasService>();
builder.Services.AddScoped<WooCommerceSyncService>();
builder.Services.AddScoped<IWooStockChangeNotifier>(sp => sp.GetRequiredService<WooCommerceSyncService>());
builder.Services.AddScoped<WooCommercePedidoImportService>();
builder.Services.AddSingleton<LocalWooCommerceGateway>();
builder.Services.AddSingleton<RestWooCommerceGateway>();
builder.Services.AddScoped<IWooCommerceGateway, WooCommerceGatewayRouter>();
builder.Services.AddSingleton<WooCredentialProtector>();
builder.Services.AddHostedService<WooCommerceWebhookRetryHostedService>();
builder.Services.AddHostedService<SubastaCierreWorker>();
builder.Services.AddHttpClient("WooCommerce", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CapitalPos-TCG/1.0");
});
builder.Services.AddScoped<ICpeEmisor, CpeEmisorRouter>();
builder.Services.AddSingleton<ICpePdfGenerator, CpePdfGenerator>();
builder.Services.AddScoped<IServicioFiscal, ServicioFiscal>();
builder.Services.AddScoped<NotaCreditoImpactoService>();
builder.Services.AddScoped<EcosistemaService>();
builder.Services.AddScoped<UsuariosService>();
builder.Services.AddScoped<ReportesService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<CajaService>();
builder.Services.AddDataProtection();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CpeApiOptions>(builder.Configuration.GetSection(CpeApiOptions.SectionName));
builder.Services.Configure<WooCommerceOptions>(builder.Configuration.GetSection(WooCommerceOptions.SectionName));

builder.Services.AddTransient<CpeApiKeyHandler>();
builder.Services.AddHttpClient("CpeApi", (sp, client) =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CpeApiOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(cfg.BaseUrl))
    {
        client.BaseAddress = new Uri(cfg.BaseUrl.TrimEnd('/') + "/");
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(cfg.TimeoutSeconds, 5, 120));
}).AddHttpMessageHandler<CpeApiKeyHandler>();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección Jwt.");
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(
                    "http://localhost:4200",
                    "https://pos-tcg.tykesoft.com")
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

builder.Services.AddDbContext<ApplicationDbContext>((_, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");
    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    await DevelopmentDataSeeder.EnsureAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}
else
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantHeaderMiddleware>();
app.UseMiddleware<WooStockFlushMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "CapitalPos.Tcg.Api" }))
    .WithName("Health")
    .AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program;
