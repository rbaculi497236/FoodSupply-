using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodSupply.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SalesOrders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SalesOrders");
        }
    }
}
