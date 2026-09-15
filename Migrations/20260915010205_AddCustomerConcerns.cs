using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace FoodSupply.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerConcerns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerConcerns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    CustomerId = table.Column<int>(type: "int", nullable: false),

                    ConcernType = table.Column<string>(
                        type: "varchar(50)",
                        maxLength: 50,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Subject = table.Column<string>(
                        type: "varchar(150)",
                        maxLength: 150,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Description = table.Column<string>(
                        type: "longtext",
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Priority = table.Column<string>(
                        type: "varchar(20)",
                        maxLength: 20,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Status = table.Column<string>(
                        type: "varchar(30)",
                        maxLength: 30,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Resolution = table.Column<string>(
                        type: "longtext",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    DateReported = table.Column<DateTime>(
                        type: "datetime(6)",
                        nullable: false),

                    ResolvedDate = table.Column<DateTime>(
                        type: "datetime(6)",
                        nullable: true),

                    Remarks = table.Column<string>(
                        type: "longtext",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    IsArchived = table.Column<bool>(
                        type: "tinyint(1)",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerConcerns", x => x.Id);

                    table.ForeignKey(
                        name: "FK_CustomerConcerns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerConcerns_CustomerId",
                table: "CustomerConcerns",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerConcerns");
        }
    }
}