using Microsoft.AspNetCore.Identity;
namespace Clinic.Infrastructure.Persistence;
public sealed class ClinicUser : IdentityUser<Guid> { public string DisplayName { get; set; } = string.Empty; public bool IsSuperAdmin { get; set; }
 public bool IsActive { get; set; } = true; }
