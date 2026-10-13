using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantBranchFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy records have no reliable owner. Never silently assign them to a tenant.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Patients") THEN
                        RAISE EXCEPTION 'Existing patients require explicit tenant mapping before this migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "PatientCode",
                table: "Patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Patients",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                    table.CheckConstraint("ck_tenants_code", "length(btrim(\"Code\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.UniqueConstraint("AK_branches_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_branches_code", "length(btrim(\"Code\")) > 0");
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_PatientCode",
                table: "Patients",
                columns: new[] { "TenantId", "PatientCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_PhoneNumber",
                table: "Patients",
                columns: new[] { "TenantId", "PhoneNumber" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_patients_code",
                table: "Patients",
                sql: "length(btrim(\"PatientCode\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Code",
                table: "branches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Code",
                table: "tenants",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_tenants_TenantId",
                table: "Patients",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_tenants_TenantId",
                table: "Patients");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_TenantId_PatientCode",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_TenantId_PhoneNumber",
                table: "Patients");

            migrationBuilder.DropCheckConstraint(
                name: "ck_patients_code",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PatientCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Patients");
        }
    }
}
