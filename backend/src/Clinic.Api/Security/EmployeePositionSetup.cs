using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

/// <summary>Creates a tenant-owned, editable position catalog and links legacy text values.</summary>
internal static class EmployeePositionSetup
{
    internal const string GroupCode = "EMPLOYEE_POSITION";
    private static readonly (string Code, string Name)[] Defaults =
    [
        ("DOCTOR", "Bác sĩ"),
        ("NURSE", "Điều dưỡng"),
        ("MIDWIFE", "Hộ sinh"),
        ("TECHNICIAN", "Kỹ thuật viên"),
        ("PHARMACIST", "Dược sĩ"),
        ("RECEPTIONIST", "Lễ tân"),
        ("CASHIER", "Thu ngân"),
        ("MANAGER", "Quản lý")
    ];

    public static async Task Sync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
        var data = scope.ServiceProvider.GetRequiredService<ClinicDataDbContext>();
        var verified = scope.ServiceProvider.GetRequiredService<VerifiedTenant>();
        var tenantIds = await authorization.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(tenant => tenant.IsActive).Select(tenant => tenant.Id).ToListAsync();
        foreach (var tenantId in tenantIds)
            await SyncTenant(data, verified, tenantId, CancellationToken.None);
    }

    public static async Task SyncTenant(ClinicDataDbContext db, VerifiedTenant verified, Guid tenantId, CancellationToken ct)
    {
        verified.Set(tenantId, Guid.Empty, isSuperAdmin: true);
        var group = await db.CategoryGroups.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Code == GroupCode, ct);
        if (group is null)
        {
            group = new CategoryGroup {
                TenantId = tenantId, Code = GroupCode, Name = "Chức vụ nhân viên",
                Description = "Danh mục chức vụ của đơn vị", SortOrder = 20
            };
            db.CategoryGroups.Add(group);
            await db.SaveChangesAsync(ct);
            for (var index = 0; index < Defaults.Length; index++)
                db.CategoryItems.Add(new CategoryItem {
                    TenantId = tenantId, CategoryGroupId = group.Id,
                    Code = Defaults[index].Code, Name = Defaults[index].Name, SortOrder = (index + 1) * 10
                });
            await db.SaveChangesAsync(ct);
        }

        var items = await db.CategoryItems.Where(item => item.TenantId == tenantId && item.CategoryGroupId == group.Id).ToListAsync(ct);
        var legacyEmployees = await db.Employees.Where(item => item.PositionCategoryId == null && item.Position != null).ToListAsync(ct);
        foreach (var employee in legacyEmployees)
        {
            var name = employee.Position!.Trim();
            if (name.Length == 0) continue;
            var item = items.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                item = new CategoryItem {
                    TenantId = tenantId, CategoryGroupId = group.Id,
                    Code = $"POS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Name = name,
                    SortOrder = items.Count == 0 ? 10 : items.Max(value => value.SortOrder) + 10
                };
                db.CategoryItems.Add(item);
                items.Add(item);
                await db.SaveChangesAsync(ct);
            }
            employee.PositionCategoryId = item.Id;
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }
}
