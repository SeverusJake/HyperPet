# HyperPet Follow-up Remediation Implementation Plan

> **Execution record:** Local implementation, independent review fixes and verification are recorded in [follow-up status](../../reviews/2026-11-13-follow-up-status.md). Full native App startup smoke was narrowed to factory/lifecycle tests and inspected wiring; this deviation and manual limits are explicit in that report. Deployment remains the owner's manual follow-up. The steps below preserve the original task brief; use the execution report for actual results.

> **For agentic workers:** Use `superpowers:executing-plans` to implement task-by-task in the current chat. Checkboxes record actual evidence. This plan does not authorize publishing, global plugin edits or destructive cleanup.

**Goal:** Close remaining local startup/settings/pet-lifecycle failure paths, then supply focused integration evidence for manual Windows validation.

**Architecture:** Keep WPF, existing profile boundaries and native adapters. Introduce test seams only at native creation, catalog I/O and operations that must be prepared before committing UI state. Keep the same shared settings object because existing windows/controllers reference it.

**Tech Stack:** Existing .NET 10, WPF, xUnit/STA, PowerShell, Velopack. No framework or dependency migration in this round.

**Spec / evidence:** [Current remediation status](../../reviews/2026-11-12-remediation-status.md), [original audit](../../reviews/2026-11-12-project-audit.md), [deployment checklist](../../reviews/2026-11-12-deployment-validation.md). The code observations below were inspected on 2026-11-13; new failure paths must be reproduced by tests before fixes. Prior evidence is 234 C# tests and 3 PowerShell regression groups, not a fresh run today.

## Scope and decisions

- User will manually test install/update/uninstall and package identity later. Do not block local fixes on a VM, or mark native deployment proven.
- Miku copyright/provenance investigation is excluded by explicit user request. Keep existing asset credits; no asset replacement or claim about rights is needed for this work.
- Prefer focused local fixes over a MainWindow rewrite. Tasks 1–2 establish safe entry points; Task 3 uses them for settings transactions; Task 4 handles deletion separately; Task 5 covers notification lifecycle.
- Disk reclamation, Git history/ref cleanup and global skill/plugin modifications stay outside execution scope. Existing proposals remain available; no extra catalog/skill-loading framework is planned.
- No new MSIX distribution, updater replacement, process-isolated UIA worker or change to the WinRT retention workaround without native evidence.

## Global constraints

- Preserve original pet/settings when preparation fails or a dialog closes. Never persist unrelated half-applied edits on exit.
- Use temporary profiles and fake native adapters. Do not test real autorun, notification consent, uninstall or production settings.
- Preserve built-in pet protection, import quotas, path/reparse checks and privacy rules from the previous round.
- No title/body logging; diagnostics contain operation/status/generation/duration only.
- Keep current working-tree changes. Inspect diff before starting; do not reset or auto-commit the previous remediation.
- Source changes need targeted red/green tests; docs-only changes need link/diff checks. Commit only when requested; no push/tag/publication.

## Review focus

1. Native listener construction throws before the main window exists → in-app monitoring and pet remain usable (Task 1).
2. Double Settings click during catalog await, followed by owner close → at most one dialog, no late ShowDialog (Task 2).
3. Change general settings and select an invalid pet, then exit → original settings, startup and visible pet retained (Task 3).
4. Delete active pet with unavailable fallback or locked source → no success claim, no destructive deletion before fallback preparation (Task 4).
5. Disable/re-enable during a pending permission request or queued callback → no stale delivery or duplicate subscription (Task 5).

## Task 1 — Make optional native startup failures recoverable

**Observed:** `App.OnStartup` constructs `WindowsNotificationListener` before showing the pet or starting in-app monitoring. Its constructor accesses `UserNotificationListener.Current` outside the existing request-access catch. Creation/enumeration of the user pet root is also outside a startup recovery boundary.

**Files:** Modify `src/HyperPet.App/App.xaml.cs`; create `src/HyperPet.App/Notifications/NotificationListenerFactory.cs`; test `tests/HyperPet.App.Tests/Notifications/NotificationStartupTests.cs`.

**Interfaces:** Internal `NotificationListenerFactory.TryCreate(Func<INotificationListener> create, out INotificationListener? listener, out Exception? error) -> bool`. App creates the Windows adapter lazily when the source is enabled. Only retain a successfully created instance; a later enable can retry. App, not Core, owns user-facing status.

- [ ] Add `ListenerCreationFailure_DoesNotBlockPetOrInAppStartup`: fake constructor throws; startup coordinator proceeds with main/in-app creation, no Windows subscription, diagnostic once per attempt.
- [ ] Add `WindowsDisabled_DoesNotConstructListener` and `EnableAfterCreationFailure_CanRetry`; assert constructor counts 0 then 1 per explicit attempt, no rapid background retry loop.
- [ ] Run `dotnet test tests/HyperPet.App.Tests -c Release --filter FullyQualifiedName~NotificationStartupTests`; capture the intended failure.
- [ ] Move optional listener creation behind the factory and the enabled-source gate. Handle expected user-root I/O/access failures by retaining built-in pets and showing a nonfatal message; never redirect writes into the built-in root.
- [ ] Repeat targeted tests; assert shutdown before creation completion cannot start monitoring. Add an STA startup smoke test through an internal coordinator seam rather than calling native APIs.

## Task 2 — Prevent duplicate Settings windows and tolerate catalog I/O failures

**Observed:** `MainWindow.OpenSettings` awaits discovery before setting any opening guard. `PetCatalog.DiscoverAsync` catches per-pet parse failures but not root enumeration failures. A slow first click permits multiple dialogs; one inaccessible root can discard healthy results from another.

**Files:** Modify `src/HyperPet.App/MainWindow.xaml.cs`, `src/HyperPet.App/Pets/PetCatalog.cs`; extend `tests/HyperPet.App.Tests/Views/RemediationWindowTests.cs`, `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs`.

**Interfaces:** Preserve public `DiscoverAsync(params string[] roots)` return type and existing call sites. Add an internal overload accepting `Func<string, IEnumerable<string>> enumerateDirectories` for deterministic I/O failures. MainWindow owns `_openingSettings`, `_settingsWindow`, and its existing closed/tuck-away state.

- [ ] Add `RepeatedOpenWhileDiscovering_ShowsOneDialog` with a controlled discovery Task; assert one discovery and one dialog. Add `OwnerClosesDuringDiscovery_DoesNotShowDialog`.
- [ ] Add `UnreadableUserRoot_StillReturnsBuiltins` and `EnumerationFailsMidRoot_KeepsEarlierEntries`; inject IOException/UnauthorizedAccessException, preserve built-in-first ID precedence and sort order. Unexpected programmer exceptions must remain visible.
- [ ] Run the new filters and confirm failures on the current implementation.
- [ ] Set opening guard before awaiting, activate the existing Settings window on repeat, recheck owner state after await and release guard in finally. Catch expected root enumeration failures independently, preserve healthy entries. Do not swallow every exception around the whole operation.
- [ ] Run catalog and WPF targeted tests. Verify cancel/error leaves the next Settings click usable.

## Task 3 — Prepare Settings changes before committing shared state

**Observed:** `SettingsWindow.CommitChanges` invokes `SettingsWindowSettingsApplier.TryApply`, changes shared dictionaries and the live pet definition, then awaits a potentially failing pet reload. A failed Apply can therefore leave general edits/startup changed even though the UI reports failure; exit can persist those edits.

**Files:** Modify `src/HyperPet.App/Views/SettingsWindow.xaml.cs`, `SettingsWindowSettingsApplier.cs`, `src/HyperPet.App/MainWindow.xaml.cs`; create `src/HyperPet.App/Pets/PreparedPetReload.cs`; extend `tests/HyperPet.App.Tests/Views/SettingsWindowSettingsApplierTests.cs`, `RemediationWindowTests.cs`.

**Interfaces:** `PreparedPetReload` is an internal immutable record containing the loaded `SpritePet`, original FPS/modes and generation. MainWindow exposes internal `PreparePetReloadAsync(string petId, CancellationToken token) -> Task<PreparedPetReload?>` and synchronous `CanCommitPreparedPet(PreparedPetReload prepared, CancellationToken token) -> bool` / `CommitPreparedPet(PreparedPetReload prepared) -> void`. Keep public `ReloadPetAsync` as a wrapper for existing callers. Settings builds a deep candidate copy, never mutating the shared object during preparation.

- [ ] Add `FailedPetPreparation_PreservesAllSettingsAndLiveOverrides`: change pet/size/source toggles/FPS, fail loading, assert deep-equivalent settings, old frame/FPS, zero persist/startup callbacks. Add cancellation/close variant.
- [ ] Add `StartupChangeFailure_DoesNotSwapPreparedPet`; fake startup callback throws, assert old pet/settings and candidate remains editable.
- [ ] Add `SuccessfulApply_CommitsOnce`: candidate and selected pet match live state, callbacks once, correct pet-specific rows/defaults. Add close/reload supersession and rapid Apply/Save cases.
- [ ] Run targeted tests and record failures before implementation.
- [ ] Parse/validate and deep-copy all candidate values first. Prepare the selected pet without changing the current animator/settings. After await, check dialog lifetime/generation. In one dispatcher turn: check readiness, apply requested startup change, copy candidate into the existing shared settings object, commit prepared pet/overrides, refresh and persist once. No await or nested message loop in the commit section. Keep any error message outside the mutation section.
- [ ] Keep expected I/O/decoding failures in preparation. If synchronous commit still has potentially throwing operations, move them into preparation or add explicit rollback for the shared object/live pet/startup; do not describe the flow as a disk/registry transaction.
- [ ] Run all settings/reload WPF tests and verify exit cannot save edits from a rejected Apply. Existing recovery-mode no-persist/no-startup policy must remain effective.

## Task 4 — Make active-pet removal recoverable

**Observed:** `OnRemovePetClick` ignores `TryRemove`'s return and deletes the directory before preparing fallback. Deletion exceptions escape the async handler. An unsuccessful fallback leaves the selected ID pointing at removed files; no-fallback case has no explicit policy.

**Files:** Modify `src/HyperPet.App/Views/SettingsWindow.xaml.cs`, `src/HyperPet.App/Pets/PetRemover.cs`; create `src/HyperPet.App/Pets/PetRemovalService.cs`; test `tests/HyperPet.App.Tests/Pets/PetRemovalServiceTests.cs` and WPF removal tests.

**Interfaces:** `PetRemovalResult(bool Success, string? Error, string? RecoveryDirectory)`. Internal `PetRemovalService.RemoveAsync(string userRoot, PetCatalogEntry entry, string activeId, IReadOnlyList<PetCatalogEntry> catalog, Func<string, CancellationToken, Task<PreparedPetReload?>> prepareFallback, Func<PreparedPetReload, bool> commitFallback, CancellationToken token) -> Task<PetRemovalResult>`. The commit callback updates live pet and shared selected ID synchronously and returns false if superseded; persistence occurs once after logical success. Inject directory rename/delete operations through the service constructor for fault tests. Reuse Task 3 preparation; keep built-ins nonremovable.

- [ ] Test locked directory, failed rename, failed fallback, no remaining valid fallback, close during preparation and partial recursive-delete failure. Assert markers survive in source/recovery path, selection remains valid and no false success/persist callback.
- [ ] Test inactive deletion succeeds without reload; successful active deletion chooses a valid fallback and persists once. Ignore the deleted ID when choosing fallback; try the next candidate if decoding fails.
- [ ] Run targeted tests red. Block concurrent import/remove/Apply while the operation is active; restore controls in finally.
- [ ] Prepare fallback first. For active pet with no valid fallback, refuse removal with a clear message. Rename target to a unique sibling quarantine outside the discovery root after path/reparse checks. Commit fallback/selection only when preparation remains current. Roll back the rename on pre-commit failure.
- [ ] Delete quarantine after logical removal. Cleanup failure returns its exact recovery path as a warning; do not pretend recursive deletion is atomic or destroy the backup attempting rollback after partial deletion. Expected I/O/access errors stay local to the dialog.
- [ ] Run service and WPF tests; confirm unrelated pet directories, source markers and built-ins are untouched.

## Task 5 — Test notification lifecycle through fake native adapters

**Evidence gap:** Previous tests cover workers/occurrence tracking, not App-level permission, source toggles and pending Windows callbacks. The retention workaround and real provider behavior remain unproven.

**Files:** Modify `src/HyperPet.App/App.xaml.cs`, `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs` only where tests expose a defect; create `tests/HyperPet.App.Tests/Notifications/MonitoringLifecycleTests.cs`. If App wiring cannot be exercised directly, extract only its source-enable state into `src/HyperPet.App/Notifications/MonitoringLifecycle.cs`.

**Interfaces:** Consume existing `INotificationListener`; fake access completion, NotificationAdded, active-poll completion and subscription counters. UI dispatch is controlled by the STA test dispatcher. Do not add a second dedupe/history store.

- [ ] Add disabled-at-launch, denied-access-with-in-app-running, pending-access-disable, queued-event-disable, late-poll-disable, re-enable-once and shutdown-pending tests. Assert no stale alert, exactly one active subscription/timer, zero requests when disabled.
- [ ] Add worker-blocked shutdown test using an event-controlled fake action: UI close returns before release; disposal does not spawn replacement workers. Release the fake in test finally to avoid a hanging harness.
- [ ] Run tests before changing lifecycle logic. If a case already passes, record it as coverage, not a newly fixed bug. Fix only demonstrated failures; use an enable generation if callbacks can survive disable→enable and be mistaken for current delivery.
- [ ] Verify metadata-only diagnostics; no notification content added to logs. Keep retained WinRT wrappers behavior unchanged pending manual/native evidence.

## Task 6 — Verification and manual handoff

**Files:** Update `docs/reviews/2026-11-12-remediation-status.md`, `docs/reviews/2026-11-12-deployment-validation.md`, `docs/README.md`; create `docs/reviews/2026-11-13-follow-up-status.md` during execution.

- [ ] Run `dotnet test HyperPet.sln -c Release`, `dotnet build HyperPet.sln -c Release --no-restore`, `./tests/ReleaseScripts.Tests.ps1`, `dotnet list HyperPet.sln package --vulnerable --include-transitive --format json`, and `git -c core.safecrlf=false diff --check`. Record actual counts/errors; prior 234 is a baseline, not a required final count.
- [ ] Publish/pack current source into a unique ignored verification directory using the existing scripts. Inspect runtimeconfig and expected pet/native assets. Label dirty-source packages as local previews; never pass them off as clean release artifacts. No version bump required for local evidence.
- [ ] Record each reproduced defect, regression test, result and unresolved limitation. Note user elected manual deployment testing; record Miku licensing work excluded, without modifying historical audit evidence.
- [ ] Preserve manual matrix: clean install → consent/deny/regrant → notification → upgrade → login startup → uninstall; Windows-disabled/in-app-enabled; repeated real notifications and forced-GC native stress; physical 100/125/150/200% and mixed-DPI transitions. Supply expected outcomes and log locations; manual items remain unchecked until user reports results.
- [ ] Review complete diff for lost settings/pet data, native side effects and stale docs. No merge/push/publish, Git ref cleanup or global skill edits as part of this handoff.

## Completion criteria

Tasks 1–5 are complete only with their targeted evidence and green full checks. Task 6 delivers a reproducible handoff; deployment/physical validation remains explicitly pending. Miku provenance is not a blocker in this plan. Global skills and disk/history cleanup are separate scopes, not unfinished product bug fixes.
