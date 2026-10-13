using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_tenants_TenantId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_memberships_AspNetUsers_UserId",
                table: "tenant_memberships");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Patients",
                table: "Patients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "Patients",
                newName: "patients");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "user_tokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "auth_user_roles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "user_logins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "user_claims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "auth_roles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "auth_role_claims");

            migrationBuilder.RenameIndex(
                name: "IX_Patients_TenantId_PhoneNumber",
                table: "patients",
                newName: "IX_patients_TenantId_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Patients_TenantId_PatientCode",
                table: "patients",
                newName: "IX_patients_TenantId_PatientCode");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "auth_user_roles",
                newName: "IX_auth_user_roles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "user_logins",
                newName: "IX_user_logins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "user_claims",
                newName: "IX_user_claims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "auth_role_claims",
                newName: "IX_auth_role_claims_RoleId");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_patients_TenantId_Id",
                table: "patients",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_patients",
                table: "patients",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_tokens",
                table: "user_tokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_auth_user_roles",
                table: "auth_user_roles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_logins",
                table: "user_logins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_claims",
                table: "user_claims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_auth_roles",
                table: "auth_roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_auth_role_claims",
                table: "auth_role_claims",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_auth_role_claims_auth_roles_RoleId",
                table: "auth_role_claims",
                column: "RoleId",
                principalTable: "auth_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_auth_user_roles_auth_roles_RoleId",
                table: "auth_user_roles",
                column: "RoleId",
                principalTable: "auth_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_auth_user_roles_users_UserId",
                table: "auth_user_roles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_patients_tenants_TenantId",
                table: "patients",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_memberships_users_UserId",
                table: "tenant_memberships",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_claims_users_UserId",
                table: "user_claims",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_logins_users_UserId",
                table: "user_logins",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tokens_users_UserId",
                table: "user_tokens",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_auth_role_claims_auth_roles_RoleId",
                table: "auth_role_claims");

            migrationBuilder.DropForeignKey(
                name: "FK_auth_user_roles_auth_roles_RoleId",
                table: "auth_user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_auth_user_roles_users_UserId",
                table: "auth_user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_patients_tenants_TenantId",
                table: "patients");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_memberships_users_UserId",
                table: "tenant_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_user_claims_users_UserId",
                table: "user_claims");

            migrationBuilder.DropForeignKey(
                name: "FK_user_logins_users_UserId",
                table: "user_logins");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tokens_users_UserId",
                table: "user_tokens");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_patients_TenantId_Id",
                table: "patients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_patients",
                table: "patients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_tokens",
                table: "user_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_logins",
                table: "user_logins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_claims",
                table: "user_claims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_auth_user_roles",
                table: "auth_user_roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_auth_roles",
                table: "auth_roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_auth_role_claims",
                table: "auth_role_claims");

            migrationBuilder.RenameTable(
                name: "patients",
                newName: "Patients");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "user_tokens",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "user_logins",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "user_claims",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "auth_user_roles",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "auth_roles",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "auth_role_claims",
                newName: "AspNetRoleClaims");

            migrationBuilder.RenameIndex(
                name: "IX_patients_TenantId_PhoneNumber",
                table: "Patients",
                newName: "IX_Patients_TenantId_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "IX_patients_TenantId_PatientCode",
                table: "Patients",
                newName: "IX_Patients_TenantId_PatientCode");

            migrationBuilder.RenameIndex(
                name: "IX_user_logins_UserId",
                table: "AspNetUserLogins",
                newName: "IX_AspNetUserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_user_claims_UserId",
                table: "AspNetUserClaims",
                newName: "IX_AspNetUserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_auth_user_roles_RoleId",
                table: "AspNetUserRoles",
                newName: "IX_AspNetUserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_auth_role_claims_RoleId",
                table: "AspNetRoleClaims",
                newName: "IX_AspNetRoleClaims_RoleId");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Patients",
                table: "Patients",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_tenants_TenantId",
                table: "Patients",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_memberships_AspNetUsers_UserId",
                table: "tenant_memberships",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
