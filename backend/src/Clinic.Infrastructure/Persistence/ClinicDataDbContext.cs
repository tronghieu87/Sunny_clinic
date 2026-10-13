using Clinic.Application;
using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Persistence;

public sealed class ClinicDataDbContext(DbContextOptions<ClinicDataDbContext> options, ICurrentTenant? currentTenant = null)
    : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeCodeSequence> EmployeeCodeSequences => Set<EmployeeCodeSequence>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<ClinicRoom> ClinicRooms => Set<ClinicRoom>();
    public DbSet<ClinicDataAuditEvent> AuditEvents => Set<ClinicDataAuditEvent>();
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();
    public DbSet<CategoryItem> CategoryItems => Set<CategoryItem>();
    public DbSet<AdministrativeUnitCatalog> AdministrativeUnitCatalogs => Set<AdministrativeUnitCatalog>();
    public DbSet<AdministrativeUnit> AdministrativeUnits => Set<AdministrativeUnit>();
    public DbSet<Icd10Catalog> Icd10Catalogs => Set<Icd10Catalog>();
    public DbSet<Icd10Code> Icd10Codes => Set<Icd10Code>();
    private Guid? CurrentTenantId => currentTenant?.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var patient = modelBuilder.Entity<Patient>();
        patient.ToTable("patients", table => table.HasCheckConstraint("ck_patients_code", "length(btrim(\"PatientCode\")) > 0"));
        patient.HasKey(item => item.Id);
        patient.Property(item => item.TenantId).IsConcurrencyToken();
        patient.HasAlternateKey(item => new { item.TenantId, item.Id });
        patient.Property(item => item.PatientCode).HasMaxLength(50).IsRequired();
        patient.HasIndex(item => new { item.TenantId, item.PatientCode }).IsUnique();
        patient.HasIndex(item => new { item.TenantId, item.PhoneNumber });
        patient.HasQueryFilter(item => item.TenantId == CurrentTenantId);
        patient.Property(item => item.FullName).IsRequired().HasMaxLength(200);
        patient.Property(item => item.PhoneNumber).HasMaxLength(30);
        patient.Property(item => item.CreatedAtUtc).IsRequired();

        var employee = modelBuilder.Entity<Employee>();
        employee.ToTable("employees", table => {
            table.HasCheckConstraint("ck_employees_code", "length(btrim(\"EmployeeCode\")) > 0");
            table.HasCheckConstraint("ck_employees_name", "length(btrim(\"FullName\")) > 0");
            table.HasCheckConstraint("ck_employees_identity_document", "(\"IdentityDocumentTypeId\" IS NULL AND \"IdentityDocumentNumber\" IS NULL) OR (\"IdentityDocumentTypeId\" IS NOT NULL AND length(btrim(\"IdentityDocumentNumber\")) > 0)");
        });
        employee.HasKey(item => item.Id);
        employee.Property(item => item.TenantId).IsConcurrencyToken();
        employee.HasAlternateKey(item => new { item.TenantId, item.Id });
        employee.Property(item => item.EmployeeCode).HasMaxLength(30).IsRequired();
        employee.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        employee.Property(item => item.Position).HasMaxLength(120);
        employee.HasOne<CategoryItem>().WithMany().HasForeignKey(item => item.PositionCategoryId).OnDelete(DeleteBehavior.Restrict);
        employee.HasIndex(item => new { item.TenantId, item.PositionCategoryId });
        employee.Property(item => item.PhoneNumber).HasMaxLength(30);
        employee.Property(item => item.Email).HasMaxLength(254);
        employee.Property(item => item.IdentityDocumentNumber).HasMaxLength(50);
        employee.HasIndex(item => new { item.TenantId, item.EmployeeCode }).IsUnique();
        employee.HasIndex(item => new { item.TenantId, item.MembershipId }).IsUnique().HasFilter("\"MembershipId\" IS NOT NULL");
        employee.HasIndex(item => new { item.TenantId, item.PhoneNumber });
        employee.HasIndex(item => new { item.TenantId, item.FullName });
        employee.HasOne<CategoryItem>().WithMany().HasForeignKey(item => item.GenderCategoryId).OnDelete(DeleteBehavior.Restrict);
        employee.HasOne<CategoryItem>().WithMany().HasForeignKey(item => item.IdentityDocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        employee.HasOne<CategoryItem>().WithMany().HasForeignKey(item => item.OccupationCategoryId).OnDelete(DeleteBehavior.Restrict);
        employee.HasQueryFilter(item => item.TenantId == CurrentTenantId);

        var employeeCodeSequence = modelBuilder.Entity<EmployeeCodeSequence>();
        employeeCodeSequence.ToTable("employee_code_sequences", table =>
            table.HasCheckConstraint("ck_employee_code_sequences_last_value", "\"LastValue\" > 0"));
        employeeCodeSequence.HasKey(item => item.TenantId);
        employeeCodeSequence.Property(item => item.TenantId).IsConcurrencyToken();
        employeeCodeSequence.HasQueryFilter(item => item.TenantId == CurrentTenantId);

        var specialty = modelBuilder.Entity<Specialty>();
        specialty.ToTable("specialties", table => {
            table.HasCheckConstraint("ck_specialties_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_specialties_name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_specialties_sort_order", "\"SortOrder\" >= 0");
        });
        specialty.HasKey(item => item.Id);
        specialty.Property(item => item.TenantId).IsConcurrencyToken();
        specialty.HasAlternateKey(item => new { item.TenantId, item.Id });
        specialty.Property(item => item.Code).HasMaxLength(30).IsRequired();
        specialty.Property(item => item.Name).HasMaxLength(120).IsRequired();
        specialty.Property(item => item.Description).HasMaxLength(500);
        specialty.HasIndex(item => new { item.TenantId, item.Code }).IsUnique();
        specialty.HasIndex(item => new { item.TenantId, item.IsActive, item.SortOrder });
        specialty.HasQueryFilter(item => item.TenantId == CurrentTenantId);

        var clinicRoom = modelBuilder.Entity<ClinicRoom>();
        clinicRoom.ToTable("clinic_rooms", table => {
            table.HasCheckConstraint("ck_clinic_rooms_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_clinic_rooms_name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_clinic_rooms_room_type", "\"RoomType\" IN ('EXAMINATION','PROCEDURE','DIAGNOSTIC','OTHER')");
        });
        clinicRoom.HasKey(item => item.Id);
        clinicRoom.Property(item => item.TenantId).IsConcurrencyToken();
        clinicRoom.HasAlternateKey(item => new { item.TenantId, item.Id });
        clinicRoom.Property(item => item.Code).HasMaxLength(30).IsRequired();
        clinicRoom.Property(item => item.Name).HasMaxLength(120).IsRequired();
        clinicRoom.Property(item => item.RoomType).HasMaxLength(20).IsRequired();
        clinicRoom.Property(item => item.Description).HasMaxLength(500);
        clinicRoom.HasIndex(item => new { item.TenantId, item.BranchId, item.Code }).IsUnique();
        clinicRoom.HasIndex(item => new { item.TenantId, item.BranchId, item.IsActive, item.Name });
        clinicRoom.HasOne<Specialty>().WithMany().HasForeignKey(item => new { item.TenantId, item.SpecialtyId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        clinicRoom.HasQueryFilter(item => item.TenantId == CurrentTenantId);

        var clinicAudit = modelBuilder.Entity<ClinicDataAuditEvent>();
        clinicAudit.ToTable("clinic_data_audit_events");
        clinicAudit.HasKey(item => item.Id);
        clinicAudit.Property(item => item.TenantId).IsConcurrencyToken();
        clinicAudit.Property(item => item.Action).HasMaxLength(100).IsRequired();
        clinicAudit.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
        clinicAudit.HasIndex(item => new { item.TenantId, item.OccurredAtUtc });
        clinicAudit.HasIndex(item => new { item.TenantId, item.EntityType, item.ResourceId });
        clinicAudit.HasQueryFilter(item => item.TenantId == CurrentTenantId);

        var categoryGroup = modelBuilder.Entity<CategoryGroup>();
        categoryGroup.ToTable("category_groups", table => {
            table.HasCheckConstraint("ck_category_groups_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_category_groups_name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_category_groups_sort_order", "\"SortOrder\" >= 0");
        });
        categoryGroup.HasKey(item => item.Id);
        categoryGroup.Property(item => item.TenantId).IsConcurrencyToken();
        categoryGroup.Property(item => item.IsSystem).IsRequired();
        categoryGroup.Property(item => item.Code).HasMaxLength(50).IsRequired();
        categoryGroup.Property(item => item.Name).HasMaxLength(120).IsRequired();
        categoryGroup.Property(item => item.Description).HasMaxLength(500);
        categoryGroup.HasIndex(item => item.Code).IsUnique().HasFilter("\"TenantId\" IS NULL");
        categoryGroup.HasIndex(item => new { item.TenantId, item.Code }).IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
        categoryGroup.HasIndex(item => new { item.TenantId, item.IsActive, item.SortOrder });
        categoryGroup.HasQueryFilter(item => item.TenantId == null || item.TenantId == CurrentTenantId);

        var categoryItem = modelBuilder.Entity<CategoryItem>();
        categoryItem.ToTable("category_items", table => {
            table.HasCheckConstraint("ck_category_items_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_category_items_name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_category_items_sort_order", "\"SortOrder\" >= 0");
        });
        categoryItem.HasKey(item => item.Id);
        categoryItem.Property(item => item.TenantId).IsConcurrencyToken();
        categoryItem.Property(item => item.Code).HasMaxLength(50).IsRequired();
        categoryItem.Property(item => item.Name).HasMaxLength(120).IsRequired();
        categoryItem.HasOne(item => item.CategoryGroup).WithMany(group => group.Items)
            .HasForeignKey(item => item.CategoryGroupId).OnDelete(DeleteBehavior.Restrict);
        categoryItem.HasIndex(item => new { item.CategoryGroupId, item.Code }).IsUnique();
        categoryItem.HasIndex(item => new { item.TenantId, item.CategoryGroupId, item.IsActive, item.SortOrder });
        categoryItem.HasQueryFilter(item => item.TenantId == null || item.TenantId == CurrentTenantId);

        var administrativeCatalog = modelBuilder.Entity<AdministrativeUnitCatalog>();
        administrativeCatalog.ToTable("administrative_unit_catalogs", table => {
            table.HasCheckConstraint("ck_administrative_unit_catalogs_version", "length(btrim(\"Version\")) > 0");
            table.HasCheckConstraint("ck_administrative_unit_catalogs_source_file", "length(btrim(\"SourceFileName\")) > 0");
        });
        administrativeCatalog.HasKey(item => item.Id);
        administrativeCatalog.Property(item => item.Version).HasMaxLength(80).IsRequired();
        administrativeCatalog.Property(item => item.SourceFileName).HasMaxLength(200).IsRequired();
        administrativeCatalog.HasIndex(item => item.Version).IsUnique();

        var administrativeUnit = modelBuilder.Entity<AdministrativeUnit>();
        administrativeUnit.ToTable("administrative_units", table => {
            table.HasCheckConstraint("ck_administrative_units_level", "\"Level\" IN (1, 2)");
            table.HasCheckConstraint("ck_administrative_units_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_administrative_units_name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_administrative_units_sort_order", "\"SortOrder\" >= 0");
            table.HasCheckConstraint("ck_administrative_units_parent_level", "(\"Level\" = 1 AND \"ParentId\" IS NULL) OR (\"Level\" = 2 AND \"ParentId\" IS NOT NULL)");
        });
        administrativeUnit.HasKey(item => item.Id);
        administrativeUnit.Property(item => item.Code).HasMaxLength(20).IsRequired();
        administrativeUnit.Property(item => item.Name).HasMaxLength(160).IsRequired();
        administrativeUnit.Property(item => item.UnitType).HasMaxLength(30).IsRequired();
        administrativeUnit.Property(item => item.LegacyProvinceCode).HasMaxLength(20);
        administrativeUnit.Property(item => item.LegacyDistrictCode).HasMaxLength(20);
        administrativeUnit.Property(item => item.LegacyDistrictName).HasMaxLength(160);
        administrativeUnit.Property(item => item.ReviewStatus).HasMaxLength(80);
        administrativeUnit.HasOne(item => item.Catalog).WithMany(catalog => catalog.Units)
            .HasForeignKey(item => item.CatalogId).OnDelete(DeleteBehavior.Restrict);
        administrativeUnit.HasOne(item => item.Parent).WithMany(parent => parent.Children)
            .HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
        administrativeUnit.HasIndex(item => new { item.CatalogId, item.Level, item.Code }).IsUnique();
        administrativeUnit.HasIndex(item => new { item.CatalogId, item.ParentId, item.SortOrder, item.Name });

        var icd10Catalog = modelBuilder.Entity<Icd10Catalog>();
        icd10Catalog.ToTable("icd10_catalogs", table => {
            table.HasCheckConstraint("ck_icd10_catalogs_version", "length(btrim(\"Version\")) > 0");
            table.HasCheckConstraint("ck_icd10_catalogs_source_file", "length(btrim(\"SourceFileName\")) > 0");
            table.HasCheckConstraint("ck_icd10_catalogs_source_count", "\"SourceRecordCount\" >= 0 AND \"DuplicateRowsConsolidated\" >= 0");
        });
        icd10Catalog.HasKey(item => item.Id);
        icd10Catalog.Property(item => item.Version).HasMaxLength(80).IsRequired();
        icd10Catalog.Property(item => item.SourceFileName).HasMaxLength(200).IsRequired();
        icd10Catalog.Property(item => item.SourceSheet).HasMaxLength(100).IsRequired();
        icd10Catalog.Property(item => item.DocumentTitle).HasMaxLength(1000);
        icd10Catalog.Property(item => item.DocumentNote).HasMaxLength(2000);
        icd10Catalog.HasIndex(item => item.Version).IsUnique();

        var icd10Code = modelBuilder.Entity<Icd10Code>();
        icd10Code.ToTable("icd10_codes", table => {
            table.HasCheckConstraint("ck_icd10_codes_code", "length(btrim(\"Code\")) > 0");
            table.HasCheckConstraint("ck_icd10_codes_normalized_code", "length(btrim(\"NormalizedCode\")) > 0");
            table.HasCheckConstraint("ck_icd10_codes_disease_name", "length(btrim(\"DiseaseName\")) > 0");
        });
        icd10Code.HasKey(item => item.Id);
        icd10Code.Property(item => item.Code).HasMaxLength(30).IsRequired();
        icd10Code.Property(item => item.NormalizedCode).HasMaxLength(30).IsRequired();
        icd10Code.Property(item => item.ChapterNumber).HasMaxLength(8);
        icd10Code.Property(item => item.ChapterCode).HasMaxLength(20);
        icd10Code.Property(item => item.MainGroupCode).HasMaxLength(30);
        icd10Code.Property(item => item.SubGroup1Code).HasMaxLength(30);
        icd10Code.Property(item => item.SubGroup2Code).HasMaxLength(30);
        icd10Code.Property(item => item.CategoryCode).HasMaxLength(30);
        foreach (var property in new[] { nameof(Icd10Code.ChapterNameEn), nameof(Icd10Code.ChapterName), nameof(Icd10Code.MainGroupNameEn), nameof(Icd10Code.MainGroupName), nameof(Icd10Code.SubGroup1NameEn), nameof(Icd10Code.SubGroup1Name), nameof(Icd10Code.SubGroup2NameEn), nameof(Icd10Code.SubGroup2Name), nameof(Icd10Code.CategoryNameEn), nameof(Icd10Code.CategoryName), nameof(Icd10Code.DiseaseNameEn) })
            icd10Code.Property<string?>(property).HasMaxLength(1000);
        icd10Code.Property(item => item.DiseaseName).HasMaxLength(1000).IsRequired();
        icd10Code.Property(item => item.Note).HasMaxLength(2000);
        icd10Code.Property(item => item.SourceUpdatedAt).HasMaxLength(100);
        icd10Code.Property(item => item.SourceRows).HasColumnType("integer[]").IsRequired();
        icd10Code.HasOne(item => item.Catalog).WithMany(catalog => catalog.Codes)
            .HasForeignKey(item => item.CatalogId).OnDelete(DeleteBehavior.Restrict);
        icd10Code.HasIndex(item => new { item.CatalogId, item.Code }).IsUnique();
        icd10Code.HasIndex(item => new { item.CatalogId, item.NormalizedCode });
        icd10Code.HasIndex(item => new { item.CatalogId, item.ChapterCode, item.Code });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateTenantWrites()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(entry => (entry.Entity is Patient or Employee or ClinicDataAuditEvent or CategoryGroup or CategoryItem) && (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)))
        {
            if (entry.Entity is ClinicDataAuditEvent && entry.State != EntityState.Added)
                throw new InvalidOperationException("Audit events cannot be edited or deleted.");
            var owner = entry.Entity switch { Patient patient => (Guid?)patient.TenantId, Employee employee => employee.TenantId, ClinicDataAuditEvent audit => audit.TenantId, CategoryGroup group => group.TenantId, CategoryItem item => item.TenantId, _ => Guid.Empty };
            if (entry.Entity is CategoryGroup or CategoryItem && owner is null)
            {
                if (currentTenant?.IsSuperAdmin != true) throw new InvalidOperationException("Only a platform super admin may change global standard categories.");
                continue;
            }
            if (CurrentTenantId is null || CurrentTenantId == Guid.Empty || owner != CurrentTenantId)
                throw new InvalidOperationException("A verified matching tenant is required for writes.");
            if (entry.State != EntityState.Added && (Guid?)entry.Property("TenantId").OriginalValue != owner)
                throw new InvalidOperationException("Tenant ownership cannot be changed.");
        }
    }
}
