# Release Procedure

Triggered when the user says **"prepare release X.Y"** or **"prepare release X.Y.Z"** (e.g. `prepare release 1.6`, `prepare release 1.5.1`). Substitute `<version>` for the user's number throughout.

## Inputs

- `<version>` — bare version string, no leading `V` (e.g. `1.6`, `1.5.1`).
- Tag name is always `V<version>` (capital V) to match the existing zip naming on prior releases.

## Preconditions

1. Working tree is clean: `git status --porcelain` returns empty. If dirty, **stop and report** — the user must commit (or stash) first; release artifacts must come from a committed state.
2. HEAD is the commit being released. Do not tag mid-feature; if recent work isn't meant to ship, ask before tagging.
3. `Releases\V<version>\` does not already exist. If it does, **stop and report** — never silently overwrite a prior release directory.

## Steps

### 1. Build a fresh DLL

```
powershell.exe -Command "& 'D:\Games\Dev\Unity\FFPR\FF5\ff5-screen-reader\build_and_deploy.bat'"
```

Confirms compile succeeds and produces `bin\Debug\net6.0\FFV_ScreenReader.dll`. If the build fails, stop.

### 2. Assemble `Releases\V<version>\`

Create the directory and copy in the four end-user files:

| File | Source |
|---|---|
| `FFV_ScreenReader.dll` | `bin\Debug\net6.0\FFV_ScreenReader.dll` (the fresh build) |
| `nvdaControllerClient64.dll` | most recent prior `Releases\V*\` directory |
| `Tolk.dll` | most recent prior `Releases\V*\` directory |
| `ReadMe.txt` | copy of repo-root `README.md`, converted to plain text and saved as `ReadMe.txt` (strip `#` heading markers, code-fence ` ``` ` lines, leading `-` bullet markers, and any other markdown syntax — the shipped file should read cleanly with a screen reader, no leftover `#` or backticks) |

Preserve casing exactly: lowercase `n` in `nvdaControllerClient64.dll`, capital `T` in `Tolk.dll`. End users follow install instructions in `ReadMe.txt` that reference these names.

Note on `waypoints.json` and `SDL3.dll`: V1.4 bundled both, V1.5 dropped them. Match the V1.5 four-file bundle by default. If the user explicitly asks to ship updated waypoints, add `waypoints.json` from the repo root and the zip becomes five files; do not add it silently.

### 3. Zip with 7-Zip

```
& "C:\Program Files\7-Zip\7z.exe" a -tzip "Releases\FFV-Screen-ReaderV<version>.zip" ".\Releases\V<version>\*"
```

Zip naming: `FFV-Screen-ReaderV<version>.zip`, placed in `Releases\` (sibling of the version directory, not inside it). The zip's root contains the four files directly — no nested `V<version>\` folder, so end users can extract and follow ReadMe placement instructions without re-routing paths.

### 4. Tag and push

```
git tag V<version>
git push ff5-screen-reader master
git push ff5-screen-reader V<version>
```

Remote name is `ff5-screen-reader` (not `origin`). Default branch is `master`. Verify with `git remote -v` if unsure. Push `master` first so the branch points at the released commit (otherwise the tagged commit only reaches the remote via the tag ref, leaving remote `master` behind HEAD). Then push the tag.

This is the **only** step in the release procedure that pushes `master` — and it's only authorized here, on the commit being tagged. Never push `master` outside of a release without an explicit ask.

### 5. Draft the changelog

**This changelog is written for end users, not developers.** End users read these notes before clicking download. They do not know what internal class names, controllers, or patch handlers are, and they should not have to. Translate every commit into the user-visible *behavior* it produced.

**Do not pass `--generate-notes`.** GitHub's auto-generated notes are commit titles, which are written for developers — they leak internal type names and refactor mechanics. Always hand-write.

Style:

- 2–6 hyphen bullets, no headings.
- Each bullet describes a user-visible behavior change, in language a player would use. Verbs like "speak," "announce," "play," "show," "open"; nouns like "key," "menu," "screen," "vehicle," and the mod's own domain words ("pip" for footsteps, "beacon" for the audio pathfinder, "waypoint," etc.).
- Themed by feature area (controller / battle / translations / bundled files / fixes), not commit-by-commit.
- Cover all user-visible work since the previous tag — review `git log V<previous>..HEAD` and `git diff V<previous>..HEAD --stat`, then translate into user terms.

**If `V<previous>` has no tag locally** (FF5 has historically shipped zips without pushed tags), use the commit closest in time to the prior `Releases\V<previous>\` directory's mtime as the baseline for the changelog diff.

**Always exclude:**

- Commit hashes, file paths, type names, method names, field names.
- Internal refactors with no user-visible effect (e.g. "handler X removed and inlined into Y" — say nothing, or describe the *fix* it enabled).
- Regressions you introduced mid-development that never reached a prior release. If `V<previous>` shipped fine and a bug existed only between `V<previous>` and `V<current>` on `master`, no user ever saw it; do not advertise the fix.
- Developer-internal numbering or counts ("19 of 20 states tracked"); say what *works* for the user.

**Concrete contrast:**

| ❌ developer-language (do NOT write) | ✅ end-user language (DO write) |
|---|---|
| Walk/run announce hooked via IsAutoDash poll | Walk/run announcements should now speak reliably when F1 is pressed |
| Per-frame tile-crossing footsteps silent in vehicles | Footsteps no longer pip while traveling in vehicles (chocobo, hydra, ship, airship) |
| WaypointManager scopes pirate-ship entries to pirate-ship context | New waypoints placed from the pirate ship now only pathfind when you're back on the pirate ship |
| FunctionKeyHandler removed and F-keys + U inlined into InputManager | The autodetail key (I) and "usable by" key (U) work consistently across menus again |
| Alt-modifier guard on bare F-keys (internal regression I introduced) | (omit entirely — never reached users) |

Write the body to `release-notes-V<version>.txt` in the repo root (untracked, local-only by convention — analogous to `docs/release.md`). Re-read it once before publishing; once published, edits via `gh release edit --notes-file ...` are visible to anyone who saw the original notes.

### 6. Create the GitHub Release with `gh` and upload the zip

```
gh release create V<version> "Releases\FFV-Screen-ReaderV<version>.zip" --title "V<version>" --notes-file "release-notes-V<version>.txt"
```

- `gh` is at `C:\Program Files\GitHub CLI\gh.exe`, authenticated as `bladestorm360`.
- `--notes-file` pulls the hand-written changelog from step 5.
- The zip is uploaded as the release's binary asset in the same call.
- If a release for `V<version>` already exists, `gh` will error — do not pass `--clobber` or delete-and-recreate without asking; that's a destructive action on a published artifact. To fix mistakes on a live release, prefer `gh release edit V<version> --notes-file ...`.

### 7. Output the release link

After `gh release create` succeeds, emit the URL as a single standalone line in the terminal, with nothing else on the line — so the user can triple-click to select and copy when shipping to end users:

```
https://github.com/bladestorm360/FF5-Screen-Reader/releases/tag/V<version>
```

(`gh release create` also prints a URL on success — using the canonical form above is fine, but if `gh` emits a different canonical URL, prefer what `gh` printed.)

## What this procedure does NOT do

- Does **not** bump `<Version>` in `FFV_ScreenReader.csproj`. The csproj version is not currently used as the release source-of-truth; do not change it as part of release prep unless the user asks.
- Does **not** modify `README.md` content. If the readme needs changes, that's a separate commit before the release-prep trigger.
- Does **not** overwrite an existing GitHub Release. If `V<version>` already exists on the remote, stop and report.
