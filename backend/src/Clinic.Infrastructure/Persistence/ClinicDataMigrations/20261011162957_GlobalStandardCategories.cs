using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class GlobalStandardCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_category_items_category_groups_TenantId_CategoryGroupId",
                table: "category_items");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_TenantId_GenderCategoryId",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_TenantId_IdentityDocumentTypeId",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_TenantId_OccupationCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_TenantId_GenderCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_TenantId_IdentityDocumentTypeId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_TenantId_OccupationCategoryId",
                table: "employees");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_category_items_TenantId_Id",
                table: "category_items");

            migrationBuilder.DropIndex(
                name: "IX_category_items_TenantId_CategoryGroupId_Code",
                table: "category_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_category_groups_TenantId_Id",
                table: "category_groups");

            migrationBuilder.DropIndex(
                name: "IX_category_groups_TenantId_Code",
                table: "category_groups");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "category_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "category_groups",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "category_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE category_groups
                SET "TenantId" = NULL, "IsSystem" = TRUE
                WHERE "Code" IN ('GENDER', 'ETHNICITY', 'OCCUPATION', 'IDENTITY_DOCUMENT', 'PATIENT_RELATIONSHIP');

                UPDATE category_items AS item
                SET "TenantId" = NULL
                FROM category_groups AS category_group
                WHERE item."CategoryGroupId" = category_group."Id"
                  AND category_group."TenantId" IS NULL
                  AND category_group."IsSystem" = TRUE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_employees_GenderCategoryId",
                table: "employees",
                column: "GenderCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_employees_IdentityDocumentTypeId",
                table: "employees",
                column: "IdentityDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_employees_OccupationCategoryId",
                table: "employees",
                column: "OccupationCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_category_items_CategoryGroupId_Code",
                table: "category_items",
                columns: new[] { "CategoryGroupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_groups_Code",
                table: "category_groups",
                column: "Code",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_category_groups_TenantId_Code",
                table: "category_groups",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_category_items_category_groups_CategoryGroupId",
                table: "category_items",
                column: "CategoryGroupId",
                principalTable: "category_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_GenderCategoryId",
                table: "employees",
                column: "GenderCategoryId",
                principalTable: "category_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_IdentityDocumentTypeId",
                table: "employees",
                column: "IdentityDocumentTypeId",
                principalTable: "category_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_OccupationCategoryId",
                table: "employees",
                column: "OccupationCategoryId",
                principalTable: "category_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_category_items_category_groups_CategoryGroupId",
                table: "category_items");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_GenderCategoryId",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_IdentityDocumentTypeId",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_OccupationCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_GenderCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_IdentityDocumentTypeId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_OccupationCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_category_items_CategoryGroupId_Code",
                table: "category_items");

            migrationBuilder.DropIndex(
                name: "IX_category_groups_Code",
                table: "category_groups");

            migrationBuilder.DropIndex(
                name: "IX_category_groups_TenantId_Code",
                table: "category_groups");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "category_groups");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "category_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "category_groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_category_items_TenantId_Id",
                table: "category_items",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_category_groups_TenantId_Id",
                table: "category_groups",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_GenderCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "GenderCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_IdentityDocumentTypeId",
                table: "employees",
                columns: new[] { "TenantId", "IdentityDocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_OccupationCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "OccupationCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_category_items_TenantId_CategoryGroupId_Code",
                table: "category_items",
                columns: new[] { "TenantId", "CategoryGroupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_groups_TenantId_Code",
                table: "category_groups",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_category_items_category_groups_TenantId_CategoryGroupId",
                table: "category_items",
                columns: new[] { "TenantId", "CategoryGroupId" },
                principalTable: "category_groups",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_TenantId_GenderCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "GenderCategoryId" },
                principalTable: "category_items",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_TenantId_IdentityDocumentTypeId",
                table: "employees",
                columns: new[] { "TenantId", "IdentityDocumentTypeId" },
                principalTable: "category_items",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_TenantId_OccupationCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "OccupationCategoryId" },
                principalTable: "category_items",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
