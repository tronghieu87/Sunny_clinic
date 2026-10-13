using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddPersonReferenceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "person_reference_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_reference_items", x => x.Id);
                    table.UniqueConstraint("AK_person_reference_items_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_person_reference_category", "\"Category\" IN ('gender', 'nationality', 'ethnicity', 'occupation', 'identity_document_type', 'guardian_relationship')");
                    table.CheckConstraint("ck_person_reference_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_person_reference_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_person_reference_sort_order", "\"SortOrder\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_reference_items_TenantId_Category_Code",
                table: "person_reference_items",
                columns: new[] { "TenantId", "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_reference_items_TenantId_Category_IsActive_SortOrder",
                table: "person_reference_items",
                columns: new[] { "TenantId", "Category", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_reference_items");
        }
    }
}
