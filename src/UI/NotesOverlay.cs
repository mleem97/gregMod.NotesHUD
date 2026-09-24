// NotesOverlay — post-it content: editable title + multiline body rendered in
// Labels (never TextField — IL2CPP TextEditor is stripped, hard crash).
// Editing: click paper/title to focus, type via SafeTextPump, Tab switches
// title/body, Escape closes. Buttons use explicit ClickEvent + manual
// worldBound routing (no EventSystem in game). Autosaves debounced.
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;
using GregMod.NotesHUD.Core;
using GregMod.NotesHUD.Input;

namespace GregMod.NotesHUD.UI
{
    internal static class NotesOverlay
    {
        private enum FocusSlot { Body, Title }

        private sealed class Clickable
        {
            public VisualElement Element;
            public Action Action;
        }

        private static bool _registered;
        private static readonly List<Clickable> _clickables = new List<Clickable>();
        private static DateTime _lastRealClickUtc = DateTime.MinValue;

        private static Label _titleLabel;
        private static Label _bodyLabel;
        private static Label _statusLabel;
        private static VisualElement _paper;
        private static VisualElement _titleRow;
        private static FocusSlot _focus = FocusSlot.Body;
        private static string _editTitle = "";
        private static string _editBody = "";
        private static float _caretAt;
        private static bool _caretOn = true;
        private static float _escapeClosedAt = -10f;
        private const float EscapeCooldownSeconds = 0.35f;

        internal static bool IsVisible { get { try { return NotesPanel.IsVisible; } catch { return false; } } }

        internal static bool IsEditing { get { try { return NotesPanel.IsVisible; } catch { return false; } } }

        internal static void EnsureRegistered() { _registered = true; }

        internal static void Toggle()
        {
            if (!_registered) return;
            try
            {
                if (!NotesPanel.EnsureRoot())
                {
                    MelonLogger.Error("[NotesHUD] Panel not available.");
                    return;
                }
                bool willShow = !NotesPanel.IsVisible;
                if (willShow)
                {
                    NotesStore.EnsureLoaded();
                    _editTitle = NotesStore.Model.Title ?? "";
                    _editBody = NotesStore.Model.Body ?? "";
                    _focus = FocusSlot.Body;
                    SafeTextPump.ResetBackspaceLatch();
                    BuildContent();
                }
                else
                {
                    FlushEdits();
                }
                if (willShow) NotesPanel.Show(); else NotesPanel.Hide();
                try { Input.NotesInputLock.SetOpen(NotesPanel.IsVisible); } catch { }
                try
                {
                    if (GregHost.HasCore) NotesCoreBridge.Report(NotesPanel.IsVisible);
                }
                catch { }
                MelonLogger.Msg("[NotesHUD] Panel " + (NotesPanel.IsVisible ? "shown." : "hidden."));
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[NotesHUD] Panel toggle failed: " + ex.GetBaseException().Message);
            }
        }

        internal static void Close()
        {
            try { if (NotesPanel.IsVisible) Toggle(); } catch { }
        }

        internal static void RouteClicks()
        {
            if (!IsVisible || _clickables.Count == 0) return;
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return;
                if (!mouse.leftButton.wasPressedThisFrame) return;
                Vector2 pos = NotesPanel.ToPanelSpace(mouse.position.ReadValue());
                try
                {
                    if ((DateTime.UtcNow - _lastRealClickUtc).TotalMilliseconds < 400.0) return;
                }
                catch { }
                // Click on paper/title focuses the slot (checked before buttons).
                try
                {
                    if (_titleRow != null)
                    {
                        Rect tb = _titleRow.worldBound;
                        if (tb.width > 0f && tb.height > 0f && tb.Contains(pos))
                        {
                            _focus = FocusSlot.Title;
                            SafeTextPump.ResetBackspaceLatch();
                            RefreshText();
                            return;
                        }
                    }
                    if (_paper != null)
                    {
                        Rect pb = _paper.worldBound;
                        if (pb.width > 0f && pb.height > 0f && pb.Contains(pos))
                        {
                            _focus = FocusSlot.Body;
                            SafeTextPump.ResetBackspaceLatch();
                            RefreshText();
                            return;
                        }
                    }
                }
                catch { }
                for (int i = _clickables.Count - 1; i >= 0; i--)
                {
                    var c = _clickables[i];
                    if (c == null || c.Element == null || c.Action == null) continue;
                    try
                    {
                        if (!c.Element.visible) continue;
                        Rect b = c.Element.worldBound;
                        if (b.width <= 0f || b.height <= 0f) continue;
                        if (b.Contains(pos))
                        {
                            try { _lastRealClickUtc = DateTime.UtcNow; } catch { }
                            c.Action();
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[NotesHUD] Click routing failed: " + ex.Message);
            }
        }

        // Called every frame while visible: Tab switches slot, Escape closes,
        // otherwise pump keys into the focused buffer.
        internal static void TickEditing()
        {
            if (!IsVisible) return;
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return;
                try
                {
                    if (kb.escapeKey.wasPressedThisFrame)
                    {
                        try { _escapeClosedAt = Time.realtimeSinceStartup; } catch { }
                        Close();
                        return;
                    }
                }
                catch { }
                bool tabPressed = false;
                try { tabPressed = kb.tabKey.wasPressedThisFrame; } catch { }
                if (tabPressed)
                {
                    // Tab switches title/body instead of inserting spaces.
                    _focus = (_focus == FocusSlot.Body) ? FocusSlot.Title : FocusSlot.Body;
                    SafeTextPump.ResetBackspaceLatch();
                    RefreshText();
                    return;
                }
                bool changed = false;
                try
                {
                    if (_focus == FocusSlot.Title)
                    {
                        string buf = _editTitle ?? "";
                        if (SafeTextPump.Pump(ref buf, NotesModel.MaxTitleLength, false))
                        {
                            _editTitle = NotesModel.SanitizeTitle(buf);
                            changed = true;
                        }
                    }
                    else
                    {
                        string buf = _editBody ?? "";
                        if (SafeTextPump.Pump(ref buf, NotesModel.MaxBodyLength, true))
                        {
                            _editBody = NotesModel.SanitizeBody(buf);
                            changed = true;
                        }
                    }
                }
                catch { }
                if (changed)
                {
                    NotesStore.Model.SetTitle(_editTitle);
                    NotesStore.Model.SetBody(_editBody);
                    NotesStore.MarkDirty(0.8f);
                    RefreshText();
                }
                // Caret blink.
                try
                {
                    if (Time.realtimeSinceStartup - _caretAt > 0.53f)
                    {
                        _caretAt = Time.realtimeSinceStartup;
                        _caretOn = !_caretOn;
                        RefreshText();
                    }
                }
                catch { }
            }
            catch { }
        }

        internal static bool IsInEscapeCooldown()
        {
            try { return Time.realtimeSinceStartup - _escapeClosedAt < EscapeCooldownSeconds; }
            catch { return false; }
        }

        internal static void RefreshStatus()
        {
            if (!IsVisible) return;
            try
            {
                if (_statusLabel != null)
                {
                    int chars = NotesStore.Model.CharCount;
                    int lines = NotesStore.Model.LineCount;
                    string scope = ModSaveScope.HasScope ? ModSaveScope.CurrentScopeId : "…";
                    _statusLabel.text = chars + "/" + NotesModel.MaxBodyLength + " chars · "
                        + lines + " lines · save " + scope;
                }
            }
            catch { }
        }

        private static void FlushEdits()
        {
            try
            {
                NotesStore.Model.SetTitle(_editTitle);
                NotesStore.Model.SetBody(_editBody);
                NotesStore.SaveNow();
            }
            catch { }
        }

        private static void BuildContent()
        {
            var content = NotesPanel.Content;
            if (content == null) return;
            content.Clear();
            _clickables.Clear();

            // Title row (click to edit title).
            _titleRow = new VisualElement();
            _titleRow.style.flexDirection = FlexDirection.Row;
            _titleRow.style.alignItems = Align.Center;
            _titleRow.style.marginBottom = 6f;
            var pinDot = new Label("●");
            pinDot.style.color = NotesPanel.PinRed;
            pinDot.style.fontSize = 16;
            pinDot.style.marginRight = 6f;
            _titleRow.Add(pinDot);
            _titleLabel = new Label("");
            _titleLabel.style.flexGrow = 1f;
            NotesPanel.ApplyInkFont(_titleLabel, true, 20);
            _titleRow.Add(_titleLabel);
            content.Add(_titleRow);

            // Ruled paper body.
            _paper = new VisualElement();
            _paper.style.flexGrow = 1f;
            _paper.style.backgroundColor = new Color(1.0f, 0.97f, 0.80f);
            _paper.style.borderTopLeftRadius = 3f;
            _paper.style.borderTopRightRadius = 3f;
            _paper.style.borderBottomLeftRadius = 3f;
            _paper.style.borderBottomRightRadius = 3f;
            _paper.style.paddingLeft = 10f;
            _paper.style.paddingRight = 10f;
            _paper.style.paddingTop = 8f;
            _paper.style.paddingBottom = 8f;
            _paper.style.marginBottom = 8f;
            _bodyLabel = new Label("");
            NotesPanel.ApplyInkFont(_bodyLabel, false, 15);
            _bodyLabel.style.whiteSpace = WhiteSpace.Normal;
            _paper.Add(_bodyLabel);
            content.Add(_paper);

            // Status line.
            _statusLabel = new Label("");
            _statusLabel.style.color = NotesPanel.InkFaint;
            _statusLabel.style.fontSize = 12;
            _statusLabel.style.marginBottom = 8f;
            content.Add(_statusLabel);

            // Buttons.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;
            AddBtn(row, "Clear", () =>
            {
                try
                {
                    if (_focus == FocusSlot.Title) { _editTitle = ""; NotesStore.Model.SetTitle(""); }
                    else { _editBody = ""; NotesStore.Model.SetBody(""); }
                    NotesStore.MarkDirty(0.4f);
                    RefreshText();
                }
                catch { }
            }, false);
            AddBtn(row, "Close (F10)", () => { try { Close(); } catch { } }, true);
            content.Add(row);

            var hint = new Label("Click paper/title to focus · Tab switches field · Enter = newline · Ctrl+Backspace clears field · Esc closes");
            hint.style.color = NotesPanel.InkFaint;
            hint.style.fontSize = 11;
            hint.style.whiteSpace = WhiteSpace.Normal;
            content.Add(hint);

            try { _caretAt = Time.realtimeSinceStartup; _caretOn = true; } catch { }
            RefreshText();
            RefreshStatus();
        }

        private static void RefreshText()
        {
            try
            {
                if (_titleLabel != null)
                {
                    string t = string.IsNullOrEmpty(_editTitle) ? "NOTES" : _editTitle;
                    if (_focus == FocusSlot.Title && IsVisible && _caretOn) t += "▌";
                    _titleLabel.text = t;
                    _titleLabel.style.color = string.IsNullOrEmpty(_editTitle)
                        ? NotesPanel.InkFaint : NotesPanel.Ink;
                }
                if (_bodyLabel != null)
                {
                    string b = _editBody ?? "";
                    if (string.IsNullOrEmpty(b)) b = "Type IPs, shop item #s, notes… (click here)";
                    else if (_focus == FocusSlot.Body && _caretOn) b += "▌";
                    // Show tabs as 2 spaces; Label has no TextField caret.
                    b = b.Replace("\t", "  ");
                    _bodyLabel.text = b;
                    _bodyLabel.style.color = string.IsNullOrEmpty(_editBody)
                        ? NotesPanel.InkFaint : NotesPanel.Ink;
                }
                RefreshStatus();
            }
            catch { }
        }

        private static void AddBtn(VisualElement parent, string label, Action action, bool primary)
        {
            var btn = new Button();
            btn.text = label;
            btn.style.height = 34f;
            btn.style.flexGrow = 1f;
            btn.style.marginRight = 6f;
            if (primary) NotesPanel.StyleDarkButton(btn);
            else
            {
                NotesPanel.StyleDarkButton(btn);
                btn.style.backgroundColor = new Color(1.0f, 0.97f, 0.80f);
                btn.style.color = NotesPanel.Ink;
            }
            try
            {
                btn.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                {
                    try { _lastRealClickUtc = DateTime.UtcNow; action?.Invoke(); } catch { }
                }));
            }
            catch { }
            _clickables.Add(new Clickable { Element = btn, Action = action });
            parent.Add(btn);
        }
    }
}
