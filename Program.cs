using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
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
builder.Services.AddScoped<IPaymentOrderProvider, PaymentOrderProvider>();
builder.Services.AddHttpContextAccessor();


// ---- Payment module ----

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<IPaymentQueries, PaymentQueries>();
builder.Services.AddScoped<IPaymentNotifier, LoggingPaymentNotifier>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
QuestPDF.Settings.License = LicenseType.Community;   // free for individuals, students and small businesses
builder.Services.AddSingleton<IReceiptService, ReceiptService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Dev only: create the database on startup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
