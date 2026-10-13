using Clinic.Application;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

var config = new ConfigurationBuilder().AddUserSecrets("f6c974f2-3a60-453d-a539-a8f183b996f7").Build();
var authorizationConnectionString = config.GetConnectionString("AuthorizationDb") ?? throw new InvalidOperationException("Authorization database connection missing.");
var clinicConnectionString = config.GetConnectionString("ClinicDb") ?? throw new InvalidOperationException("Clinic database connection missing.");
await using var authorizationConnection = new NpgsqlConnection(authorizationConnectionString);
await using var clinicConnection = new NpgsqlConnection(clinicConnectionString);
await authorizationConnection.OpenAsync();
await clinicConnection.OpenAsync();
await using var authorizationTransaction = await authorizationConnection.BeginTransactionAsync();
await using var clinicTransaction = await clinicConnection.BeginTransactionAsync();
var authorizationOptions = new DbContextOptionsBuilder<AuthorizationDbContext>().UseNpgsql(authorizationConnection).Options;
var clinicOptions = new DbContextOptionsBuilder<ClinicDataDbContext>().UseNpgsql(clinicConnection).Options;
async Task<AuthorizationDbContext> AuthorizationContext(Guid? tenantId) {
    var db = new AuthorizationDbContext(authorizationOptions, new Scope(tenantId));
    await db.Database.UseTransactionAsync(authorizationTransaction);
    return db;
}
async Task<ClinicDataDbContext> ClinicContext(Guid? tenantId, bool isSuperAdmin = false) {
    var db = new ClinicDataDbContext(clinicOptions, new Scope(tenantId, isSuperAdmin));
    await db.Database.UseTransactionAsync(clinicTransaction);
    return db;
}
void Check(bool condition, string name) {
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
async Task<long> AllocateEmployeeCode(Guid tenantId, NpgsqlConnection connection, NpgsqlTransaction? transaction = null) {
    await using var command = new NpgsqlCommand("""
        INSERT INTO employee_code_sequences ("TenantId", "LastValue")
        VALUES (@tenantId, COALESCE((
            SELECT MAX(substring("EmployeeCode" from 4)::bigint)
            FROM employees
            WHERE "TenantId" = @tenantId AND "EmployeeCode" ~ '^NV-[0-9]+$'
        ), 0) + 1)
        ON CONFLICT ("TenantId") DO UPDATE
        SET "LastValue" = employee_code_sequences."LastValue" + 1
        RETURNING "LastValue"
        """, connection, transaction);
    command.Parameters.AddWithValue("tenantId", tenantId);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
async Task Reject<T>(Func<Task> action, string name) where T : Exception {
    try { await action(); } catch (T) { Check(true, name); return; }
    throw new Exception("FAIL: " + name);
}
try {
    var a = Guid.NewGuid(); var b = Guid.NewGuid();
    var firstEmployeeNumber = await AllocateEmployeeCode(a, clinicConnection, clinicTransaction);
    var secondEmployeeNumber = await AllocateEmployeeCode(a, clinicConnection, clinicTransaction);
    Check(firstEmployeeNumber == 1 && secondEmployeeNumber == 2, "employee codes allocate sequentially per tenant");
    var concurrentTenant = Guid.NewGuid();
    try {
        var allocations = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            await using var connection = new NpgsqlConnection(clinicConnectionString);
            await connection.OpenAsync();
            return await AllocateEmployeeCode(concurrentTenant, connection);
        }));
        Check(allocations.Distinct().Count() == 8 && allocations.Order().SequenceEqual(Enumerable.Range(1, 8).Select(value => (long)value)), "concurrent employee code allocations are unique and sequential");
    } finally {
        await using var cleanupConnection = new NpgsqlConnection(clinicConnectionString);
        await cleanupConnection.OpenAsync();
        await using var cleanup = new NpgsqlCommand("DELETE FROM employee_code_sequences WHERE \"TenantId\" = @tenantId", cleanupConnection);
        cleanup.Parameters.AddWithValue("tenantId", concurrentTenant);
        await cleanup.ExecuteNonQueryAsync();
    }
    var code = "check-" + Guid.NewGuid().ToString("N");
    await using var dbA = await AuthorizationContext(a);
    await using var dbB = await AuthorizationContext(b);
    await using var clinicA = await ClinicContext(a);
    await using var clinicB = await ClinicContext(b);
    dbA.Add(new Tenant { Id = a, Code = code + "a", Name = "Isolation check A" });
    dbB.Add(new Tenant { Id = b, Code = code + "b", Name = "Isolation check B" });
    await dbA.SaveChangesAsync(); await dbB.SaveChangesAsync();
    var patientA = new Patient { TenantId = a, PatientCode = "P001", FullName = "Synthetic A" };
    var patientB = new Patient { TenantId = b, PatientCode = "P001", FullName = "Synthetic B" };
    var categoryA = new CategoryGroup { TenantId = a, Code = "CUSTOM_A", Name = "Synthetic tenant A category" };
    var categoryB = new CategoryGroup { TenantId = b, Code = "CUSTOM_B", Name = "Synthetic tenant B category" };
    var itemA = new CategoryItem { TenantId = a, CategoryGroupId = categoryA.Id, Code = "F", Name = "Synthetic female" };
    var itemB = new CategoryItem { TenantId = b, CategoryGroupId = categoryB.Id, Code = "F", Name = "Synthetic other tenant" };
    var globalGroup = new CategoryGroup { TenantId = null, IsSystem = true, Code = "CHECK_GLOBAL", Name = "Synthetic global category" };
    var globalItem = new CategoryItem { TenantId = null, CategoryGroupId = globalGroup.Id, Code = "G", Name = "Synthetic global value" };
    await using var clinicAdmin = await ClinicContext(a, true);
    clinicAdmin.AddRange(globalGroup, globalItem);
    await clinicAdmin.SaveChangesAsync();
    clinicA.AddRange(patientA, categoryA, itemA); clinicB.AddRange(patientB, categoryB, itemB);
    dbA.AddRange(new Branch { TenantId = a, Code = "B01", Name = "A1" }, new Branch { TenantId = a, Code = "B02", Name = "A2" });
    dbB.Add(new Branch { TenantId = b, Code = "B01", Name = "B1" });
    await clinicA.SaveChangesAsync(); await clinicB.SaveChangesAsync();
    await dbA.SaveChangesAsync(); await dbB.SaveChangesAsync();
    Check(await clinicA.Patients.CountAsync() == 1 && await clinicB.Patients.CountAsync() == 1, "same patient code across isolated tenants");
    Check(await dbA.Branches.CountAsync() == 2 && await dbB.Branches.CountAsync() == 1, "chain branches share tenant scope");
    Check(!await clinicA.Patients.AnyAsync(p => p.Id == patientB.Id), "foreign patient invisible");
    Check(await clinicA.CategoryGroups.AnyAsync(x => x.Id == globalGroup.Id) && await clinicA.CategoryGroups.AnyAsync(x => x.Id == categoryA.Id) && !await clinicA.CategoryGroups.AnyAsync(x => x.Id == categoryB.Id), "global category is visible while tenant categories remain isolated");
    Check(await clinicB.CategoryGroups.AnyAsync(x => x.Id == globalGroup.Id) && await clinicB.CategoryGroups.AnyAsync(x => x.Id == categoryB.Id) && !await clinicB.CategoryGroups.AnyAsync(x => x.Id == categoryA.Id), "global category is shared across tenants");
    Check(await clinicA.CategoryItems.AnyAsync(x => x.Id == globalItem.Id) && await clinicA.CategoryItems.AnyAsync(x => x.Id == itemA.Id) && !await clinicA.CategoryItems.AnyAsync(x => x.Id == itemB.Id), "global values are visible while tenant values remain isolated");
    var positionGroupA = new CategoryGroup { TenantId = a, Code = "EMPLOYEE_POSITION", Name = "Positions A" };
    var positionGroupB = new CategoryGroup { TenantId = b, Code = "EMPLOYEE_POSITION", Name = "Positions B" };
    var positionA = new CategoryItem { TenantId = a, CategoryGroupId = positionGroupA.Id, Code = "DOCTOR", Name = "Doctor A" };
    var positionB = new CategoryItem { TenantId = b, CategoryGroupId = positionGroupB.Id, Code = "DOCTOR", Name = "Doctor B" };
    clinicA.AddRange(positionGroupA, positionA);
    clinicB.AddRange(positionGroupB, positionB);
    await clinicA.SaveChangesAsync(); await clinicB.SaveChangesAsync();
    clinicA.Employees.Add(new Employee { TenantId = a, EmployeeCode = "NV-CHECK-1", FullName = "Synthetic doctor", PositionCategoryId = positionA.Id });
    await clinicA.SaveChangesAsync();
    Check(await clinicA.Employees.AnyAsync(employee => employee.PositionCategoryId == positionA.Id), "employee may select a position from its tenant catalog");
    await using var foreignPosition = await ClinicContext(a);
    foreignPosition.Employees.Add(new Employee { TenantId = a, EmployeeCode = "NV-CHECK-2", FullName = "Foreign position", PositionCategoryId = positionB.Id });
    await Reject<DbUpdateException>(() => foreignPosition.SaveChangesAsync(), "employee cannot select another tenant's position");
    await using var unrelatedCategory = await ClinicContext(a);
    unrelatedCategory.Employees.Add(new Employee { TenantId = a, EmployeeCode = "NV-CHECK-3", FullName = "Wrong catalog", PositionCategoryId = itemA.Id });
    await Reject<DbUpdateException>(() => unrelatedCategory.SaveChangesAsync(), "employee position must come from the position catalog");
    await using var anonymousAuthorization = await AuthorizationContext(null);
    await using var anonymousClinic = await ClinicContext(null);
    Check(await anonymousClinic.Patients.CountAsync() == 0 && await anonymousClinic.CategoryGroups.AnyAsync(x => x.Id == globalGroup.Id) && await anonymousAuthorization.Branches.CountAsync() == 0 && await anonymousAuthorization.Tenants.CountAsync() == 0, "missing tenant reads global references but no tenant data");
    anonymousClinic.Add(new Patient { TenantId = a, PatientCode = "P002", FullName = "Synthetic" });
    await Reject<InvalidOperationException>(() => anonymousClinic.SaveChangesAsync(), "missing tenant write denied");
    anonymousClinic.Add(new CategoryGroup { TenantId = a, Code = "NO_SCOPE", Name = "Synthetic" });
    await Reject<InvalidOperationException>(() => anonymousClinic.SaveChangesAsync(), "category group write without tenant denied");
    anonymousClinic.Add(new CategoryGroup { TenantId = null, IsSystem = true, Code = "NO_ADMIN", Name = "Synthetic" });
    await Reject<InvalidOperationException>(() => anonymousClinic.SaveChangesAsync(), "global category write requires super admin");
    await using var crossWrite = await ClinicContext(a);
    crossWrite.Add(new Patient { TenantId = b, PatientCode = "P002", FullName = "Synthetic" });
    await Reject<InvalidOperationException>(() => crossWrite.SaveChangesAsync(), "cross-tenant insert denied");
    await using var crossCategoryWrite = await ClinicContext(a);
    crossCategoryWrite.Add(new CategoryGroup { TenantId = b, Code = "CROSS", Name = "Synthetic" });
    await Reject<InvalidOperationException>(() => crossCategoryWrite.SaveChangesAsync(), "cross-tenant category group insert denied");
    await using var crossItemWrite = await ClinicContext(a);
    crossItemWrite.Add(new CategoryItem { TenantId = a, CategoryGroupId = categoryB.Id, Code = "FOREIGN", Name = "Synthetic" });
    await Reject<DbUpdateException>(() => crossItemWrite.SaveChangesAsync(), "category item cannot reference another tenant group");
    await using var forged = await ClinicContext(a);
    forged.Attach(new Patient { Id = patientB.Id, TenantId = a, PatientCode = "P001", FullName = "Forged" }).Property(p => p.FullName).IsModified = true;
    await Reject<DbUpdateConcurrencyException>(() => forged.SaveChangesAsync(), "forged detached update denied");
    await using var duplicate = await ClinicContext(a);
    duplicate.Add(new Patient { TenantId = a, PatientCode = "P001", FullName = "Synthetic duplicate" });
    await Reject<DbUpdateException>(() => duplicate.SaveChangesAsync(), "duplicate patient code within tenant denied");
} finally {
    await clinicTransaction.RollbackAsync();
    await authorizationTransaction.RollbackAsync();
}
Console.WriteLine("All checks passed; synthetic records rolled back.");
sealed record Scope(Guid? TenantId, bool IsSuperAdmin = false) : ICurrentTenant;
