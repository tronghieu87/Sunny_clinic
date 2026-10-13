using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class ReplacePersonReferencesWithCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_groups", x => x.Id);
                    table.UniqueConstraint("AK_category_groups_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_category_groups_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_category_groups_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_category_groups_sort_order", "\"SortOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "category_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_items", x => x.Id);
                    table.UniqueConstraint("AK_category_items_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_category_items_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_category_items_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_category_items_sort_order", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_category_items_category_groups_TenantId_CategoryGroupId",
                        columns: x => new { x.TenantId, x.CategoryGroupId },
                        principalTable: "category_groups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                WITH old_groups AS (
                    SELECT "TenantId", "Category", MIN("SortOrder") AS "SortOrder", BOOL_OR("IsActive") AS "IsActive",
                           MIN("CreatedAtUtc") AS "CreatedAtUtc", MAX("UpdatedAtUtc") AS "UpdatedAtUtc"
                    FROM person_reference_items
                    GROUP BY "TenantId", "Category"
                ), inserted_groups AS (
                    INSERT INTO category_groups ("Id", "TenantId", "Code", "Name", "Description", "SortOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                    SELECT gen_random_uuid(), "TenantId", UPPER("Category"),
                           CASE "Category"
                               WHEN 'gender' THEN 'Giới tính'
                               WHEN 'nationality' THEN 'Quốc tịch'
                               WHEN 'ethnicity' THEN 'Dân tộc'
                               WHEN 'occupation' THEN 'Nghề nghiệp'
                               WHEN 'identity_document_type' THEN 'Loại giấy tờ định danh'
                               WHEN 'guardian_relationship' THEN 'Quan hệ người giám hộ'
                               ELSE "Category"
                           END,
                           NULL, "SortOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc"
                    FROM old_groups
                    RETURNING "TenantId", "Code", "Id"
                )
                INSERT INTO category_items ("Id", "TenantId", "CategoryGroupId", "Code", "Name", "SortOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT old."Id", old."TenantId", groups."Id", old."Code", old."Name", old."SortOrder", old."IsActive", old."CreatedAtUtc", old."UpdatedAtUtc"
                FROM person_reference_items old
                JOIN inserted_groups groups ON groups."TenantId" = old."TenantId" AND groups."Code" = UPPER(old."Category");
                """);

            migrationBuilder.DropTable(
                name: "person_reference_items");

            migrationBuilder.CreateIndex(
                name: "IX_category_groups_TenantId_Code",
                table: "category_groups",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_groups_TenantId_IsActive_SortOrder",
                table: "category_groups",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_category_items_TenantId_CategoryGroupId_Code",
                table: "category_items",
                columns: new[] { "TenantId", "CategoryGroupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_items_TenantId_CategoryGroupId_IsActive_SortOrder",
                table: "category_items",
                columns: new[] { "TenantId", "CategoryGroupId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_items");

            migrationBuilder.DropTable(
                name: "category_groups");

            migrationBuilder.CreateTable(
                name: "person_reference_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
    }
}
