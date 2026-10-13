using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovePatientsToClinicData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.patients') IS NOT NULL
                       AND EXISTS (SELECT 1 FROM public.patients LIMIT 1) THEN
                        RAISE EXCEPTION 'Cannot split databases while authorization.patients contains data. Export patient records to ClinicDataDb first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "patients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PatientCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patients", x => x.Id);
                    table.UniqueConstraint("AK_patients_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_patients_code", "length(btrim(\"PatientCode\")) > 0");
                    table.ForeignKey(
                        name: "FK_patients_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patients_TenantId_PatientCode",
                table: "patients",
                columns: new[] { "TenantId", "PatientCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patients_TenantId_PhoneNumber",
                table: "patients",
                columns: new[] { "TenantId", "PhoneNumber" });
        }
    }
}
