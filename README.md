# gregMod.NotesHUD

In-game post-it notebook: type IPs, shop item #s, or general notes for later reference. Per-save persisted, fully via GregCore (F1 hub + key HUD + input locks) with standalone fallback.

## Controls

- `F10` = notebook open/close (configurable via `ToggleKey` pref in category `gregMod.NotesHUD`; use a function key — letter keys would type into the notebook).
- Click paper/title to focus · `Tab` switches title/body · `Enter` = newline · `Ctrl+Backspace` clears field · `Esc` closes.
- Drag the header pin row to move the post-it.

## Persistence

- Primary: `UserData/gregMod.NotesHUD/notes_<saveScope>.json` (per-save binding, same pattern as gregMod.IPAM — a new game never inherits old notes).
- Mirror (gregCore only, best-effort): text travels inside the savegame via `ModItemSaveData.saveIntArray` (UTF-16 code units, folder `gregMod.NotesHUD/notes`, max 1800 chars). See `docs/GREGCORE_GAPS.md` — GregModSave has no string slot today.
- Autosave debounced (~0.8 s after typing) + save on close/quit.

## Why no normal text field?

The game strips `TextEditor` APIs used by `GUI.TextField` and UIToolkit `TextField` (hard crash — see Trainer + IPAM `IpamFormInput`). The notebook renders text in `Label`s and pumps `UnityEngine.InputSystem.Keyboard` manually (`src/Input/SafeTextPump.cs`).

## GregCore integration

- `GregMenuRegistry.RegisterMenu` (LockCamera/Movement/Interact + ShowCursor), `GregMenuBinding.BindToggle`, `GregHudRegistry.Register`, `GregModRegistry.Register` — all behind `GregHost.HasCore` (JIT-split in `src/Core/NotesCoreBridge.cs`).
- Missing GregCore SDK pieces are proposed in `docs/GREGCORE_GAPS.md` (per-save text store, safe text field widget).

## Build

```bash
dotnet build gregMod.NotesHUD.csproj -c Release
# deploy:
cp bin/Release/net6.0/gregMod.NotesHUD.dll "$DATACENTER_HOME/Mods/"
```

Check `MelonLoader/Latest.log` for `[NotesHUD] Loaded`.

## Known quirks

- Standalone (no gregCore): closing with `Esc` may also open the game's pause menu (no central input lock to consume the keypress). With gregCore this is suppressed via escape-cooldown reporting.
- US keyboard layout for symbols (`Shift+2` = `@`, etc.); letters/digits work on any layout.
