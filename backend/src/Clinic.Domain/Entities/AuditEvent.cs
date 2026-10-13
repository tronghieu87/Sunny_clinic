namespace Clinic.Domain.Entities;
public sealed class AuditEvent {
 public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; }
 public Guid ActorId { get; set; } public string Action { get; set; } = string.Empty;
 public Guid ResourceId { get; set; } public string Reason { get; set; } = string.Empty;
 public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
