using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerPreference",
                columns: table => new
                {
                    CustomerPreferenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<int>(type: "int", nullable: false),
                    DietaryNeeds = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FavouriteCategories = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeatingPreferences = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreference", x => x.CustomerPreferenceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreference_CustomerUserId",
                table: "CustomerPreference",
                column: "CustomerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPreference");
        }
    }
}
