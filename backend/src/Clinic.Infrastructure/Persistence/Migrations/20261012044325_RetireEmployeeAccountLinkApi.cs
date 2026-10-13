using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireEmployeeAccountLinkApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Account linking now belongs to the control-plane account workflow.
            // Keep operation rows and grants for history, but make them ineffective.
            migrationBuilder.Sql("""
                DELETE FROM platform_menu_endpoints
                WHERE "OperationCode" IN ('employees.account-options', 'employees.account-link', 'employees.account-unlink');
                UPDATE platform_operations SET "IsActive" = false
                WHERE "Code" IN ('employees.account-options', 'employees.account-link', 'employees.account-unlink');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Historical grants remain inert on rollback. Re-enabling access requires an
            // explicit security review and a new mapping, not an automatic migration.
        }
    }
}
