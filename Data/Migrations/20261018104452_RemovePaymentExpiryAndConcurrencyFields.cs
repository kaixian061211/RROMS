using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePaymentExpiryAndConcurrencyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "StripeRefundId",
                table: "Payment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Payment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payment",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "StripeRefundId",
                table: "Payment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
