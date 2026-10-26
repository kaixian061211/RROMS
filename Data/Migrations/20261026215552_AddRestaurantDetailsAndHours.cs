using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantDetailsAndHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatingHour",
                columns: table => new
                {
                    OperatingHourId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingHour", x => x.OperatingHourId);
                    table.CheckConstraint("CK_OperatingHour_Times", "[IsClosed] = 1 OR ([OpenTime] IS NOT NULL AND [CloseTime] IS NOT NULL AND [CloseTime] > [OpenTime])");
                });

            migrationBuilder.CreateTable(
                name: "RestaurantInfo",
                columns: table => new
                {
                    RestaurantInfoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ServiceChargePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsAcceptingOrders = table.Column<bool>(type: "bit", nullable: false),
                    IsAcceptingReservations = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantInfo", x => x.RestaurantInfoId);
                    table.CheckConstraint("CK_RestaurantInfo_ServiceCharge", "[ServiceChargePercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_RestaurantInfo_Tax", "[TaxPercent] BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingHour_DayOfWeek",
                table: "OperatingHour",
                column: "DayOfWeek",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatingHour");

            migrationBuilder.DropTable(
                name: "RestaurantInfo");
        }
    }
}
