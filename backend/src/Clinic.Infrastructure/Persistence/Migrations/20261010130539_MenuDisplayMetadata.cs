using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuDisplayMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseApi",
                table: "menu_items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "menu_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "menu_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "function");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseApi",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "menu_items");
        }
    }
}
