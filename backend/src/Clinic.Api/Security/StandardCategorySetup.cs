using System.Text.Json;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

/// <summary>Seeds missing global standard category definitions without overwriting local edits.</summary>
internal static class StandardCategorySetup
{
    private sealed record Definition(string Code, string Name, string? Description, int SortOrder, Value[] Items);
    private sealed record Value(string Code, string Name, int SortOrder);

    public static async Task Sync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var verified = scope.ServiceProvider.GetRequiredService<VerifiedTenant>();
        verified.Set(Guid.Empty, Guid.Empty, isSuperAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ClinicDataDbContext>();
        await using var json = typeof(StandardCategorySetup).Assembly.GetManifestResourceStream("Clinic.Api.Data.standard-categories.json")
            ?? throw new InvalidOperationException("The standard category seed data is missing.");
        var definitions = await JsonSerializer.DeserializeAsync<Definition[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The standard category seed data is invalid.");

        foreach (var definition in definitions)
        {
            var group = await db.CategoryGroups.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == null && x.Code == definition.Code);
            if (group is null)
            {
                group = new CategoryGroup { TenantId = null, IsSystem = true, Code = definition.Code, Name = definition.Name, Description = definition.Description, SortOrder = definition.SortOrder };
                db.CategoryGroups.Add(group);
                await db.SaveChangesAsync();
            }

            var existingCodes = await db.CategoryItems.IgnoreQueryFilters().Where(x => x.TenantId == null && x.CategoryGroupId == group.Id).Select(x => x.Code).ToHashSetAsync();
            foreach (var item in definition.Items.Where(x => !existingCodes.Contains(x.Code)))
                db.CategoryItems.Add(new CategoryItem { TenantId = null, CategoryGroupId = group.Id, Code = item.Code, Name = item.Name, SortOrder = item.SortOrder });
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        }
    }
}
