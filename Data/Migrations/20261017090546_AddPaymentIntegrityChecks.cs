using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIntegrityChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payment",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payment",
                sql: "([PaymentStatus] IN ('paid','refunded') AND [PaidAt] IS NOT NULL) OR ([PaymentStatus] IN ('pending','failed') AND [PaidAt] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payment");
        }
    }
}
