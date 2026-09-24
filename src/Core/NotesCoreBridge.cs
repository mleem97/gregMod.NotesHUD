// NotesCoreBridge — the ONLY file that touches gregCore types.
// RULE (Greg-Vertrag): call these methods only when GregHost.HasCore is true,
// and keep every gregCore.* reference inside this file (JIT split — without
// gregCore.dll the file never loads, the rest of the mod stays runnable).
// Covers: F1-hub wiring (menu + HUD + toggle binding), input-lock reporting,
// and the per-save text mirror via GregModSave (see docs/GREGCORE_GAPS.md:
// GregModSave has no string slot, so UTF-16 text is chunked into
// saveIntArray under folder "gregMod.NotesHUD/notes").
using System;
using System.Collections.Generic;

namespace GregMod.NotesHUD.Core
{
    internal static class NotesCoreBridge
    {
        private const string MenuId = "noteshud";
        private const string ModId = "gregMod.NotesHUD";
        private const string SaveFolder = "gregMod.NotesHUD/notes";
        private const int MaxMirrorChars = 1800;

        internal static void RegisterMenu()
        {
            gregCore.UI.GregMenuRegistry.RegisterMenu(MenuId,
                new gregCore.UI.GregMenuOptions
                {
                    LockCamera = true,
                    LockMovement = true,
                    LockInteract = true,
                    ShowCursor = true,
                });
        }

        internal static void RegisterExtras(string toggleKeyText)
        {
            gregCore.Core.Mods.GregModRegistry.Register(
                ModId, "NotesHUD", "1.0.0", new string[] { "noteshud" });
            gregCore.UI.GregHudRegistry.Register(MenuId, toggleKeyText ?? "F10", "Notes");
            gregCore.UI.GregMenuRegistry.RegisterOpener(MenuId,
                () => { try { UI.NotesOverlay.Toggle(); } catch { } try { SetOpen(UI.NotesOverlay.IsVisible); } catch { } });
            gregCore.UI.GregMenuRegistry.RegisterCloser(MenuId,
                () => { try { if (UI.NotesOverlay.IsVisible) UI.NotesOverlay.Toggle(); } catch { } try { SetOpen(false); } catch { } });
        }

        internal static void Report(bool open)
        {
            SetOpen(open);
        }

        internal static void SetOpen(bool open)
        {
            try { gregCore.UI.GregMenuRegistry.SetOpen(MenuId, open); } catch { }
        }

        // Stores title + "\n" + body as UTF-16 code units in saveIntArray.
        // Prefers gregCore's GregModSaveStrings (v1.3+); falls back to the
        // local encoder for older gregCore.dll (MissingMethod-safe).
        internal static void MirrorToSave(string title, string body)
        {
            try
            {
                var save = greg.Sdk.GregPublicAPI.GetSaveDataSafe();
                if (save == null) return;
                var list = save.modItemData;
                if (list == null) return;
                string text = gregCore.Core.Mods.GregModSaveStrings.CombineTitleBody(title, body);
                try
                {
                    gregCore.Core.Mods.GregModSaveStrings.UpsertText(list, SaveFolder, text, MaxMirrorChars);
                }
                catch
                {
                    // Older gregCore without GregModSaveStrings: local fallback.
                    LegacyUpsert(list, text);
                }
            }
            catch { /* best-effort: persistence never breaks the save */ }
        }

        internal static bool TryReadFromSave(out string title, out string body)
        {
            title = "";
            body = "";
            try
            {
                var save = greg.Sdk.GregPublicAPI.GetSaveDataSafe();
                if (save == null) return false;
                var list = save.modItemData;
                if (list == null) return false;
                string text = "";
                bool found = false;
                try
                {
                    found = gregCore.Core.Mods.GregModSaveStrings.TryReadText(list, SaveFolder, out text);
                }
                catch
                {
                    // Older gregCore without GregModSaveStrings: local fallback.
                    found = LegacyTryRead(list, out text);
                }
                if (!found || string.IsNullOrEmpty(text)) return false;
                try
                {
                    gregCore.Core.Mods.GregModSaveStrings.SplitTitleBody(text, out title, out body);
                }
                catch
                {
                    // Older gregCore without GregModSaveStrings: manual split.
                    int nl = text.IndexOf('\n');
                    if (nl < 0) { title = text; body = ""; }
                    else { title = text.Substring(0, nl); body = text.Substring(nl + 1); }
                }
                return true;
            }
            catch { }
            return false;
        }

        // Local fallback for gregCore.dll without GregModSaveStrings.
        private static void LegacyUpsert(object list, string text)
        {
            try
            {
                var typed = list as Il2CppSystem.Collections.Generic.List<global::Il2Cpp.ModItemSaveData>;
                if (typed == null) return;
                if (text != null && text.Length > MaxMirrorChars) text = text.Substring(0, MaxMirrorChars);
                var dto = new gregCore.Core.Mods.GregModSave.ItemSave();
                dto.ModFolderName = SaveFolder;
                dto.SaveIntArray = StringToCodeUnits(text ?? "");
                gregCore.Core.Mods.GregModSave.Upsert(typed, dto);
            }
            catch { }
        }

        private static bool LegacyTryRead(object list, out string text)
        {
            text = "";
            try
            {
                var typed = list as Il2CppSystem.Collections.Generic.List<global::Il2Cpp.ModItemSaveData>;
                if (typed == null) return false;
                var all = gregCore.Core.Mods.GregModSave.ReadAll(typed);
                if (all == null) return false;
                foreach (var dto in all)
                {
                    try
                    {
                        if (dto == null) continue;
                        if (!string.Equals(dto.ModFolderName ?? "", SaveFolder, StringComparison.OrdinalIgnoreCase))
                            continue;
                        text = CodeUnitsToString(dto.SaveIntArray);
                        return !string.IsNullOrEmpty(text);
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private static int[] StringToCodeUnits(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
                var outList = new List<int>(text.Length);
                foreach (char c in text) outList.Add((int)c);
                return outList.ToArray();
            }
            catch { return Array.Empty<int>(); }
        }

        private static string CodeUnitsToString(int[] units)
        {
            try
            {
                if (units == null || units.Length == 0) return "";
                var chars = new char[Math.Min(units.Length, MaxMirrorChars)];
                for (int i = 0; i < chars.Length; i++)
                {
                    int u = units[i];
                    chars[i] = (u < 0 || u > 0xFFFF) ? '?' : (char)u;
                }
                return new string(chars);
            }
            catch { return ""; }
        }
    }
}
