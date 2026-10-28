using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    ReservationId = table.Column<int>(type: "int", nullable: true),
                    PaymentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.NotificationId);
                    table.CheckConstraint("CK_Notification_RelatedRecord", "([Type] = 'order' AND [OrderId] IS NOT NULL AND [ReservationId] IS NULL AND [PaymentId] IS NULL) OR ([Type] = 'reservation' AND [ReservationId] IS NOT NULL AND [OrderId] IS NULL AND [PaymentId] IS NULL) OR ([Type] = 'payment' AND [PaymentId] IS NOT NULL AND [OrderId] IS NULL AND [ReservationId] IS NULL) OR ([Type] IN ('promotion','announcement') AND [OrderId] IS NULL AND [ReservationId] IS NULL AND [PaymentId] IS NULL)");
                    table.CheckConstraint("CK_Notification_Type", "[Type] IN ('reservation','order','payment','promotion','announcement')");
                    table.ForeignKey(
                        name: "FK_Notification_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payment",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservation",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_OrderId",
                table: "Notification",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_PaymentId",
                table: "Notification",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ReservationId",
                table: "Notification",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_NotificationId",
                table: "Notification",
                columns: new[] { "UserId", "NotificationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notification");
        }
    }
}
