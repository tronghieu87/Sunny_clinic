using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddClinicStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "specialties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialties", x => x.Id);
                    table.UniqueConstraint("AK_specialties_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_specialties_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_specialties_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_specialties_sort_order", "\"SortOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "clinic_rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RoomType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_rooms", x => x.Id);
                    table.UniqueConstraint("AK_clinic_rooms_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_clinic_rooms_code", "length(btrim(\"Code\")) > 0");
                    table.CheckConstraint("ck_clinic_rooms_name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("ck_clinic_rooms_room_type", "\"RoomType\" IN ('EXAMINATION','PROCEDURE','DIAGNOSTIC','OTHER')");
                    table.ForeignKey(
                        name: "FK_clinic_rooms_specialties_TenantId_SpecialtyId",
                        columns: x => new { x.TenantId, x.SpecialtyId },
                        principalTable: "specialties",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_rooms_TenantId_BranchId_Code",
                table: "clinic_rooms",
                columns: new[] { "TenantId", "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clinic_rooms_TenantId_BranchId_IsActive_Name",
                table: "clinic_rooms",
                columns: new[] { "TenantId", "BranchId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_rooms_TenantId_SpecialtyId",
                table: "clinic_rooms",
                columns: new[] { "TenantId", "SpecialtyId" });

            migrationBuilder.CreateIndex(
                name: "IX_specialties_TenantId_Code",
                table: "specialties",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_specialties_TenantId_IsActive_SortOrder",
                table: "specialties",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_rooms");

            migrationBuilder.DropTable(
                name: "specialties");
        }
    }
}
