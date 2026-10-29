using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuestPDF.Infrastructure;
using RROMS.Web.Data;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments.Receipts;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.Restaurant;

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

// Sign-in cookie, carrying the claims the login module will issue: a numeric NameIdentifier and a Role.
// TODO: the login module replaces this. Until then only the Development test login signs anyone in.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Not signed in, or signed in without the right role: choose a test user.
            options.LoginPath = "/Dev/SwitchUser";
            options.AccessDeniedPath = "/Dev/SwitchUser";
        }
        else
        {
            // There is no login page yet, so refuse with 401 / 403 instead of redirecting to one.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        }
    });

builder.Services.AddScoped<IRestaurantLookup, RestaurantLookup>();
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
}

app.Run();
