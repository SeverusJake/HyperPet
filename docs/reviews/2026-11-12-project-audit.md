# HyperPet — audit ngày 12/11/2026

Snapshot: `88a5026`, nhánh `main`, app `0.7.1`. Audit tìm lỗi và đề xuất xử lý; không sửa production code, không xóa artifact, không commit/push/release.

## Phạm vi và bằng chứng

- Kiểm kê toàn bộ cây thư mục, kể cả file ẩn, output, Git và worktree. Repo có 155 file tracked: 85 trong src, 34 trong tests, 29 trong docs, 7 file gốc.
- Rà logic chính của cả ba project, XAML, settings, import/pet lifecycle, notification pipelines, roaming, update và scripts. Đối chiếu tests, tài liệu và bản phát hành. Binary được kiểm kê/checksum; không reverse-engineer mọi bản cũ.
- Không có `AGENTS.md` hay `SKILL.md` trong cây dự án, kể cả worktree cũ. Đã đọc AGENTS cá nhân và các skill thực sự dùng trong lượt audit; không audit toàn bộ nội dung mọi skill cài ngoài workspace.
- Debug tests: **198/198 pass** — 61 Core, 137 App.
- Release tests: **198/198 pass** — 61 Core, 137 App.
- `dotnet list HyperPet.sln package --vulnerable --include-transitive --format json`: không liệt kê package có advisory tại thời điểm kiểm tra. Không có nghĩa mọi dependency đều không thể có lỗi.
- Cả **5 checksum** trong `Releases/SHA256SUMS.txt` khớp file hiện tại.
- Chạy probe riêng trong `%TEMP%`, tham chiếu project thật. Các phép xóa chỉ tác động dữ liệu giả trong sandbox đã kiểm tra đường dẫn.
- Chưa thử end-to-end install/update/uninstall, đăng nhập Windows, nhiều màn hình vật lý khác DPI, hoặc Zalo thật. Không xem static audit là bằng chứng đã tìm hết mọi lỗi runtime.

Mức: **P1** cần chặn trước phát hành tiếp; **P2** lỗi chức năng/độ tin cậy cần sửa; **P3** bảo trì, tính nhất quán hoặc tối ưu. Mỗi mục ghi rõ nếu mới là rủi ro chưa tái hiện.

## P1 — mất dữ liệu và tính đúng đắn bản phát hành

### 01. ID pet `.` / `..` có thể xóa cả Pets hoặc thư mục cha

[PetImporter.cs:57](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:57), [bộ lọc tên:134](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:134).

`SanitizeFolderName` chỉ thay ký tự cấm; `.` và `..` vẫn hợp lệ với cách lọc này. Ghép vào userPetsRoot rồi xóa recursive khi callback cho phép ghi đè. `..` trỏ về `%APPDATA%/HyperPet`, nơi còn settings, logs và các pet khác. Không có kiểm tra target phải là thư mục con trực tiếp của root sau canonicalization.

**Đã tái hiện:** cả hai import trả failure nhưng marker dữ liệu cũ đã bị xóa. Trên UI hiện còn lỗi callback ở mục 03; không thể dựa vào lỗi đó để bảo vệ filesystem API này.

Hướng xử lý: từ chối dot segments, tên thiết bị/reserved names, đường dẫn tuyệt đối; kiểm tra resolved target và quan hệ với root trước mọi mutation. Kiểm tra thêm junction/reparse point và source/target overlap.

### 02. Ghi đè xóa bản cũ trước khi bản mới được chuẩn bị xong

[PetImporter.cs:67](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:67).

Staging có tồn tại nhưng được dùng **sau** `Directory.Delete(target)`. Copy thất bại không thể rollback. Đặc biệt chọn import chính folder đang cài: source cũng bị xóa trước khi copy.

**Đã tái hiện:** import lại `userRoot/self` với ID `self` trả lỗi không tìm thấy source; folder gốc biến mất.

Hướng xử lý: kiểm tra source/target không trùng/lồng nhau; stage và validate hoàn chỉnh trước; đổi tên bản cũ thành backup, swap bản mới, rollback nếu lỗi.

### 03. Release có thể chứa code không có trong tag

[release.ps1:43](H:/MyProjects/HyperPet/release.ps1:43), [git:76](H:/MyProjects/HyperPet/release.ps1:76).

Publish lấy working tree trước khi commit. Script chỉ `git add` csproj; các thay đổi source chưa stage vẫn vào binary nhưng không vào commit/tag. Ngược lại, file đã stage từ trước cũng có thể bị commit kèm. Script push cố định `origin main`, nhưng tag được tạo ở HEAD hiện tại; chạy từ nhánh khác có thể đẩy main và tag từ hai nguồn khác nhau.

**Xác định từ luồng script**, không chạy release để tái hiện vì sẽ publish ra ngoài.

Hướng xử lý: release từ commit xác định, kiểm tra branch/index/working tree trước build, build từ checkout sạch, ghi commit SHA vào metadata và dùng tag đúng commit. Kiểm tra exit code riêng cho `git add`, `git tag` và bước xác minh remote.

## P2 — lỗi chức năng và độ tin cậy

### 04. Xác nhận ghi đè pet chạy sai UI thread

[PetImporter.cs:38](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:38), [SettingsWindow.xaml.cs:368](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:368).

Sau `ConfigureAwait(false)`, importer gọi callback `ConfirmOverwrite`, callback mở MessageBox với WPF Window làm owner. Continuation có thể chạy trên thread pool.

**Đã tái hiện trên WPF dispatcher:** `callbackHasUiAccess=False`; `VerifyAccess()` báo “The calling thread cannot access this object because a different thread owns it.” Import thất bại trước xác nhận. Tách UI confirmation khỏi phần I/O hoặc marshal callback về owner.Dispatcher.

### 05. Import báo thành công dù spritesheet không dùng được

[PetImporter.cs:49](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:49), [PetDefinitionLoader.cs:47](H:/MyProjects/HyperPet/src/HyperPet.Core/Pets/PetDefinitionLoader.cs:47).

Chỉ kiểm tra metadata và file tồn tại; chưa decode ảnh hoặc kiểm tra crop bounds lúc import. `spritesheetPath` cũng không bị buộc nằm trong folder pet: absolute path hoặc `../` có thể trỏ ra ngoài; file ngoài đó không được copy vào gói mới nên import không tự chứa dữ liệu.

**Đã tái hiện:** ảnh chỉ gồm ba byte `{1,2,3}` vẫn `importSuccess=True`. Test “Valid” hiện cũng dùng ảnh giả ba byte nên hợp thức hóa thiếu sót này. Validate staged pet bằng loader thực trước swap; ràng buộc đường dẫn ảnh nằm trong staging.

### 06. Chưa giới hạn tài nguyên của pet import

[PetImporter.cs:108](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetImporter.cs:108), [SpritePetLoader.cs:24](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/SpritePetLoader.cs:24).

ZIP giải nén toàn bộ, copy mọi file recursive; không giới hạn số entry, tổng byte giải nén hay độ sâu. Loader decode ảnh trước giới hạn pixel/frame budget. Pet quá lớn có thể làm đầy ổ hoặc cạn RAM. Đây là **rủi ro xác định từ thiếu guard**, chưa chạy ZIP bomb/OOM. Thêm quota và kiểm tra kích thước ảnh trước allocation; không bỏ cơ chế chống ZIP traversal sẵn có của .NET.

### 07. Từ chối quyền Windows làm tắt cả watcher Zalo

[App.xaml.cs:165](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:165), [StartInAppWatcher:437](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:437).

OnStartup return khi quyền không Allowed; in-app watcher chỉ khởi động bên trong StartMonitoring. Hai toggle riêng nhưng lifecycle phụ thuộc nhau. Lỗi đã được release notes thừa nhận. Khởi động và báo trạng thái hai nguồn độc lập.

### 08. Cache popup dùng vị trí làm đại diện cho nội dung

[InAppNotificationWatcher.cs:194](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/InAppNotificationWatcher.cs:194).

HWND/rect không đổi thì text không được đọc lại. Zalo/Electron thay text tại chỗ sẽ mãi nhận cached title/body; cây accessibility lúc đầu chưa sẵn sàng cũng có thể bị cache thành text rỗng. Content hash phía sau không cứu được vì nó hash text cũ. Cần TTL/invalidation theo thay đổi nội dung và retry extraction rỗng.

### 09. UI Automation và scan cross-process chạy trên UI thread

[InAppNotificationWatcher.cs:69](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/InAppNotificationWatcher.cs:69), [ChromiumAccessibilityActivator.cs:57](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs:57).

DispatcherTimer gọi scan đồng bộ: resolve process, enumerate HWND, UIA walk, đăng ký accessibility event. Own process luôn nằm trong watch list. Provider chậm có thể treo animation/UI; thao tác UIA lên UI của chính client có nguy cơ deadlock. Microsoft hướng dẫn dùng thread riêng, MTA cho trường hợp này: [UI Automation threading](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading).

`MaxTextNodes=64` giới hạn số tên thu được, không giới hạn tổng node đã đi qua/depth: cây nhiều node không có Name vẫn có thể quét dài. Chưa tái hiện hang thực tế. Dùng worker chuyên trách, budget số node/thời gian và marshal dữ liệu đã trích xuất về UI.

### 10. Tắt in-app notifications vẫn tiếp tục scan

[App.xaml.cs:488](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:488), [ApplyMonitoringSettings:544](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:544).

Toggle chỉ bỏ kết quả ở handler Detected; timer, process lookup và UIA vẫn chạy. Apply chỉ chỉnh interval; watch list cũng không được refresh. Stop/start nguồn theo toggle và cập nhật watch list khi cấu hình đổi.

### 11. Dedupe tăng vô hạn và có thể nuốt thông báo mới giống nội dung cũ

[NotificationDedupe.cs:5](H:/MyProjects/HyperPet/src/HyperPet.Core/Notifications/NotificationDedupe.cs:5), [source ID:372](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/InAppNotificationWatcher.cs:372).

HashSet sống cả session, không TTL/cap. In-app source ID chỉ có app, HWND và hash title+body; HWND tái sử dụng với text giống lần trước sẽ bị chặn, kể cả popup đã đóng rồi mở lại. Hai cặp `(title="ab", body="c")` và `(title="a", body="bc")` cũng tạo cùng đầu vào hash. Dùng identity theo lần xuất hiện/content revision, phân tách trường và dedupe có thời hạn/giới hạn.

### 12. Nội dung notification vẫn bị ghi vào log khi đã ẩn nội dung

[InAppNotificationWatcher.cs:383](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/InAppNotificationWatcher.cs:383), [SetupWindow.xaml:25](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SetupWindow.xaml:25).

Watcher ghi raw title xuống disk trước filter/redaction, không phụ thuộc DebugMode hay ShowFullNotificationContent. Clear history không xóa title đã log. UI Setup nói nội dung chỉ dùng để hiện bubble; người dùng dễ hiểu sai phạm vi lưu trữ. Bỏ title/body khỏi log mặc định; diagnostic content cần lựa chọn rõ ràng và retention riêng.

### 13. Quiet hours không chủ động dọn bubble/queue đúng thời điểm bắt đầu

[MainWindow.Notifications.cs:31](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.Notifications.cs:31), [DismissAlert:62](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.Notifications.cs:62).

Không có timer theo mốc quiet hours. Đang hiển thị trước giờ bắt đầu thì bubble còn đến lần dismiss/refresh sau; khi hover làm dừng expiry, có thể còn rất lâu. Nếu không có thao tác trong cả khoảng quiet hours, pending có thể tồn tại qua cửa sổ đó. Test hiện gọi DismissAlert thủ công sau khi đổi giờ nên không kiểm tra transition tự động. Cần scheduler theo boundary và clock inject được.

### 14. Ghi settings không nguyên tử

[SettingsStore.cs:53](H:/MyProjects/HyperPet/src/HyperPet.Core/Settings/SettingsStore.cs:53), [Save:61](H:/MyProjects/HyperPet/src/HyperPet.Core/Settings/SettingsStore.cs:61).

File.Create truncate bản tốt trước serialize. Crash/I/O failure có thể mất cấu hình; backup corrupt chỉ giữ file đã hỏng, không khôi phục bản tốt trước đó. Ghi file tạm cùng volume rồi atomic replace, giữ backup bản hợp lệ; serialize các lần save.

### 15. JSON hợp lệ chứa null trong override map làm lỗi startup

[SettingsStore.cs:101](H:/MyProjects/HyperPet/src/HyperPet.Core/Settings/SettingsStore.cs:101), [PetOverrides.cs:24](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetOverrides.cs:24).

Sanitize chỉ xử lý outer dictionary null. `{ "StateSpeedOverrides": { "miku-kimono": null } }` được load, nhưng foreach fpsMap ném NullReferenceException. Override được áp dụng trước khi MainWindow/tray được tạo. **Đã tái hiện exception**; app còn đánh dấu unhandled UI exception là Handled nên có nguy cơ sống nền mà không hiện cửa sổ. Sanitize sâu map, enum và entry; log rồi bỏ entry lỗi.

### 16. Chưa có single-instance hoặc cơ chế phối hợp settings giữa process

[App.xaml.cs:29](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:29), [SaveSettings:588](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:588).

Không thấy mutex/instance guard/IPC. Chạy hai instance có thể hiện thông báo đôi và save đè settings bằng snapshot cũ. Đây là kết luận từ lifecycle, chưa chạy hai app vì startup có ghi registry. Dùng mutex theo user và chuyển yêu cầu Summon/Settings sang instance hiện hữu.

### 17. Catalog pet bị cũ khi mở lại Settings

[MainWindow.xaml.cs:34](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:34), [SettingsWindow.xaml.cs:442](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:442).

MainWindow giữ readonly catalog lúc startup; mỗi dialog nhận snapshot đó. Import/remove chỉ refresh catalog trong dialog hiện tại. Đóng rồi mở lại có thể mất pet vừa thêm hoặc hiện pet đã xóa. Rediscover khi mở dialog hoặc dùng catalog service chung có thông báo thay đổi.

### 18. Sau đổi pet, State tab vẫn chỉnh pet cũ

[SettingsWindow.xaml.cs:26](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:26), [ApplyStateSpeedChanges:629](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:629).

Dialog giữ readonly spritePet, original FPS và state rows từ lúc mở. Chọn B và Apply chỉ reload MainWindow; State tab vẫn là A. Sửa FPS tiếp trong cùng dialog ghi vào override A. Cần cập nhật active pet/rows sau swap thành công; disable thao tác trong lúc load.

### 19. Reload pet thiếu thứ tự và báo lỗi cho người dùng

[SettingsWindow.xaml.cs:615](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:615), [ReloadPetAsync:919](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:919).

Reload fire-and-forget, không cancellation/generation guard. Chọn B rồi C nhanh: nếu B hoàn tất sau C, pet B có thể đè C. SelectedPet đã lưu trước khi load được xác nhận; loader fail/null giữ pet cũ nhưng dialog coi swap thành công. Đây là race xác định từ ordering, chưa điều khiển load delay để tái hiện. Trả kết quả rõ ràng, chỉ nhận request mới nhất và đồng bộ persisted selection với pet thực tế.

### 20. Swap khi đang alert có thể không hiển thị pet mới ngay

[MainWindow.xaml.cs:942](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:942), [StartBehaviorMode:195](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:195).

Tạo PetAnimator mới nhưng chưa Play; StartBehaviorMode return nếu `_alertActive`. Image.Source vẫn giữ frame pet cũ trong khi `_spritePet` đã là pet mới, cho đến transition sau. Tương tự cần kiểm tra trạng thái summon/hover đang dở. Khởi tạo animation phù hợp trạng thái hiện hành sau swap; reset controller/timer liên quan có chủ đích.

### 21. Roaming dùng bounding box cửa sổ thay cho sprite

[MainWindow.xaml:8](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml:8), [MainWindow.xaml.cs:257](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:257).

Window cố định 390×260; sprite mặc định 192×208, đặt giữa. Controller đặt window.Bottom trên ledge nên mép dưới sprite vẫn cách ledge **26 DIP**; size=1 cách **117 DIP**. Chiều ngang cũng bị giới hạn bởi 390 thay vì sprite width. Đây là phép tính layout xác định, chưa chụp pet trên cửa sổ thật. Dùng sprite bounds/foot anchor và offset nhất quán cho ledge, movement và clamp.

### 22. Animator không đáp ứng đầy đủ play-mode/completion contract

[PetAnimator.cs:71](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetAnimator.cs:71), [AdvanceFrame:194](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetAnimator.cs:194), [PlaybackStep.cs:35](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PlaybackStep.cs:35).

Single-frame non-loop return ngay và không phát Completed. PingPong non-loop coi lần chạm cuối chiều đi là Completed rồi giữ frame cuối, nên không phát chiều về dù UI cho chọn Ping-pong. Pure math test hiện còn cố định hành vi complete tại forward end. TimeUntilCycleEnd cũng chỉ tính phần còn lại theo Forward. Chốt semantics một lượt PingPong, thêm test ở PetAnimator với dispatcher, và xử lý one-frame completion. Tuck Away đã có timeout nên lỗi one-frame không còn chặn exit tại flow đó.

### 23. Hướng dẫn notification permission chưa có pipeline cài đặt tương ứng

[Package.appxmanifest:31](H:/MyProjects/HyperPet/src/HyperPet.App/Package.appxmanifest:31), [HyperPet.App.csproj:12](H:/MyProjects/HyperPet/src/HyperPet.App/HyperPet.App.csproj:12), [release.ps1:43](H:/MyProjects/HyperPet/release.ps1:43).

Manifest khai báo `HyperPet.App.exe`, nhưng assembly/output là `HyperPet.exe`; manifest version vẫn 0.1.0.0. Csproj chỉ coi manifest là None; release tạo Velopack Setup/ZIP, không có bước build/register MSIX hoặc external-location identity. Vì vậy README chỉ người dùng chạy packaged install nhưng repo chưa cung cấp pipeline manifest đó. Không khẳng định mọi máy unpackaged đều thất bại; cần test supported deployment thật.

Microsoft mô tả capability trong [notification listener](https://learn.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/notification-listener) và quy trình [cấp package identity](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps). Chọn deployment được hỗ trợ, sửa executable, đồng bộ metadata, cung cấp lệnh build/install kiểm chứng được.

### 24. FirstChanceException logging có thể tự gọi đệ quy

[App.xaml.cs:241](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:241), [HyperPetLogger.cs:65](H:/MyProjects/HyperPet/src/HyperPet.Core/Diagnostics/HyperPetLogger.cs:65).

Mọi exception, kể cả exception đã catch trong logger, kích hoạt handler rồi logger lại ghi file đồng bộ. Khi log path không ghi được, chính thao tác ghi lại ném exception, handler tiếp tục gọi logger. Catch “never throws” không ngăn FirstChance event phát trước catch.

**Đã tái hiện:** probe dùng handler có guard ở depth=5 quan sát được 5 mức gọi lồng nhau. Không cố chạy tới stack overflow. Bỏ global FirstChance logging ở bản thường; thêm reentrancy guard, level/filter và đường fallback không ghi lại bằng chính logger lỗi.

### 25. Release thiếu gate kiểm thử và checksum tự động

[release.ps1:33](H:/MyProjects/HyperPet/release.ps1:33), [assets:53](H:/MyProjects/HyperPet/release.ps1:53).

Pipeline bump → publish → pack → push; không chạy tests hoặc dependency audit. SHA256SUMS hiện đúng nhưng script không tạo và không upload file đó, trái với artifacts liệt kê trong release notes. Có thể phát hành regression hoặc checksum cũ. Thêm gates trước push, tạo checksum từ đúng tập output, upload checksum và xác minh assets sau release.

## P3 — nhất quán, hiệu quả và phần dư thừa

### 26. Subscription UIA không được gỡ theo từng process

[ChromiumAccessibilityActivator.cs:85](H:/MyProjects/HyperPet/src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs:85).

PruneStale chỉ bỏ PID khỏi HashSet, không giữ AutomationElement để remove handler tương ứng. Dispose dùng RemoveAllEventHandlers toàn process, có thể ảnh hưởng client UIA khác nếu thêm sau này. Chưa đo leak; nên quản lý subscription theo owner/process và gỡ bằng handler cụ thể.

### 27. Workaround giữ COM object mâu thuẫn chính cách triển khai

[WindowsNotificationListener.cs:16](H:/MyProjects/HyperPet/src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs:16), [TrimRetained:170](H:/MyProjects/HyperPet/src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs:170).

Comment nói giữ mọi UserNotification và collection cả session để tránh finalizer crash; thực tế chỉ giữ tối đa 256 entries, thêm lặp lại mỗi poll rồi RemoveRange; biến collection cũng không được giữ. Không đủ bằng chứng kết luận workaround ngăn crash được mô tả. Cần ghi issue/crash dump, môi trường bị ảnh hưởng, bài stress/GC test và lifetime strategy nhất quán; không xóa workaround trước khi xác minh.

### 28. Script launcher/pack có điều kiện chạy không rõ

[build.bat:3](H:/MyProjects/HyperPet/build.bat:3), [pack.ps1:9](H:/MyProjects/HyperPet/pack.ps1:9).

build.bat vẫn start exe nếu build fail, có thể mở binary cũ. pack.ps1 dùng relative paths mà không Set-Location về script root; chạy bằng absolute script path từ folder khác có thể thất bại. `-DryRun` trong release vẫn sửa version/build/ghi output; chỉ bỏ git/gh. Thêm exit-code guard, resolve paths theo PSScriptRoot, mô tả đúng tác dụng DryRun. Version cũng đang giữ lặp bốn trường csproj và manifest độc lập.

### 29. Factory defaults bị định nghĩa nhiều nơi và đã lệch

[HyperPetSettings.cs:56](H:/MyProjects/HyperPet/src/HyperPet.Core/Settings/HyperPetSettings.cs:56), [SettingsWindow.xaml.cs:55](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:55).

In-app interval mặc định model **4 giây**, nút Default đặt **2 giây**, dù comment khẳng định mirror model. UI labels/ranges và settings sanitation cũng lặp. Dùng một nguồn defaults và constraints; test nút Default so với CreateDefault.

### 30. Input lỗi có thể bị bỏ qua nhưng UI vẫn báo lưu thành công

[SettingsWindowSettingsApplier.cs:60](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindowSettingsApplier.cs:60), [SettingsWindow.xaml.cs:608](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml.cs:608).

Quiet-hours sai format giữ giá trị cũ; numeric parse lỗi dùng fallback, out-of-range clamp. TextBox không được đồng bộ lại, dirty vẫn reset. Người dùng nhìn giá trị không phải thứ app đang dùng. Validate tại field và chặn Apply khi lỗi, hoặc hiển thị rõ giá trị đã chuẩn hóa.

### 31. Settings cố định kích thước, không bảo đảm dùng được ở work area thấp

[SettingsWindow.xaml:7](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsWindow.xaml:7), [SettingsPlacement.cs:24](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SettingsPlacement.cs:24).

Dialog cao 640 DIP, NoResize. Placement chỉ clamp vị trí, không giảm kích thước. Trên work area thấp hơn 640 DIP, các nút dưới có thể nằm ngoài màn hình; scroll từng tab không cứu footer. Chưa thử màn hình vật lý nhỏ. Cho resize, giới hạn theo work area, bảo đảm footer và accessibility scaling.

### 32. Còn code và asset từ flow đã bỏ

[MainWindow.xaml.cs:90](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.xaml.cs:90), [HyperPetSettings.cs:35](H:/MyProjects/HyperPet/src/HyperPet.Core/Settings/HyperPetSettings.cs:35).

`appLauncher` vẫn được tạo/truyền nhưng constructor không dùng. OpenAppOnBubbleClick chỉ được preserve/serialize/test dù click đã luôn advance. TryActivateAsync luôn false, CanActivate hiện không điều khiển UI. MainWindowViewModel có PetState/IsBubbleVisible không được XAML binding; PetController giữ State/CurrentAlert riêng nhưng UI dismiss không gọi controller dismiss. Properties/Settings.settings và generated class rỗng; Assets/Pets/Default không có pet.json nên catalog không dùng hai PNG fallback.

Chọn một owner cho alert state, bỏ APIs/args/assets không còn dùng. Nếu cần đọc setting cũ để migration thì giữ đúng migration boundary, không luân chuyển qua toàn bộ UI/applier. Chưa đề xuất xóa debug simulator: nó còn phục vụ smoke test.

### 33. Tài liệu lịch sử chưa được đánh dấu superseded

[review cũ](H:/MyProjects/HyperPet/docs/reviews/2026-11-11-release-review.md:1), [sprite plan](H:/MyProjects/HyperPet/docs/superpowers/plans/2026-05-15-sprite-pet-states.md:9).

Review 0.6.1 còn đặt ImageSharp/uninstall/Tuck Away ở danh sách cần sửa, trong khi 0.7.x đã xử lý. Nhiều plan còn hàng chục checkbox chưa tick và code mẫu nguyên khối từ thời cũ. Đây là hồ sơ lịch sử, không phải bằng chứng các vấn đề đó còn ở HEAD. Tổng docs tracked **400.626 byte**; đọc cả kho sẽ tốn cỡ hàng chục nghìn token, tùy tokenizer.

Giữ lịch sử nhưng thêm status/superseded-by và index tài liệu hiện hành. Agent chỉ đọc docs đúng subsystem; không đưa toàn bộ plan vào context. Những nhận định “No functional bugs found” phải gắn snapshot và giới hạn test, không dùng lại như kết luận hiện tại.

### 34. Output và lịch sử Git chiếm phần lớn dung lượng, làm nhiễu tìm kiếm

[.gitignore](H:/MyProjects/HyperPet/.gitignore:1).

| Folder | File | Dung lượng đo được (MiB) |
|---|---:|---:|
| publish | 8.343 | 5.380,92 |
| Releases | 47 | 2.366,51 |
| .git | 3.404 | 2.659,51 |
| dist | 20 | 591,71 |
| src, gồm bin/obj | 945 | 1.086,63 |
| tests, gồm bin/obj | 526 | 723,58 |

publish/Releases hiện untracked nhưng không ignored, nên `rg --files` lẫn hàng nghìn generated file; ngược lại các `.nupkg` bị ignore bởi rule toàn cục. `.git` có **2,36 GiB loose objects**, không pack, và **236,89 MiB garbage**. Lịch sử reachable vẫn chứa exe 180,6 MiB của publish/v0.3.x, installer/ZIP cũ và libSkiaSharp.pdb 92,15 MiB.

Ignore output hoặc đặt ngoài repo; dùng Releases/CI artifact làm kho binary. GC/packing có thể tối ưu object storage nhưng không loại binary còn reachable khỏi lịch sử. Rewrite history là thao tác riêng cần thống nhất với người dùng/collaborator; audit không thực hiện.

Worktree `.claude/worktrees/strange-matsumoto-dd92cc` vẫn đăng ký ở commit cũ `c7b68dc`, trạng thái sạch khi kiểm tra. Chỉ khoảng 2,99 MiB, không phải nguồn tốn đĩa lớn. Xác nhận còn cần nhánh trước khi dọn bằng git worktree; không xóa tay metadata.

### 35. Thiếu môi trường kiểm chứng release được version-control

Không thấy CI Windows, global.json, local tool manifest cho vpk hoặc NuGet lock files. Máy audit có vpk 1.0.1 toàn cục; máy khác có thể dùng bản khác. Đây là **khoảng trống tái lập build**, không phải bằng chứng build hiện tại lỗi. Pin SDK/tool phù hợp, thêm Windows CI chạy Release tests/audit và kiểm tra packaging contents; tách việc build artifact khỏi publish ra GitHub.

### 36. Skills có chỉ dẫn xung đột và metadata dư cho dự án này

[using-superpowers:11](C:/Users/Zero/.codex/plugins/cache/openai-curated-remote/superpowers/7.0.1/skills/using-superpowers/SKILL.md:11) bắt load skill ở ngưỡng “1% chance”. Cách này mở rộng context quá dễ, trái tiêu chí chọn skill thực sự hữu ích của harness hiện tại. Các đoạn MUST/STOP/red flags lặp nhiều làm tăng instruction surface; core workflow vẫn có ích, không cần bỏ toàn bộ.

[codex-tools.md:21](C:/Users/Zero/.codex/plugins/cache/openai-curated-remote/superpowers/7.0.1/skills/using-superpowers/references/codex-tools.md:21) nói full-history fork nhận model/effort overrides, trong khi tool hiện tại chỉ cho overrides với fork_turns=none. [Mục đợi:51](C:/Users/Zero/.codex/plugins/cache/openai-curated-remote/superpowers/7.0.1/skills/using-superpowers/references/codex-tools.md:51) yêu cầu mỗi lần đợi 5–10 phút, xung đột giới hạn wait blocking 60 giây của session này. [Mục routing:64](C:/Users/Zero/.codex/plugins/cache/openai-curated-remote/superpowers/7.0.1/skills/using-superpowers/references/codex-tools.md:64) ép mọi spawn có model/effort, không phù hợp default kế thừa của tool. Tool schema và developer instructions hiện tại được ưu tiên.

Catalog skill gửi vào chat có các mục trùng seo-dataforseo, seo-firecrawl, seo-image-gen từ hai location. Nhiều metadata SEO/Godot/Unity/Cloudflare không phục vụ WPF audit; chúng chiếm input context dù chưa load SKILL.md. Không phải mọi token đó đều bị tính mới ở mọi request vì còn phụ thuộc caching/harness. Nên dùng bộ plugin/skill theo project, gộp duplicate, load nội dung khi có nhu cầu. Chưa audit correctness toàn bộ các gói ngoài workspace và chưa gỡ plugin nào.

### 37. AGENTS cá nhân gọn; thiếu hướng dẫn riêng của HyperPet

[AGENTS cá nhân](C:/Users/Zero/.codex/AGENTS.md:1) chỉ yêu cầu caveman khi bắt đầu session. Không thấy mâu thuẫn hay lượng token đáng kể; không cần xóa. Tuyên bố giảm token ~75% trong mô tả skill là mục tiêu quảng bá, chưa được đo trên dự án này; không dùng làm số tiết kiệm đã chứng minh.

Repo chưa có AGENTS riêng để chỉ đường cho contributor/agent. Đề xuất file ngắn: cấu trúc App/Core/Windows, lệnh test Release, WPF STA conventions, docs hiện hành, loại output khỏi tìm kiếm, quy tắc safe import và release từ commit sạch. Không nhúng bản sao skill hoặc toàn bộ kế hoạch cũ vào AGENTS. Không cần thêm nhiều agent persona cho repo WPF nhỏ này.

### 38. Thiếu hồ sơ nguồn gốc/license cho code và pet asset của dự án

Repo có SkiaSharp license/notices và đang copy ra publish đúng hướng, nhưng không có project LICENSE hay attribution/source record cho pet “Miku-inspired”. pet.json không ghi tác giả/nguồn/điều kiện phân phối. Đây là thiếu metadata cần làm rõ, **không phải kết luận vi phạm bản quyền**. Ghi nguồn, quyền sử dụng/phân phối và lựa chọn license thực tế; không tự gán license cho asset chưa xác minh.

## Chỗ còn cần kiểm chứng, chưa nâng thành lỗi đã xác nhận

- Mixed-DPI vật lý: code có bước chuyển physical/DIP và test synthetic; chưa đủ bằng chứng kết luận conversion đúng trong mọi lần chuyển monitor. Không lặp lại kết luận cũ rằng toàn bộ DPI chưa được sửa.
- Work-area thay đổi khi di chuyển taskbar: listener hiện dựa DisplaySettingsChanged/DpiChanged; cần thử loại thay đổi này có được cập nhật ngay không.
- Retained WinRT objects: chưa có crash dump/reference issue chứng minh giả thuyết CFG trong comment.
- App rule auto-discovery khi Settings đang mở: dialog dùng snapshot; Apply có thể thay danh sách mới auto-discovered. Cần test concurrency của luồng UI này.
- HistoryWindow rebuild toàn bộ tối đa 50 entries mỗi inbox Changed, kể cả chỉ tăng pending/advance; tối ưu nhỏ, chưa có profile chứng minh là bottleneck.
- Debug toast shortcut tồn tại sau uninstall; cần test uninstall thực trước khi khẳng định dọn sạch mọi dấu vết.
- Icon/NotifyIcon ownership và debug popup timer cleanup cần profile nếu thấy resource growth; chưa có số liệu để gọi là leak thực tế.

## Đối chiếu danh sách bổ sung — thêm 6 mục (39–44)

Đã đọc attachment người dùng cung cấp sau lượt audit đầu, đối chiếu lại source và manifest nhúng trong EXE Release. Tổng hiện có **44 mục đánh số**; không coi các ghi chú bảo trì dưới đây là thêm lỗi runtime đã tái hiện.

### 39. P2 — Load settings thất bại tạm thời có thể dẫn tới ghi đè bản tốt bằng defaults

[App.xaml.cs:84](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:84), [OnExit:201](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:201).

Khác mục 14: đây là lỗi recovery policy, không chỉ là atomic write. Nếu SettingsStore đã được tạo nhưng LoadAsync gặp IOException/UnauthorizedAccessException, app dùng defaults nhưng không đánh dấu session chỉ đọc hoặc load thất bại. Nếu lỗi tạm hết trước SaveSettings/OnExit, app ghi defaults lên settings thật. Trước đó còn có thể áp dụng StartWithWindows=true từ defaults thay cho lựa chọn đã lưu.

Xác định từ luồng catch/save, chưa tái hiện cả lifecycle app vì sẽ đụng registry. Giữ cờ loadSucceeded; khi fallback vì lỗi truy cập, không ghi đè nguồn hoặc áp dụng side effect từ defaults cho đến khi đọc lại/phục hồi được. Cho người dùng chọn reset riêng.

### 40. P2 — Chạy dev có thể chiếm autorun của bản đã cài

[App.xaml.cs:94](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:94), [ApplyStartupSetting:530](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:530), [StartupService.cs:22](H:/MyProjects/HyperPet/src/HyperPet.Windows/Startup/StartupService.cs:22).

Dev và installed app dùng chung `%APPDATA%/HyperPet` và registry value `HKCU/.../Run/HyperPet`. Mỗi launch áp dụng startup setting với Environment.ProcessPath. Khi bật startup, `dotnet run` trỏ registration sang exe trong bin/Debug; launch portable cũng có thể trỏ sang folder tạm. Nếu setting đang tắt, launch dev có thể xóa registration chung.

Đã đối chiếu code, không sửa registry để thử. Phân biệt deployment/channel; dev không tự quản lý autorun bản production. Cho dev data/profile riêng và dùng launcher ổn định của installation phù hợp.

### 41. P2 — Chưa khai báo per-monitor DPI awareness trong binary hiện tại

[HyperPet.App.csproj](H:/MyProjects/HyperPet/src/HyperPet.App/HyperPet.App.csproj:1), [DpiChanged subscription](H:/MyProjects/HyperPet/src/HyperPet.App/MainWindow.Monitors.cs:80).

Đã đọc XML manifest nhúng trong `bin/Release/net8.0-windows10.0.19041.0/HyperPet.exe`: chỉ có assembly identity và requestedExecutionLevel=asInvoker, không có dpiAware/dpiAwareness. Không thấy app.manifest, ApplicationManifest hoặc lệnh SetProcessDpiAwareness* trong source. WPF mặc định system-DPI-aware theo [Microsoft](https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware); tính toán DIP và subscribe DpiChanged tự chúng không bật chế độ per-monitor.

Đây là thiếu cấu hình xác định, bổ sung quan trọng cho phần mixed-DPI trước đó. Chưa quan sát mờ/sai vị trí trên hai màn hình thật; không dùng câu tuyệt đối “DpiChanged không bao giờ chạy”, vì còn môi trường compatibility hoặc host có thể ảnh hưởng awareness. Khai báo awareness theo [hướng dẫn manifest của Microsoft](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process), kiểm tra context thực của HWND rồi test chuyển monitor 100%/150%/200% và tọa độ âm.

### 42. P2 — Import trùng ID built-in báo thành công nhưng pet mới không xuất hiện

[App.xaml.cs:109](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:109), [PetCatalog.cs:44](H:/MyProjects/HyperPet/src/HyperPet.App/Pets/PetCatalog.cs:44).

Importer chỉ kiểm tra target trong userPetsRoot. Nếu ID trùng pet built-in, nó vẫn cài và UI báo Added. DiscoverAsync nhận builtin root trước user root và loại ID trùng theo first-root-wins; bản user bị che hoàn toàn. Đây khác catalog snapshot cũ ở mục 17: rediscover đúng vẫn không hiện pet vừa import. Chọn policy rõ: reject ID built-in với thông báo, đổi ID, hoặc cho user override có chủ đích và sửa thứ tự resolve.

### 43. P3 — Bootstrap Velopack trong OnStartup chưa theo entry-point được khuyến nghị

[App.xaml.cs:41](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:41).

Run ở đầu OnStartup nhưng WPF Application đã được khởi tạo trước đó; không phải thao tác đầu tiên của process như comment mô tả. [Velopack hướng dẫn custom Main cho WPF](https://docs.velopack.io/getting-started/csharp) để bootstrap trước WPF, giảm overhead khi lifecycle command chạy. Tài liệu cũng nói đặt trong App.xaml.cs là khả thi, nên không nâng thành kết luận install/update chắc chắn hỏng chỉ vì vị trí hiện tại.

Nên chuyển hook vào Main trước tạo App và kiểm thử install/update/uninstall. Đây là mục có trong review cũ nhưng báo cáo đầu chưa tách ra đầy đủ.

### 44. P3 — Setup hiển thị trạng thái chưa xin quyền sau khi đã xin

[App.xaml.cs:163](H:/MyProjects/HyperPet/src/HyperPet.App/App.xaml.cs:163), [SetupWindow.xaml:31](H:/MyProjects/HyperPet/src/HyperPet.App/Views/SetupWindow.xaml:31).

App đã await RequestAccess trước khi mở Setup nhưng StatusText mặc định vẫn “Access has not been requested yet.” Constructor không nhận accessStatus ban đầu. Truyền kết quả trước đó và phân biệt Denied/Unspecified/error; hướng dẫn tiếp theo phù hợp thay vì yêu cầu thử lại chung chung.

### Chi tiết bổ sung vào các mục đã có

- **Mục 09–10:** ownProcessName luôn được thêm vào watch list, kể cả DebugMode=false. Process phụ Electron chưa có MainWindowHandle cũng bị thử activation lại mỗi scan. Chỉ watch simulator khi Debug bật và thêm backoff/identity process nếu profile cho thấy overhead đáng kể.
- **Mục 32:** SimulateMessagingNotification và StartupService.IsEnabled không thấy call site production. State `running` của pet mặc định không có flow dùng trực tiếp, nhưng có thể là dữ liệu tương thích; xác minh contract trước khi bỏ. Hai smoke log hiện dài 0 byte. Nhánh `codex/auto-notification-placement` cùng commit `88a5026` với main, có thể dọn nếu không còn mục đích.
- **Mục 33:** comment “logged” trong auto-update thực tế catch rỗng; comment simulator còn mô tả press-9 gửi Windows toast, dù flow hiện dùng popup; comment TrayIcon chưa kể History. Cập nhật comment, không suy ra menu thật thiếu History.
- **Mục 34:** đã kiểm tra `refs/codex/turn-diffs/checkpoints/...` bằng git ls-tree: ref này giữ publish/v0.3.4/HyperPet.exe **189.385.896 byte**, v0.3.7 **189.398.184 byte**. Có bằng chứng trực tiếp checkpoint giữ output lớn. Ignore output giúp tránh snapshot mới; không tự loại object cũ còn được ref giữ.
- **Mục 36–37:** đã đọc thêm [CLAUDE.md cá nhân](C:/Users/Zero/.claude/CLAUDE.md:1). Có hai đoạn mô tả trigger graphify, có thể gộp để tránh drift. Caveman ở CLAUDE.md và Codex AGENTS.md phục vụ hai harness khác nhau; chưa chứng minh cả hai được nạp tự động trong cùng một Codex session. Không cần xóa một bên chỉ vì văn bản giống nhau.
- **Hiệu quả layout:** RoamTick gán Left/Top rồi ClampToWorkArea lại gán Left/Top. Không đồng nghĩa 4 OS moves: WPF có thể bỏ qua set cùng giá trị/coalesce. Có nhiều call site UpdateNotificationPlacement trong inbox Changed, UpdateBubbleContent và PresentAlert, nhưng guard làm nhiều lần return sớm; “3 lần layout cho mọi alert” chưa đúng. Có thể gom việc cập nhật vào một lần sau transaction và profile trước/sau.
- **Logic trùng:** privacy redaction ở PetController, MainWindow và inbox; quiet-hours gate ở Core/UI. Một phần là bảo vệ replay/queued content có chủ đích, không nên xóa guard mà mất privacy. Có thể chia sẻ policy/helper và test acceptance/replay riêng.
- **Logger:** open/append/close cho mỗi dòng và heartbeat mỗi poll là overhead thật về số thao tác, nhưng mức mặc định 30 giây không tự chứng minh là bottleneck. FirstChance storm/reentrancy ở mục 24 đáng ưu tiên hơn việc thêm logging queue phức tạp.

### Những nhận định trong phần bổ sung cần sửa hoặc chưa đủ bằng chứng

- **“Mail chặn Gmail”**: đúng với substring matching được UI ghi rõ. Không phải bug lệch implementation/spec; có thể cải thiện bằng exact AUMID hoặc kiểu match tùy chọn để giảm bất ngờ.
- **“Quit bỏ qua Tuck Away”**: hai hành vi có nhãn khác nhau, chưa có yêu cầu mọi exit phải animate. Bỏ qua _requestShutdown là bất nhất về abstraction/testability, không tự chứng minh lỗi thoát app.
- **“MainWindowViewModel không có XAML binding nên xóa hết”**: code-behind vẫn dùng CurrentAlert/AlertsPaused và DataContext. Chỉ xóa member hoặc cả lớp sau khi chuyển call site, không bỏ kiểu đang được tham chiếu.
- **“MonitorWorkArea.ForPoint không dùng”**: vẫn được OpenSettings gọi ở MainWindow.xaml.cs:876. Có logic overlap với DesktopMonitors nhưng cần hợp nhất call site trước khi bỏ.
- **“Bỏ CommunityToolkit.WinUI.Notifications và System.Drawing.Common ngay”**: generic debug toast vẫn gọi SimulateGenericNotification, phụ thuộc toolkit. System.Drawing còn dùng ở TrayIcon; cần phân biệt framework reference và package override trong Windows project. Chỉ bỏ dependency sau khi thay flow và kiểm tra dependency graph/audit.
- **“build.bat trùng Run HyperPet.bat”**: một file build solution rồi mở exe, file kia dotnet run project. Có thể hợp nhất UX, nhưng không phải hai lệnh có semantics giống hệt.
- **“README nói không notification content nào xuống disk”**: câu “not written to disk” hiện nói riêng history; log title vẫn là vấn đề privacy thật theo mục 12 và mô tả Setup. Cần chỉ đúng phạm vi lời hứa.
- **“Worktree cũ nhân đôi mọi grep”**: .claude đã nằm trong .gitignore; rg mặc định bỏ qua nó. Chỉ tìm kiếm dùng --no-ignore hoặc traversal không tôn trọng ignore mới nhân đôi kết quả. publish/Releases không ignored mới là nguồn nhiễu mặc định rõ ràng.
- **“Tạo delta chỉ cần full nupkg mới nhất nên xóa toàn bộ lịch sử”**: chưa xác minh policy updater và phiên bản client đang dùng. Không lấy nhận định này làm căn cứ xóa releases/manifest/history; cần phân biệt cache build local với artifacts đang phục vụ người dùng cũ.
- **“Pet hở 26px/117px”**: số đo layout đang là **DIP**, không phải physical pixel ở mọi DPI.

Không chạy lại tests vì lượt bổ sung chỉ thay tài liệu và kiểm tra read-only; kết quả 198/198 ở trên thuộc lần chạy thực trong lượt audit trước.

## Bổ sung khi lập kế hoạch — runtime lifecycle

### 45. P2 — Runtime .NET 8 đã hết support tại ngày lập plan

Bổ sung khi lập kế hoạch: cả runtime target net8 của source/tests và self-contained release cần được đánh giá nâng cấp, không chỉ pin SDK. [Microsoft xác nhận .NET 8 hết support ngày 10/11/2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/); ngày audit là 12/11/2026. Build bằng SDK 10 vẫn có thể sinh app chạy runtime 8. NuGet audit không thay thế kiểm tra runtime lifecycle.

Đề xuất .NET 10 LTS sau khi kiểm tra support minimum Windows, WPF, WinRT, SkiaSharp và Velopack; tách commit để phân biệt regression migration với bugfix khác. Mapping thực thi nằm trong Task 12 của remediation plan. Chưa đổi target trong lượt lập plan. Tổng audit hiện **45 mục đánh số**; kết quả test ban đầu vẫn thuộc net8.

## Những kết luận cũ không còn đúng tại snapshot này (đối chiếu)

- Không còn dependency ImageSharp trong source hiện tại; audit NuGet không báo sáu advisory cũ.
- Uninstall callback đã gọi gỡ startup registration, tách best-effort cleanup; chưa xác minh end-to-end installer.
- Tuck Away đã dừng timer liên quan và có timeout thoát dự phòng.
- Queue 100 pending, history 50, replay và redaction đã có tests; không cần xây lại từ đầu.
- Licenses SkiaSharp đã được cấu hình copy ra output/publish.
- 5 checksum bản local hiện tại khớp; vấn đề là pipeline chưa tự tạo/upload chúng.

## Thứ tự xử lý đề xuất

1. Chặn import thoát root, chống xóa trước staging/rollback; thêm regression tests bằng dữ liệu sandbox.
2. Sửa callback UI thread, validation pet, settings atomic/deep sanitation và logger reentrancy.
3. Tách notification pipelines, sửa cache/identity dedupe, đưa UIA khỏi UI thread; bảo vệ privacy logging và quiet-hours transitions.
4. Đồng bộ catalog/active pet/State tab, kiểm soát reload race, sửa foot-anchor và completion semantics.
5. Cố định release provenance, gates, checksum và deployment/identity; kiểm thử install/update/uninstall trên Windows.
6. Dọn code chết, archive/index docs, ignore output, tối ưu Git và thu gọn catalog skill theo môi trường. Không rewrite history/xóa pet tự động như một phần audit.

## Kết quả probe dùng để đối chiếu

```text
DOT_ID success=False oldMarkerExists=False
PARENT_ID success=False settingsMarkerExists=False
SAME_SOURCE success=False originalExists=False
CONFIRM_UI success=False callbackHasUiAccess=False
INVALID_IMAGE importSuccess=True
NULL_OVERRIDE NullReferenceException
LOGGER_NESTED_FIRSTCHANCE depth=5
SPRITE_LEDGE gapDefault=26 gapAtSize1=117
DEFAULT_POLL model=4 dialog=2
```

Probe có guard dừng logger recursion; không crash process để chứng minh stack overflow. Các output import thất bại nhưng marker bị xóa là bằng chứng lỗi, không phải import đã được sửa.
