using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodSupply.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAlertsAndProductPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Boxes",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PiecesPerBox",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DamagedQuantity",
                table: "Inventories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Inventories",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpoiledQuantity",
                table: "Inventories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boxes",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PiecesPerBox",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DamagedQuantity",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "SpoiledQuantity",
                table: "Inventories");
        }
    }
}
