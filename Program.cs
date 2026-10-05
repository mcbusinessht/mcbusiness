using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using MCBusiness.Middlewares;
using MCBusiness.Filters;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.SignalR;
using MCBusiness.Hubs;
using System.Collections.Generic;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

// Les secrets MonCash doivent provenir de l'environnement du processus
// (gestionnaire de secrets de l'hebergeur, Docker/Kubernetes secrets, etc.).
// Ils ne sont jamais acceptes depuis le fichier .env local du projet.
var hostedMonCashClientId = Environment.GetEnvironmentVariable("MonCash__ClientId");
var hostedMonCashClientSecret = Environment.GetEnvironmentVariable("MonCash__ClientSecret");

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);

    Environment.SetEnvironmentVariable("MonCash__ClientId", hostedMonCashClientId);
    Environment.SetEnvironmentVariable("MonCash__ClientSecret", hostedMonCashClientSecret);
}

var builder = WebApplication.CreateBuilder(args);

// Ajouter support pour les proxys
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

// Configurer la protection des données et persister les clés
var keyPath = Path.Combine(Directory.GetCurrentDirectory(), "keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("MCBusiness")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
    // .DisableAutomaticKeyGeneration(); // Évite la génération automatique de nouvelles clés

// Configurer DbContext
// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30); // Timeout de 30 secondes
        npgsqlOptions.EnableRetryOnFailure(3); // 3 tentatives en cas d'échec
    });
    
    // Optimisations Entity Framework
    options.EnableSensitiveDataLogging(false); // Désactiver en production
    options.EnableServiceProviderCaching();
    options.EnableDetailedErrors(builder.Environment.IsDevelopment());
    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.RowLimitingOperationWithoutOrderByWarning);
    });
});


// Ajouter les services en arrière-plan
builder.Services.AddHostedService<SubscriptionStatusUpdater>();
// BackupService est un service scopé, pas un HostedService
builder.Services.AddHostedService<TransactionStatusUpdater>();
builder.Services.AddScoped<NoticeService>();
builder.Services.AddScoped<IOrderStatusService, OrderStatusService>();
builder.Services.AddScoped<ShipmentStatusHandler>();
builder.Services.AddScoped<OrderAmountService>();    
builder.Services.AddScoped<ISellerPaymentService, SellerPaymentService>();
builder.Services.AddScoped<IAmazonLikeDistributionService, AmazonLikeDistributionService>();
builder.Services.AddScoped<IPlatformTransactionAuditService, PlatformTransactionAuditService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();    
builder.Services.AddScoped<ILocalTimeService, LocalTimeService>();
builder.Services.AddHostedService<CancelOrderService>();
builder.Services.AddHostedService<CleanupService>();
// builder.Services.AddHostedService<AutoBackupService>();
// builder.Services.AddHostedService<CookieCleanupService>(); // Temporairement désactivé
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IImageOptimizationService, ImageOptimizationService>();
builder.Services.AddScoped<IPaginationService, PaginationService>();
builder.Services.AddScoped<IStockReservationService, StockReservationService>();
builder.Services.AddScoped<ISecureFinancialService, SecureFinancialService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IReferralCodeGenerator, ReferralCodeGenerator>();
// Services d'optimisation de performance
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IOptimizedProductService, OptimizedProductService>();
builder.Services.AddScoped<IOptimizedCartService, OptimizedCartService>();

// Configuration et service reCAPTCHA
builder.Services.Configure<ReCaptchaSettings>(builder.Configuration.GetSection("ReCaptcha"));
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();
// Service de dashboard dynamique
builder.Services.AddScoped<IDashboardService, DashboardService>();
// Service de métriques avancées
builder.Services.AddScoped<IAdvancedMetricsService, AdvancedMetricsService>();
// Service de gestion d'accès ambassador (CENTRAL)
builder.Services.AddScoped<IAmbassadorAccessService, AmbassadorAccessService>();
// Services de gestion des commissions ambassadeur (AUTONOMES)
builder.Services.AddScoped<AmbassadorCommissionService>();
builder.Services.AddScoped<AmbassadorCommissionCalculator>(); // NOUVEAU: Logique de calcul par vendeur/panier
builder.Services.AddHostedService<AmbassadorCommissionProcessor>(); // RÉACTIVÉ pour traitement automatique
// NOUVEAU: Service background pour logs d'activité (gain 10-20× sur requêtes HTTP)
builder.Services.AddHostedService<SellerPaymentReconciliationProcessor>();
builder.Services.AddSingleton<UserActivityBackgroundService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<UserActivityBackgroundService>());
builder.Services.AddScoped<OrderAmbassadorIntegrationService>();
// NOUVEAU: Service de points de fidélité (séparé de l'argent)
builder.Services.AddScoped<PointTransactionService>();
// builder.Services.AddScoped<IBackupService, BackupService>();
// builder.Services.AddScoped<IDockerBackupService, DockerBackupService>();
// builder.Services.AddScoped<IBackupManagementService, BackupManagementService>();



//SignalR
builder.Services.AddSignalR();

// Ajouter les services de logging et Webhook
builder.Services.AddLogging();
builder.Services.AddScoped<MonCashWebhookService>();

// Configurer Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configurer l'authentification avec les cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/SignOut";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });

// Configurer l'autorisation
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    options.AddPolicy("SecondAdminPolicy", policy => policy.RequireRole("SecondAdmin"));
    options.AddPolicy("UserPolicy", policy => policy.RequireRole("User"));
    options.AddPolicy("SupplierPolicy", policy => policy.RequireRole("Seller"));
    options.AddPolicy("EmployeePolicy", policy => policy.RequireRole("Employee"));
    
    // AMBASSADOR POLICY: Protège l'accès aux fonctionnalités ambassador
    // Requirement: Utilisateur DOIT avoir le rôle "Ambassador"
    // Note: La vérification de subscription active est faite via IAmbassadorAccessService en backend
    options.AddPolicy("AmbassadorPolicy", policy => 
        policy.RequireRole("Ambassador")
               .RequireAuthenticatedUser());

    options.AddPolicy("RequireAdminOrSecondAdmin", policy => 
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || 
            context.User.IsInRole("SecondAdmin")
        ));
});

// Ajouter la compression des réponses pour optimiser la bande passante
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    
    // En développement, exclure text/html pour éviter les conflits avec Browser Refresh
    if (builder.Environment.IsDevelopment())
    {
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/javascript",
            "text/css",
            "image/svg+xml",
            "application/json"
            // text/html exclu en développement
        });
    }
    else
    {
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/javascript",
            "text/css", 
            "image/svg+xml",
            "application/json",
            "text/html"
        });
    }
    
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});

// Configurer la compression Gzip et Brotli
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Ajouter les sessions avec timeout optimisé et gestion d'erreur améliorée
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "MCBusiness.Session";
    options.Cookie.MaxAge = TimeSpan.FromHours(2);
});
//  Configuration avancée des cookies conformes RGPD
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => {
        // Toujours vérifier le consentement sauf pour les cookies essentiels
        return true;
    };
    options.MinimumSameSitePolicy = SameSiteMode.Lax; // Plus sécurisé et compatible
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always; // Sécurité XSS
    options.Secure = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    
    // Gestion des cookies conformes RGPD
    options.OnAppendCookie = cookieContext =>
    {
        // Marquer les cookies essentiels comme toujours autorisés
        if (cookieContext.CookieName.StartsWith(".AspNetCore") || 
            cookieContext.CookieName == "cookie_consent" ||
            cookieContext.CookieName == "cookie_settings" ||
            cookieContext.CookieName.StartsWith("ambassador_") ||
            cookieContext.CookieName.StartsWith("__RequestVerificationToken"))
        {
            cookieContext.CookieOptions.IsEssential = true;
        }
        
        // Optimiser la durée de vie des cookies
        if (cookieContext.CookieName == "cookie_settings")
        {
            cookieContext.CookieOptions.MaxAge = TimeSpan.FromDays(365);
        }
    };
    
    options.OnDeleteCookie = cookieContext =>
    {
        cookieContext.CookieOptions.IsEssential = false;
    };
});

//Empêcher le Bruteforce avec Rate Limiting-------------------------------------------------
// Ajouter le cache mémoire
builder.Services.AddMemoryCache();

// Ajouter la configuration des options
builder.Services.AddOptions();

// Ajouter la limitation de taux basée sur la mémoire
builder.Services.AddInMemoryRateLimiting();

// Configurer les règles de limitation
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "10m",
            Limit = 1000
        },
        new RateLimitRule
        {
            Endpoint = "users/user/conversations/chat/*", // Exclure les endpoints de chat
            Period = "10m",
            Limit = 1000 
        }
    };
});

// Ajouter la configuration de la limitation de taux
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();


// Applique le filtre à tout le répertoire Admin et sous-répertoires
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/Users/Admin", model =>
    {
        model.Filters.Add(new AdminAccessFilter());
    });
});

// Configurer Razor Pages et les policies
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Users/User", "UserPolicy");
    options.Conventions.AuthorizeFolder("/Orders", "UserPolicy");
    
    // Modifier comme ceci pour /Users/Admin
    options.Conventions.AuthorizeFolder("/Users/Admin", "RequireAdminOrSecondAdmin");
    
    options.Conventions.AuthorizeFolder("/Users/Admin/Admins", "AdminPolicy");
    options.Conventions.AuthorizeFolder("/Users/Admin/Payouts", "AdminPolicy");
    options.Conventions.AuthorizeFolder("/Users/Admin/Promotions", "AdminPolicy");
    options.Conventions.AuthorizeFolder("/Users/Admin/BankTransferPayments", "AdminPolicy");
    options.Conventions.AuthorizeFolder("/Users/Admin/GestionRoles", "AdminPolicy");
    options.Conventions.AuthorizeFolder("/Users/Seller", "SupplierPolicy");
    options.Conventions.AuthorizeFolder("/Users/Employee", "EmployeePolicy");
});

// Ajouter HttpClient et MonCashService
builder.Services.AddHttpClient("MonCash", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient();
builder.Services.AddTransient<MonCashService>();
builder.Services.AddScoped<StripeService>();
builder.Services.AddScoped<ICookieConsentService, CookieConsentService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Ajouter les contrôleurs
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Configurer ApplicationCookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Charger les paramètres d'email et Twilio et Stripe
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<MCBusiness.Interfaces.IEmailSender, MCBusiness.Services.EmailSender>();
builder.Services.AddScoped<ITwoFactorSendThrottle, TwoFactorSendThrottle>();
builder.Services.Configure<StripeConfig>(builder.Configuration.GetSection("Stripe"));

var twilioSettings = builder.Configuration.GetSection("Twilio");
builder.Services.AddSingleton(new MessageService(twilioSettings["AccountSid"], twilioSettings["AuthToken"]));


builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();


var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";
app.Urls.Add($"http://*:{port}");

// Appliquer la localisation
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// Et dans les middlewares  Pour SignalR
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<ChatHub>("/chatHub");

// Configurer le pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseResponseCompression(); // Activer la compression
app.UseStaticFiles();
app.UseCookiePolicy();


app.UseRouting();

// Ajouter le middleware de gestion des erreurs de session AVANT UseSession
app.UseMiddleware<SessionErrorHandlingMiddleware>();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<UserActivityMiddleware>();
app.UseMiddleware<CurrencyMiddleware>(); // NOUVEAU: Multi-currency support
app.UseAmbassadorTracking(); // NOUVEAU: Tracking ambassadeur
app.UseAuthorization();

// Activer les contrôleurs
app.MapControllers();

// URLs compatibles Return URL / Alert URL. La transaction est toujours
// relue côté serveur auprès de MonCash avant comptabilisation.
Func<HttpContext, MonCashWebhookService, Task> monCashCallbackHandler = async (context, webhookService) =>
{
    await webhookService.HandleWebhookAsync(context);
};

app.MapGet("/api/moncash/callback", monCashCallbackHandler).AllowAnonymous();
app.MapGet("/Payments/MonCash/Return", monCashCallbackHandler).AllowAnonymous();

// Ajouter les rôles au démarrage
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "User", "Admin", "Seller", "SecondAdmin", "Employee", "Ambassador" })
    {
        if (!roleManager.RoleExistsAsync(role).Result)
        {
            roleManager.CreateAsync(new IdentityRole(role)).Wait();
        }
    }
}
// Configurer les routes pour Razor Pages et les contrôleurs
app.MapRazorPages();
app.MapControllers();


// Appliquer les migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Middleware pour cache intelligent par type de contenu
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    
    // Cache longue durée pour les ressources statiques
    if (path.EndsWith(".css") || path.EndsWith(".js") || 
        path.EndsWith(".png") || path.EndsWith(".jpg") || 
        path.EndsWith(".jpeg") || path.EndsWith(".gif") || 
        path.EndsWith(".ico") || path.EndsWith(".svg"))
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=31536000"; // 1 an
        context.Response.Headers["Expires"] = DateTimeOffset.UtcNow.AddYears(1).ToString("R");
    }
    // Cache court pour les API
    else if (path.StartsWith("/api/"))
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes
    }
    // Pas de cache pour les pages dynamiques sensibles
    else if (path.Contains("/admin/") || path.Contains("/user/") || path.Contains("/account/"))
    {
        context.Response.Headers["Cache-Control"] = "no-cache, private";
        context.Response.Headers["Pragma"] = "no-cache";
    }
    // Cache court pour les autres pages
    else
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=60"; // 1 minute
    }
    
    await next();
});

// SUPPRIMÉ: Middleware destructeur qui vidait les sessions automatiquement
// Ce middleware causait la perte de données utilisateur importantes
// Remplacé par une gestion sélective du cache si nécessaire

app.UseIpRateLimiting();
app.Run();
