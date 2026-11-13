# Follow-up remediation — 2026-11-13

Plan: [follow-up remediation](../superpowers/plans/2026-11-13-follow-up-remediation.md). Pre-integration snapshot: branch `codex/audit-remediation`; source was uncommitted at verification time. No install/consent/autorun changes, push, tag or publication had been performed. Subsequent authorized Git cleanup/integration is recorded in [maintenance evidence](2026-11-13-git-maintenance.md).

## Changes

- Windows native listener is created lazily only for an enabled source. Constructor/access failures are contained; in-app monitoring starts independently. User pet-directory creation/access failures retain readable built-ins.
- Catalog enumeration keeps healthy entries and continues with other roots after expected I/O/access failures. Settings opening has a guard before discovery, activates the existing dialog and rejects late opening after the owner closes.
- Settings uses a deep candidate and prepares the pet without swapping it. Startup, shared values and the live pet commit synchronously after preparation is current. Failed preparation/startup and closing during preparation retain the current state. Reset restores animation defaults before reapplying remaining overrides.
- Active-pet removal prepares a valid fallback before renaming the source into a unique sibling quarantine outside discovery. Pre-commit failure restores the folder; no playable fallback refuses removal. Recursive cleanup failures report their recovery path without presenting a partial tree as an intact rollback. Pet operations and Apply cannot overlap in one dialog.
- Windows permission requests share pending native work while enable generations reject old results. Subscription, posted event and poll delivery are guarded across disable/re-enable and shutdown. In-app dispatcher delivery also checks its configuration generation.
- Explicit reload requires the requested ID; startup-only fallback normalizes the selected ID in memory. Removal classifies the active pet from the live sprite and rejects a fallback returning the wrong pet or the pet being removed.
- Native subscriptions capture their own generation before retrieval/conversion. A callback completing after unsubscribe cannot publish to the new subscription. The bounded WinRT retention workaround remains unchanged.

## Verification

- Baseline repeated at start: 234 tests passed.
- Final full Release run: **265 passed, 0 failed, 0 skipped** (69 Core, 196 App), including the final review fixes and partial-cleanup/rollback-failure coverage.
- Release build: **0 warnings, 0 errors**.
- PowerShell: **3 regression groups passed**. NuGet audit including transitive packages reported no advisories.
- Targeted tests cover preparation/startup failure, successful swap, close/cancel, repeated Apply, reset, duplicate Settings opening, catalog enumeration interruption, deletion/rollback/cleanup fault injection and native lifecycle generations. Additional passing cases are coverage, not newly claimed bug fixes.
- Test-first API compilation failures established missing test seams; the animation-reset regression was observed failing with expected FPS 2 versus actual 20 before correction. A Settings opening test initially lacked a shown WPF owner; its setup was corrected before the passing result.
- Independent read-only review found three P2 paths: wrong explicit reload identity, active-removal classification from stale stored ID, and delayed native conversion crossing subscriptions. Each was addressed in the fix pass; behavioral identity regressions were observed failing before the correction. The native gate has deterministic paused-conversion coverage. Review also identified a partial-cleanup coverage gap, now covered.
- Author review regraded dictionary-comparer loss as a bug after a mixed-case `IDLE` reset test failed. Deep drafts now preserve case-insensitive override maps; the regression passes.
- Current production source was published to `publish/follow-up-verification-2026-11-13` and packed to `publish/follow-up-packed-2026-11-13`. Core/Desktop runtimeconfig reports **10.0.15**. Native Skia, notices, shipped pet metadata and sheet were checked. Velopack verified the custom Main bootstrap and created Setup/Portable; signing is not configured.
- Preview manifest explicitly records dirty source and base commit. All **9** payload/manifest checksums in `SHA256SUMS-preview.txt` were recomputed successfully. This does not identify an exact clean release source and is not a publication candidate.
- `git diff --check` passes. No product code changed after the final publish; the last added test covers rollback failure.

## Limits and scope

The tests use temporary directories, STA windows and fake native adapters. They do not prove real package identity, consent, upgrades, login startup, uninstall, mixed physical DPI or the legacy WinRT retention workaround. The owner chose manual deployment testing; use the [manual matrix](2026-11-12-deployment-validation.md).

Startup failure coverage exercises the factory/lifecycle plus inspected App wiring; it does not launch a complete native App session. This is a narrower validation than the full STA startup coordinator smoke test proposed in Task 1. Existing startup ownership was retained to avoid a broad bootstrap rewrite. Native startup remains part of the manual matrix.

Miku copyright/provenance investigation is excluded by the owner's request. Existing attribution stays. Disk/Git cleanup and global skill/plugin edits remain separate scopes; no cached plugin was modified. Dirty version 0.7.1 local packages are verification previews and must not be uploaded as a clean release.
