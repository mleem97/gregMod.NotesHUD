// NotesPanel — standalone UIToolkit host in post-it look (no gregCore
// dependency, same architecture as TrainerPanel/MusicPlayer PanelChrome).
// Own GameObject + UIDocument + PanelSettings, own drag + cursor handling,
// manual click routing by NotesOverlay (game ships without EventSystem),
// game font lookup (Toolkit default font renders invisible on IL2CPP).
using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;

namespace GregMod.NotesHUD.UI
{
    internal static class NotesPanel
    {
        // Post-it theme: warm paper, dark ink, red pin header.
        internal static readonly Color Paper = new Color(1.0f, 0.95f, 0.72f, 0.98f);
        internal static readonly Color PaperEdge = new Color(0.85f, 0.74f, 0.42f, 1.0f);
        internal static readonly Color Ink = new Color(0.16f, 0.14f, 0.10f);
        internal static readonly Color InkFaint = new Color(0.35f, 0.31f, 0.24f);
        internal static readonly Color PinRed = new Color(0.78f, 0.22f, 0.20f);
        internal static readonly Color BtnDark = new Color(0.16f, 0.14f, 0.10f);
        internal static readonly Color BtnInkOnDark = new Color(1.0f, 0.95f, 0.72f);

        internal const float PanelWidth = 420f;
        internal const float PanelHeight = 560f;

        private static GameObject _host;
        private static UIDocument _uiDoc;
        private static VisualElement _root;
        private static VisualElement _panel;
        private static VisualElement _dragHandle;
        private static bool _visible;
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static Font _font;

        internal static VisualElement Content { get; private set; }
        internal static bool IsVisible { get { return _visible; } }

        internal static Vector2 ToPanelSpace(Vector2 screenPoint)
        {
            try
            {
                IPanel panel = _root != null ? _root.panel : null;
                if (panel != null) return RuntimePanelUtils.ScreenToPanel(panel, screenPoint);
            }
            catch { }
            try { return new Vector2(screenPoint.x, Screen.height - screenPoint.y); } catch { return screenPoint; }
        }

        internal static bool EnsureRoot()
        {
            try
            {
                if (_panel != null) return true;

                _host = new GameObject("NotesHUD_UIHost");
                UnityEngine.Object.DontDestroyOnLoad(_host);

                var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "NotesHUD_PanelSettings";
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                panelSettings.sortingOrder = 1000;

                _uiDoc = _host.AddComponent<UIDocument>();
                _uiDoc.panelSettings = panelSettings;
                _root = _uiDoc.rootVisualElement;
                _root.pickingMode = PickingMode.Position;
                _root.style.display = DisplayStyle.None;

                _panel = new VisualElement();
                _panel.name = "NotesPanel";
                _panel.style.position = Position.Absolute;
                _panel.style.left = 24f;
                _panel.style.top = 70f;
                _panel.style.width = PanelWidth;
                _panel.style.height = PanelHeight;
                _panel.style.backgroundColor = Paper;
                _panel.style.borderTopLeftRadius = 4f;
                _panel.style.borderTopRightRadius = 4f;
                _panel.style.borderBottomLeftRadius = 4f;
                _panel.style.borderBottomRightRadius = 4f;
                _panel.style.borderLeftWidth = 2f;
                _panel.style.borderRightWidth = 2f;
                _panel.style.borderTopWidth = 2f;
                _panel.style.borderBottomWidth = 2f;
                _panel.style.borderLeftColor = PaperEdge;
                _panel.style.borderRightColor = PaperEdge;
                _panel.style.borderTopColor = PaperEdge;
                _panel.style.borderBottomColor = PaperEdge;
                _panel.style.paddingLeft = 14f;
                _panel.style.paddingRight = 14f;
                _panel.style.paddingTop = 10f;
                _panel.style.paddingBottom = 10f;

                _dragHandle = new VisualElement();
                _dragHandle.name = "DragHandle";
                _dragHandle.style.height = 30f;
                _dragHandle.style.flexDirection = FlexDirection.Row;
                _dragHandle.style.alignItems = Align.Center;
                var pin = new Label("●");
                pin.style.color = PinRed;
                pin.style.fontSize = 20;
                pin.style.marginRight = 8f;
                _dragHandle.Add(pin);
                var handleTitle = new Label("NOTES  ·  drag here");
                handleTitle.style.color = InkFaint;
                handleTitle.style.fontSize = 13;
                handleTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                _dragHandle.Add(handleTitle);
                _panel.Add(_dragHandle);

                Content = new VisualElement();
                Content.name = "Content";
                Content.style.flexGrow = 1f;
                _panel.Add(Content);

                _root.Add(_panel);
                _root.style.display = DisplayStyle.Flex;
                _visible = false;
                Hide();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[NotesHUD] Panel root failed: " + ex.GetBaseException().Message);
                return false;
            }
        }

        internal static void Show()
        {
            if (_panel == null) return;
            _visible = true;
            try { if (_root != null) _root.style.display = DisplayStyle.Flex; } catch { }
            RefreshCursor();
        }

        internal static void Hide()
        {
            _visible = false;
            _dragging = false;
            try { if (_root != null) _root.style.display = DisplayStyle.None; } catch { }
            RefreshCursor();
        }

        internal static void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        internal static void Tick()
        {
            if (!_visible || _panel == null) return;
            try
            {
                if (UnityEngine.Cursor.lockState != CursorLockMode.None || !UnityEngine.Cursor.visible)
                    RefreshCursor();
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return;
                Vector2 pos = ToPanelSpace(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame && !_dragging)
                {
                    try
                    {
                        Rect b = _dragHandle.worldBound;
                        if (b.width > 0f && b.height > 0f && b.Contains(pos))
                        {
                            _dragging = true;
                            Rect r = _panel.worldBound;
                            _dragOffset = new Vector2(pos.x - r.x, pos.y - r.y);
                        }
                    }
                    catch { }
                    return;
                }
                if (_dragging)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        float nx = pos.x - _dragOffset.x;
                        float ny = pos.y - _dragOffset.y;
                        if (nx < 0f) nx = 0f;
                        if (ny < 0f) ny = 0f;
                        _panel.style.left = nx;
                        _panel.style.top = ny;
                    }
                    else _dragging = false;
                }
            }
            catch { }
        }

        internal static void ApplyInkFont(VisualElement el, bool bold, int size)
        {
            try
            {
                if (el is Label l)
                {
                    l.style.color = Ink;
                    l.style.fontSize = size;
                    l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
                    try { if (GameFont != null) l.style.unityFont = GameFont; } catch { }
                }
                else if (el is Button b)
                {
                    b.style.fontSize = size;
                    try { if (GameFont != null) b.style.unityFont = GameFont; } catch { }
                }
            }
            catch { }
        }

        internal static void StyleDarkButton(Button b)
        {
            try
            {
                b.style.backgroundColor = BtnDark;
                b.style.color = BtnInkOnDark;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                b.style.borderTopLeftRadius = 6f;
                b.style.borderTopRightRadius = 6f;
                b.style.borderBottomLeftRadius = 6f;
                b.style.borderBottomRightRadius = 6f;
                b.style.unityTextAlign = TextAnchor.MiddleCenter;
                try { if (GameFont != null) b.style.unityFont = GameFont; } catch { }
            }
            catch { }
        }

        private static void RefreshCursor()
        {
            try
            {
                if (_visible)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
            catch { }
        }

        internal static Font GameFont
        {
            get
            {
                if (_font != null) return _font;
                try
                {
                    var fonts = Resources.FindObjectsOfTypeAll<Font>();
                    if (fonts != null)
                        foreach (var f in fonts)
                            if (f != null) { _font = f; return _font; }
                }
                catch { }
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                return _font;
            }
        }
    }
}
