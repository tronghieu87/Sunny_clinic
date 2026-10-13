namespace Clinic.Domain.Entities;

public sealed class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
