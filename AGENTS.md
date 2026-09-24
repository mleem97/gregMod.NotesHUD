# AGENTS.md — Notes for AI agents (gregMod.NotesHUD)

Repo: gregMod.NotesHUD · License: Apache-2.0 · Version: see `VERSION`.

## Duties

1. **Read first:** `README.md`, `docs/INDEX.md` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Back up changes:** before reporting done, build the mod (`dotnet build gregMod.NotesHUD.csproj -c Release` or `./build.sh NotesHUD`).
5. **Keep docs in sync:** for new features update `README.md` + `docs/` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Hard rules (IL2CPP)

- **Never** `GUI.TextField` / UIToolkit `TextField` — stripped `TextEditor` = hard crash. Text goes through `src/Input/SafeTextPump.cs` + `Label` rendering.
- **Never** touch gregCore types outside `src/Core/NotesCoreBridge.cs` (JIT split — mod must run without gregCore.dll).
- **Never** commit `references/*.dll`, `bin/`, `obj/` (see `.gitignore`).
- Defensive `try/catch` + null checks in every per-frame path; no per-frame reflection.

## Layout

- `src/NotesHUDMod.cs` — MelonMod entry, prefs, tick, scene hooks.
- `src/Core/` — `NotesModel` (pure DTO), `NotesStore`, `ModSaveScope`, `GregHost` probe, `NotesCoreBridge` (gregCore only).
- `src/Input/` — `SafeTextPump`, `NotesInputLock`.
- `src/UI/` — `NotesPanel` (host), `NotesOverlay` (content/editing).
- `docs/NOTES_HUD_SPEC.md` — behavior spec; `docs/GREGCORE_GAPS.md` — upstream proposals.
