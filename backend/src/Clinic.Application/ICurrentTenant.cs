namespace Clinic.Application;

// Must be resolved from authenticated membership, never an unverified request header.
public interface ICurrentTenant
{
    Guid? TenantId { get; }
    bool IsSuperAdmin => false;
}
