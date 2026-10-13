using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeUnitCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "administrative_unit_catalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_unit_catalogs", x => x.Id);
                    table.CheckConstraint("ck_administrative_unit_catalogs_source_file", "length(btrim(\"SourceFileName\")) > 0");
                    table.CheckConstraint("ck_administrative_unit_catalogs_version", "length(btrim(\"Version\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "administrative_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Level = table.Column<short>(type: "smallint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LegacyProvinceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LegacyDistrictCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LegacyDistrictName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SourceRowNumber = table.Column<int>(type: "integer", nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_units", x => x.Id);
                    table.CheckConstraint("ck_administrative_units_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_administrative_units_level", "\"Level\" IN (1, 2)");
                    table.CheckConstraint("ck_administrative_units_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_administrative_units_parent_level", "(\"Level\" = 1 AND \"ParentId\" IS NULL) OR (\"Level\" = 2 AND \"ParentId\" IS NOT NULL)");
                    table.CheckConstraint("ck_administrative_units_sort_order", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_administrative_units_administrative_unit_catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "administrative_unit_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_administrative_units_administrative_units_ParentId",
                        column: x => x.ParentId,
                        principalTable: "administrative_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_unit_catalogs_Version",
                table: "administrative_unit_catalogs",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_units_CatalogId_Level_Code",
                table: "administrative_units",
                columns: new[] { "CatalogId", "Level", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_units_CatalogId_ParentId_SortOrder_Name",
                table: "administrative_units",
                columns: new[] { "CatalogId", "ParentId", "SortOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_units_ParentId",
                table: "administrative_units",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_units");

            migrationBuilder.DropTable(
                name: "administrative_unit_catalogs");
        }
    }
}
