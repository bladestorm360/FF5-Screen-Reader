# FF5 Screen Reader Mod

Screen-reader/accessibility mod for FF5 Remaster. MelonLoader + Harmony patches hook Il2CPP game code. Output via Tolk to NVDA.

## Critical Rules

| Rule | Requirement |
|------|-------------|
| 0: User Approval | STOP after presenting plans. Wait for explicit "yes/approved/proceed" before implementing. |
| 1: No Doc Overwrites | Use Edit tool only for *.md files. Never use Write to replace documentation. |
| 2: No Polling | Never use per-frame checks, OnUpdate, or continuous coroutines. Find the exact Harmony hook. |
| 3: No Timers | Never use `WaitForSeconds` or hardcoded delays. Hook the precise moment instead. Exception: game's own timing systems. |
| 4: Update Docs | After completing a feature or during debugging, update `docs/plan.md` (feature status) and `docs/debug.md` (architecture/troubleshooting). Update `docs/PerformanceIssues.md` if performance-related. |
| 5: No PowerShell Edits | Never use PowerShell scripts to edit files containing non-ASCII characters (e.g., arrows →, Japanese text). They corrupt the encoding. Use the Edit tool instead. |
| 6: No Large Files | Never load `GameAssembly.dll.c` or other large files directly — use Grep. Max 50 lines from `GameAssembly.dll.c`/`dump.cs` unless user permits. |
| 7: Logs First | Always check game logs before debugging. |

## General Rules
- **Reference FF4 mod** (`ff4/ff4-screen-reader`) — port shared patterns, only generate FF5-specific
- **FF6 mod** (`ff6/ff6ScreenReader`) — only reference when user gives explicit permission to port or research code
- **Investigation scope**: Limit to `ff5-screen-reader/` and one level up (`FF5/`) unless user permits otherwise
- **Never edit** game or reference mod folders
- No duplicates — reference existing code
- **Build**: Always use `ff5/ff5-screen-reader/build_and_deploy.bat`
- **PowerShell scripts**: `ModMap.ps1`, `FindMainClass.ps1`, `ExtractCode.ps1`

## Syntax Rules
- **IL2CPP prefix**: All game classes require `IL2CPP` prefix (e.g., `IL2CppLast.Map.FieldController`)
- **Harmony**: `[HarmonyPatch(typeof(Class), nameof(Class.Method))]`
- **Caching**: `GameObjectCache` for frequent access
- **Coroutines**: `CoroutineManager.StartManaged()`
- **Speech**: `FFV_ScreenReaderMod.SpeakText(text, interrupt)`

## Directory Structure
```
D:\games\dev\unity\ffpr\                          # Root project directory (pwd)
├── CLAUDE.md                                      # This file — rules and references
├── FF5\                                           # Il2CPP dump directory (game code research)
│   ├── ff5-screen-reader\                        # Mod source code
│   │   ├── build_and_deploy.bat                  # Build script
│   │   ├── Patches\                              # Harmony patches
│   │   ├── Core\                                 # Core mod classes
│   │   ├── Menus\                                # Menu reading logic
│   │   ├── Utils\                                # Utility classes
│   │   └── Field\                                # Field navigation
│   ├── DummyDll\                                 # Il2CPP dummy assemblies
│   ├── dump.cs                                   # Il2CPP class dump
│   ├── script.json                               # Il2CPP script data
│   ├── stringliteral.json                        # String literals
│   └── GameAssembly.dll.c                        # Il2CPP native code (NEVER LOAD)
├── ff6\                                          # Reference FF6 mod (READ ONLY)
│   └── ff6ScreenReader\
└── d:\Games\SteamLibrary\steamapps\common\FINAL FANTASY V PR\
    └── MelonLoader\
        ├── Latest.log                            # Game log (current session)
        └── Logs\                                 # Older logs
```

## Documentation
- **docs/plan.md** — Project overview, feature sections, completion status
- **docs/debug.md** — Architecture, class references, debug history, known issues
- **docs/PerformanceIssues.md** — Performance audit, optimizations done

## Bash Reference (Windows)
- `ls`, `cat`, `rm`, `./build_and_deploy.bat` — work
- PowerShell cmdlets (`Get-ChildItem`, `Sort-Object`, etc.) — do NOT work
- Logs: Use Read tool with `d:/Games/SteamLibrary/steamapps/common/FINAL FANTASY V PR/MelonLoader/Latest.log`
