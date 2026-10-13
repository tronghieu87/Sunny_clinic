namespace Clinic.Domain.Entities;

/// <summary>A tenant-owned staff profile. Authentication and branch permissions remain in the authorization database.</summary>
public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? MembershipId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public Guid? PositionCategoryId { get; set; }
    public Guid? GenderCategoryId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public Guid? IdentityDocumentTypeId { get; set; }
    public string? IdentityDocumentNumber { get; set; }
    public Guid? OccupationCategoryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Atomic per-tenant allocator for human-readable employee codes.</summary>
public sealed class EmployeeCodeSequence
{
    public Guid TenantId { get; set; }
    public long LastValue { get; set; }
}

/// <summary>Audit metadata for changes to tenant-owned clinical data records.</summary>
public sealed class ClinicDataAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
