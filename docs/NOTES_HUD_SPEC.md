# NotesHUD spec (v1)

## Goal

Simple on-screen in-game HUD notebook (post-it style) for text/words/numbers — IPs, shop item #s, general notes — typed in game, kept for later reference. Via GregCore.

## Non-goals (v1)

- No rich text / images, no multi-page tabs, no sharing between saves, no web UI.
- Single notebook (title + multiline body), max 64 / 4000 chars.

## UX

- Toggle `F10` (pref `gregMod.NotesHUD/ToggleKey`, function keys only recommended).
- Post-it window 420×560, draggable header, game font, cursor unlocked while open.
- Focus: click title row → title; click paper → body. `Tab` switches, `Enter` newline (body only), `Ctrl+Backspace` clears focused field, `Esc` closes (does not reach pause menu).
- Status line: `<chars>/4000 · <lines> lines · save <scope8>`.
- Buttons: `Clear` (clears focused field), `Close (F10)`.

## Input safety (IL2CPP)

- No `GUI.TextField`, no UIToolkit `TextField` (stripped `TextEditor` → crash).
- `SafeTextPump.Pump(ref buffer, maxLen, allowNewline)` reads `Keyboard.current` `wasPressedThisFrame` per `KeyControl`: A-Z (+shift caps), 0-9 (+shift `!@#$%^&*()`), numpad digits, space, `_-+=[]{ } ;:'",.<>/?\|~`` ` `` `, backspace with hold-repeat (0.45 s delay, 70/30 ms), `Ctrl+Backspace` = clear.
- Sanitized on commit (`NotesModel.Sanitize*`): strip `\r` + control chars, clamp length.

## Persistence

- Scope: `ModSaveScope` hash (scene + server/switch counts + money) captured after gameplay load; binding file `save_binding.json`; notebook file `notes_<scope>.json` under `UserData/gregMod.NotesHUD/`.
- Live-save mirror (gregCore only): `NotesCoreBridge.MirrorToSave` upserts `ModItemSaveData{modFolderName="gregMod.NotesHUD/notes", saveIntArray=UTF16(title\nbody)}`, max 1800 chars; read back when no JSON file exists.
- Autosave debounce 0.8 s; save on close + `OnApplicationQuit`.

## GregCore wiring (all behind `GregHost.HasCore`, JIT-split)

- `RegisterMenu("noteshud", LockCamera/Movement/Interact, ShowCursor)`.
- `GregModRegistry.Register("gregMod.NotesHUD", …)`, `GregHudRegistry.Register("noteshud", key, "Notes")`, `GregMenuBinding.BindToggle("noteshud", Toggle, IsVisible)` + `Report` on hotkey path.
- `SetOpen(open)` on show/hide for ref-counted input locks.

## File map

- `src/NotesHUDMod.cs` — MelonMod entry, prefs, per-frame tick, scene hooks.
- `src/Core/GregHost.cs` — soft probe (no gregCore types).
- `src/Core/NotesCoreBridge.cs` — ONLY gregCore-touching code.
- `src/Core/NotesModel.cs` — DTO + sanitize (no Unity refs).
- `src/Core/ModSaveScope.cs` — per-save binding.
- `src/Core/NotesStore.cs` — load/save/autosave.
- `src/Input/SafeTextPump.cs` — keyboard pump.
- `src/Input/NotesInputLock.cs` — cursor/menu locks.
- `src/UI/NotesPanel.cs` — UIToolkit host (post-it theme).
- `src/UI/NotesOverlay.cs` — content, focus, click routing, editing tick.

## Tests (manual, in game)

1. Fresh save → `F10` opens empty post-it; type IP `10.0.0.5`, shop `# 42`, multiline notes; close; reopen → text kept.
2. Save game, reload → notes still there. New game → empty notebook (no leak).
3. With gregCore: F1 hub lists NotesHUD, key HUD shows `F10 Notes`, game camera/movement frozen while open.
4. Without gregCore.dll: panel still opens, notes persist per-save JSON.
5. Typing never toggles other mods; `Esc` never opens pause menu from notebook.
