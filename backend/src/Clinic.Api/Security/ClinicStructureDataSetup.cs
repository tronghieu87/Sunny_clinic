using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

/// <summary>Idempotently supplies the core clinic specialties for new or existing tenants.</summary>
internal static class ClinicStructureDataSetup
{
    private static readonly (string Code, string Name, int SortOrder)[] CoreSpecialties =
    [
        ("INTERNAL_MEDICINE", "Nội tổng quát", 10),
        ("PEDIATRICS", "Nhi khoa", 20),
        ("OBSTETRICS_GYNECOLOGY", "Sản phụ khoa", 30),
        ("OPHTHALMOLOGY", "Mắt", 40),
        ("OTOLARYNGOLOGY", "Tai Mũi Họng", 50)
    ];

    public static async Task Sync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
        var data = scope.ServiceProvider.GetRequiredService<ClinicDataDbContext>();
        var verified = scope.ServiceProvider.GetRequiredService<VerifiedTenant>();
        var workplaces = await (from tenant in authorization.Tenants.IgnoreQueryFilters().AsNoTracking()
            join branch in authorization.Branches.IgnoreQueryFilters().AsNoTracking() on tenant.Id equals branch.TenantId
            where tenant.IsActive && branch.IsActive
            select new { TenantId = tenant.Id, BranchId = branch.Id }).Distinct().ToListAsync();
        foreach (var workplace in workplaces)
        {
            verified.Set(workplace.TenantId, workplace.BranchId);
            foreach (var item in CoreSpecialties)
            {
                if (!await data.Specialties.AnyAsync(x => x.Code == item.Code))
                    data.Specialties.Add(new Specialty { TenantId = workplace.TenantId, Code = item.Code, Name = item.Name, SortOrder = item.SortOrder });
            }
            await data.SaveChangesAsync();
        }
    }
}
