# Dead-Code Cleanup Plan

**Goal:** Remove leftover code from the removed "OnlyMessagingApps" feature.

**Findings (audit):**
- `MessagingAppFilter` (`src/HyperPet.Core/Notifications/MessagingAppFilter.cs`) — referenced only by its own test file. Dead in production.
- `MessagingAppRule.Matches` + private `Contains` — used only by `MessagingAppFilter`. Become dead once it is removed. (PetController has its own correct matching that intentionally ignores `Enabled` during matching, so these are not shared.)
- `PetController._messagingAppFilter` field + constructor parameter — assigned, never read.
- `MessagingAppRule` class doc comment references the removed `OnlyMessagingApps` setting.

**No functional bugs found.** Roam + quiet-hours code is tested and correct.

## Steps

1. Delete `src/HyperPet.Core/Notifications/MessagingAppFilter.cs`.
2. Delete `tests/HyperPet.Core.Tests/Notifications/MessagingAppFilterTests.cs`.
3. `MessagingAppRule.cs`: remove `Matches` + `Contains`; fix the class doc comment (drop the OnlyMessagingApps sentence).
4. `PetController.cs`: drop the `MessagingAppFilter? _messagingAppFilter` field and the constructor parameter; simplify the constructor to `PetController(Func<DateTime>? clock = null)`. Keep the parameterless usage working.
5. Update `PetControllerTests` quiet-hours tests that call `new PetController(messagingAppFilter: null, clock: ...)` → `new PetController(clock: ...)`.
6. Build + full test run (filter tests removed; expect green).
7. Commit.
