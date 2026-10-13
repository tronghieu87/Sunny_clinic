using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddEmployeePositionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PositionCategoryId",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_PositionCategoryId",
                table: "employees",
                column: "PositionCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_PositionCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "PositionCategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_employees_category_items_PositionCategoryId",
                table: "employees",
                column: "PositionCategoryId",
                principalTable: "category_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION clinic_validate_employee_position() RETURNS trigger AS $$
                DECLARE position_tenant uuid; position_group_code text; group_tenant uuid;
                BEGIN
                    IF NEW."PositionCategoryId" IS NULL THEN RETURN NEW; END IF;
                    SELECT item."TenantId", group_item."Code", group_item."TenantId"
                      INTO position_tenant, position_group_code, group_tenant
                      FROM category_items item
                      JOIN category_groups group_item ON group_item."Id" = item."CategoryGroupId"
                     WHERE item."Id" = NEW."PositionCategoryId";
                    IF NOT FOUND OR position_tenant IS DISTINCT FROM NEW."TenantId"
                        OR group_tenant IS DISTINCT FROM NEW."TenantId"
                        OR position_group_code IS DISTINCT FROM 'EMPLOYEE_POSITION' THEN
                        RAISE EXCEPTION 'Employee position must belong to the employee tenant'
                            USING ERRCODE = '23503', CONSTRAINT = 'fk_employees_position_scope';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_employees_position_scope
                    BEFORE INSERT OR UPDATE OF "TenantId", "PositionCategoryId" ON employees
                    FOR EACH ROW EXECUTE FUNCTION clinic_validate_employee_position();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_employees_position_scope ON employees; DROP FUNCTION IF EXISTS clinic_validate_employee_position();");
            migrationBuilder.DropForeignKey(
                name: "FK_employees_category_items_PositionCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_PositionCategoryId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_TenantId_PositionCategoryId",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "PositionCategoryId",
                table: "employees");
        }
    }
}
