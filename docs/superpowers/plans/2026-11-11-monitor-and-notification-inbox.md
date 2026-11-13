> Historical engineering record. Read docs/README.md for current guidance; do not re-execute this plan against HEAD.

# Monitor selection and notification inbox implementation plan

**Goal:** Let the user select a pet monitor, recover from display disconnection,
and receive notifications sequentially with a bounded, replayable history.

**Architecture:** Keep notification queue/history rules in a pure Core inbox.
Keep monitor enumeration/selection/coordinate conversion outside MainWindow.
WPF views own rendering, timers, and the history window; existing filtering stays
in PetController. Extend existing settings and preserve pending earlier fixes.

**Tech stack:** .NET 8, WPF, Win32/Windows Forms monitor enumeration, xUnit.

## Scope and defaults

- Monitor dropdown in General: Automatic/current or an available display.
- Persist display identifier. If unavailable, use nearest available display;
  restore preferred display when it becomes available again.
- Apply selection immediately; Running/Desktop/Summon use chosen display bounds.
- Display-change events marshal to WPF dispatcher and unsubscribe on close.
- FIFO queue, 100 pending maximum, discard oldest pending on overflow.
- 50 newest history entries, session memory by default; privacy choice asked
  asynchronously. Replay does not duplicate history. Clear history leaves queue.
- Next advances current alert. Pause, quiet-hours suppression and exit clear
  pending alerts. Disabling full content also redacts queue/history/current data.
- History window accessible from pet and tray menus; replay and clear actions.
- Do not publish a new GitHub release as part of this implementation request.

## Tasks

- [x] Test and implement Core NotificationInbox and NotificationHistoryEntry.
- [x] Test and implement monitor descriptor/enumeration, selection and placement
      helpers; normalize roaming ledges using pet DPI and selected work area.
- [x] Persist monitor preference; wire General picker, Apply and Default.
- [x] Wire queue/timer lifecycle and add Next/count/History controls.
- [x] Add history view with replay/clear and privacy-aware updates.
- [x] Test WPF integration, monitor fallback, queue overflow, history replay,
      pause/quiet-hours/privacy and Tuck Away regression.
- [x] Run full Release tests, build/publish, dependency audit, and independent review.

## Verification

- Full Release suite: 174 passed (61 Core, 113 App). App suite repeated after
  serializing WPF resource-loading tests: 113 passed again.
- Windows self-contained publish: `publish/monitor-history/HyperPet.exe`;
  native Skia library, licenses and pet assets present; ImageSharp absent.
- NuGet audit including transitive packages reports no known vulnerabilities.
- Independent review found two issues, both fixed with RED→GREEN tests:
  Bubble hit testing above the sprite, and cursor-point monitor selection near
  a shared display edge. Incremental review found no remaining blocker.
- Rendered Settings and History previews inspected. Monitor disconnection and
  DPI coordinate handling tested with synthetic displays; multiple physical
  monitors with different DPI were not available for end-to-end validation.

## Review focus

Mixed-DPI and negative monitor origins; preferred display missing/reappearing;
late display events after close; queue clearing without promoting stale items;
privacy changes while history is open; history replay while paused; preserving
previous ImageSharp replacement and uninstall/Tuck Away fixes in working tree.
