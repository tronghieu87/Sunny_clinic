using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAccessCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_menus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseApi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_menus_platform_menus_ParentId",
                        column: x => x.ParentId,
                        principalTable: "platform_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_operations",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_operations", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "platform_menu_operations",
                columns: table => new
                {
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_menu_operations", x => new { x.MenuId, x.OperationCode });
                    table.ForeignKey(
                        name: "FK_platform_menu_operations_platform_menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "platform_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_menu_operations_platform_operations_OperationCode",
                        column: x => x.OperationCode,
                        principalTable: "platform_operations",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_menu_endpoints",
                columns: table => new
                {
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EndpointCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_menu_endpoints", x => new { x.MenuId, x.OperationCode, x.EndpointCode });
                    table.ForeignKey(
                        name: "FK_platform_menu_endpoints_api_endpoints_EndpointCode",
                        column: x => x.EndpointCode,
                        principalTable: "api_endpoints",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_menu_endpoints_platform_menu_operations_MenuId_Ope~",
                        columns: x => new { x.MenuId, x.OperationCode },
                        principalTable: "platform_menu_operations",
                        principalColumns: new[] { "MenuId", "OperationCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_role_menu_grants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_role_menu_grants", x => new { x.TenantId, x.RoleId, x.MenuId, x.OperationCode });
                    table.ForeignKey(
                        name: "FK_tenant_role_menu_grants_platform_menu_operations_MenuId_Ope~",
                        columns: x => new { x.MenuId, x.OperationCode },
                        principalTable: "platform_menu_operations",
                        principalColumns: new[] { "MenuId", "OperationCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_role_menu_grants_roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Keep the former tenant catalog tables as audit/history. Select the most complete
            // configured tenant as the initial software catalog. Grants migrate only where
            // every effective global endpoint already existed for that tenant's same function.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE _platform_catalog_source ON COMMIT DROP AS
                SELECT "TenantId" FROM menu_items
                GROUP BY "TenantId"
                ORDER BY count(*) FILTER (WHERE "IsActive") DESC, count(*) DESC, "TenantId"
                LIMIT 1;

                INSERT INTO platform_operations ("Code", "Description", "IsActive", "IsSystem")
                SELECT DISTINCT ON (p."Code") p."Code", p."Description", p."IsActive", false
                FROM permissions p
                WHERE (NOT p."IsSystem" OR EXISTS (
                    SELECT 1 FROM menu_operations mo WHERE mo."TenantId" = p."TenantId" AND mo."PermissionCode" = p."Code"))
                ORDER BY p."Code", (p."TenantId" = (SELECT "TenantId" FROM _platform_catalog_source)) DESC, p."IsActive" DESC;

                INSERT INTO platform_menus ("Id", "ParentId", "Code", "Name", "Path", "Kind", "BaseApi", "Description", "SortOrder", "IsActive")
                SELECT m."Id", m."ParentId", m."Code", m."Name", m."Path", m."Kind", m."BaseApi", m."Description", m."SortOrder", m."IsActive"
                FROM menu_items m JOIN _platform_catalog_source source ON source."TenantId" = m."TenantId";

                INSERT INTO platform_menu_operations ("MenuId", "OperationCode")
                SELECT mo."MenuId", mo."PermissionCode"
                FROM menu_operations mo JOIN _platform_catalog_source source ON source."TenantId" = mo."TenantId"
                JOIN platform_operations po ON po."Code" = mo."PermissionCode";

                INSERT INTO platform_menu_endpoints ("MenuId", "OperationCode", "EndpointCode")
                SELECT moe."MenuId", moe."PermissionCode", moe."EndpointCode"
                FROM menu_operation_endpoints moe
                JOIN _platform_catalog_source source ON source."TenantId" = moe."TenantId"
                JOIN platform_menu_operations pmo ON pmo."MenuId" = moe."MenuId" AND pmo."OperationCode" = moe."PermissionCode";

                INSERT INTO tenant_role_menu_grants ("TenantId", "RoleId", "MenuId", "OperationCode")
                SELECT DISTINCT old_grant."TenantId", old_grant."RoleId", pm."Id", old_grant."PermissionCode"
                FROM role_menu_operations old_grant
                JOIN menu_items old_menu ON old_menu."TenantId" = old_grant."TenantId" AND old_menu."Id" = old_grant."MenuId"
                JOIN platform_menus pm ON pm."Code" = old_menu."Code"
                JOIN platform_menu_operations pmo ON pmo."MenuId" = pm."Id" AND pmo."OperationCode" = old_grant."PermissionCode"
                WHERE NOT EXISTS (
                    SELECT 1 FROM platform_menu_endpoints pe
                    WHERE pe."MenuId" = pm."Id" AND pe."OperationCode" = old_grant."PermissionCode"
                    AND NOT EXISTS (
                        SELECT 1 FROM menu_operation_endpoints old_endpoint
                        WHERE old_endpoint."TenantId" = old_grant."TenantId"
                        AND old_endpoint."MenuId" = old_grant."MenuId"
                        AND old_endpoint."PermissionCode" = old_grant."PermissionCode"
                        AND old_endpoint."EndpointCode" = pe."EndpointCode"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_platform_menu_endpoints_EndpointCode",
                table: "platform_menu_endpoints",
                column: "EndpointCode");

            migrationBuilder.CreateIndex(
                name: "IX_platform_menu_operations_OperationCode",
                table: "platform_menu_operations",
                column: "OperationCode");

            migrationBuilder.CreateIndex(
                name: "IX_platform_menus_Code",
                table: "platform_menus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_menus_ParentId",
                table: "platform_menus",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_menus_Path",
                table: "platform_menus",
                column: "Path",
                unique: true,
                filter: "\"Path\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_role_menu_grants_MenuId_OperationCode",
                table: "tenant_role_menu_grants",
                columns: new[] { "MenuId", "OperationCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_menu_endpoints");

            migrationBuilder.DropTable(
                name: "tenant_role_menu_grants");

            migrationBuilder.DropTable(
                name: "platform_menu_operations");

            migrationBuilder.DropTable(
                name: "platform_menus");

            migrationBuilder.DropTable(
                name: "platform_operations");
        }
    }
}
