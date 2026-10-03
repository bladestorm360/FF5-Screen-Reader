# FF5 screen-reader — source guidance

Read the shared [development agreements](../../../../AGENTS.md), [FFPR guide](../../AGENTS.md), and [CODEX_MEMORY.md](CODEX_MEMORY.md) once if not loaded. Converted from CLAUDE.md on 2026-10-02; original retained for technical detail.

- Use build_and_deploy.bat from this source directory; it builds and deploys. Do not replace it with a bare dotnet build that leaves the live mod stale.
- Read this game's own latest MelonLoader log for a defect, then relevant docs/debug.md and docs/plan.md sections; keep searches within this game and authorized references.
- Keep UI narration event-driven; no new per-frame UI readers or timer delays. Preserve documented game-driven timing and one-frame event deferrals.
- Use the game's own IL2CPP types and self-contained translation.json entries. Do not transplant translation strings from another FF game.
- Preserve Unicode/line endings with targeted edits. Read large metadata/decompile files only through bounded searches/slices.
- A direct implementation request already authorizes work; do not repeatedly stop after plans. Relevant docs should reflect changed facts and actual user validation, not a duplicate checklist.
- Keep investigations in this repo and parent FF5 dump unless the user authorizes another reference; FF6 is not a default porting source.
