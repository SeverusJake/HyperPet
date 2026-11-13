# Git maintenance and integration — 2026-11-13

The owner explicitly requested Git disk cleanup, committing the remediation, integration into `main` and pushing `origin/main`. This is separate from publishing an application release.

## Cleanup performed

- Before: 3,538 loose objects using **2.53 GiB**, plus six abandoned `tmp_obj_*` files using **236.89 MiB** according to `git count-objects -vH`.
- One obsolete Codex checkpoint tree, `8fcdf495b646ada638f311fcc8bc1238a4f51049`, retained generated `publish`/`Releases` binaries, including approximately 189 MB executables. It was not a commit in `main` history.
- Exported and ZIP-verified all source root entries from that tree, excluding only `publish` and `Releases`, before deleting its one ref with an expected-old-object check. The source archive is **1,161,533 bytes**, with ref/tree manifests under the ignored local directory `.superpowers/git-maintenance/2026-11-13/`.
- Ran `git gc --prune=2026-11-11T17:00:00Z`, preserving newer unreachable objects as a safety window. No `--prune=now`, history filtering, forced push or blanket reflog expiration was used.
- After maintenance: **496.46 MiB** in two packs, **zero loose objects and zero garbage**. The full `.git` directory measured **520,661,370 bytes** at that point. Most remaining packed space is recent unreachable-object retention; it was deliberately not force-pruned.
- `git fsck --connectivity-only --no-dangling` passed. Both current source-only Codex refs, all branches/tags and the registered Claude worktree were preserved. No existing `publish`, `Releases`, `dist` or user-profile files were deleted.

## Integration evidence

- Fetched `origin` before integration; local `main` and `origin/main` both identified base `88a5026fb35a991f6df23614d7f59baecc4171ad`.
- Fresh full Release tests: **265 passed, 0 failed, 0 skipped** — 69 Core, 196 App.
- Fresh Release build: **0 warnings, 0 errors**. All **3 PowerShell regression groups** passed and `git diff --check` passed.
- Remediation source, tests, build/CI configuration and review documents are committed together. Generated packages, local checkpoint backups and execution ledgers are excluded from the commit.
- Requested integration uses a normal fast-forward into `main` and a normal push. The resulting commit and remote verification are reported in the chat; consult Git history for the authoritative integration state.

Earlier audit/remediation reports preserve their pre-integration snapshots. Deployment/identity/physical DPI/native GC stress remain the owner's manual follow-up; Miku provenance investigation remains excluded. No application version bump or release publication is part of this operation.
