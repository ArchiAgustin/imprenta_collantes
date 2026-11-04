using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ProyectoFinal.Data;
using ProyectoFinal.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before starting the application.")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
             options.SignIn.RequireConfirmedAccount = false;
             options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
             options.Lockout.MaxFailedAccessAttempts = 5;
        })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<MenuAuthorizationFilter>();
});
builder.Services.AddRazorPages();
builder.Services.AddTransient<IEmailSender, FakeEmailSender>();
builder.Services.AddScoped<IMenuAccessService, MenuAccessService>();
builder.Services.AddScoped<MenuAuthorizationFilter>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<SerieDocumentoInternoService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<SugerenciaPrecioService>();
builder.Services.AddScoped<IdentificacionService>();
builder.Services.AddHttpClient<ISolicitudExtractor, GroqSolicitudExtractor>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("groq", context => RateLimitPartition.GetFixedWindowLimiter(
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

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

public partial class Program { }
