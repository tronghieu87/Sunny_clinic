using System.Text.Encodings.Web;
using System.Net.Security;
using Clinic.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Clinic.Api.Security;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) {
 private readonly EmailOptions settings = options.Value;

 public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Smtp.Host)
  && !string.IsNullOrWhiteSpace(settings.Smtp.FromAddress)
  && Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var address)
  && (address.Scheme == Uri.UriSchemeHttps || address.IsLoopback)
  && (string.IsNullOrWhiteSpace(settings.Smtp.UserName) == string.IsNullOrWhiteSpace(settings.Smtp.Password));

 public string CreateResetLink(Guid userId, string token) => settings.PublicBaseUrl.TrimEnd('/')
  + "/reset-password?userId=" + Uri.EscapeDataString(userId.ToString())
  + "&token=" + Uri.EscapeDataString(token);

 public async Task SendPasswordResetLinkAsync(string email, string link, CancellationToken cancellationToken) {
  var message = new MimeMessage();
  message.From.Add(new MailboxAddress(settings.Smtp.FromName, settings.Smtp.FromAddress));
  message.To.Add(MailboxAddress.Parse(email));
  message.Subject = "Đặt lại mật khẩu Clinic";
  var safeLink = HtmlEncoder.Default.Encode(link);
  message.Body = new BodyBuilder {
   TextBody = "Bạn nhận được yêu cầu đặt lại mật khẩu Clinic. Mở liên kết sau để tạo mật khẩu mới: " + link + "\nLiên kết chỉ dùng một lần và sẽ hết hạn.",
   HtmlBody = "<p>Bạn nhận được yêu cầu đặt lại mật khẩu Clinic.</p><p><a href=\"" + safeLink + "\">Đặt lại mật khẩu</a></p><p>Nếu bạn không yêu cầu thay đổi này, hãy bỏ qua email. Liên kết chỉ dùng một lần và sẽ hết hạn.</p>"
  }.ToMessageBody();

  using var client = new SmtpClient();
  client.CheckCertificateRevocation = settings.Smtp.CheckCertificateRevocation;
  client.ServerCertificateValidationCallback = (_, certificate, chain, policyErrors) => {
   if (policyErrors == SslPolicyErrors.None) return true;
   var chainStatuses = chain is null ? "none" : string.Join(",", chain.ChainStatus.Select(status => status.Status.ToString()));
   logger.LogError("SMTP server certificate rejected. PolicyErrors={PolicyErrors}; ChainStatuses={ChainStatuses}; Subject={Subject}; Issuer={Issuer}", policyErrors, chainStatuses, certificate?.Subject ?? "none", certificate?.Issuer ?? "none");
   return false;
  };
  var socket = settings.Smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
  await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, socket, cancellationToken);
    if (!string.IsNullOrWhiteSpace(settings.Smtp.UserName)) {
      var password = new string(settings.Smtp.Password!.Where(character => !char.IsWhiteSpace(character)).ToArray());
      await client.AuthenticateAsync(settings.Smtp.UserName, password, cancellationToken);
    }
  await client.SendAsync(message, cancellationToken);
  await client.DisconnectAsync(true, cancellationToken);
 }
}