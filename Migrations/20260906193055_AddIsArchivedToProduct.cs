using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodSupply.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchivedToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Categories");
        }
    }
}
