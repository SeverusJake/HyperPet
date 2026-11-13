# HyperPet Audit Remediation Implementation Plan

> **Execution status (2026-11-12):** Local implementation and verification are recorded in [remediation status](../../reviews/2026-11-12-remediation-status.md). This plan preserves the original proposal; unchecked steps and proposed class names are not a substitute for the status report. Runtime implementation now targets .NET 10. Isolated deployment/device validation and owner decisions remain open.

> **For agentic workers:** Use `superpowers:executing-plans` to implement task-by-task. Execute in the current chat by default; delegation only when authorized by the user or applicable instructions. Checkboxes track actual work, not intent. This document is a proposed plan, not approval to publish or delete data.

**Goal:** Sửa các lỗi có bằng chứng trong audit, bảo vệ dữ liệu và privacy, ổn định notifications/pet lifecycle, rồi làm quy trình build/release và hướng dẫn agent nhất quán.

**Architecture:** Giữ .NET/WPF và ba project App/Core/Windows. Core chứa policy thuần và state machine; App sở hữu UI dispatcher/lifecycle; Windows sở hữu adapters Win32/WinRT. Tách service nhỏ tại chỗ cần test, không rewrite MainWindow toàn bộ hoặc thêm framework DI.

**Tech Stack:** C#, .NET 8 Windows, WPF/WinForms interop, SkiaSharp, Velopack, xUnit; PowerShell cho build/release.

**Spec:** [Audit](H:/MyProjects/HyperPet/docs/reviews/2026-11-12-project-audit.md): 44 mục ban đầu và mục 45 về runtime support bổ sung khi lập plan. Snapshot ban đầu `88a5026`, version 0.7.1. Baseline đã quan sát: Debug 198/198, Release 198/198; dependency audit không báo advisory; 5 checksum hiện tại khớp. Chạy lại baseline tại thời điểm bắt đầu triển khai.

## Phương án và các quyết định đề xuất

Chọn **sửa từng cụm có test hồi quy**, tách commit/PR theo deliverable. Hotfix chỉ P1 sẽ nhanh nhưng để nguyên lỗi settings/notifications đã rõ; rewrite lớn làm khó đối chiếu regression và tăng context. Plan này dùng một backlog có dependency rõ, triển khai tuần tự mặc định để tránh đụng chung App.xaml.cs/MainWindow.

Giả định do người lập plan đề xuất, chưa coi là quyết định đã được người dùng duyệt:

- Pet import trùng ID built-in: **từ chối với thông báo rõ**, giữ nguyên built-in và bản user hiện tại; không âm thầm override hoặc đổi ID.
- Import từ chối dot segments, reserved Windows names, rooted/escaping paths, reparse points; validate staged copy trước swap. Quota ban đầu: ZIP nén tối đa 100 MiB, giải nén/copy tối đa 256 MiB, mỗi file tối đa 64 MiB, 1.024 file, sâu 8 cấp; tối đa 32 state, 256 frame tổng, cạnh ảnh tối đa 8.192 pixel, tổng ảnh tối đa 16.777.216 pixel, decoded frame budget 256 MiB, FPS 1–60. Đặt các giá trị tại một policy, test boundary; đánh giá lại nếu pet hợp lệ thực tế vượt quota.
- Defaults dùng model làm nguồn: **in-app polling 4 giây**, Windows polling 30 giây. Không tăng tần suất mặc định lên 2 giây chỉ để khớp nút Default hiện tại.
- Debug build mặc định dùng `%LOCALAPPDATA%/HyperPet/Dev`, không sửa autorun production. Bản đã cài giữ `%APPDATA%/HyperPet`; không tự chuyển/xóa settings cũ. Bản portable không tự chiếm autorun của bản installed; cần policy launch rõ trước khi cho bật startup.
- Notifications giữ queue 100, history 50, session-only. Log mặc định không chứa title/body. Tắt source thì dừng scan và loại current/pending của source đó; giữ lịch sử đã nhận. Pause/quiet-hours vẫn dọn current/pending toàn bộ.
- Windows dedupe: theo notification identity/revision, TTL đề xuất 24 giờ, tối đa 4.096 key. Đây là cache chống lặp, không phải kho lịch sử.
- Một lượt PingPong non-loop là `0→N-1→0`, mỗi đầu hiển thị một frame-duration trước Completed; one-frame non-loop cũng phải Completed một lần.
- Framework hiện tại net8 đã hết support ngày 10/11/2026 theo [Microsoft](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/). Đề xuất chuyển .NET 10 LTS trong Task 12, thành một commit riêng trước release scripts; cần kiểm tra WPF/WinRT/Skia/Velopack và minimum Windows thực tế trước khi chọn target mới. Cài SDK 10 không đổi runtime của app net8 self-contained. Không nâng package hàng loạt, thêm telemetry hoặc đổi nhân vật mặc định. Không tự xóa binary/worktree/ref, rewrite Git history, sửa plugin cache hay file global của người dùng.
- Deployment có package identity là điểm phải thử nghiệm riêng ở Task 13. Không hứa Velopack hiện tại tự cấp MSIX identity. Chưa quyết định chuyển toàn bộ updater sang MSIX.

## Global Constraints

- Mọi test filesystem nguy hiểm chỉ chạy trong thư mục sandbox đã canonicalize; tuyệt đối không dùng `%APPDATA%/HyperPet` thật.
- Các giá trị queue/history/privacy và compatibility settings hợp lệ phải được giữ qua migration.
- WPF tests chạy STA, pump dispatcher có timeout; native calls qua adapter có thể fake. Không test registry production, gửi toast thật hay chạm GitHub trong unit tests.
- Mỗi lỗi hành vi: viết regression fail vì lỗi đó, sửa tối thiểu, chạy targeted tests; chạy full Release suite ở cuối mỗi đợt có thay đổi runtime. Không ép viết tests cho sửa comment/.gitignore đơn giản.
- Chưa publish, tag, push hay release trong giai đoạn sửa. Build và pack kiểm chứng phải dùng output cách ly; remote side effects là bước riêng sau khi kết quả review được.
- Thay đổi API nội bộ phải cập nhật toàn bộ call sites/tests cùng task. Không bỏ dependency hoặc workaround WinRT chỉ dựa vào suy đoán.
- Commit/PR nếu được thực hiện phải giới hạn file của task; không `git add .` vì publish/Releases đang untracked. Không tự commit khi lượt thực thi chưa xác định cách quản lý branch.

## Review Focus

1. Source/target import trùng, lồng nhau, có junction, hoặc copy/swap lỗi giữa chừng: dữ liệu cũ còn nguyên — Tasks 2–3.
2. Settings load lỗi tạm, disk full, hai launch sát nhau: không overwrite defaults, không giành autorun — Tasks 4–5.
3. Popup giữ HWND/rect nhưng text cập nhật hoặc accessibility chậm/hang: UI sống, nội dung cuối được nhận, không duplicate — Tasks 7–8.
4. Reload B/C hoàn tất ngược thứ tự trong lúc alert/hover và quiet-hours đổi mốc: state thực tế khớp settings, không phát alert bị cấm — Tasks 8–9.
5. Binary/tag/deployment không cùng nguồn hoặc khác DPI/monitor: release bị chặn khi không chứng minh provenance/permission/placement — Tasks 10, 12–13.

## Lộ trình

| Đợt | Deliverable | Tasks | Audit |
|---|---|---|---|
| 0 | Workspace ít nhiễu, baseline rõ | 1 | 34, 37 |
| 1 | Import không làm mất dữ liệu | 2–3 | 01, 02, 04–06, 42 |
| 2 | Settings/startup/logging an toàn | 4–6 | 12, 14–16, 24, 39–40, 43 |
| 3 | Notifications độc lập, không bỏ sót vì cache | 7–8 | 07–11, 13, 26–27, 44 |
| 4 | Pet/UI nhất quán | 9–11 | 17–22, 29–31, 41 |
| 5 | Runtime còn support, release tái lập, deployment được kiểm chứng | 12–13 | 03, 23, 25, 28, 35, 45 |
| 6 | Dọn dư thừa, docs/skills/license rõ phạm vi | 14 | 32–38 |

Đợt 0–2 là ưu tiên thực thi trước. Các đợt sau có thể review riêng; không cần chờ toàn bộ cleanup mới chặn được mất dữ liệu. Không phát hành hotfix chỉ vì tests pass khi các P1 chưa đóng.

## Task 1 — Baseline và phạm vi tìm kiếm

**Files:** sửa [`.gitignore`](H:/MyProjects/HyperPet/.gitignore), tạo `H:/MyProjects/HyperPet/AGENTS.md`; cập nhật trạng thái trong audit. Không chỉnh global skills.

**Interfaces:** không thêm runtime API. AGENTS tối đa khoảng 60 dòng: project map, lệnh test, safe filesystem/registry, docs hiện hành, output exclusions và release provenance.

- [ ] Ghi nhận git status/worktree, SDK và vpk đang có; giữ file untracked của người dùng. Lúc thực thi tạo branch/worktree cách ly nếu cần; không copy publish/Releases vào checkout mới.
- [ ] Ignore `/publish/`, `/Releases/`; giữ `docs/releases` tracked. Di chuyển notes nguồn cần giữ sang docs bằng thao tác riêng chỉ khi đã xác định file cụ thể, không move recursive output.
- [ ] Chạy `dotnet test HyperPet.sln -c Release` và dependency audit tại checkout thực thi. Nếu baseline khác 198, giải thích trước khi dùng số cũ.
- [ ] Kiểm tra `rg --files` mặc định không liệt kê generated output/worktree; không dùng blanket `--no-ignore` khi review.

**Done:** không thay runtime behavior; có baseline và scope search rõ. `.git` cũ vẫn giữ nguyên.

## Task 2 — Import path guard và overwrite transaction

**Files:** sửa `src/HyperPet.App/Pets/PetImporter.cs`, `PetRemover.cs`, `Views/SettingsWindow.xaml.cs`; tạo `Pets/PetPathPolicy.cs`, `Pets/PetImportFileOperations.cs`; tests trong `tests/HyperPet.App.Tests/Pets/PetImporterTests.cs`, `PetRemoverTests.cs`, tạo `PetPathPolicyTests.cs`; mở rộng `tests/HyperPet.App.Tests/TestSupport/StaThread.cs` với overload `RunAsync(Func<Task> action): Task` pump dispatcher tới completion/timeout để kiểm tra continuation UI thật, không dùng async void dưới overload Action.

**Interfaces:** giữ `PetImportResult` và hai public `ImportFromFolderAsync`/`ImportFromZipAsync` cùng callback hiện tại để giảm churn. Internal `PetPathPolicy.ResolveInstallTarget(string root, string id): string` trả canonical direct child hoặc throw validation exception; `PetPathPolicy.ValidateSource(string source, string target): void` từ chối source=target, ancestor/descendant và reparse points. `PetImportFileOperations` là internal adapter với virtual `CopyFile(string source, string destination)` và `MoveDirectory(string source, string destination)` để inject failure trong tests; public path dùng adapter mặc định.

- [ ] Thêm tests `DotAndParentIds_DoNotDeleteMarkers`, `ReservedNames_AreRejected`, `SameOrNestedSource_IsRejectedBeforeMutation`, `ReparsePoint_IsRejected`, `CopyFailure_PreservesOldPet`, `SwapFailure_RestoresBackup`. Assert marker outside target và bản cũ vẫn tồn tại, không để lại staging được discover.
- [ ] Run `dotnet test tests/HyperPet.App.Tests --filter "FullyQualifiedName~PetImporterTests|FullyQualifiedName~PetPathPolicyTests|FullyQualifiedName~PetRemoverTests"`; xác nhận fail đúng lỗi cần sửa. Test junction chỉ skip với lý do cụ thể nếu máy không hỗ trợ, không coi skip là pass.
- [ ] Implement thứ tự: resolve/validate → stage sibling ở cùng volume → validate staged payload → xác nhận overwrite trên caller UI context → rename old sang backup → move staged vào target → cleanup backup. Nếu move staged lỗi, restore old trước khi trả failure. Nếu rollback cũng lỗi, giữ backup và trả vị trí recovery, không xóa backup.
- [ ] Callback UI không chạy sau continuation đã bỏ dispatcher context. File I/O chạy worker; chỉ callback và UI result về dispatcher. Test với WPF dispatcher đang pump: callback `CheckAccess()==true`; không mở MessageBox thật.
- [ ] Run targeted tests green; review riêng tính đúng canonical path và rollback. Stage các file chỉ định nếu có commit cho task này.

**Done:** tất cả reproducer mất dữ liệu từ audit chuyển green; không dựa vào hộp xác nhận để bảo vệ root.

## Task 3 — Validate pet thực và quota trước install

**Depends on:** Task 2.

**Files:** sửa `PetImporter.cs`, `PetCatalog.cs`, `SpritePetLoader.cs`, `src/HyperPet.Core/Pets/PetDefinitionLoader.cs`; tạo `src/HyperPet.App/Pets/PetImportLimits.cs`; tests `PetImporterTests.cs`, `SpritePetLoaderTests.cs`, `PetDefinitionLoaderTests.cs`.

**Interfaces:** `PetImportLimits` chứa các quota nêu trong quyết định đề xuất; không hard-code lại trong UI. Thêm overload importer nhận `IReadOnlySet<string> protectedIds` và `CancellationToken`; giữ wrapper hiện tại nếu tests/call sites cần. Settings truyền IDs built-in lấy từ catalog built-in, không suy luận từ tên thư mục. `SpritePetLoader.LoadAsync(string petDirectory, CancellationToken cancellationToken = default)` kiểm tra dimensions/pixel budget trước decode và total frame budget trước crop.

- [ ] Tests: `InvalidImage_IsRejected`, `EscapingSpritesheet_IsRejected`, `BuiltinIdCollision_IsRejected`, `MultiplePetJsonInZip_IsRejectedAsAmbiguous`, `ZipQuota_StopsExtraction`, `MetadataBudget_RejectsBeforeAllocation`, `Cancellation_PreservesOldPet`. Dùng PNG/WebP nhỏ hợp lệ thay ảnh giả trong test “Valid”.
- [ ] Run các test trên fail; không dùng ZIP bomb hoặc allocation lớn để kiểm quota: hạ limits bằng test policy hoặc fake header.
- [ ] Duyệt ZIP entry trước extraction: canonical containment, size/count/depth; vẫn đếm byte thực khi extract để không chỉ tin metadata. Copy folder có cùng quota; từ chối mọi reparse point trong tree. Không follow external sheet path.
- [ ] Validate staged pet bằng loader thật, chỉ promote sau khi ảnh/crop/state hợp lệ; disposal bitmap/staging phải có đường cleanup khi cancel/fail.
- [ ] Run targeted tests rồi full Release suite. Ghi rõ import limit trong thông báo lỗi/README, không đổi format pet hợp lệ hiện có.

**Done:** “Added” luôn có nghĩa pet load được, không bị built-in che, và không vượt budget đã chọn.

## Task 4 — Settings atomic, sanitation và recovery policy

**Files:** sửa `src/HyperPet.Core/Settings/SettingsStore.cs`, `HyperPetSettings.cs`, `src/HyperPet.App/App.xaml.cs`, `Pets/PetOverrides.cs`; tạo `src/HyperPet.Core/Settings/SettingsLoadResult.cs`, `SettingsWriteOperations.cs`; mở rộng `SettingsStoreTests.cs`, `PetOverridesTests.cs`; tạo `tests/HyperPet.App.Tests/SettingsRecoveryTests.cs`.

**Interfaces:** `SettingsLoadResult(HyperPetSettings Settings, bool CanPersist, bool CanApplyStartup, string? Warning)`; thêm `SettingsStore.LoadSessionAsync(): Task<SettingsLoadResult>`. Wrapper `LoadAsync()` cho callers hiện tại nếu cần. Read I/O failure trả defaults với cả hai bool=false; missing file/valid data=true; corrupt JSON chỉ cho persist sau khi backup thành công. App lưu session policy thay vì suy ra từ `_settings != null`. `SettingsWriteOperations` internal virtual `Replace(string temporaryPath, string destinationPath, string backupPath): void` và `MoveNew(string temporaryPath, string destinationPath): void` cho fault injection.

- [ ] Tests `WriteFailure_KeepsLastValidFile`, `LockedLoad_DoesNotAllowDefaultOverwrite`, `NullNestedOverrides_AreDropped`, `UnknownEnum_IsSanitized`, `ExistingValidSettings_RoundTrip`, `CorruptBackupFailure_DisablesPersistence`.
- [ ] Run Core/SettingsStore và App/PetOverrides/SettingsRecovery tests fail trước sửa. Test load failure qua file lock/adapter trong sandbox, không khóa settings thật.
- [ ] Serialize snapshot sang temp cùng directory; flush/close; replace existing với backup hoặc move khi chưa tồn tại. Sanitize sâu dictionaries, key casing, finite coordinates và enum. Dùng một gate cho Save/SaveAsync; không để hai writer trong cùng process truncate/replace chéo.
- [ ] App không save/autorun từ fallback session read-failure; OnExit tôn trọng CanPersist. UI cảnh báo rõ và có retry load, không tự reset file khi người dùng chưa yêu cầu.
- [ ] Targeted tests green, full Release suite; kiểm tra restore từ backup trong sandbox.

**Done:** lỗi đọc/ghi không phá bản hợp lệ trước đó; settings JSON null hợp lệ không làm startup dừng giữa chừng.

## Task 5 — Launch context, single instance và bootstrap

**Depends on:** Task 4 trước khi startup side effects được áp dụng.

**Files:** sửa `App.xaml.cs`, `App.xaml`, `HyperPet.App.csproj`, `src/HyperPet.Windows/Startup/StartupService.cs`; tạo `src/HyperPet.App/Lifecycle/AppLaunchContext.cs`, `SingleInstanceCoordinator.cs`; tạo tests `LaunchContextTests.cs`, `SingleInstanceCoordinatorTests.cs`.

**Interfaces:** `enum AppLaunchKind { Development, Installed, Portable }`; `AppLaunchContext(AppLaunchKind Kind, string DataDirectory, bool CanManageStartup)`. Resolver nhận build flavor/installed state/path có thể fake. `SingleInstanceCoordinator.TryAcquire(string instanceKey): bool` và `RequestActivationAsync(string instanceKey, CancellationToken): Task<bool>` dùng mutex + named pipe theo current user/profile; activation marshaled về UI để gọi Summon hoặc OpenSettings. Giữ startup registration adapter có thể fake trong test.

- [ ] Tests `Development_DoesNotWriteProductionRunKey`, `LoadFailure_DoesNotApplyStartup`, `SecondLaunch_DoesNotLoadOrSaveSettings`, `SeparateProfiles_DoNotConflict`, `Activation_UsesUiDispatcher`, `PipeRequest_IsBoundedAndCurrentUserOnly`.
- [ ] Run fail; tạo `[STAThread] Main` gọi Velopack bootstrap trước tạo App. Lifecycle install/uninstall command phải được xử lý trước mutex; launch thường acquire mutex trước load/save/autorun.
- [ ] Dùng profile dev riêng; không migrate/xóa production data tự động. Second process gửi activation có timeout rồi exit; lỗi IPC không được mở instance thứ hai và ghi settings chung.
- [ ] Giới hạn named pipe cho current user; disposal mutex/pipe trên shutdown. Giữ uninstall cleanup độc lập registry/data hiện có.
- [ ] Run tests green; smoke bằng build test profile không sửa registry thật. Kiểm tra built entry point và install/uninstall hook ở Task 13.

**Done:** dotnet run không làm mất autorun production; một profile chỉ có một writer/process.

## Task 6 — Logger reentrancy và privacy

**Files:** sửa `App.xaml.cs`, `src/HyperPet.Core/Diagnostics/HyperPetLogger.cs`, `Notifications/InAppNotificationWatcher.cs`, `Views/SetupWindow.xaml`; tạo `tests/HyperPet.Core.Tests/Diagnostics/HyperPetLoggerTests.cs` và App `NotificationPrivacyTests.cs`.

**Interfaces:** giữ Info/Warn/Error/Fatal. Bản thường không subscribe global FirstChanceException; nếu giữ chế độ diagnostic explicit thì handler dùng thread-local reentrancy guard và không log raw content. Không thêm background queue/telemetry khi chưa có nhu cầu đo được.

- [ ] Tests `UnwritablePath_DoesNotReenterLogging`, `LockedLog_DoesNotPropagate`, `NotificationLog_DoesNotContainTitleOrBody`, `Rotation_IsBounded`. Dùng subprocess/bounded depth, không cố gây stack overflow trong test runner.
- [ ] Run fail → loại subscription mặc định, bảo vệ fallback không tự gọi logger; watcher chỉ log metadata không chứa nội dung. Review exception messages có khả năng chứa raw title/body.
- [ ] Làm mô tả Setup/README đúng policy; không xóa log người dùng sẵn có tự động.
- [ ] Run targeted tests, full Release suite.

**Done:** không còn recursive logging path; privacy setting không bị raw notification logging đi vòng.

## Task 7 — Tách notification lifecycle và UIA worker

**Files:** sửa `App.xaml.cs`, `Notifications/InAppNotificationWatcher.cs`, `ChromiumAccessibilityActivator.cs`, `Views/SetupWindow.xaml.cs/.xaml`, Core `HyperNotification.cs`, `PetAlert.cs`; tạo Core `Notifications/NotificationSource.cs`, App `Notifications/NotificationCoordinator.cs`, `Notifications/AutomationWorker.cs`, `Notifications/IPopupTextReader.cs`; tạo tests `NotificationCoordinatorTests.cs`, `InAppNotificationWatcherTests.cs`, `ChromiumAccessibilityActivatorTests.cs`.

**Interfaces:** tạo `NotificationSource { Windows, InApp }` tại Core, thêm Source vào HyperNotification/PetAlert với default tương thích constructors; task này gán đúng nguồn, Task 8 dùng để dọn queue. Coordinator `Apply(HyperPetSettings settings, NotificationAccessStatus windowsAccess, bool debugMode): void`, `Dispose(): void`. `IPopupTextReader.Read(IntPtr hwnd, CancellationToken cancellationToken): PopupText`; `PopupText(string Title, string Body, bool IsComplete)`. `AutomationWorker` là một MTA thread duy nhất, một scan tại một thời điểm; UI chỉ nhận immutable notification result. Sự kiện được add/remove trên worker sở hữu subscription. Adapter native/process/window để fake thay vì gọi Zalo trong tests.

- [ ] Tests `WindowsDenied_InAppStillStarts`, `DisabledSource_DoesNotScan`, `DebugOff_DoesNotWatchOwnProcess`, `WatchListApply_ReplacesTrackedProcesses`, `ProviderSlow_UiDispatcherRemainsResponsive`, `Dispose_UnsubscribesOnlyOwnedHandlers`, `InitialAccessStatus_IsDisplayed`.
- [ ] Run fail → tách start/stop từng source; chỉ ask Windows access khi source cần, Setup nhận trạng thái đã xin. Introduce Source metadata ngay ở task này; bước dọn current/pending khi tắt source hoàn tất trong Task 8, không để task này phụ thuộc code chưa tồn tại.
- [ ] Chuyển toàn bộ UIA operations sang worker; node budget đề xuất 256 node/32 depth mỗi popup, tối đa một scan outstanding. Cancellation ngăn kết quả trễ sau disable/exit. Không hứa CancellationToken ngắt được COM call đang hang: worker timeout phải bỏ kết quả, không chờ vô hạn trên UI/shutdown, không spawn thêm thread mỗi timeout.
- [ ] Track `(pid, process start time, hwnd)` để xử lý PID reuse; retry activation không-window có backoff, gỡ handler riêng khi stale. Không RemoveAllEventHandlers toàn process.
- [ ] Run fake-adapter tests green, rồi smoke Zalo trên máy thật có đo duration/UI responsiveness. Nếu provider hang không thể recover bằng thread isolation, ghi bằng chứng và đề xuất helper process ở task riêng; không âm thầm thêm kiến trúc process mới.

**Done:** permission và toggle của source này không chặn source kia; UIA không chạy trên dispatcher UI.

## Task 8 — Popup identity, dedupe và quiet-hours boundary

**Depends on:** Task 7 để test nguồn qua adapters.

**Files:** sửa `InAppNotificationWatcher.cs`, `src/HyperPet.Core/Notifications/NotificationDedupe.cs`, `HyperNotification.cs`, `src/HyperPet.Core/Pet/PetAlert.cs`, `NotificationInbox.cs`, `MainWindow.Notifications.cs`, `MainWindow.NotificationPlacement.cs`; tạo `Notifications/QuietHoursCoordinator.cs`; mở rộng Core notification/quiet-hours tests, App queue tests.

**Interfaces:** dùng NotificationSource và Source metadata từ Task 7. Dedupe giữ `ShouldAlert(HyperNotification notification): bool`, thêm constructor `NotificationDedupe(TimeProvider? timeProvider = null, int capacity = 4096, TimeSpan? ttl = null)`; ttl mặc định 24 giờ. Inbox thêm `RemoveSource(NotificationSource source): void`, loại current/pending source đó rồi phát Changed; MainWindow render lại current phù hợp. `QuietHoursCoordinator(HyperPetSettings settings, TimeProvider timeProvider, Action onEntered)` tính boundary theo local timezone của timeProvider và re-evaluate khi resume/timezone/time changed.

- [ ] Tests `SameHwndRect_NewText_IsDelivered`, `EmptyExtraction_IsRetried`, `ClosedThenReopened_SameText_IsNewOccurrence`, `DistinctTitleBodyPairs_DoNotShareIdentity`, `Dedupe_CapacityAndTtlAreBounded`, `DisableSource_DoesNotDiscardOtherSources`.
- [ ] Popup text cache có TTL đề xuất 1 giây, invalidation theo UIA content event nếu có; đọc lại ít nhất mỗi scan hợp lệ. Occurrence có sequence mới sau biến mất/reappear, content revision có tuple title/body tách trường, không dùng hash32 nối chuỗi làm identity duy nhất. Windows poll/event dùng chung identity/revision và TTL.
- [ ] Tests `QuietHoursBoundary_ClearsWhileHovered`, `NoIncomingNotifications_BoundaryStillClears`, `ResumeAcrossBoundary_DoesNotPromoteOldPending`, `ClockChange_ReevaluatesSchedule`, `ClearHistory_DoesNotClearPending`. Dùng fake time, không đợi tới giờ thật hoặc sleep phút.
- [ ] Implement scheduler một timer tới boundary tiếp theo, có re-evaluation khi resume/clock change; gọi UI clear trên dispatcher. Alert hover chỉ điều khiển expiry, không chặn quiet-hours suppression. Tách policy chung để Core/UI nhất quán nhưng giữ redaction cho replay/queued data.
- [ ] Run targeted tests fail→green và full Release suite. Test Windows event/poll duplicate qua fake listener, không dùng WinRT thật.

**Riêng audit 27:** stress WinRT wrapper retention/GC trên deployment được hỗ trợ, thu crash dump nếu gặp. Chưa thay retention workaround trước khi có bằng chứng; ghi issue/reference và sửa comment ngay. Nếu không tái hiện được, đóng ở trạng thái “investigated, behavior change deferred”, không coi crash đã được sửa.

## Task 9 — Catalog và active-pet state thống nhất

**Files:** sửa `App.xaml.cs`, `MainWindow.xaml.cs`, `Views/SettingsWindow.xaml.cs`, `Pets/PetCatalog.cs`; tạo `Pets/PetSession.cs`, `Pets/PetReloadResult.cs`; mở rộng PetCatalog/Overrides tests, tạo `Views/PetSwitchWindowTests.cs`.

**Interfaces:** `PetSession` là owner chung của catalog hiện tại, active SpritePet và original overrides; `ReloadAsync(string petId, CancellationToken cancellationToken): Task<PetReloadResult>` với `PetReloadResult(bool Success, bool Superseded, string? Message, SpritePet? Pet)`. Catalog refresh khi mở Settings/import/remove; không snapshot readonly suốt app. Latest-request-wins bằng generation check, kể cả decoder cũ không ngắt được ngay.

- [ ] Tests `Import_CloseReopenSettings_PetRemainsVisible`, `Remove_CloseReopen_DoesNotResurrectEntry`, `SwitchB_ThenEditFps_UpdatesB`, `SlowB_FastC_CRemainsActive`, `FailedLoad_DoesNotPersistNewSelection`, `SwitchDuringAlert_PlaysNewPetWaving`, `CloseDuringReload_DoesNotTouchClosedWindow`.
- [ ] Run fail → await swap trước commit SelectedPet. Dialog refresh State rows và defaults sau success; busy state chặn thao tác xung đột. Failure có thông báo và selection revert. Tách generic settings persistence khỏi commit selection để không ghi ID chưa load.
- [ ] Giữ một owner cho animation state; reset summon/hover/controller cần thiết, không reset queue/history. Pet mới phải Play đúng state dù đang alert.
- [ ] Run targeted tests green và full Release suite; test import/remove khi dialog còn mở và sau restart bằng sandbox settings.

**Done:** dropdown, pet đang hiển thị, State tab và persisted ID luôn cùng một pet sau task hoàn tất.

## Task 10 — Sprite geometry, DPI awareness và cửa sổ Settings

**Files:** sửa `MainWindow.xaml/.xaml.cs`, `MainWindow.Monitors.cs`, `MainWindow.NotificationPlacement.cs`, `Pets/Roaming/WindowLedgeProvider.cs`, `Views/DesktopMonitors.cs`, `MonitorWorkArea.cs`, `SettingsPlacement.cs`, `SettingsWindow.xaml`; tạo `src/HyperPet.App/app.manifest`, `Pets/PetGeometry.cs`; sửa csproj; mở rộng placement/ledge tests.

**Interfaces:** `PetGeometry.GetSpriteBounds(Image image, Window owner): Rect` trả DIP trong owner; `PetGeometry.WindowOriginForFoot(Point footPosition, Rect spriteBounds): Point` quy đổi foot anchor về window origin. Giữ fixed window tạm nếu cần hit area, nhưng collision/ledge dùng sprite bounds và offset, không window height/width. Native monitor adapter tiếp tục trả physical pixels; conversion chỉ tại boundary, không chia origin bằng scale của monitor khác tùy tiện.

- [ ] Tests `SpriteFoot_OnLedge_Size1_Size8_Size10`, `SpriteBounce_UsesVisibleBounds`, `SettingsFooter_VisibleInSmallWorkArea`, `NegativeOrigin_MixedScale_PointRoundTrips`, `DpiTransition_DoesNotOscillatePlacement`. Assert tọa độ bằng DIP trong pure tests, physical pixel ở HWND integration tests; không trộn đơn vị.
- [ ] Run fail → khai báo `PerMonitorV2` với fallback phù hợp Windows 10 build 19041+ trong executable manifest; kiểm manifest nhúng sau build. Chuẩn hóa geometry và hạn chế duplicate Left/Top assignments sau khi đã tính clamp.
- [ ] Settings cho resize, MaxHeight theo work area, giữ footer visible và scroll nội dung; hợp nhất MonitorWorkArea/DesktopMonitors bằng cách đổi call sites trước khi xóa helper.
- [ ] Run tests green; manual matrix: một monitor 100/125/150/200%, hai monitor 100↔150/200%, monitor trái/trên origin âm, unplug/replug, taskbar/work-area change. Record actual HWND awareness context. Nếu thiếu phần cứng, giữ matrix chưa xác minh; không tuyên bố xong mixed-DPI chỉ từ synthetic test.

**Done:** chân sprite chạm ledge trong bounds đúng, Settings thao tác được trên work area thấp; DPI claim có binary và runtime evidence.

## Task 11 — Playback và settings defaults/validation

**Files:** sửa `Pets/PlaybackStep.cs`, `PetAnimator.cs`, `Views/SettingsWindow.xaml.cs`, `SettingsWindowSettingsApplier.cs`; dùng `HyperPetSettings.CreateDefault()`; mở rộng PlaybackStep/SettingsWindow tests, tạo `Pets/PetAnimatorTests.cs`.

**Interfaces:** giữ `PlaybackStep.Next(PlayMode mode, int index, int direction, int frameCount): PlaybackResult`; chốt Completed là kết thúc cả pass, PingPong chỉ complete sau lượt về. `PetAnimator.Completed` vẫn `Action<string>`, phát một lần cho one-frame/non-loop; timer debug pause không tự phát completion. Settings validation trả lỗi field thay vì silently parse fallback rồi đánh dấu clean.

- [ ] Tests `PingPong_NonLoop_VisitsZeroLastZero`, `SingleFrame_NonLoop_CompletesOnce`, `ReverseHover_StopsAtCycleBoundary`, `DefaultControls_MatchModel`, `InvalidQuietHours_ApplyStaysDirty`, `ClampedNumber_ReflectsAppliedValue`. Test animator phải qua dispatcher/tick thật hoặc clock adapter, không chỉ pure math.
- [ ] Run fail → sửa completion semantics và duration/cycle calculations dùng mode; không dùng frameCount/fps cho mọi mode. Loại constants defaults lặp; defaults in-app=4. Hiển thị validation tại field, không persist khi còn lỗi; chuẩn hóa lại text khi clamp hợp lệ.
- [ ] Run targeted tests green, full Release suite; test Tuck Away timeout và hover/alert regression vẫn pass.

## Task 12 — Runtime support, release provenance, scripts và CI

**Files:** sửa `release.ps1`, `pack.ps1`, `build.bat`, `Run HyperPet.bat`, cả 5 csproj, README; tạo `.github/workflows/ci.yml`, `.config/dotnet-tools.json`, `global.json`, `tests/ReleaseScripts.Tests.ps1`.

**Interfaces:** tách `release.ps1 -Version X.Y.Z -BuildOnly` và publish step nhận build manifest chứa `Version`, `CommitSha`, `Artifacts` (name/hash/size). Giữ `-DryRun` như alias được mô tả rõ cho BuildOnly nếu còn cần compatibility; không tự sửa csproj rồi release từ dirty tree. Một nguồn Version trong project; Assembly/File/InformationalVersion suy ra khi có thể. Máy lập plan có SDK 8.0.423 và 10.0.302; dùng số đã đo làm baseline, nhưng trước release chọn/pin bản .NET 10 SDK/runtime servicing còn support từ nguồn Microsoft, rồi verify lại checkout. Pin vpk=1.0.1 cùng package hiện tại; không chọn số SDK mới bằng đoán.

- [ ] Tests PowerShell trong temp git repo, fake dotnet/vpk/gh: `DirtyTree_RejectsBeforePublish`, `WrongBranch_DoesNotPushMain`, `FailedTag_StopsRelease`, `BuildOnly_HasNoRemoteSideEffects`, `Checksum_CoversExactlyUploadedAssets`, `ExistingTag_MismatchRejected`, `ScriptRunsFromOtherWorkingDirectory`, `BuildFailure_DoesNotLaunchOldExe`.
- [ ] Run `pwsh -NoProfile -File tests/ReleaseScripts.Tests.ps1` fail trước sửa; Dùng assertions PowerShell native, không thêm Pester dependency. Không gọi remote GitHub từ test runner.
- [ ] Chuyển runtime trong commit riêng: Core/tests thuần sang net10.0, App/Windows/WPF tests sang net10.0-windows10.0.19041.0 nếu supported OS matrix đã xác nhận. Chỉ nâng dependency khi cần cho compatibility. Run full tests, publish win-x64 self-contained, kiểm runtimeconfig/deps và smoke WinRT/Skia/updater trên Windows. Nếu minimum Windows hiện tại không còn được .NET 10 support, đưa tradeoff cho người dùng trước khi đổi minimum OS; không ghi nhận cài SDK mới là đã sửa EOL runtime. Các command/path cố định net8 trong launcher phải đổi theo output mới.
- [ ] Release build từ commit sạch đã chứa version; ghi CommitSha vào artifact manifest và assembly metadata. Pack trong output mới/rỗng, không tái dùng file cũ. Test/audit gates trước pack/publish; checksum sau pack; validate checksum/asset list trước gh upload. Tránh tự commit/tag/push main trong đường BuildOnly.
- [ ] Windows CI restore/build/Release tests/audit và upload verification artifact; CI này không có quyền tự publish release. Bảo đảm audit warnings được kiểm tra, không chỉ dựa exit code `dotnet list`.
- [ ] Targeted script tests green; full Release suite; local BuildOnly tạo đúng artifacts/license/native Skia/pets, không bump production release hoặc push.

**Done:** có thể truy ngược artifact về đúng commit, không upload output cũ/checksum cũ; lệnh fail không launch hoặc push tiếp.

## Task 13 — Kiểm chứng deployment/notification access và installer lifecycle

**Depends on:** Tasks 5, 7, 10, 12.

**Files:** `src/HyperPet.App/Package.appxmanifest`, csproj, README, packaging scripts được chọn; tạo `docs/reviews/2026-11-12-deployment-validation.md`. Không thêm packaging project trước khi lựa chọn được kiểm chứng.

**Interfaces:** trạng thái source phân biệt `Allowed`, `Denied`, `UnsupportedDeployment`, `Error`; UI không coi lỗi deployment là người dùng chưa bấm Request. Nếu enum WindowsNotificationAccessStatus hiện tại không đủ thì mở rộng adapter result, giữ pure permission status riêng.

- [ ] Trên Windows test profile/VM, đo identity và permission của dev, portable, Velopack installed hiện tại; dùng adapter/runtime evidence, không suy từ chỉ việc có file Package.appxmanifest. Test revoke/regrant và restart.
- [ ] Thử hai phương án có thật: giữ Velopack + external-location identity package, hoặc MSIX deployment đầy đủ. Ghi compatibility với update path, capability support, install/uninstall/rollback và minimum OS. Chỉ dùng tài liệu primary đang hiệu lực khi thực thi.
- [ ] **Checkpoint quyết định:** nếu external identity đáp ứng listener và update/uninstall end-to-end, đề xuất giữ Velopack để bảo toàn updater. Nếu phải đổi distribution/updater, đưa kết quả và tác động cho người dùng chọn trước khi triển khai chuyển đổi. Đây là quyết định sản phẩm sau thử nghiệm, không chặn các hotfix trước đó.
- [ ] Sửa executable trong manifest thành HyperPet.exe; đồng bộ version từ nguồn build sau khi pipeline thực sự dùng manifest. Không ship manifest sai như lời hứa permission.
- [ ] Manual acceptance: clean install → consent → notification → update → startup login → uninstall; denied Windows vẫn có in-app source; uninstall gỡ autorun và identity artifacts thuộc app, không đụng package/data khác. Test giữ nguyên/khôi phục dữ liệu theo policy đã thống nhất, không dùng dữ liệu thật.

**Done:** có distribution được hỗ trợ với bằng chứng permission/lifecycle. Nếu môi trường thiếu VM/phần cứng hoặc cần quyết định updater, đánh dấu blocked đúng hạng mục, không claim toàn bộ audit resolved.

## Task 14 — Cleanup code, docs, skills và dung lượng theo phạm vi

**Depends on:** runtime tasks liên quan đã ổn, đặc biệt 9 và 11 trước khi bỏ state cũ.

**Files:** call sites của các API dead đã liệt kê; `Properties/Settings.*`, assets fallback không dùng, docs/superpowers index/archive, README, AGENTS và audit. Global skills/CLAUDE/AGENTS không tự sửa trong task repo.

- [ ] Dùng call-site search sau refactor xác nhận từng candidate: appLauncher/IAppLauncher/OpenAppOnBubbleClick/TryActivateAsync/CanActivate; redundant ViewModel/controller state; generated Settings rỗng; fallback PNG; unused debug method/state. Bỏ từng cụm sau migration, không xóa type đang có C# caller hoặc public compatibility còn cần. Generic debug toast và tray icon vẫn cần dependency phù hợp.
- [ ] Run full Release suite sau cleanup code, build/publish inventory để xác nhận assets/license/native DLL không bị mất. Comment/docs/ignore đơn giản không cần tests riêng.
- [ ] Thêm docs index “current vs historical”, status/superseded banner cho plan cũ; không sửa lịch sử thành giả như lỗi cũ chưa từng tồn tại. Audit đổi mỗi mục sang `fixed / verified / deferred / needs-user-decision`, kèm test/commit evidence thay vì chỉ tick toàn bộ.
- [ ] Lập đề xuất skill riêng: gộp duplicate metadata, sửa hướng dẫn fork/model/wait theo harness, giảm auto-load; không patch cache plugin vì sẽ bị update ghi đè. Repo AGENTS trỏ workflow ngắn, không copy toàn skill/catalog vào file.
- [ ] Tạo cleanup inventory: folder/ref/worktree, size, reason, retention và exact proposed action. Đặc biệt phân biệt local output, reachable checkpoints, published updater assets. Không xóa `.git/objects`, refs/codex, releases hoặc rewrite history trong bước sửa code. GC/packing an toàn vẫn cần kiểm tra worktree/process trạng thái; history rewrite cần kế hoạch backup/coordination riêng.
- [ ] Tạo `docs/asset-provenance.md`: nguồn/tác giả/quyền đã biết của pet/icon. Không tự gán license cho Miku/third-party asset hoặc thêm LICENSE thay chủ sở hữu quyết định. Phần thiếu thông tin ghi pending, không kết luận vi phạm.

**Done:** code chết có bằng chứng được bỏ; docs hiện hành tìm được ngay; thay đổi global/license/destructive được tách rõ để người dùng quyết định trên inventory cụ thể.

## Coverage audit → task

| Audit IDs | Owner task |
|---|---|
| 01, 02, 04 | 2 |
| 05, 06, 42 | 3 |
| 14, 15, 39 | 4 |
| 16, 40, 43 | 5 |
| 12, 24 | 6 |
| 07, 09, 10, 26, 44 | 7 |
| 08, 11, 13, 27 | 8; 27 investigation gate riêng |
| 17, 18, 19, 20 | 9 |
| 21, 31, 41 | 10 |
| 22, 29, 30 | 11 |
| 03, 25, 28, 35, 45 | 12 |
| 23 | 13 |
| 32, 33, 34, 36, 37, 38 | 1 và 14 |

## Kiểm chứng cuối mỗi đợt và bàn giao

```powershell
dotnet test HyperPet.sln --configuration Release
dotnet list HyperPet.sln package --vulnerable --include-transitive --format json
```

Chỉ Task 12 mới cần script test suite và pack. Sau thay đổi dependency/packaging, restore/publish mới rồi kiểm native Skia, pet assets và licenses; không lặp publish sau sửa comment. Số tests sẽ tăng, không khóa cứng phải bằng 198; không được có skip không giải thích.

- [ ] Mỗi task có diff giới hạn, regression evidence, risk/rollback note; review sau từng deliverable có tác động dữ liệu/lifecycle.
- [ ] Cập nhật mapping 45 mục (44 ban đầu + runtime EOL), bao gồm các mục investigated/deferred và phần cần quyết định; không gộp “chưa kiểm chứng” vào “fixed”.
- [ ] Bàn giao kết quả local có thể review. Publish, thay updater, dọn output thật, rewrite Git history hoặc chỉnh global plugin là bước riêng với phạm vi cụ thể, không phải side effect của chạy plan.

**Phương thức đề xuất:** triển khai trực tiếp theo Task 1→14 trong cùng chat, chỉ đọc subsystem/docs cần cho task đang làm. Không mặc định spawn 14 implementer/reviewer hoặc sao chép toàn bộ audit vào mỗi context.

**Trạng thái:** plan đã soạn; chưa triển khai, chưa được người dùng xác nhận các policy đề xuất. Không thay production code trong lượt lập kế hoạch.
