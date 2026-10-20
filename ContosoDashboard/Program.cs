using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Endpoints;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
});

var documentOptions = new DocumentOptions();
builder.Configuration.GetSection(DocumentOptions.SectionName).Bind(documentOptions);
documentOptions.Validate();
documentOptions.StorageRoot = DocumentOptions.ResolveAndValidateStorageRoot(
    builder.Environment.ContentRootPath,
    builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"),
    documentOptions.StorageRoot);
builder.Services.Configure<DocumentOptions>(options =>
{
    options.StorageRoot = documentOptions.StorageRoot;
    options.ClamScanPath = documentOptions.ClamScanPath;
    options.ScannerTimeoutSeconds = documentOptions.ScannerTimeoutSeconds;
    options.MaxFileSizeBytes = documentOptions.MaxFileSizeBytes;
    options.AllowedExtensions = documentOptions.AllowedExtensions;
    options.AllowedContentTypes = documentOptions.AllowedContentTypes;
});

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IDocumentFileStorage, DocumentFileStorage>();
builder.Services.AddSingleton<ILocalMalwareScanner, LocalClamAvScanner>();
builder.Services.AddSingleton<IDocumentFileValidator, DocumentFileValidator>();
builder.Services.AddScoped<IDocumentAuthorizationService, DocumentAuthorizationService>();
builder.Services.AddScoped<IDocumentAuditService, DocumentAuditService>();
builder.Services.AddScoped<IDocumentOperationRecoveryService, DocumentOperationRecoveryService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IDocumentQueryService, DocumentQueryService>();
builder.Services.AddScoped<DocumentDatabaseInitializer>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DocumentDatabaseInitializer>();
    await initializer.InitializeAsync();
    var recovery = scope.ServiceProvider.GetRequiredService<IDocumentOperationRecoveryService>();
    await recovery.ReconcileAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; connect-src 'self' wss: ws:;";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapDocumentContentEndpoint();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();

public partial class Program { }
