# Cấu hình SMTP cho Production

Checklist cấu hình email đặt lại mật khẩu Clinic sau khi deploy. SMTP credential phải lưu trong secret manager của nền tảng hosting; không commit vào repository.

## 1. Chuẩn bị tài khoản gửi mail

Với Gmail, bật 2-Step Verification rồi tạo App Password. Dùng App Password, không dùng mật khẩu Google thông thường. Không gửi App Password qua chat, không ghi vào source, `appsettings*.json` hoặc file Compose được commit.

## 2. Khai báo cấu hình Production

Trong phần environment variables/secrets của nền tảng hosting Production, cấu hình các biến sau cho **backend API service**:

| Biến | Ví dụ / giá trị | Là bí mật? |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Không |
| `Email__PublicBaseUrl` | `https://clinic.example.com` | Không |
| `Email__Smtp__Host` | `smtp.gmail.com` | Không |
| `Email__Smtp__Port` | `587` | Không |
| `Email__Smtp__UserName` | Địa chỉ Gmail gửi thư | Không |
| `Email__Smtp__Password` | Gmail App Password | **Có** |
| `Email__Smtp__FromAddress` | Cùng địa chỉ Gmail gửi thư | Không |
| `Email__Smtp__FromName` | `Clinic` | Không |
| `Email__Smtp__UseSsl` | `false` với cổng 587/STARTTLS | Không |
| `Email__Smtp__CheckCertificateRevocation` | `true` | Không |

.NET ánh xạ `__` trong tên environment variable thành `:` trong khóa cấu hình. Đặt các biến này cho process/container API, không chỉ cho frontend hoặc database service.

Nếu dùng implicit TLS, thường là cổng `465`, đặt `Email__Smtp__UseSsl=true`. Frontend sau khi deploy phải truy cập được tại địa chỉ `Email__PublicBaseUrl`, vì liên kết reset mật khẩu trỏ tới đó. URL Production phải dùng HTTPS.

## 3. Giữ xác minh TLS bật

Production phải giữ `Email:Smtp:CheckCertificateRevocation=true`. `appsettings.json` mặc định bật kiểm tra này; chỉ môi trường Development local đặt `false` do máy phát hiện `RevocationStatusUnknown`. Kiểm tra certificate chain và hostname vẫn bắt buộc ở mọi môi trường. Không cài callback chấp nhận mọi certificate.

Nếu Production lỗi kiểm tra revocation, hãy sửa trust store của server/container, giờ hệ thống, proxy/firewall tới OCSP/CRL của nhà cấp certificate, hoặc dùng SMTP relay đáng tin cậy. Không sao chép ngoại lệ Development local sang Production.

## 4. Deploy và xác minh

1. Lưu biến/secret trên nền tảng hosting rồi restart/redeploy backend API để ứng dụng nạp cấu hình.
2. Kiểm tra API health và log khởi động để phát hiện lỗi cấu hình/runtime. Không in environment variables hoặc file secrets ra log.
3. Trong Clinic, vào **Phân quyền → Quản lý tài khoản → Gửi link đặt lại** cho tài khoản có email kiểm thử mà bạn truy cập được.
4. Xác nhận thư tới Inbox hoặc Spam, mở link HTTPS, đặt mật khẩu mới rồi đăng nhập bằng mật khẩu đó.
5. Xác nhận token sai/hết hạn bị từ chối và lỗi SMTP được ghi log mà không lộ credential.

## 5. Kiểm tra lỗi thường gặp

- `Chưa cấu hình dịch vụ gửi email`: một hoặc nhiều cấu hình backend bắt buộc đang trống, sai định dạng hoặc API process chưa nạp cấu hình.
- `AuthenticationException`: kiểm tra username gửi thư và Gmail App Password; khoảng trắng trong App Password được loại bỏ trước khi xác thực.
- `SslHandshakeException` / lỗi certificate chain: kiểm tra giờ hệ thống, CA trust store, proxy/TLS inspection đi ra ngoài và kết nối tới OCSP/CRL. Giữ certificate validation bật.
- Connection timeout/refused: cho phép kết nối TCP outbound tới SMTP host và port đã cấu hình.
- Link reset mở localhost hoặc URL nội bộ: sửa `Email__PublicBaseUrl` thành URL HTTPS công khai của frontend rồi deploy lại.

## Lưu ý bảo mật

- Nếu có thể, dùng mailbox gửi riêng với quyền tối thiểu và App Password riêng; rotate/revoke credential nếu bị lộ.
- Giới hạn người có quyền xem/sửa production secrets. Tránh log SMTP values, reset token hoặc dữ liệu người nhận.
- Không dùng Development User Secrets làm nguồn cấu hình Production.
