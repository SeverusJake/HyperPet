# Automatic Notification Position Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Implement the automatic notification layout approved in the HTML comparison.

**Architecture:** A pure rectangle solver chooses a free area around the rendered pet. An owned WPF window hosts the existing Bubble controls; MainWindow manages its placement and lifetime. A scroll viewer keeps title/body readable and hover pauses expiry.

**Tech Stack:** .NET 8, WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-11-auto-notification-position.md`

## Global Constraints

- Keep a 12-DIP gap and stay in the monitor work area; do not move/resize the pet.
- Keep click-to-advance, icon History, FIFO, and session-only history.
- Read the full title/body by scrolling; do not steal focus on appearance.
- Keep the earlier release filename changes intact; do not publish a release.

## Review Focus

- Negative monitor origins and constrained work areas must keep the bubble visible and separate.
- Moving/resizing the pet or reconnecting a display must reposition an active bubble.
- Clicking History or a scrollbar must not consume a queued notification.
- Long content must scroll and hover must pause expiry.
- Closing, pause and Tuck Away must hide/close the notification window.

### Task 1: Rectangle placement

Files: `src/HyperPet.App/Views/NotificationPlacement.cs`, `tests/HyperPet.App.Tests/Views/NotificationPlacementTests.cs`.

Interface: `NotificationPlacement.Compute(Rect petBounds, Size desiredSize, WorkArea workArea) -> Rect`.

- [x] Add tests for above/right/left/below, negative origins, exact fits and constrained rectangles; observe RED.
- [x] Implement full-size candidates in preference order, then choose the largest available bounded viewport; observe GREEN.

### Task 2: Window integration and readable content

Files: `MainWindow.NotificationPlacement.cs`, `MainWindow.Notifications.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, monitor hooks, and notification window tests.

- [x] Add UI tests proving a separate visible window, non-overlap, following movement/size, scrolling without advancing, and cleanup; observe RED.
- [x] Reparent existing Bubble into an owned, nonactivating transparent window and wire lifecycle, geometry updates, content measurement and scroll viewer.
- [x] Pause/restart alert expiry on hover, including advancing while hovered; run UI tests and full solution suite.

### Task 3: Review and validation

- [x] Review final working tree with a fresh reviewer and resolve actionable findings.
- [x] Render actual WPF notification with short/long content; inspect appearance and run final tests.
- [x] Update README and record verification results here.

## Execution notes

Continue in the existing checkout on `codex/auto-notification-placement`, preserving the already tested interaction and release-name edits. The user's choice of the working mockup authorizes this implementation; no further design check-in is needed.

## Verification

- Pure placement: 9 tests, including 81 size/corner combinations and no available space.
- Window integration: 12 tests, including all corners, pet resize, changed monitor work area, full title/body scrolling, pause, Tuck Away, closing and hover expiry.
- Reviewer reproduced ScrollViewer consuming real content mouse-down events. Regression failed before correction; a handled-event listener now accepts content clicks and excludes History/buttons/scrollbars.
- At actual 125% DPI, a short message initially had 0.26 DIP of overflow. Device-pixel rounding fixes it; the regression now reports zero scrollable height.
- Initial detached WPF content must receive its first layout before measuring. The notification is prepared at opacity zero without activation, then sized/placed and made opaque.
- Full solution: 198 passed (61 Core + 137 App), zero failures.
- Actual WPF renders inspected: `publish/auto-notification-preview/short.png`, `long-top.png`, `long-end.png`.
- Physical mixed-DPI monitor transitions remain unvalidated; pure negative-origin fixtures, actual single-monitor 125% DPI, movement/size, and work-area refresh are covered.
- No release published; existing versioned Setup/Portable script changes preserved.
