> Historical review of v0.6.1. ImageSharp/uninstall/Tuck Away findings have since changed. See the 2026-11-12 audit and remediation status for HEAD.

# HyperPet v0.6.1 — review ngày 11/11/2026

## Phạm vi và kiểm chứng

Rà mã nguồn hiện tại, thay đổi settings mới, vòng đời app, notifications,
lưu settings, nhiều màn hình và quy trình release. Có một lượt review độc lập.
Không thấy regression chặn release do hai settings mới. Chưa kiểm thử đăng nhập
Windows, uninstall, hoặc toàn bộ tình huống bên dưới trên máy thật.

- `dotnet test HyperPet.sln --configuration Release`: 112 tests qua (45 Core, 67 App).
- Audit NuGet gồm dependency gián tiếp: còn 6 advisories ImageSharp, 4 High và 2 Moderate.
- Giữ lựa chọn startup đã lưu; settings mới mặc định bật startup và nhớ vị trí.

## Đã xử lý trong v0.6.1

- Thêm tùy chọn nhớ vị trí pet khi mở; mặc định bật, có Apply/Save/Default.
- Startup mặc định bật cho settings mới và được đăng ký lúc app khởi động.
- Khôi phục vị trí trước khi bắt đầu roaming; clamp theo màn hình gần vị trí đó.
- Xóa biến `_calmDragging` không dùng và các phép gán thừa (warning CS0414).
- Override dependency System.Drawing.Common từ 4.7.0 lên 8.0.34; audit không còn
  liệt kê advisory của dependency này.
- `pack.ps1` dừng khi `vpk pack` trả lỗi, tránh báo đóng gói thành công giả.

## Lỗi và rủi ro cần ưu tiên

| Ưu tiên | Vấn đề, cách gặp | Hướng sửa |
| --- | --- | --- |
| P1 | ImageSharp 3.1.12 còn 6 advisories; app giải mã ảnh từ pet được import. NuGet xác nhận dependency bị ảnh hưởng, chưa tái hiện exploit trong HyperPet. | Nâng lên bản vá với license hợp lệ hoặc thay thư viện ảnh. Đã thử 3.2.0; build bị chặn vì repo chưa có Six Labors license key. Không tắt kiểm tra license/audit. |
| P2 | Zalo/Electron tái sử dụng popup HWND, cùng kích thước/vị trí, nhưng thay nội dung: cache trả text cũ, hash không đổi nên bỏ qua thông báo tiếp theo. | Thêm thời hạn cache hoặc invalidate theo sự kiện thay nội dung accessibility. |
| P2 | Từ chối quyền Windows notifications: app return trước khi khởi động in-app watcher, nên Zalo cũng không được theo dõi dù bật riêng. | Khởi động hai pipeline độc lập; hiển thị trạng thái quyền riêng. |
| P2 | Uninstall chỉ xóa user data, bỏ lại lệnh tự chạy trong HKCU Run. Mặc định startup mới làm nhiều người dùng gặp hơn. | Gỡ Run entry trong uninstall callback. |
| P2 | Desktop roaming dùng tọa độ Win32 physical pixel chung với WPF DIP. Ở DPI 150%/nhiều màn hình, pet có thể đứng lệch mép cửa sổ/taskbar. | Chuẩn hóa tọa độ và xử lý thay đổi DPI/màn hình. |
| P2 | Chọn pet B rồi Apply trong settings của pet A; sửa FPS tiếp trong cùng dialog vẫn ghi override vào A. | Reload pet/animation rows trong dialog sau khi swap thành công. |
| P2 | Ghi settings bằng File.Create làm truncate file trước khi ghi xong. Crash/ghi lỗi có thể làm mất settings cũ. | Ghi file tạm cùng thư mục rồi replace nguyên tử, giữ backup. |
| P2 | Chưa có khóa single-instance. Mở app hai lần cho phép lần save/exit sau ghi đè settings mới bằng snapshot cũ. | Khóa single-instance và chuyển yêu cầu mở Settings/Summon đến process hiện tại. |
| P2 | Tuck Away trong lúc bubble alert sắp hết hạn: alert timer có thể gọi DismissAlert, hủy animation failed đang chờ để Shutdown. | Dừng alert/interaction timers khi thoát, thêm timeout thoát dự phòng. |

### Nguồn trong code

- Cache popup: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs:194`.
- Quyền notifications: `src/HyperPet.App/App.xaml.cs:165`; watcher bắt đầu ở `:437`.
- Uninstall: `src/HyperPet.App/App.xaml.cs:46`; registry startup:
  `src/HyperPet.Windows/Startup/StartupService.cs:16`.
- Tọa độ roaming: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs:50`.
- Swap pet/FPS: `src/HyperPet.App/Views/SettingsWindow.xaml.cs:596`, `:610`.
- Ghi settings: `src/HyperPet.Core/Settings/SettingsStore.cs:53`, `:61`.
- Thoát: `src/HyperPet.App/MainWindow.xaml.cs:833`.

### Nguồn advisory

- [ImageSharp tiled TIFF decoder](https://github.com/advisories/GHSA-v76p-62qx-wwq2).
- [ICC allocation](https://github.com/advisories/GHSA-gwg2-r3hj-4w44).
- [HistogramEqualization](https://github.com/advisories/GHSA-j3p4-wp97-rph4).
- [TIFF T4 encoder](https://github.com/advisories/GHSA-j9gm-c75j-xc9q).
- [BigTIFF decoding loop](https://github.com/advisories/GHSA-wmxv-xphr-5c9g).
- [TIFF T6 encoder](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w).

Không phải mọi code path trong các advisory đều được HyperPet sử dụng.

## Feature nên thêm, theo ưu tiên

1. Trạng thái notifications: quyền Windows, Zalo watcher, lần nhận gần nhất;
   nút Test để người dùng tự kiểm tra.
2. Phím tắt toàn cục Summon/Recenter, cứu pet khi bị lạc màn hình.
3. Chọn màn hình và tự phục hồi vị trí khi rút màn hình ngoài.
4. Hàng đợi/lịch sử thông báo giới hạn, tránh bubble mới ghi đè bubble chưa đọc;
   có nút xóa lịch sử vì nội dung có thể riêng tư.
5. Export/import settings và phục hồi bản backup khi lưu lỗi.

## Cải tiến kỹ thuật nên làm

1. Ưu tiên ImageSharp, sau đó cache popup và tách hai pipeline notifications.
2. Thêm test cho toast đổi nội dung nhưng giữ HWND/rect, quyền bị từ chối,
   Tuck Away khi đang alert và DPI 150%.
3. Release script cần bảo đảm code tính năng đã commit trước khi tag;
   hiện script chỉ tự stage file csproj. Release này stage rõ toàn bộ file liên quan.
4. Chuyển VelopackApp.Run về entry point Main đúng khuyến nghị của packager;
   hiện packager cảnh báo vì hook ở OnStartup. Cần test install/update/uninstall.
5. CI Windows chạy test/audit/build và tạo checksum; cân nhắc ký số installer.

Đề xuất thứ tự bản tiếp theo: thư viện ảnh → notifications → lưu settings/
single-instance → DPI → các feature tiện dụng.
