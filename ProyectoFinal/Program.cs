using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using ProyectoFinal.Localization;
using ProyectoFinal.Data;
using ProyectoFinal.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before starting the application.")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    dataProtectionKeysPath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASP.NET", "DataProtection-Keys")
        : Path.Combine(userProfile, ".aspnet", "DataProtection-Keys");
}
Directory.CreateDirectory(dataProtectionKeysPath);
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
if (OperatingSystem.IsWindows())
{
    // Matches the default ASP.NET Core Windows key-ring protection so existing auth/reset tokens
    // remain readable while the repository location is made explicit.
    dataProtection.ProtectKeysWithDpapi();
}
else
{
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var certificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
    if (!string.IsNullOrWhiteSpace(certificatePath))
    {
        var certificate = new X509Certificate2(certificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        dataProtection.ProtectKeysWithCertificate(certificate);
    }
    else if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Configure DataProtection:CertificatePath and CertificatePassword to protect the persistent key ring outside Windows.");
    }
}

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
              options.SignIn.RequireConfirmedAccount = false;
             options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
             options.Lockout.MaxFailedAccessAttempts = 5;
             options.Lockout.AllowedForNewUsers = true;
        })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<MenuAuthorizationFilter>();
    options.ModelMetadataDetailsProviders.Add(new SpanishValidationMetadataProvider());
    SpanishModelBindingMessages.Configure(options.ModelBindingMessageProvider);
});
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ProyectoFinal.RoleSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<IEmailSender>(services => services.GetRequiredService<SmtpEmailSender>());
builder.Services.AddScoped<ICorreoComercialSender>(services => services.GetRequiredService<SmtpEmailSender>());
builder.Services.AddScoped<CotizacionPdfService>();
builder.Services.AddScoped<IMenuAccessService, MenuAccessService>();
builder.Services.AddScoped<MenuAuthorizationFilter>();
builder.Services.AddScoped<CotizacionService>();
builder.Services.AddScoped<ICotizacionService>(services => services.GetRequiredService<CotizacionService>());
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<IVentaService>(services => services.GetRequiredService<VentaService>());
builder.Services.AddScoped<SerieDocumentoInternoService>();
builder.Services.AddScoped<ISerieDocumentoInternoService>(services => services.GetRequiredService<SerieDocumentoInternoService>());
builder.Services.AddScoped<CatalogoSunatService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<TransferenciaAlmacenService>();
builder.Services.AddScoped<ITransferenciaAlmacenService>(services => services.GetRequiredService<TransferenciaAlmacenService>());
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<IAnalyticsService>(services => services.GetRequiredService<AnalyticsService>());
builder.Services.AddScoped<AnalyticsExportService>();
builder.Services.AddHostedService<AnalyticsWarmupService>();
builder.Services.AddScoped<SugerenciaPrecioService>();
builder.Services.AddScoped<IClientesCatalogoService, ClientesCatalogoService>();
builder.Services.AddScoped<IProveedoresCatalogoService, ProveedoresCatalogoService>();
builder.Services.AddScoped<IProductosCatalogoService, ProductosCatalogoService>();
builder.Services.AddScoped<IParametrosCorreoService, ParametrosCorreoService>();
builder.Services.AddScoped<IConfiguracionGroqService, ConfiguracionGroqService>();
builder.Services.AddScoped<IAlmacenesAdminService, AlmacenesAdminService>();
builder.Services.AddScoped<IAuditoriaConsultaService, AuditoriaConsultaService>();
builder.Services.AddScoped<ITallerConsultaService, TallerConsultaService>();
builder.Services.AddScoped<IPreCotizacionCatalogoService, PreCotizacionCatalogoService>();
builder.Services.AddScoped<IInventarioConsultaService, InventarioConsultaService>();
builder.Services.AddScoped<ITransaccionIdentidadService, TransaccionIdentidadService>();
builder.Services.AddScoped<IPermisosMatrizService, PermisosMatrizService>();
builder.Services.AddScoped<IMenusAdminService, MenusAdminService>();
builder.Services.AddScoped<ITramosDescuentoService, TramosDescuentoService>();
builder.Services.AddScoped<IdentificacionService>();
builder.Services.AddScoped<IUbigeoConsultaService>(services => services.GetRequiredService<IdentificacionService>());
builder.Services.AddScoped<FichaComercialService>();
builder.Services.AddHttpClient<ISolicitudExtractor, GroqSolicitudExtractor>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Se alcanzó el límite de solicitudes. Espera un momento e inténtalo de nuevo.", cancellationToken);
    };
    options.AddPolicy("groq", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "sin-usuario",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("cotizacion-email", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "sin-usuario",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddScoped<CotizacionExpirationService>();
builder.Services.AddHostedService<CotizacionExpirationWorker>();


var app = builder.Build();

// Explicit maintenance command. Normal application startup never applies migrations.
if (args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await migrationDb.Database.MigrateAsync();
    Console.WriteLine("Migraciones EF aplicadas.");
    return;
}
if (args.Length == 3 && args[0] == "--send-test-pdf"
    && int.TryParse(args[1], out var testQuoteId)
    && !string.IsNullOrWhiteSpace(args[2]))
{
    using var testScope = app.Services.CreateScope();
    var quotes = testScope.ServiceProvider.GetRequiredService<ICotizacionService>();
    var quote = await quotes.ObtenerConLineasAsync(testQuoteId)
        ?? throw new InvalidOperationException("No existe la cotización de prueba.");
    var pdf = testScope.ServiceProvider.GetRequiredService<CotizacionPdfService>().Generar(quote);
    var sender = testScope.ServiceProvider.GetRequiredService<ICorreoComercialSender>();
    try
    {
        await sender.SendEmailWithAttachmentAsync(args[2],
            "Prueba de PDF comercial · Imprenta Collantes",
            "<p>Prueba solicitada del envío de una cotización comercial en PDF. No es un comprobante tributario.</p>",
            pdf, $"Cotizacion-prueba-{testQuoteId}.pdf", "application/pdf");
    }
    catch (CryptographicException)
    {
        Console.Error.WriteLine("La contraseña SMTP restaurada usa una clave Data Protection ausente. Vuelve a guardarla en Parámetros.");
        Environment.ExitCode = 1;
        return;
    }
    Console.WriteLine("SMTP aceptó el mensaje de prueba con PDF adjunto. Confirma la recepción en el buzón.");
    return;
}

// Bootstrap only with credentials supplied outside source control.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        var result = await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!result.Succeeded) throw new InvalidOperationException("Could not create the Admin role.");
    }

    var adminEmail = builder.Configuration["BootstrapAdmin:Email"];
    var adminPassword = builder.Configuration["BootstrapAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            user = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, adminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("Could not create the bootstrap administrator: " +
                    string.Join(", ", created.Errors.Select(error => error.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var assigned = await userManager.AddToRoleAsync(user, "Admin");
            if (!assigned.Succeeded) throw new InvalidOperationException("Could not assign the Admin role.");
        }
    }
}


// Configure the HTTP request pipeline.
var peruCulture = CultureInfo.GetCultureInfo("es-PE");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(peruCulture),
    SupportedCultures = [peruCulture],
    SupportedUICultures = [peruCulture]
});
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseStatusCodePages(async status =>
{
    var response = status.HttpContext.Response;
    var message = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => "No se encontró la página solicitada. Revisa la dirección e inténtalo de nuevo.",
        StatusCodes.Status403Forbidden => "No tienes permiso para acceder a esta página. Consulta al administrador si necesitas acceso.",
        _ => "No se pudo completar la solicitud. Vuelve a intentarlo."
    };
    response.ContentType = "text/plain; charset=utf-8";
    await response.WriteAsync(message);
});

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<ActiveRoleMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

public partial class Program { }
