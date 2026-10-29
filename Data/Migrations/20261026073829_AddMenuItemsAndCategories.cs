using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuCategory",
                columns: table => new
                {
                    MenuCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategory", x => x.MenuCategoryId);
                    table.CheckConstraint("CK_MenuCategory_DisplayOrder", "[DisplayOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MenuItem",
                columns: table => new
                {
                    MenuItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MenuCategoryId = table.Column<int>(type: "int", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItem", x => x.MenuItemId);
                    table.CheckConstraint("CK_MenuItem_Price", "[Price] > 0");
                    table.ForeignKey(
                        name: "FK_MenuItem_MenuCategory_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalTable: "MenuCategory",
                        principalColumn: "MenuCategoryId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategory_Name",
                table: "MenuCategory",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItem_MenuCategoryId",
                table: "MenuItem",
                column: "MenuCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItem");

            migrationBuilder.DropTable(
                name: "MenuCategory");
        }
    }
}
