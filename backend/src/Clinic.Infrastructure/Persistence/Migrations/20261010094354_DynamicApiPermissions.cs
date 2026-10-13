using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Clinic.Infrastructure.Persistence.Migrations;
public partial class DynamicApiPermissions : Migration {
 protected override void Up(MigrationBuilder migrationBuilder) {
  migrationBuilder.Sql("""
   ALTER TABLE role_permissions DROP CONSTRAINT "FK_role_permissions_permissions_PermissionCode";
   ALTER TABLE permissions RENAME TO system_permission_templates;
   ALTER TABLE system_permission_templates RENAME CONSTRAINT "PK_permissions" TO "PK_system_permission_templates";
   CREATE TABLE permissions (
    "TenantId" uuid NOT NULL, "Code" varchar(100) NOT NULL, "Description" varchar(200) NOT NULL,
    "IsSystem" boolean NOT NULL, "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_permissions" PRIMARY KEY ("TenantId", "Code"),
    CONSTRAINT "FK_permissions_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants("Id") ON DELETE RESTRICT);
   INSERT INTO permissions SELECT t."Id", p."Code", p."Description", true, true FROM tenants t CROSS JOIN system_permission_templates p;
   ALTER TABLE role_permissions ADD CONSTRAINT "FK_role_permissions_permissions_TenantId_PermissionCode"
    FOREIGN KEY ("TenantId", "PermissionCode") REFERENCES permissions("TenantId", "Code") ON DELETE RESTRICT;
   CREATE TABLE api_endpoints (
    "Code" varchar(100) NOT NULL, "Method" varchar(10) NOT NULL, "Route" varchar(500) NOT NULL,
    "Description" varchar(200) NOT NULL, "TenantWide" boolean NOT NULL, "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_api_endpoints" PRIMARY KEY ("Code"));
   INSERT INTO api_endpoints VALUES
    ('organization.details','GET','organization','Thông tin đơn vị',false,true),
    ('organization.branches','GET','organization/branches','Danh sách cơ sở',true,true);
   CREATE TABLE permission_endpoints (
    "TenantId" uuid NOT NULL, "PermissionCode" varchar(100) NOT NULL, "EndpointCode" varchar(100) NOT NULL,
    CONSTRAINT "PK_permission_endpoints" PRIMARY KEY ("TenantId", "PermissionCode", "EndpointCode"),
    CONSTRAINT "FK_permission_endpoints_permissions_TenantId_PermissionCode" FOREIGN KEY ("TenantId", "PermissionCode") REFERENCES permissions("TenantId", "Code") ON DELETE RESTRICT,
    CONSTRAINT "FK_permission_endpoints_api_endpoints_EndpointCode" FOREIGN KEY ("EndpointCode") REFERENCES api_endpoints("Code") ON DELETE RESTRICT);
   CREATE INDEX "IX_permission_endpoints_EndpointCode" ON permission_endpoints("EndpointCode");
   INSERT INTO permission_endpoints SELECT "TenantId", "Code", CASE "Code" WHEN 'organization.read' THEN 'organization.details' ELSE 'organization.branches' END
    FROM permissions WHERE "Code" IN ('organization.read','branches.read');
   """);
 }
 protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Tenant-specific permissions cannot be safely merged into a global catalog. Restore a backup or apply a forward migration.");
}
