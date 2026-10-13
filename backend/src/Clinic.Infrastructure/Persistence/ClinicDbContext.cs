using Clinic.Domain.Entities;
using Clinic.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Clinic.Infrastructure.Persistence;

public sealed class AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options,
    ICurrentTenant? currentTenant = null)
    : IdentityDbContext<ClinicUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();
    public DbSet<BranchAccess> BranchAccesses => Set<BranchAccess>();
    public DbSet<BusinessRole> BusinessRoles => Set<BusinessRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ApiEndpoint> ApiEndpoints => Set<ApiEndpoint>();
    public DbSet<PermissionEndpoint> PermissionEndpoints => Set<PermissionEndpoint>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuOperation> MenuOperations => Set<MenuOperation>();
    public DbSet<RoleMenuOperation> RoleMenuOperations => Set<RoleMenuOperation>();
    public DbSet<MenuOperationEndpoint> MenuOperationEndpoints => Set<MenuOperationEndpoint>();
    public DbSet<PlatformOperation> PlatformOperations => Set<PlatformOperation>();
    public DbSet<PlatformMenu> PlatformMenus => Set<PlatformMenu>();
    public DbSet<PlatformMenuOperation> PlatformMenuOperations => Set<PlatformMenuOperation>();
    public DbSet<PlatformMenuEndpoint> PlatformMenuEndpoints => Set<PlatformMenuEndpoint>();
    public DbSet<TenantRoleMenuGrant> TenantRoleMenuGrants => Set<TenantRoleMenuGrant>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    private Guid? CurrentTenantId => currentTenant?.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var audit = modelBuilder.Entity<AuditEvent>();
        audit.ToTable("audit_events");
        audit.HasKey(a => a.Id);
        audit.Property(a => a.Action).HasMaxLength(100).IsRequired();
        audit.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        audit.HasIndex(a => new { a.TenantId, a.OccurredAt });
        audit.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        audit.HasQueryFilter(a => a.TenantId == CurrentTenantId);
        // Authentication storage is separate from future tenant-scoped business roles.
        modelBuilder.Entity<ClinicUser>().ToTable("users");
        modelBuilder.Entity<ClinicUser>().Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("auth_roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("auth_user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("auth_role_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        var session = modelBuilder.Entity<UserSession>();
        session.ToTable("user_sessions"); session.HasKey(x => x.Id);
        session.HasIndex(x => new { x.UserId, x.RevokedAtUtc, x.ExpiresAtUtc });
        session.Property(x => x.CreatedFromIp).HasMaxLength(64);
        session.Property(x => x.UserAgent).HasMaxLength(500);
        session.HasOne<ClinicUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.ToTable("refresh_tokens"); refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();
        refreshToken.HasIndex(x => new { x.SessionId, x.RevokedAtUtc });
        refreshToken.HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        var role = modelBuilder.Entity<BusinessRole>();
        role.ToTable("roles");
        role.HasKey(r => r.Id);
        role.HasAlternateKey(r => new { r.TenantId, r.Id });
        role.Property(r => r.TenantId).IsConcurrencyToken();
        role.Property(r => r.Code).HasMaxLength(50).IsRequired();
        role.Property(r => r.Name).HasMaxLength(200).IsRequired();
        role.HasIndex(r => new { r.TenantId, r.Code }).IsUnique();
        role.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        role.HasQueryFilter(r => r.TenantId == CurrentTenantId);
        var permission = modelBuilder.Entity<Permission>();
        permission.ToTable("permissions");
        permission.HasKey(p => new { p.TenantId, p.Code });
        permission.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        permission.HasQueryFilter(p => p.TenantId == CurrentTenantId);
        permission.Property(p => p.Code).HasMaxLength(100);
        permission.Property(p => p.Description).HasMaxLength(200).IsRequired();
        var endpoint = modelBuilder.Entity<ApiEndpoint>();
        endpoint.ToTable("api_endpoints"); endpoint.HasKey(e => e.Code);
        endpoint.Property(e => e.Code).HasMaxLength(100); endpoint.Property(e => e.Method).HasMaxLength(10).IsRequired();
        endpoint.Property(e => e.Route).HasMaxLength(500).IsRequired(); endpoint.Property(e => e.Description).HasMaxLength(200).IsRequired();
        var mapping = modelBuilder.Entity<PermissionEndpoint>();
        mapping.ToTable("permission_endpoints"); mapping.HasKey(m => new { m.TenantId, m.PermissionCode, m.EndpointCode });
        mapping.HasOne<Permission>().WithMany().HasForeignKey(m => new { m.TenantId, Code = m.PermissionCode }).OnDelete(DeleteBehavior.Restrict);
        mapping.HasOne<ApiEndpoint>().WithMany().HasForeignKey(m => m.EndpointCode).OnDelete(DeleteBehavior.Restrict);
        mapping.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var rp = modelBuilder.Entity<RolePermission>();
        rp.ToTable("role_permissions");
        rp.HasKey(p => new { p.TenantId, p.RoleId, p.PermissionCode });
        rp.HasOne<BusinessRole>().WithMany().HasForeignKey(p => new { p.TenantId, p.RoleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
        rp.HasOne<Permission>().WithMany().HasForeignKey(p => new { p.TenantId, Code = p.PermissionCode }).OnDelete(DeleteBehavior.Restrict);
        rp.HasQueryFilter(p => p.TenantId == CurrentTenantId);
        var assignment = modelBuilder.Entity<RoleAssignment>();
        assignment.ToTable("role_assignments");
        assignment.HasKey(a => a.Id);
        assignment.Property(a => a.TenantId).IsConcurrencyToken();
        assignment.HasIndex(a => new { a.TenantId, a.MembershipId, a.RoleId })
            .IsUnique().HasFilter("\"BranchId\" IS NULL");
        assignment.HasIndex(a => new { a.TenantId, a.MembershipId, a.RoleId, a.BranchId })
            .IsUnique().HasFilter("\"BranchId\" IS NOT NULL");
        assignment.HasOne<BusinessRole>().WithMany().HasForeignKey(a => new { a.TenantId, a.RoleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<TenantMembership>().WithMany().HasForeignKey(a => new { a.TenantId, a.MembershipId })
            .HasPrincipalKey(m => new { m.TenantId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<Branch>().WithMany().HasForeignKey(a => new { a.TenantId, a.BranchId })
            .HasPrincipalKey(b => new { b.TenantId, b.Id }).OnDelete(DeleteBehavior.Restrict);
        assignment.HasQueryFilter(a => a.TenantId == CurrentTenantId);
        var menu = modelBuilder.Entity<MenuItem>();
        menu.ToTable("menu_items"); menu.HasKey(m => m.Id); menu.HasAlternateKey(m => new { m.TenantId, m.Id });
        menu.Property(m => m.TenantId).IsConcurrencyToken(); menu.Property(m => m.Code).HasMaxLength(100).IsRequired(); menu.Property(m => m.Name).HasMaxLength(200).IsRequired(); menu.Property(m => m.Path).HasMaxLength(300);
        menu.Property(m => m.Kind).HasMaxLength(20).HasDefaultValue("function").IsRequired();
        menu.Property(m => m.BaseApi).HasMaxLength(300); menu.Property(m => m.Description).HasMaxLength(1000);
        menu.HasIndex(m => new { m.TenantId, m.Code }).IsUnique(); menu.HasIndex(m => new { m.TenantId, m.Path }).IsUnique().HasFilter("\"Path\" IS NOT NULL");
        menu.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        menu.HasOne<MenuItem>().WithMany().HasForeignKey(m => new { m.TenantId, m.ParentId }).HasPrincipalKey(m => new { m.TenantId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        menu.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var mo = modelBuilder.Entity<MenuOperation>(); mo.ToTable("menu_operations"); mo.HasKey(m => new { m.TenantId, m.MenuId, m.PermissionCode });
        mo.HasOne<MenuItem>().WithMany().HasForeignKey(m => new { m.TenantId, m.MenuId }).HasPrincipalKey(m => new { m.TenantId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        mo.HasOne<Permission>().WithMany().HasForeignKey(m => new { m.TenantId, Code = m.PermissionCode }).OnDelete(DeleteBehavior.Restrict); mo.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var rmo = modelBuilder.Entity<RoleMenuOperation>(); rmo.ToTable("role_menu_operations"); rmo.HasKey(m => new { m.TenantId, m.RoleId, m.MenuId, m.PermissionCode });
        rmo.HasOne<BusinessRole>().WithMany().HasForeignKey(m => new { m.TenantId, m.RoleId }).HasPrincipalKey(m => new { m.TenantId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        rmo.HasOne<MenuOperation>().WithMany().HasForeignKey(m => new { m.TenantId, m.MenuId, m.PermissionCode }).OnDelete(DeleteBehavior.Restrict); rmo.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var moe = modelBuilder.Entity<MenuOperationEndpoint>(); moe.ToTable("menu_operation_endpoints"); moe.HasKey(m => new { m.TenantId, m.MenuId, m.PermissionCode, m.EndpointCode });
        moe.HasOne<MenuOperation>().WithMany().HasForeignKey(m => new { m.TenantId, m.MenuId, m.PermissionCode }).OnDelete(DeleteBehavior.Restrict);
        moe.HasOne<ApiEndpoint>().WithMany().HasForeignKey(m => m.EndpointCode).OnDelete(DeleteBehavior.Restrict); moe.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var platformOperation = modelBuilder.Entity<PlatformOperation>();
        platformOperation.ToTable("platform_operations"); platformOperation.HasKey(x => x.Code);
        platformOperation.Property(x => x.Code).HasMaxLength(100);
        platformOperation.Property(x => x.Description).HasMaxLength(200).IsRequired();
        var platformMenu = modelBuilder.Entity<PlatformMenu>();
        platformMenu.ToTable("platform_menus"); platformMenu.HasKey(x => x.Id);
        platformMenu.Property(x => x.Code).HasMaxLength(100).IsRequired();
        platformMenu.Property(x => x.Name).HasMaxLength(200).IsRequired();
        platformMenu.Property(x => x.Path).HasMaxLength(300);
        platformMenu.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        platformMenu.Property(x => x.BaseApi).HasMaxLength(300);
        platformMenu.Property(x => x.Description).HasMaxLength(1000);
        platformMenu.HasIndex(x => x.Code).IsUnique();
        platformMenu.HasIndex(x => x.Path).IsUnique().HasFilter("\"Path\" IS NOT NULL");
        platformMenu.HasOne<PlatformMenu>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        var platformMenuOperation = modelBuilder.Entity<PlatformMenuOperation>();
        platformMenuOperation.ToTable("platform_menu_operations");
        platformMenuOperation.HasKey(x => new { x.MenuId, x.OperationCode });
        platformMenuOperation.Property(x => x.OperationCode).HasMaxLength(100);
        platformMenuOperation.HasOne<PlatformMenu>().WithMany().HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Restrict);
        platformMenuOperation.HasOne<PlatformOperation>().WithMany().HasForeignKey(x => x.OperationCode).OnDelete(DeleteBehavior.Restrict);
        var platformMenuEndpoint = modelBuilder.Entity<PlatformMenuEndpoint>();
        platformMenuEndpoint.ToTable("platform_menu_endpoints");
        platformMenuEndpoint.HasKey(x => new { x.MenuId, x.OperationCode, x.EndpointCode });
        platformMenuEndpoint.Property(x => x.OperationCode).HasMaxLength(100);
        platformMenuEndpoint.Property(x => x.EndpointCode).HasMaxLength(100);
        platformMenuEndpoint.HasOne<PlatformMenuOperation>().WithMany().HasForeignKey(x => new { x.MenuId, x.OperationCode }).OnDelete(DeleteBehavior.Restrict);
        platformMenuEndpoint.HasOne<ApiEndpoint>().WithMany().HasForeignKey(x => x.EndpointCode).OnDelete(DeleteBehavior.Restrict);
        var tenantRoleMenuGrant = modelBuilder.Entity<TenantRoleMenuGrant>();
        tenantRoleMenuGrant.ToTable("tenant_role_menu_grants");
        tenantRoleMenuGrant.HasKey(x => new { x.TenantId, x.RoleId, x.MenuId, x.OperationCode });
        tenantRoleMenuGrant.Property(x => x.OperationCode).HasMaxLength(100);
        tenantRoleMenuGrant.HasOne<BusinessRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.RoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        tenantRoleMenuGrant.HasOne<PlatformMenuOperation>().WithMany().HasForeignKey(x => new { x.MenuId, x.OperationCode }).OnDelete(DeleteBehavior.Restrict);
        tenantRoleMenuGrant.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        var membership = modelBuilder.Entity<TenantMembership>();
        membership.ToTable("tenant_memberships");
        membership.HasKey(m => m.Id);
        membership.HasAlternateKey(m => new { m.TenantId, m.Id });
        membership.Property(m => m.TenantId).IsConcurrencyToken();
        membership.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        membership.HasOne<ClinicUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        membership.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        membership.HasQueryFilter(m => m.TenantId == CurrentTenantId);
        var access = modelBuilder.Entity<BranchAccess>();
        access.ToTable("branch_access");
        access.HasKey(a => a.Id);
        access.Property(a => a.TenantId).IsConcurrencyToken();
        access.HasIndex(a => new { a.TenantId, a.MembershipId, a.BranchId }).IsUnique();
        access.HasOne<TenantMembership>().WithMany().HasForeignKey(a => new { a.TenantId, a.MembershipId })
            .HasPrincipalKey(m => new { m.TenantId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        access.HasOne<Branch>().WithMany().HasForeignKey(a => new { a.TenantId, a.BranchId })
            .HasPrincipalKey(b => new { b.TenantId, b.Id }).OnDelete(DeleteBehavior.Restrict);
        access.HasQueryFilter(a => a.TenantId == CurrentTenantId);

        var tenant = modelBuilder.Entity<Tenant>();
        tenant.ToTable("tenants", t => t.HasCheckConstraint("ck_tenants_code", "length(btrim(\"Code\")) > 0"));
        tenant.HasKey(t => t.Id);
        tenant.Property(t => t.Code).HasMaxLength(50).IsRequired();
        tenant.Property(t => t.Name).HasMaxLength(200).IsRequired();
        tenant.HasIndex(t => t.Code).IsUnique();
        tenant.HasQueryFilter(t => t.Id == CurrentTenantId);

        var branch = modelBuilder.Entity<Branch>();
        branch.ToTable("branches", t => t.HasCheckConstraint("ck_branches_code", "length(btrim(\"Code\")) > 0"));
        branch.HasKey(b => b.Id);
        branch.Property(b => b.TenantId).IsConcurrencyToken();
        branch.HasAlternateKey(b => new { b.TenantId, b.Id });
        branch.Property(b => b.Code).HasMaxLength(50).IsRequired();
        branch.Property(b => b.Name).HasMaxLength(200).IsRequired();
        branch.Property(b => b.Address).HasMaxLength(500);
        branch.Property(b => b.TimeZone).HasMaxLength(100).IsRequired();
        branch.HasIndex(b => new { b.TenantId, b.Code }).IsUnique();
        branch.HasOne<Tenant>().WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Restrict);
        branch.HasQueryFilter(b => b.TenantId == CurrentTenantId);

    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateTenantWrites()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditEvent && entry.State != EntityState.Added)
                throw new InvalidOperationException("Audit events cannot be edited or deleted.");
            var owner = entry.Entity switch
            {
                Tenant t => t.Id,
                Branch b => b.TenantId,
                TenantMembership m => m.TenantId,
                BranchAccess a => a.TenantId,
                BusinessRole r => r.TenantId,
                RolePermission p => p.TenantId,
                RoleAssignment a => a.TenantId,
                AuditEvent a => a.TenantId,
                Permission p => p.TenantId,
                PermissionEndpoint m => m.TenantId,
                MenuItem m => m.TenantId,
                MenuOperation m => m.TenantId,
                RoleMenuOperation m => m.TenantId,
                TenantRoleMenuGrant m => m.TenantId,
                MenuOperationEndpoint m => m.TenantId,
                _ => (Guid?)null
            };
            if (owner is null) continue;
            if (CurrentTenantId is null || CurrentTenantId == Guid.Empty || owner != CurrentTenantId)
                throw new InvalidOperationException("A verified matching tenant is required for writes.");
            if (entry.Entity is not Tenant && entry.State != EntityState.Added &&
                (Guid)entry.Property("TenantId").OriginalValue! != owner)
                throw new InvalidOperationException("Tenant ownership cannot be changed.");
        }
    }
}
