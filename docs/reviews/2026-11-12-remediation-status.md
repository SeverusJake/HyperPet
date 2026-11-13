# Remediation status — 2026-11-12

> Follow-up execution on 2026-11-13 is tracked in [follow-up status](2026-11-13-follow-up-status.md). Evidence below is the previous round's snapshot. The owner will perform deployment checks manually; Miku provenance investigation is excluded from the follow-up request.

Pre-integration snapshot: branch `codex/audit-remediation`; source was local and uncommitted and no push/tag/publication had been performed at verification time. This status supersedes conclusions about HEAD in the historical audit, without rewriting that snapshot. Subsequent authorized Git cleanup/integration is recorded in [maintenance evidence](2026-11-13-git-maintenance.md).

## Verification evidence

- Baseline: 198 tests on .NET 8. Current full Release run: **234 passed, 0 failed, 0 skipped** — 69 Core, 165 App on .NET 10.
- Build: **0 warnings, 0 errors**. The hybrid WPF/WinForms DPI analyzer warning is suppressed intentionally: WPF owns HWND startup and uses the executable manifest.
- PowerShell: **3 regression groups pass** — clean/dirty commit guard, checksum/manifest contents, native exit-code failure. Tests use native assertions, no Pester dependency.
- NuGet audit including transitive dependencies lists no advisory. This is separate from runtime lifecycle/servicing validation.
- Self-contained x64 publish succeeds. Included Core/Desktop runtimes are **10.0.15**, verified in runtimeconfig. Unit tests run on the installed servicing runtime, **10.0.10**; this does not substitute for installer smoke tests of the packaged app.
- Velopack 1.0.1 produces Setup/Portable locally and verifies `VelopackApp.Run()` in `App::Main`. CLI notices about a newer version and unsigned artifacts remain informational; signing was not configured or claimed.
- Final source was published to `publish/audit-verification-final` and packaged successfully in `publish/audit-verification-final-release`; both embedded frameworks were rechecked at 10.0.15. These are local verification artifacts, not a published release.
- Independent read-only review found cancellation/late reload, unbounded metadata parsing, and Uniform-stretch geometry issues. All three were addressed with regression coverage. A cancellation test initially lacked Loaded/Show setup; the test now exercises an actual visible pet and passes.
- `git diff --check` passes. Git warns about LF→CRLF conversion under the existing checkout policy; no whitespace errors were reported.

## Audit coverage

“Implemented” means code changed with the local evidence described here, not proof of every Windows environment. “Deferred” names work that needs evidence, input, or an explicitly separate destructive/global operation.

| Audit | Status | Change / remaining limit |
|---|---|---|
| 01–02 | Implemented, regression tested | Unsafe IDs/reparse ancestors/overlap rejected; stage before backup/swap; injected swap and rollback failures preserve original or report recoverable backup |
| 03 | Implemented locally | Clean committed source/version required; no implicit commit/tag/push; publication explicitly requested and existing local/remote tag checked. No real GitHub publication tested |
| 04 | Implemented, STA test | Confirmation keeps caller dispatcher; I/O/decoding occurs separately |
| 05–06 | Implemented with limits | Real image/crop validation, metadata 1 MiB before parse, import byte/count/depth/image/frame/FPS budgets; invalid image, external path, depth and protected-ID tests. Not stress-tested with malicious native decoders |
| 07 | Implemented; native integration pending | In-app watcher starts before Windows permission; source-off avoids request; enable later can request access. Windows consent/deployment path still requires VM validation |
| 08 | Implemented, tracker tests | Read popup text every scan; empty extraction retried; HWND+rect no longer freezes cached content |
| 09 | Implemented; real provider stress pending | One MTA worker, serialized scans, node/depth budget. A hanging COM provider can stall this worker; UI and shutdown do not wait for it. No unbounded replacement-thread spawning |
| 10 | Implemented | Toggles stop/restart sources; watch list refreshes; own process debug-only; cancelled scan results discarded |
| 11 | Implemented, expiry/capacity tests | Bounded 4,096-entry/24h dedupe; popup tuple content and unique occurrence IDs; Windows content revision contributes to identity |
| 12 | Implemented | Popup log metadata excludes title/body; Setup/README describes privacy accurately. Existing user logs are not erased |
| 13 | Implemented, WPF test | Quiet-hours re-evaluates every second, clears hovered/current/pending without another notification; boundary precision bounded to one second |
| 14–15 | Implemented, tests | Temp+flush+atomic replace+backup; shared writer gate; deep dictionaries/enums/finite-coordinate sanitation |
| 16 | Implemented, native local test | Per-user/profile mutex and current-user-only pipe; second owner rejected and activates first. No production registry used in tests |
| 17–20 | Implemented; additional UI scenarios pending | Catalog refresh when opening dialog; State rows/defaults follow successful swap; latest generation wins; dialog cancellation/close invalidates pending swap; SelectedPet persists after success; active alerts play new pet |
| 21 | Implemented, geometry tests | Visible sprite bounds include Uniform letterboxing/aspect ratio; roam/bounce/clamp use sprite offset and dimensions |
| 22 | Implemented, playback/WPF tests | PingPong completes after return; one-frame non-loop completes once; hover/jump/exit durations account for play mode |
| 23 | Partially implemented, needs deployment decision | Template executable/version corrected and documented; no false claim that Velopack grants identity. See deployment validation document |
| 24 | Implemented, fault-path test | Removed global FirstChance logging; inaccessible file logger does not escape/recurse through the app's handler |
| 25 | Implemented locally | Test/audit/clean-source gates, checksums and build manifest; CI configured. Real GitHub Actions run/upload not performed |
| 26 | Implemented; native cleanup stress pending | Track PID/start time/HWND; remove only owned handlers; prune stale subscriptions and back off no-window Electron helpers |
| 27 | Investigated, behavior deferred | Legacy retention cap preserved; misleading “retain forever/collection” comments corrected. Need deployment/GC crash evidence before changing workaround |
| 28 | Implemented | Build fails before launch; pack resolves paths and refuses mixed old output/version mismatch; DryRun aliases BuildOnly and never mutates source |
| 29–30 | Implemented, UI/default test | Defaults from model, polling 4s; invalid fields stay uncommitted with field feedback instead of silently clearing dirty |
| 31 | Implemented; physical display validation pending | Resizable Settings, max dimensions from work area, footer retained |
| 32 | Partial cleanup by verified call sites | Unused launcher/activation stubs/StartupService.IsEnabled, empty generated Settings and fallback PNGs removed; compatibility setting/public controller state retained rather than breaking existing data/tests |
| 33 | Implemented | Current docs index and historical/superseded banners; old plans not re-executed wholesale |
| 34 | Prevention implemented; reclamation deferred | publish/Releases ignored; cleanup/ref inventory prepared; no pre-existing output, worktree, Git refs/objects/history deleted |
| 35 | Implemented locally | global.json, local vpk manifest, Windows CI build/test/audit/publish, exact runtime servicing pin for self-contained artifacts |
| 36–37 | Repo implemented, global proposal only | Concise repo AGENTS; documented exact global skill conflicts/duplication; no cached plugin or personal instructions silently edited |
| 38 | Needs owner evidence | Provenance inventory created; no invented asset rights/project license |
| 39 | Implemented, locked-file test | Read-failure session cannot persist defaults or apply startup; exit honors recovery policy |
| 40 | Implemented, policy tests | Debug profile separate; portable/dev cannot manage installed autorun; UI disabled for unsupported startup management and preserves stored preference |
| 41 | Implemented binary configuration; physical matrix pending | Executable embeds PerMonitorV2; do not equate synthetic tests with mixed-DPI device proof |
| 42 | Implemented | Built-in ID collision rejected explicitly instead of silently hiding imported pet |
| 43 | Implemented, packager verified | Velopack bootstrap precedes creating WPF App in custom Main |
| 44 | Implemented | Setup receives the prior access result, does not claim consent was never requested |
| 45 | Implemented runtime migration | All 5 projects target net10; self-contained Core/Desktop and drawing package servicing 10.0.15; no automatic minimum Windows API bump |

## Plan adjustments and remaining work

- Kept existing App/MainWindow ownership with focused helpers instead of introducing every service class named in the proposal; this reduces API churn. The old broad native pipelines still need more adapter-based integration coverage.
- Used native PowerShell regression assertions instead of adding a legacy Pester dependency to CI.
- Task 13 requires an isolated Windows environment and a distribution choice backed by install/update/uninstall tests. Physical mixed-DPI and WinRT GC/retention stress remain unverified.
- Exact global skill edits, disk reclamation/history changes and asset/project licensing remain separate owner decisions. The cleanup proposal identifies targets and consequences; nothing is silently removed from user data.
- Local verification builds retain version 0.7.1 and a dirty working-tree source state. They must not be uploaded as a new release. Commit/version/tag preparation and publishing are separate next steps.

References: [deployment validation](2026-11-12-deployment-validation.md), [cleanup/global skill proposal](2026-11-12-cleanup-proposal.md), [provenance](../asset-provenance.md).
