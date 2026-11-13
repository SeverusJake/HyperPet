# Cleanup inventory and global skill proposal

> Historical proposal. Authorized Git cleanup was subsequently performed on 2026-11-13; see [maintenance evidence](2026-11-13-git-maintenance.md). Output-directory deletion and global skill edits remain outside that operation.

No generated output, Git refs/history, registered worktree, or global skill configuration was deleted by this implementation.

## Local inventory from audit

| Target | Audit size | Proposed action |
|---|---:|---|
| publish | 5,380.92 MiB before new verification builds | Keep required verification evidence; select explicit old folders for deletion after owner review |
| Releases | 2,366.51 MiB | Separate local duplicates from assets still referenced by updater manifests; no blanket deletion |
| dist | 591.71 MiB | Identify retained deliverables, then delete only approved paths |
| .git | 2,659.51 MiB | Back up refs/worktrees and inspect reachability before packing or any history/ref operation |
| .git temporary garbage | 236.89 MiB included above | Git maintenance after checking active processes; never manually remove arbitrary object files |
| .claude/worktrees/strange-matsumoto-dd92cc | 2.99 MiB, registered and clean at audit | Confirm branch still needed, then use git worktree commands if removal is chosen |
| codex/auto-notification-placement | Same commit as main at audit | Optional branch cleanup; no performance benefit requiring immediate deletion |

Output is now ignored, preventing new accidental traversal/checkpoints. Existing reachable binary objects, including refs/codex checkpoints, remain reachable; ignore rules do not reclaim their storage.

## Global instructions/skills — proposed only

- Preserve personal caveman preference; its one-line AGENTS rule is already small.
- Fix Superpowers Codex reference to match current tool schema: model overrides only on isolated forks where supported; no fixed 5–10 minute blocking wait; do not force model/effort overrides against harness defaults.
- Replace the “1% chance” loading rule with relevance-based loading; keep useful debugging/verification substance without repeated imperative tables.
- Merge duplicate graphify trigger text in personal CLAUDE.md. Keep harness-specific startup instructions separate unless both are actually loaded together.
- Remove duplicate catalog registrations for SEO packages and use project-appropriate plugin profiles where the host supports them. WPF does not require loading all Godot/Unity/SEO/Cloudflare metadata.
- Change the maintained plugin source/configuration, not cached SKILL.md copies that updates overwrite. Identify those editable sources and show exact changes before global edits.

## Repository cleanup performed

Removed unused AppLauncher/IAppLauncher and the unused activation stub/StartupService.IsEnabled; removed generated empty Properties/Settings files and undiscoverable Default PNG fallback assets. Remaining legacy serialized OpenAppOnBubbleClick is retained as compatibility data; metadata and pure controller public state are not blindly removed while callers/tests still use them.

Historical docs now have banners and a current index. No copied skill bodies or complete historical plans were added to repository AGENTS.
