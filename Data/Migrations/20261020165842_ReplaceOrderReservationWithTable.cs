using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceOrderReservationWithTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Order_Reservation_ReservationId",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Order_ReservationId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Order");

            migrationBuilder.AddColumn<int>(
                name: "TableId",
                table: "Order",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Order");

            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "Order",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_ReservationId",
                table: "Order",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_Reservation_ReservationId",
                table: "Order",
                column: "ReservationId",
                principalTable: "Reservation",
                principalColumn: "ReservationId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
