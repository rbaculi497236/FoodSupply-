using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodSupply.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCrmFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CrmNotes",
                table: "Customers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpDate",
                table: "Customers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrmNotes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NextFollowUpDate",
                table: "Customers");
        }
    }
}
