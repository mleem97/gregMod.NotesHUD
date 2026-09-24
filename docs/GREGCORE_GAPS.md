# GregCore gaps for NotesHUD — status: INTEGRATED

Both missing SDK pieces below are now implemented in gregCore (`Unreleased`, see gregCore `CHANGELOG.md`). NotesHUD consumes the new APIs with a local fallback for older gregCore.dll (see `src/Core/NotesCoreBridge.cs`); its own keyboard pump stays local because standalone mode (no gregCore.dll) needs it anyway.

## Gap 1 — Per-save TEXT store (C# mods)

**Today:**
- `IGregPersistenceService` (`gregCore.Core.Abstractions`) is global-file only (`AppData/gregCore/Saves/{key}.json`) — no save scoping.
- `GregModSave` (`gregCore.Core.Mods`) travels with the save (`SaveData.modItemData`) but only carries `float[]/int[]` — no string slot, no C# key-value helper (Lua has `greg.modsave.*` + `greg.io.*` file sandbox instead).

**Workaround in NotesHUD (v1):** per-save JSON via own `ModSaveScope` (pattern copied from gregMod.IPAM) + best-effort mirror of UTF-16 text into `saveIntArray` (`NotesCoreBridge.MirrorToSave`, folder `gregMod.NotesHUD/notes`, 1800 chars).

**Proposal (GregCore):**
```csharp
// New: per-save scoped key-value for text, backed by ModItemSaveData + JSON fallback.
namespace gregCore.Core.Abstractions {
    public interface IGregPerSaveTextStore {
        void SetText(string modId, string key, string value);          // sanitized, size-capped
        string GetText(string modId, string key, string defaultValue = "");
        bool HasText(string modId, string key);
        void DeleteText(string modId, string key);
    }
}
// + GregModSave string extension: UTF-16 chunking into saveIntArray/saveIntArray2
//   with folder convention "<modId>/<key>", exposed via GregApiContext.PersistScoped.
```

## Gap 2 — IL2CPP-safe text input widget

**Today:**
- `GregPanelBuilder.AddInputField` builds a UIToolkit `TextField` → uses stripped `TextEditor` → hard crash on this game's IL2CPP build. Same for `GUI.TextField` (IPAM avoids it with a custom pump; Trainer ships zero text inputs for the same reason).

**Workaround in NotesHUD (v1):** `SafeTextPump` (InputSystem `wasPressedThisFrame` per `KeyControl`, backspace repeat, shift symbols) + `Label`-only rendering with caret; `Tab` focus-switch handled by overlay.

**Proposal (GregCore.UI):**
```csharp
// New: crash-safe text field for mods — Label rendering + central keyboard pump,
// focus management, paste-suppression, per-frame budget. Replaces AddInputField impl.
namespace gregCore.UI {
    public sealed class GregSafeTextField : VisualElement {
        public string Value { get; set; }
        public int MaxLength { get; set; }
        public bool Multiline { get; set; }
        public event Action<string> Changed;
    }
    // + GregPanelBuilder.AddSafeInputField(...) backed by it.
}
```

## Suggested upstream steps

1. Land `IGregPerSaveTextStore` + `GregModSave` string helpers (with tests on DTO level, no game needed).
2. Land `GregSafeTextField` + migrate `AddInputField` to it (manual in-game test per `docs/modding/ui-panels.md`).
3. Migrate NotesHUD workarounds onto the new APIs (drop local copies, keep file fallback).
