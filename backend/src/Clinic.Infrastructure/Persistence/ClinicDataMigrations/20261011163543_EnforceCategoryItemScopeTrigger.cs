using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class EnforceCategoryItemScopeTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION clinic_validate_category_item_scope() RETURNS trigger AS $$
                DECLARE group_tenant uuid;
                BEGIN
                    SELECT "TenantId" INTO group_tenant FROM category_groups WHERE "Id" = NEW."CategoryGroupId";
                    IF NOT FOUND OR group_tenant IS DISTINCT FROM NEW."TenantId" THEN
                        RAISE EXCEPTION 'Category item tenant must match its category group scope'
                            USING ERRCODE = '23503', CONSTRAINT = 'fk_category_items_scope';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_category_items_scope
                    BEFORE INSERT OR UPDATE OF "TenantId", "CategoryGroupId" ON category_items
                    FOR EACH ROW EXECUTE FUNCTION clinic_validate_category_item_scope();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_category_items_scope ON category_items; DROP FUNCTION IF EXISTS clinic_validate_category_item_scope();");
        }
    }
}
