using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Payment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Payment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundedByStaffId",
                table: "Payment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeRefundId",
                table: "Payment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_RefundedAt",
                table: "Payment",
                sql: "([PaymentStatus] = 'refunded' AND [RefundedAt] IS NOT NULL) OR ([PaymentStatus] <> 'refunded' AND [RefundedAt] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_RefundedAt",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RefundedByStaffId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "StripeRefundId",
                table: "Payment");
        }
    }
}
