using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuOperationAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => x.Id);
                    table.UniqueConstraint("AK_menu_items_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_menu_items_menu_items_TenantId_ParentId",
                        columns: x => new { x.TenantId, x.ParentId },
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_menu_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "menu_operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_operations", x => new { x.TenantId, x.MenuId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_menu_operations_menu_items_TenantId_MenuId",
                        columns: x => new { x.TenantId, x.MenuId },
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_menu_operations_permissions_TenantId_PermissionCode",
                        columns: x => new { x.TenantId, x.PermissionCode },
                        principalTable: "permissions",
                        principalColumns: new[] { "TenantId", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "menu_operation_endpoints",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    EndpointCode = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_operation_endpoints", x => new { x.TenantId, x.MenuId, x.PermissionCode, x.EndpointCode });
                    table.ForeignKey(
                        name: "FK_menu_operation_endpoints_api_endpoints_EndpointCode",
                        column: x => x.EndpointCode,
                        principalTable: "api_endpoints",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_menu_operation_endpoints_menu_operations_TenantId_MenuId_Pe~",
                        columns: x => new { x.TenantId, x.MenuId, x.PermissionCode },
                        principalTable: "menu_operations",
                        principalColumns: new[] { "TenantId", "MenuId", "PermissionCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_menu_operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_menu_operations", x => new { x.TenantId, x.RoleId, x.MenuId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_role_menu_operations_menu_operations_TenantId_MenuId_Permis~",
                        columns: x => new { x.TenantId, x.MenuId, x.PermissionCode },
                        principalTable: "menu_operations",
                        principalColumns: new[] { "TenantId", "MenuId", "PermissionCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_menu_operations_roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_Code",
                table: "menu_items",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_ParentId",
                table: "menu_items",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_Path",
                table: "menu_items",
                columns: new[] { "TenantId", "Path" },
                unique: true,
                filter: "\"Path\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_menu_operation_endpoints_EndpointCode",
                table: "menu_operation_endpoints",
                column: "EndpointCode");

            migrationBuilder.CreateIndex(
                name: "IX_menu_operations_TenantId_PermissionCode",
                table: "menu_operations",
                columns: new[] { "TenantId", "PermissionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menu_operations_TenantId_MenuId_PermissionCode",
                table: "role_menu_operations",
                columns: new[] { "TenantId", "MenuId", "PermissionCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_operation_endpoints");

            migrationBuilder.DropTable(
                name: "role_menu_operations");

            migrationBuilder.DropTable(
                name: "menu_operations");

            migrationBuilder.DropTable(
                name: "menu_items");
        }
    }
}
