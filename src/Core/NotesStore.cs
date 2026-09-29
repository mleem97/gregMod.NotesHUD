// NotesStore — per-save load/save for the notebook.
// Primary: UserData/gregMod.NotesHUD/notes_<scope>.json (per-save binding via
// ModSaveScope, same pattern as gregMod.IPAM).
// Secondary (best-effort, only with gregCore): mirror the text into the game's
// own save via GregModSave (ModItemSaveData.saveIntArray, UTF-16 code units),
// so notes travel WITH the savegame. See docs/GREGCORE_GAPS.md — GregModSave
// has no string slot today, hence the int-array encoding lives in
// NotesCoreBridge (isolated, JIT-split) and is strictly additive.
using System;
using System.IO;
using System.Text.Json;
using MelonLoader;

namespace GregMod.NotesHUD.Core
{
    internal static class NotesStore
    {
        private static readonly NotesModel _model = new NotesModel();
        private static bool _loadedForScope = false;
        private static string _loadedScope = "";
        private static float _nextSaveAtRealtime = -1f;
        private static bool _dirty = false;

        internal static NotesModel Model { get { return _model; } }

        internal static void MarkDirty(float debounceSeconds)
        {
            try
            {
                _dirty = true;
                _nextSaveAtRealtime = UnityEngine.Time.realtimeSinceStartup + Math.Max(0.2f, debounceSeconds);
            }
            catch { }
        }

        internal static void EnsureLoaded()
        {
            try
            {
                ModSaveScope.TickCapture();
                string scope = ModSaveScope.CurrentScopeId;
                if (_loadedForScope && string.Equals(_loadedScope, scope, StringComparison.Ordinal))
                    return;
                // Scope changed (e.g. menu -> loaded save): persist in-memory edits
                // to the OLD scope first so typing before capture is never lost.
                try { if (_loadedForScope && _dirty) SaveToScope(_loadedScope); } catch { }
                LoadScope(scope);
            }
            catch (Exception ex)
            {
                try { MelonLogger.Warning("[NotesHUD] EnsureLoaded failed: " + ex.GetBaseException().Message); } catch { }
            }
        }

        internal static void TickAutosave()
        {
            try
            {
                if (!_dirty) return;
                if (UnityEngine.Time.realtimeSinceStartup < _nextSaveAtRealtime) return;
                _dirty = false;
                SaveNow();
            }
            catch { }
        }

        internal static void SaveNow()
        {
            try
            {
                EnsureLoaded();
                SaveToScope(_loadedScope);
                // Best-effort mirror into the live save (gregCore only, never throws).
                try { NotesCoreBridge.MirrorToSave(_model.Title, _model.Body); } catch { }
            }
            catch (Exception ex)
            {
                try { MelonLogger.Warning("[NotesHUD] Save failed: " + ex.GetBaseException().Message); } catch { }
            }
        }

        private static void SaveToScope(string scope)
        {
            try
            {
                string path = ModSaveScope.GetNotesFilePathFor(scope);
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                catch { }
                var file = new NotesFile { Title = _model.Title ?? "", Body = _model.Body ?? "" };
                try
                {
                    AtomicFile.WriteAllText(path, JsonSerializer.Serialize(file));
                    _dirty = false;
                }
                catch (Exception ex)
                {
                    // Disk full / no permission: keep dirty so the text is
                    // retried on the next tick instead of silently dropped.
                    try { MelonLogger.Warning("[NotesHUD] Save failed, will retry: " + ex.GetBaseException().Message); } catch { }
                }
            }
            catch { }
        }

        internal static void LoadScope(string scope)
        {
            _loadedScope = scope ?? "global";
            _loadedForScope = true;
            try
            {
                // 1) Per-save file wins (backup fallback on torn writes).
                string path = ModSaveScope.GetNotesFilePath();
                if (File.Exists(path) || File.Exists(path + ".bak"))
                {
                    try
                    {
                        var file = JsonSerializer.Deserialize<NotesFile>(AtomicFile.ReadAllTextWithBackup(path));
                        if (file != null)
                        {
                            _model.SetTitle(file.Title);
                            _model.SetBody(file.Body);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        try { MelonLogger.Warning("[NotesHUD] Notes file unreadable, trying save mirror: " + ex.GetBaseException().Message); } catch { }
                    }
                }
                // 2) Fall back to the live-save mirror (gregCore only).
                try
                {
                    string title, body;
                    if (NotesCoreBridge.TryReadFromSave(out title, out body))
                    {
                        _model.SetTitle(title);
                        _model.SetBody(body);
                        return;
                    }
                }
                catch { }
                // 3) Fresh notebook.
                _model.SetTitle("NOTES");
                _model.SetBody("");
            }
            catch
            {
                try { _model.SetTitle("NOTES"); _model.SetBody(""); } catch { }
            }
        }

        internal static void OnScopeInvalidated()
        {
            // Flush pending keystrokes first: invalidating on scene change
            // previously discarded everything still inside the debounce window.
            try { if (_loadedForScope && _dirty) SaveToScope(_loadedScope); } catch { }
            _loadedForScope = false;
            _loadedScope = "";
            _dirty = false;
        }

        private sealed class NotesFile
        {
            public string Title { get; set; }
            public string Body { get; set; }
        }
    }
}
