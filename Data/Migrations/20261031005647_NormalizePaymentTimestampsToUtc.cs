using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePaymentTimestampsToUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CreatedAt is also the Stripe checkout attempt token. Close outstanding
            // online attempts before deploying this one-time conversion.
            // EF runs this migration in a transaction; hold the table lock until it finishes.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [Payment] WITH (TABLOCKX, HOLDLOCK)
                    WHERE [PaymentStatus] = 'pending'
                      AND [PaymentMethod] IN ('card', 'online_banking', 'e-wallet')
                      AND [RecordedByStaffId] IS NULL)
                    THROW 51000, 'Close pending online payment checkouts before converting Payment timestamps. No timestamps were changed.', 1;

                UPDATE [Payment]
                SET [CreatedAt] = DATEADD(hour, -8, [CreatedAt]),
                    [PaidAt] = DATEADD(hour, -8, [PaidAt]),
                    [RefundedAt] = DATEADD(hour, -8, [RefundedAt]);
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Payment",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CreatedAt is also the Stripe checkout attempt token. Close outstanding
            // online attempts before deploying this one-time conversion.
            // EF runs this migration in a transaction; hold the table lock until it finishes.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [Payment] WITH (TABLOCKX, HOLDLOCK)
                    WHERE [PaymentStatus] = 'pending'
                      AND [PaymentMethod] IN ('card', 'online_banking', 'e-wallet')
                      AND [RecordedByStaffId] IS NULL)
                    THROW 51000, 'Close pending online payment checkouts before converting Payment timestamps. No timestamps were changed.', 1;

                UPDATE [Payment]
                SET [CreatedAt] = DATEADD(hour, 8, [CreatedAt]),
                    [PaidAt] = DATEADD(hour, 8, [PaidAt]),
                    [RefundedAt] = DATEADD(hour, 8, [RefundedAt]);
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Payment",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");
        }
    }
}
