using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.ClinicDataMigrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinic_data_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_data_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GenderCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    IdentityDocumentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityDocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OccupationCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.Id);
                    table.UniqueConstraint("AK_employees_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("ck_employees_code", "length(btrim(\"EmployeeCode\")) > 0");
                    table.CheckConstraint("ck_employees_identity_document", "(\"IdentityDocumentTypeId\" IS NULL AND \"IdentityDocumentNumber\" IS NULL) OR (\"IdentityDocumentTypeId\" IS NOT NULL AND length(btrim(\"IdentityDocumentNumber\")) > 0)");
                    table.CheckConstraint("ck_employees_name", "length(btrim(\"FullName\")) > 0");
                    table.ForeignKey(
                        name: "FK_employees_category_items_TenantId_GenderCategoryId",
                        columns: x => new { x.TenantId, x.GenderCategoryId },
                        principalTable: "category_items",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employees_category_items_TenantId_IdentityDocumentTypeId",
                        columns: x => new { x.TenantId, x.IdentityDocumentTypeId },
                        principalTable: "category_items",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employees_category_items_TenantId_OccupationCategoryId",
                        columns: x => new { x.TenantId, x.OccupationCategoryId },
                        principalTable: "category_items",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_data_audit_events_TenantId_EntityType_ResourceId",
                table: "clinic_data_audit_events",
                columns: new[] { "TenantId", "EntityType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_data_audit_events_TenantId_OccurredAtUtc",
                table: "clinic_data_audit_events",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_EmployeeCode",
                table: "employees",
                columns: new[] { "TenantId", "EmployeeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_FullName",
                table: "employees",
                columns: new[] { "TenantId", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_GenderCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "GenderCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_IdentityDocumentTypeId",
                table: "employees",
                columns: new[] { "TenantId", "IdentityDocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_MembershipId",
                table: "employees",
                columns: new[] { "TenantId", "MembershipId" },
                unique: true,
                filter: "\"MembershipId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_OccupationCategoryId",
                table: "employees",
                columns: new[] { "TenantId", "OccupationCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_TenantId_PhoneNumber",
                table: "employees",
                columns: new[] { "TenantId", "PhoneNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_data_audit_events");

            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}
