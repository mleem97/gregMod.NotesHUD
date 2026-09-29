// AtomicFile — crash-safe JSON persistence for NotesHUD.
// Writes go to <path>.tmp + atomic move, keeping <path>.bak of the last good
// state. A torn write therefore never destroys data: readers fall back to
// the backup instead of failing over to an empty notebook.
using System;
using System.IO;

namespace GregMod.NotesHUD.Core
{
    internal static class AtomicFile
    {
        internal static void WriteAllText(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) return;
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            try
            {
                File.WriteAllText(tmp, content ?? "");
                try
                {
                    if (File.Exists(path))
                        File.Copy(path, bak, true);
                }
                catch { /* backup best-effort */ }
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                File.Move(tmp, path);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        /// <summary>Reads path, falling back to path.bak when main is missing/corrupt.</summary>
        internal static string ReadAllTextWithBackup(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { return File.ReadAllText(path); }
                catch { /* fall through to backup */ }
            }
            string bak = (path ?? "") + ".bak";
            if (File.Exists(bak))
                return File.ReadAllText(bak);
            throw new FileNotFoundException("No readable file or backup.", path);
        }
    }
}
