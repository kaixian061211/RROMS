using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using RROMS.Web.Models.Payments;
using RROMS.Web.Data;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments.Receipts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddRazorPages();
builder.Services.AddAuthentication();

builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
// TODO: use a lookup that reads the real MenuItem table once it's on main.
builder.Services.AddScoped<IMenuItemLookup, TemporaryMenuItemLookup>();
builder.Services.AddScoped<IPaymentOrderProvider, PaymentOrderProvider>();
builder.Services.AddHttpContextAccessor();


// ---- Payment module ----

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<IPaymentQueries, PaymentQueries>();
builder.Services.AddScoped<IPaymentNotifier, LoggingPaymentNotifier>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
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
app.UseRouting();
app.UseAuthentication();
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        // Match the order/reservation demo: customer 1 and local staff access.
        // Real signed-in users retain their own identity; no demo cookie is stored.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "Development user"),
                new Claim(ClaimTypes.Role, PaymentRoles.Customer),
                new Claim(ClaimTypes.Role, PaymentRoles.Staff)
            }, "DevelopmentFallback"));
        }
        await next();
    });
}
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapStripeWebhook();   // POST /webhooks/stripe

// Dev only: create the database on startup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
