namespace Clinic.Application;
public static class PermissionCatalog {
 public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string,string> {
  ["organization.read"] = "Xem thông tin đơn vị",
  ["branches.read"] = "Xem danh sách cơ sở",
  ["branches.manage"] = "Quản lý cơ sở",
  ["members.manage"] = "Quản lý thành viên",
  ["roles.manage"] = "Quản lý vai trò và phân quyền",
  ["patients.read"] = "Xem thông tin hành chính bệnh nhân",
  ["patients.write"] = "Cập nhật thông tin hành chính bệnh nhân"
 };
 public static readonly string[] Management = ["organization.read", "branches.read", "branches.manage", "members.manage", "roles.manage"];
}
