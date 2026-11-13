# HyperPet

- Windows WPF app; Core owns pure rules, Windows owns native adapters.
- Build: `dotnet build HyperPet.sln`.
- Test: `dotnet test HyperPet.sln -c Release` (WPF tests require Windows/STA).
- Read `docs/reviews/2026-11-12-project-audit.md` and the relevant task in
  `docs/superpowers/plans/2026-11-12-audit-remediation.md`; do not load old plans wholesale.
- Search src/tests first. Ignore publish, Releases, dist, bin, obj, .claude and .git.
- Filesystem/registry tests use temporary profiles; never real user settings or autorun.
- Validate canonical import paths and reparse points before mutation; stage/validate before replacing.
- Preserve existing pet/settings on failed writes, imports or reloads.
- Never log notification titles/bodies by default.
- No publish/push, destructive cleanup, history rewrite or global plugin edits without explicit scope.
- Release artifacts must identify the exact clean source commit and have verified checksums.
