# Tiêu chí UI/UX của dự án

Áp dụng cho mọi màn hình mới và mọi lần sửa UI. Cập nhật 2026-10-12.

| Control | Quy chuẩn |
|---|---|
| Button, input, select | Cao 34px, chữ 13–14px, cùng hàng phải cùng chiều cao; không giãn theo panel. |
| Checkbox, radio | 16×16px, căn giữa với nhãn; vùng nhãn bấm được, tối thiểu cao 30px. |
| Toolbar, phân trang | Control nhỏ 30px; không kéo giãn, luôn nằm ngoài vùng cuộn dữ liệu. |
| Label | Cách input 4–6px; dấu * đỏ với trường bắt buộc, trùng với validation. |
| Mã định danh | Mã nghiệp vụ do backend tự cấp khi tạo; biểu mẫu không cho nhập/sửa mã. Mã chuẩn từ nguồn chính thức chỉ được nạp/import, không nhập tay trong màn hình. |
| Chức vụ nhân viên | Chọn từ dropdown danh mục chức vụ của tenant hiện tại; không nhập tự do. Chức vụ tách biệt với Nghề nghiệp và nhóm quyền đăng nhập. Giá trị cũ đã ngừng vẫn hiển thị khi sửa hồ sơ, nhưng không cho chọn mới. |
| Chọn API | Danh sách endpoint phải tìm kiếm được theo route, method, tên/mô tả thao tác; dropdown dài có vùng cuộn giới hạn, hỗ trợ bàn phím và hiện rõ method + route + mô tả trước khi chọn. |
| Textarea | Cao theo số dòng, tối thiểu 64px; cho phép tăng chiều cao khi cần. |
| Grid | Hàng khoảng 38px, tiêu đề cố định; hai grid liên quan thẳng hàng toolbar/header/footer. |
| Cây quyền | Các node bắt đầu ngay dưới thông tin nhóm; dòng 30–34px; chỉ vùng cây cuộn. |
| Panel | Header và footer theo nội dung; chỉ body nhận chiều cao còn lại. Không chia đều chiều cao cho các control. |
| Split view | Mọi nội dung chia thành hai khung liên quan (trái/phải hoặc trên/dưới) có thanh split kéo được. Giới hạn mỗi khung tối thiểu 22%; hỗ trợ chuột, cảm ứng và phím mũi tên/Home/End; giữ lại tỷ lệ theo màn hình. Khi bố cục xếp dọc ở màn hình hẹp, thanh đổi sang kéo lên/xuống. |
| Dialog | Padding 16px, gap 12px; nút Đóng/Lưu cùng hàng/cùng chiều cao; vừa viewport HD. |
| Spacing | Thang 4/8/12/16px; tránh thêm khoảng trống không có mục đích. |

## Bố cục và trạng thái
- Ưu tiên 1366×768 và1280×720; kiểm tra thêm1920×1080 để bắt lỗi giãn control trên màn hình lớn.
- Form nhập liệu: grid phải align-content:start. Form toàn chiều cao: dùng flex-column hoặc grid với hàng auto / minmax(0,1fr) / auto; không dùng các hàng auto chia đều khoảng trống.
- Luôn kiểm tra không dữ liệu, chỉ1–3 dòng, nhiều dữ liệu, đang tải, lỗi và tên dài. Ít dữ liệu không được khiến nút cao lên hoặc cây nằm giữa panel.
- Nội dung dài được cuộn trong vùng phù hợp; không cắt dữ liệu, không thu nhỏ chữ để che lỗi overflow.
- Dùng chung thành phần split view cho danh sách/chi tiết và các bố cục hai khung; không để vùng làm việc lớn bị bỏ trống do chiều cao bảng bị chặn. Thanh split phải dễ nắm, có focus rõ và không che nội dung.
- Giữ focus bàn phím rõ ràng, nhãn truy cập được, disabled rõ, báo lỗi gần trường/form; không chỉ dùng màu để truyền trạng thái.
- Với dữ liệu tenant, phải hiển thị rõ tenant đang làm việc; chỉ bật thao tác sau khi tenant/cơ sở được chọn đã áp dụng. API luôn lấy tenant từ workplace đã xác thực, không tin TenantId do client gửi.
- Tạo Tenant và khai báo Chi nhánh là hai bước riêng: biểu mẫu Tenant không hỏi thông tin chi nhánh; sau khi tạo Tenant, trang Chi nhánh cho Super Admin chọn Tenant đích để tạo cơ sở đầu tiên. Người dùng tenant thông thường chỉ thấy và thao tác chi nhánh của Tenant đã xác thực. Mã Tenant và mã Chi nhánh do backend cấp, không cho nhập tay.
- Danh mục chuẩn dùng chung toàn hệ thống hiển thị tại mọi tenant; chỉ Super Admin được tạo/sửa/ngừng, tài khoản khác chỉ xem. Danh mục tùy chỉnh thuộc tenant chỉ hiện và được quản lý trong tenant đó.
- Chức năng thao tác, Cài đặt chức năng và Cấu hình API là một khung dùng chung toàn hệ thống, không có ô chọn Tenant trong biểu mẫu. Chỉ Super Admin được sửa khung này; Nhóm người dùng và quyền cấp cho nhóm vẫn theo Tenant đã xác thực.
- Chỉ liên kết hoặc gỡ liên kết tài khoản với hồ sơ nhân viên trong Quản lý tài khoản. Danh sách nhân viên để chọn phải thuộc Tenant đang làm việc; biểu mẫu thêm/sửa nhân viên không có bước liên kết tài khoản.
- Quản lý tài khoản phải hiển thị riêng cờ Super Admin toàn hệ thống và nhóm quyền tenant; không gọi nhóm “Quản lý đơn vị” là Super Admin. Chỉ Super Admin hiện hữu được cấp/thu hồi cờ này qua thao tác có mô tả phạm vi toàn hệ thống, không yêu cầu nhập lý do. Không cho tự thu hồi hoặc xóa Super Admin hoạt động cuối cùng.
- Các chức năng phân quyền không buộc nhập lý do. Nhật ký được hệ thống ghi tự động.
- Trong popup Cấu hình API, từng dòng endpoint phải hiển thị Tên thao tác tương ứng, kể cả khi một thao tác có nhiều API; không để các dòng API sau trống tên. Ưu tiên tên của quyền thao tác, dùng mô tả endpoint khi quyền chưa có tên; nếu cả hai thiếu, hiển thị cảnh báo “Chưa đặt tên thao tác” để quản trị viên nhận ra và bổ sung, không hiển thị ô trống.

## Quy chuẩn lưới dữ liệu
- Mọi lưới nghiệp vụ và quản trị dùng chuẩn tương tác của DevExtreme DataGrid. Có thể dùng DevExtreme hoặc thành phần tương đương trong stack hiện tại, nhưng các hành vi quy định dưới đây phải nhất quán giữa các màn hình.
- Cho phép lọc theo từng cột. Khi có bộ lọc đang áp dụng, footer của chính lưới phải hiển thị điều kiện lọc dễ đọc; hỗ trợ bỏ từng điều kiện và xóa toàn bộ bộ lọc.
- Cho phép ẩn/hiện cột qua menu ngữ cảnh khi nhấn chuột phải trên lưới hoặc qua column chooser tương đương. Có thể khôi phục các cột đã ẩn.
- Hỗ trợ group theo cột từ menu ngữ cảnh chuột phải; nhóm phải có thể mở/thu gọn và bỏ nhóm được. Khu vực nhóm không được làm tràn ngang trang ở độ phân giải HD.
- Paging mặc định 200 dòng mỗi trang. Người dùng có thể đổi kích thước trang; paging và trạng thái bộ lọc nằm trong footer cố định của lưới, ngoài vùng cuộn hàng.
- Với 200 dòng mỗi trang, giới hạn cuộn trong thân lưới, giữ header/footer dễ thấy, tránh tạo cuộn dọc toàn trang. Lưới vẫn phải xử lý gọn khi rỗng hoặc chỉ có vài dòng.
- Lưới và thân lưới phải phủ hết chiều rộng panel khả dụng. Khi ít cột hoặc ít dữ liệu, phân bổ cột hợp lý trên toàn bề ngang; không để bảng co theo nội dung rồi chừa một dải trống lớn bên cạnh. Khi dữ liệu cần chiều rộng tối thiểu, giữ min-width và chỉ cuộn ngang bên trong lưới. Không kéo giãn chiều cao hàng chỉ để lấp khoảng trống dọc.
- Thanh cuộn dọc/ngang của lưới phải nhìn thấy rõ, nhất quán giữa các màn hình và nằm trong chính vùng thân lưới; cuộn ngang không được làm trượt toàn trang hoặc khiến header, hàng và footer lệch nhau.
- Khi bảng rộng hơn panel, giữ chiều rộng cột đủ dùng và cuộn ngang trong thân lưới. Không ép nội dung xuống dòng theo từng ký tự hoặc làm hàng cao bất thường; nội dung dài được ellipsis có tooltip/chi tiết truy cập được khi cần. Không cắt mất cột mà không có thanh cuộn hay cách khôi phục cột.
- Kiểm thử cuộn ngang trên mọi loại lưới ở độ rộng panel hẹp và HD: kéo thanh ngang tới đầu/cuối, xác nhận cột và nội dung vẫn thẳng hàng, footer không chạy theo bảng, trang không tràn ngang. Rà soát thành phần lưới dùng chung trước khi sửa riêng từng màn hình.
- Danh sách tài khoản hiển thị riêng cột “Tài khoản” (tên đăng nhập) và “Tên tài khoản” (tên người dùng). Ưu tiên danh sách liền mạch cho thao tác quản lý tài khoản; chỉ thêm panel chi tiết khi nghiệp vụ cần và người dùng đã thống nhất bố cục đó.
- Ở bố cục danh sách/chi tiết, hai pane phải lấp đầy cùng một khung. Nội dung dài cuộn bên trong pane; trạng thái rỗng hoặc thông báo rỗng không được kéo giãn và tạo khoảng trống giả. Giữ nút lưu/cập nhật ở cuối vùng thao tác.
- Menu phân cấp phải tự mở các menu cha chứa trang đang hoạt động sau khi tải/đổi đường dẫn, kể cả khi dữ liệu menu tải bất đồng bộ. Không để mở trang con làm menu cha tự thu gọn; trạng thái mở/đóng người dùng chọn được giữ cho tới khi điều hướng sang trang khác.
- Trong mục Tổ chức phòng khám, quản lý chi nhánh dùng một menu “Chi nhánh” duy nhất; không tạo thêm menu nhóm “Cơ sở và chi nhánh”.
- Mã định danh dài trong lưới phải giữ hàng gọn trên HD; không để mã tự xuống nhiều dòng làm tăng chiều cao hàng bất thường.
- Khi nghiệm thu UI, kiểm tra lọc cột và nội dung footer, ẩn/hiện cột, group/ungroup bằng chuột phải, paging mặc định 200, cùng trạng thái rỗng và nhiều dữ liệu ở 1366×768 và 1280×720.

## Kiểm tra trước khi báo hoàn thành
- Đo bounding boxes: button/input/select không vượt kích thước quy định; panel trái/phải lệch mép không quá1px.
- Kiểm tra khoảng cách đầu cây/list với heading, footer không bị giãn; page không tràn ngang tại HD.
- Đo chiều rộng bảng so với panel ở các màn hình dùng chung grid và từng grid chuyên biệt; khi ít dữ liệu, bảng vẫn phủ panel, cột không co cụm một bên và footer thẳng mép với bảng.
- Xem screenshot ít dữ liệu VÀ nhiều dữ liệu. Kiểm tra tương tác chính; build pass không thay thế kiểm tra bố cục.
- Ghi đúng phạm vi đã thử (dữ liệu thật hay giả lập), không suy rộng từ một ảnh đẹp.
