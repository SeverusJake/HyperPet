# Quiet Hours (Scheduled DND) Implementation Plan

**Goal:** Suppress pet alerts during a user-set daily time window (handles overnight wrap).

**Architecture:** Pure `QuietHoursSchedule` (Core) decides active/parse; `PetController.HandleNotification` gates on it via an injectable clock; settings persist enabled + start/end "HH:mm"; Advanced tab exposes the controls.

**Tech Stack:** C# .NET 8, xUnit.

---

## Files

- Create: `src/HyperPet.Core/Settings/QuietHoursSchedule.cs`
- Create: `tests/HyperPet.Core.Tests/Settings/QuietHoursScheduleTests.cs`
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs` (3 fields)
- Modify: `src/HyperPet.Core/Settings/SettingsStore.cs` (Sanitize carries fields)
- Modify: `src/HyperPet.Core/Pet/PetController.cs` (clock + gate)
- Modify: `tests/HyperPet.Core.Tests/...` (PetController gate test, store round-trip)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` (+ Advanced controls)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` (load/commit/default/dirty)
- Modify: `src/HyperPet.App/Views/SettingsWindowSettingsApplier.cs` (3 params)
- Modify: `tests/HyperPet.App.Tests/Views/SettingsWindowSettingsApplierTests.cs` (named args)

## Behavior

- `QuietHoursSchedule.TryParse("HH:mm")` → `TimeOnly?`.
- `IsActive(now, start, end)`:
  - `start == end` → false (empty window).
  - `start < end` → `start <= now < end`.
  - `start > end` (overnight) → `now >= start || now < end`.
- Gate: in `PetController.HandleNotification`, after `AlertsPaused`, if
  `QuietHoursEnabled` and both times parse and `IsActive(clock().TimeOfDay)` →
  return null.

## Settings defaults

`QuietHoursEnabled=false`, `QuietHoursStart="22:00"`, `QuietHoursEnd="07:00"`.

## UI (Advanced tab)

Checkbox "Quiet hours (suppress alerts)" + two TextBoxes "Start (HH:mm)" /
"End (HH:mm)". Invalid time on commit falls back to the existing stored value.
