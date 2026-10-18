using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeTransactionRefUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_TransactionRef",
                table: "Payment");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_TransactionRef",
                table: "Payment",
                column: "TransactionRef",
                unique: true,
                filter: "[TransactionRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_TransactionRef",
                table: "Payment");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_TransactionRef",
                table: "Payment",
                column: "TransactionRef");
        }
    }
}
