namespace Clinic.Api.Security;
// Request-local, loaded from active database user after cookie authentication.
public sealed class SuperAdminAccess { public bool IsSuperAdmin { get; internal set; } }
