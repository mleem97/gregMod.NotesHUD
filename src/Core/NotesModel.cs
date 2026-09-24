// NotesModel — post-it notebook data (title + multiline body).
// Plain DTO + guardrails: max length, control-char filtering, line counting.
// No Unity / gregCore / Melon references: unit-testable without the game.
using System;

namespace GregMod.NotesHUD.Core
{
    public sealed class NotesModel
    {
        public const int MaxTitleLength = 64;
        public const int MaxBodyLength = 4000;

        public string Title = "NOTES";
        public string Body = "";

        public int CharCount
        {
            get { try { return Body != null ? Body.Length : 0; } catch { return 0; } }
        }

        public int LineCount
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Body)) return 1;
                    int n = 1;
                    foreach (char c in Body) if (c == '\n') n++;
                    return n;
                }
                catch { return 1; }
            }
        }

        public static string SanitizeTitle(string value)
        {
            try
            {
                if (value == null) return "";
                var s = value.Replace("\r", "").Replace("\n", " ").Trim();
                if (s.Length > MaxTitleLength) s = s.Substring(0, MaxTitleLength);
                return s;
            }
            catch { return ""; }
        }

        public static string SanitizeBody(string value)
        {
            try
            {
                if (value == null) return "";
                var s = value.Replace("\r", "");
                // Strip control chars except \n and \t (tab becomes 2 spaces later at render).
                var buf = new char[s.Length];
                int w = 0;
                foreach (char c in s)
                {
                    if (c == '\n' || c == '\t' || c >= 0x20 || c == 0x0A) buf[w++] = c;
                }
                s = new string(buf, 0, w);
                if (s.Length > MaxBodyLength) s = s.Substring(0, MaxBodyLength);
                return s;
            }
            catch { return ""; }
        }

        public void SetTitle(string value) { try { Title = SanitizeTitle(value); } catch { } }

        public void SetBody(string value) { try { Body = SanitizeBody(value); } catch { } }

        public void Clear() { try { Body = ""; } catch { } }
    }
}
