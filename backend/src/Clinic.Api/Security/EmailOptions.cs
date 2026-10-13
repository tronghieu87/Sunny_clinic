namespace Clinic.Api.Security;

public sealed class EmailOptions {
 public string PublicBaseUrl { get; set; } = string.Empty;
 public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions {
 public string Host { get; set; } = string.Empty;
 public int Port { get; set; } = 587;
 public string? UserName { get; set; }
 public string? Password { get; set; }
 public string FromAddress { get; set; } = string.Empty;
 public string FromName { get; set; } = "Clinic";
 public bool UseSsl { get; set; }
 public bool CheckCertificateRevocation { get; set; } = true;
}