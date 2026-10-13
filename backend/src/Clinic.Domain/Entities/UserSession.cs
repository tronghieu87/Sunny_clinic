namespace Clinic.Domain.Entities;

/// <summary>One authenticated device/browser session. Revocation immediately invalidates its access tokens.</summary>
public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastRefreshedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? CreatedFromIp { get; set; }
    public string? UserAgent { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? BranchId { get; set; }
}

/// <summary>A single opaque refresh token in a rotation family.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
