using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuestPDF.Infrastructure;
using RROMS.Web.Data;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments.Receipts;

using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.Tracking;

using RROMS.Web.Services.Restaurant;
using RROMS.Web.Services.Accounts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddRazorPages(options =>
{
    // The test login (/Dev/SwitchUser) only has an address in Development. Anywhere else the page doesn't exist.
    if (!builder.Environment.IsDevelopment())
        options.Conventions.AddFolderRouteModelConvention("/Dev", page => page.Selectors.Clear());
});

// Sign-in cookie. Real accounts are signed in by /Account/Login (AccountClaims.CreatePrincipal): a numeric
// NameIdentifier, Name, Email and Role. In Development the test login (/Dev/SwitchUser) still signs in the test users.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);   // log out after 30 minutes without activity
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;


        // Not signed in, or signed in without the right role: choose a test user (log in with a real account at /Account/Login).

        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";


        // On every request: if a real account was deactivated, deleted or had its role changed, force a new login.
        // The Development test users have no UserAccount row, so only sign-ins marked as real accounts are checked.
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            if (principal is null || !principal.HasClaim(c => c.Type == AccountClaims.AccountClaimType))
                return;

            var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
            if (!principal.TryGetUserId(out var userId)
                || await accounts.GetActiveRoleAsync(userId) != principal.FindFirstValue(ClaimTypes.Role))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountMailer, AccountMailer>();   // sends through the shared Smtp settings (see Notification module)
builder.Services.AddScoped<IRestaurantLookup, RestaurantLookup>();
builder.Services.AddScoped<TrackingQueries>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IMenuItemLookup, MenuItemLookup>();
builder.Services.AddScoped<IPaymentOrderProvider, PaymentOrderProvider>();
builder.Services.AddHttpContextAccessor();


// ---- Notification module ----

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.AddScoped<ISmtpEmailSender, SmtpEmailSender>();
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationRealtime, NotificationRealtime>();
builder.Services.AddScoped<INotificationEmailSender, NotificationEmailSender>();
builder.Services.AddScoped<INotificationDelivery, NotificationDelivery>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.Configure<WebPushSettings>(builder.Configuration.GetSection(WebPushSettings.SectionName));
builder.Services.AddSingleton<WebPushIdentity>();
builder.Services.AddHttpClient<INotificationPushSender, OneSignalPushSender>(http => http.Timeout = TimeSpan.FromSeconds(15));

// ---- Payment module ----

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<IPaymentQueries, PaymentQueries>();
builder.Services.AddScoped<IPaymentNotifier, LoggingPaymentNotifier>();
builder.Services.AddScoped<IPaymentNotifier, EmailPaymentNotifier>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.AddSingleton<IReceiptService, ReceiptService>();
builder.Services.AddScoped<StripeWebhookHandler>();



// ---- Customer support and feedback module ----
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddHttpClient<IChatModelClient, GeminiChatClient>(client => client.Timeout = TimeSpan.FromSeconds(25));
builder.Services.AddScoped<IChatContextProvider, ChatContextProvider>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

builder.Services.AddScoped<ICustomerHistoryService, CustomerHistoryService>();
builder.Services.AddScoped<ICustomerPreferenceService, CustomerPreferenceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapStripeWebhook();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapNotificationWebPush();

// Dev only: create the database on startup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

    // The first administrator, so someone can log in and create the other accounts.
    // Override with DefaultAdmin:Email / DefaultAdmin:Password in user-secrets, and change the password after the first login.
    await scope.ServiceProvider.GetRequiredService<IAccountService>().EnsureDefaultAdminAsync(
        builder.Configuration["DefaultAdmin:Email"] ?? "admin@gmail.com",
        builder.Configuration["DefaultAdmin:Password"] ?? "admin123");
}

app.Run();
