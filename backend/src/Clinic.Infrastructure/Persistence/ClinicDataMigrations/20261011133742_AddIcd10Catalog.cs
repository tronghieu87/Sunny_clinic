using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddIcd10Catalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "icd10_catalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceSheet = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentTitle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceRecordCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateRowsConsolidated = table.Column<int>(type: "integer", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_icd10_catalogs", x => x.Id);
                    table.CheckConstraint("ck_icd10_catalogs_source_count", "\"SourceRecordCount\" >= 0 AND \"DuplicateRowsConsolidated\" >= 0");
                    table.CheckConstraint("ck_icd10_catalogs_source_file", "length(btrim(\"SourceFileName\")) > 0");
                    table.CheckConstraint("ck_icd10_catalogs_version", "length(btrim(\"Version\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "icd10_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChapterNumber = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ChapterCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChapterNameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChapterName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MainGroupCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MainGroupNameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MainGroupName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubGroup1Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SubGroup1NameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubGroup1Name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubGroup2Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SubGroup2NameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubGroup2Name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CategoryCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CategoryNameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DiseaseNameEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DiseaseName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceUpdatedAt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceRows = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_icd10_codes", x => x.Id);
                    table.CheckConstraint("ck_icd10_codes_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_icd10_codes_disease_name", "length(btrim(\"DiseaseName\")) > 0");
                    table.CheckConstraint("ck_icd10_codes_normalized_code", "length(btrim(\"NormalizedCode\")) > 0");
                    table.ForeignKey(
                        name: "FK_icd10_codes_icd10_catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "icd10_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_icd10_catalogs_Version",
                table: "icd10_catalogs",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_icd10_codes_CatalogId_ChapterCode_Code",
                table: "icd10_codes",
                columns: new[] { "CatalogId", "ChapterCode", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_icd10_codes_CatalogId_Code",
                table: "icd10_codes",
                columns: new[] { "CatalogId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_icd10_codes_CatalogId_NormalizedCode",
                table: "icd10_codes",
                columns: new[] { "CatalogId", "NormalizedCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "icd10_codes");

            migrationBuilder.DropTable(
                name: "icd10_catalogs");
        }
    }
}
