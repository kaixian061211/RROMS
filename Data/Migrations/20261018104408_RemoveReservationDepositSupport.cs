using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReservationDepositSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Payment] WHERE [OrderId] IS NULL)
                    THROW 51000, 'Cannot require OrderId while reservation-only payments exist. Resolve those payment records before applying this migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Payment_OrderId",
                table: "Payment");

            migrationBuilder.DropIndex(
                name: "IX_Payment_ReservationId",
                table: "Payment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_HasReservationOrOrder",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Payment");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "Payment",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_OrderId",
                table: "Payment",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_OrderId",
                table: "Payment");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "Payment",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "Payment",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_OrderId",
                table: "Payment",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ReservationId",
                table: "Payment",
                column: "ReservationId",
                unique: true,
                filter: "[OrderId] IS NULL AND [ReservationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_HasReservationOrOrder",
                table: "Payment",
                sql: "[ReservationId] IS NOT NULL OR [OrderId] IS NOT NULL");
        }
    }
}
