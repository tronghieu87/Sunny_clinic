using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

/// <summary>Seeds missing software-wide clinic structure functions without overwriting configured mappings.</summary>
internal static class ClinicStructureMenuSetup
{
    private sealed record MenuDefinition(string Code, string Name, string Path, string BaseApi, int SortOrder, (string Code, string Name, string EndpointCode)[] Operations);
    private static readonly MenuDefinition[] Definitions =
    [
        new("master-data.specialties", "Chuyên khoa", "/master-data/specialties", "/master-data/specialties", 40,
        [("specialties.list", "Danh sách chuyên khoa", "specialties.list"), ("specialties.create", "Tạo chuyên khoa", "specialties.create"), ("specialties.update", "Cập nhật chuyên khoa", "specialties.update"), ("specialties.status", "Cập nhật trạng thái chuyên khoa", "specialties.status")]),
        new("master-data.clinic-rooms", "Phòng khám và bàn khám", "/master-data/clinic-rooms", "/master-data/clinic-rooms", 50,
        [("clinic-rooms.list", "Danh sách phòng khám và bàn khám", "clinic-rooms.list"), ("clinic-rooms.lookups", "Tra cứu chuyên khoa cho phòng khám/bàn khám", "clinic-rooms.lookups"), ("clinic-rooms.create", "Tạo phòng khám hoặc bàn khám", "clinic-rooms.create"), ("clinic-rooms.update", "Cập nhật phòng khám hoặc bàn khám", "clinic-rooms.update"), ("clinic-rooms.status", "Cập nhật trạng thái phòng khám hoặc bàn khám", "clinic-rooms.status")]),
        new("master-data.branches", "Chi nhánh", "/master-data/branches", "/master-data/branches", 30,
        [("branches.read", "Danh sách chi nhánh", "branches.catalog.list"), ("branches.create", "Tạo chi nhánh", "branches.catalog.create"), ("branches.update", "Cập nhật chi nhánh", "branches.catalog.update"), ("branches.status", "Cập nhật trạng thái chi nhánh", "branches.catalog.status")])
    ];

    public static async Task Sync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
        var verified = scope.ServiceProvider.GetRequiredService<VerifiedTenant>();
        var tenantId = await db.Tenants.IgnoreQueryFilters().Where(x => x.IsActive).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (tenantId is null) return;
        verified.Set(tenantId.Value, Guid.Empty, true);
        var parent = await db.PlatformMenus.SingleOrDefaultAsync(x => x.Code == "clinic-organization");
        if (parent is null)
        {
            parent = new PlatformMenu { Code = "clinic-organization", Name = "Tổ chức phòng khám", Kind = "menu", SortOrder = 20, Description = "Tổ chức phòng khám" };
            db.PlatformMenus.Add(parent);
        }
        foreach (var definition in Definitions)
        {
            if (await db.PlatformMenus.AnyAsync(x => x.Code == definition.Code)) continue;
            var menu = new PlatformMenu { ParentId = parent.Id, Code = definition.Code, Name = definition.Name, Path = definition.Path, BaseApi = definition.BaseApi, Kind = "function", SortOrder = definition.SortOrder, Description = definition.Name };
            db.PlatformMenus.Add(menu);
            foreach (var operation in definition.Operations)
            {
                if (!await db.ApiEndpoints.AnyAsync(x => x.Code == operation.EndpointCode && x.IsActive)) throw new InvalidOperationException($"Clinic structure API '{operation.EndpointCode}' was not registered.");
                if (!await db.PlatformOperations.AnyAsync(x => x.Code == operation.Code) && !db.PlatformOperations.Local.Any(x => x.Code == operation.Code))
                    db.PlatformOperations.Add(new PlatformOperation { Code = operation.Code, Description = operation.Name });
                db.PlatformMenuOperations.Add(new PlatformMenuOperation { MenuId = menu.Id, OperationCode = operation.Code });
                db.PlatformMenuEndpoints.Add(new PlatformMenuEndpoint { MenuId = menu.Id, OperationCode = operation.Code, EndpointCode = operation.EndpointCode });
            }
        }
        await db.SaveChangesAsync();
    }
}
