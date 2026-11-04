using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FamilyAdventure.Presentation
{
    // Shared UI look, matching the game art (FamilyQuest_Assets UI sheets): cream cards in a wooden frame,
    // wooden and leaf-green pill buttons with white rounded lettering, round wooden icon buttons, and the
    // rounded Sniglet typeface (OFL, Resources/Fonts). Panel/button sprites are drawn in code so they stretch.
    public static class UiKit
    {
        public static readonly Color Ink = new Color32(84, 52, 26, 255);
        public static readonly Color Paper = new Color32(252, 245, 226, 255);
        public static readonly Color Green = new Color32(62, 128, 52, 255);
        public static readonly Color Gold = new Color32(246, 198, 98, 255);
        public static readonly Color Wood = new Color32(206, 140, 72, 255);
        public static readonly Color WoodDark = new Color32(112, 66, 30, 255);
        static Font font, bold;
        public static Font Font => font != null ? font : font = Resources.Load<Font>("Fonts/Sniglet-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Font Bold => bold != null ? bold : bold = Resources.Load<Font>("Fonts/Sniglet-ExtraBold") ?? Font;

        public enum Skin { Card, Wood, Leaf, Field, Disc, Soft, Gold, Frame }
        static readonly Dictionary<Skin, Sprite> skins = new Dictionary<Skin, Sprite>();
        public static Sprite Sprite(Skin skin)
        {
            if (skins.TryGetValue(skin, out var s) && s != null) return s;
            const int size = 128; const float radius = 40;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            bool disc = skin == Skin.Disc;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + .5f, fy = y + .5f, d;
                    if (disc) d = size / 2f - Vector2.Distance(new Vector2(fx, fy), new Vector2(size / 2f, size / 2f));
                    else
                    {
                        // Distance inside a rounded rectangle.
                        float qx = Mathf.Max(radius - fx, fx - (size - radius), 0), qy = Mathf.Max(radius - fy, fy - (size - radius), 0);
                        d = radius - Mathf.Sqrt(qx * qx + qy * qy);
                        d = Mathf.Min(d, Mathf.Min(Mathf.Min(fx, fy), Mathf.Min(size - fx, size - fy)));
                    }
                    float alpha = Mathf.Clamp01(d);
                    float v = fy / size; // 0 bottom .. 1 top
                    Color fill, edge; float border = 6;
                    switch (skin)
                    {
                        case Skin.Card: fill = Color.Lerp(new Color32(244, 232, 204, 255), Paper, v); edge = WoodDark; border = 9; break;
                        case Skin.Leaf: fill = Color.Lerp(new Color32(70, 140, 50, 255), new Color32(128, 196, 84, 255), v); edge = new Color32(38, 88, 30, 255); break;
                        case Skin.Gold: fill = Color.Lerp(new Color32(232, 160, 40, 255), new Color32(255, 214, 110, 255), v); edge = new Color32(140, 84, 20, 255); break;
                        case Skin.Field: fill = Color.white; edge = new Color32(190, 150, 104, 255); border = 4; break;
                        case Skin.Soft: fill = Color.white; edge = Color.white; border = 0; break;
                        case Skin.Frame: fill = Color.clear; edge = WoodDark; border = 12; break;
                        default:
                            // Wood with a soft grain.
                            float grain = .06f * Mathf.Sin(fy * .9f + Mathf.Sin(fx * .05f) * 3) + .03f * Mathf.Sin(fy * 2.3f);
                            fill = Color.Lerp(new Color32(176, 110, 52, 255), new Color32(232, 172, 104, 255), v) * (1 + grain); fill.a = 1;
                            edge = WoodDark; break;
                    }
                    Color c = d < border ? edge : fill;
                    // Light rim just inside the frame, and a glossy top.
                    if (skin == Skin.Frame && d < border) c = Color.Lerp(new Color32(150, 92, 42, 255), new Color32(222, 160, 92, 255), Mathf.Abs(d - border / 2) < border / 3 ? 1 : 0);
                    if (skin != Skin.Frame && border > 0 && d >= border && d < border + 3) c = Color.Lerp(c, Color.white, .35f);
                    if (skin != Skin.Soft && skin != Skin.Field && skin != Skin.Frame && d >= border && v > .78f) c = Color.Lerp(c, Color.white, .12f);
                    c.a *= alpha;
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(false);
            float cut = disc ? 0 : radius + 4;
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(cut, cut, cut, cut));
            skins[skin] = s; return s;
        }
        public static Image Skinned(GameObject go, Skin skin, bool shadow = true)
        {
            var image = go.GetComponent<Image>(); if (image == null) image = go.AddComponent<Image>();
            image.sprite = Sprite(skin); image.type = skin == Skin.Disc ? Image.Type.Simple : Image.Type.Sliced; image.color = Color.white;
            // Cards get a thicker wooden frame than buttons.
            image.pixelsPerUnitMultiplier = skin == Skin.Card || skin == Skin.Frame ? 1.25f : 2.2f;
            if (shadow && go.GetComponent<Shadow>() == null) { var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(.2f, .1f, .02f, .35f); sh.effectDistance = new Vector2(0, -5); }
            return image;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        public static void Anchor(RectTransform r, float x0, float y0, float x1, float y1, float margin = 0)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = new Vector2(margin, margin); r.offsetMax = new Vector2(-margin, -margin);
        }
        // Panels take the theme from their colour: paper = framed card, green = leaf, gold = gold,
        // light warm colours = round wooden button base; anything else stays a rounded tinted panel.
        public static RectTransform Panel(string name, Transform parent, Color color)
        {
            var r = Rect(name, parent);
            if (color == Paper) { Skinned(r.gameObject, Skin.Card); Decorate(r); }
            else if (color == Green) Skinned(r.gameObject, Skin.Leaf);
            else if (color == Gold) Skinned(r.gameObject, Skin.Gold, false);
            else { var image = Skinned(r.gameObject, Skin.Soft, false); image.color = color; }
            return r;
        }
        // Leaf bunches and a flower growing over the corners of a framed card (art: Resources/Decor).
        static readonly Dictionary<string, Sprite> decor = new Dictionary<string, Sprite>();
        public static Sprite Decor(string name)
        {
            if (decor.TryGetValue(name, out var s) && s != null) return s;
            var t = Resources.Load<Texture2D>("Decor/" + name); if (t == null) return null;
            s = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100); decor[name] = s; return s;
        }
        static void Ornament(RectTransform parent, string art, Vector2 corner, Vector2 offset, float width, float angle)
        {
            var sprite = Decor(art); if (sprite == null) return;
            var r = Rect("Decor " + art, parent); r.anchorMin = r.anchorMax = corner;
            r.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width); r.anchoredPosition = offset; r.localRotation = Quaternion.Euler(0, 0, angle);
            var image = r.gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        public static void Decorate(RectTransform card)
        {
            // Big bunches on the top corners, smaller ones below, a flower on each bunch and a vine along the top.
            Ornament(card, "leaves", new Vector2(0, 1), new Vector2(14, -6), 150, 28);
            Ornament(card, "flower", new Vector2(0, 1), new Vector2(46, 10), 66, 0);
            Ornament(card, "leaves-small", new Vector2(1, 1), new Vector2(-12, -4), 124, -26);
            Ornament(card, "flower", new Vector2(1, 1), new Vector2(-40, 12), 52, 0);
            Ornament(card, "leaves-small", new Vector2(.5f, 1), new Vector2(-30, 4), 70, 70);
            Ornament(card, "leaves-small", new Vector2(.5f, 1), new Vector2(30, 4), 70, -70);
            Ornament(card, "leaves-small", new Vector2(1, 0), new Vector2(-10, 14), 104, -150);
            Ornament(card, "flower", new Vector2(1, 0), new Vector2(-38, 14), 50, 0);
            Ornament(card, "leaves", new Vector2(0, 0), new Vector2(12, 12), 100, 160);
        }
        // Round wooden button base for an icon.
        public static RectTransform Disc(string name, Transform parent)
        {
            var r = Rect(name, parent); Skinned(r.gameObject, Skin.Disc); return r;
        }
        public static Text Label(Transform parent, string text, int size = 22, Color? color = null, float height = 0)
        {
            var r = Rect("Text", parent); var label = r.gameObject.AddComponent<Text>();
            // Headings use the extra-bold face.
            label.font = size >= 28 ? Bold : Font; label.text = text; label.fontSize = size; label.color = color ?? Ink; label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false; label.lineSpacing = 1.05f;
            // Text calculates its wrapped height; large legacy spacer heights caused unnecessary scrolling.
            var layout = r.gameObject.AddComponent<LayoutElement>(); layout.minHeight = size + 6;
            layout.preferredHeight = -1;
            return label;
        }
        public static Button Button(Transform parent, string text, Action action, bool primary = false, float height = 60, string icon = null)
        {
            var r = Rect(text, parent); var image = Skinned(r.gameObject, primary ? Skin.Leaf : Skin.Wood);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1f, .96f, .86f); colors.pressedColor = new Color(.8f, .78f, .72f); colors.disabledColor = new Color(.7f, .7f, .7f, .6f); button.colors = colors;
            var label = Label(r, text, 22, Color.white); label.font = Bold; label.alignment = TextAnchor.MiddleCenter;
            // Long labels (e.g. microphone names) shrink instead of being cut off.
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 22;
            Anchor(label.rectTransform, 0, 0, 1, 1, 8);
            var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.25f, .14f, .05f, .8f); outline.effectDistance = new Vector2(1.6f, -1.6f);
            if (!string.IsNullOrEmpty(icon))
            {
                var glyph = Rect("Icon", r); glyph.anchorMin = new Vector2(0, .5f); glyph.anchorMax = new Vector2(0, .5f);
                glyph.sizeDelta = new Vector2(height * .62f, height * .62f); glyph.anchoredPosition = new Vector2(height * .5f, 0);
                var g = glyph.gameObject.AddComponent<IntroIcon>(); g.symbol = icon; g.color = Color.white; g.raycastTarget = false;
                label.rectTransform.offsetMin = new Vector2(height * .85f, 8);
            }
            button.onClick.AddListener(() => action()); return button;
        }
        public static Button IconButton(Transform parent, string name, Sprite sprite, Action action)
        {
            var r = Rect(name, parent); var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.color = Color.white;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1f, .92f, .58f); colors.pressedColor = new Color(.84f, .84f, .84f); button.colors = colors;
            button.onClick.AddListener(() => action()); return button;
        }
        public static InputField Input(Transform parent, string placeholder, bool secret = false)
        {
            var r = Rect(placeholder, parent); Skinned(r.gameObject, Skin.Field, false); r.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            var input = r.gameObject.AddComponent<InputField>(); input.characterLimit = secret ? 6 : 24;
            var text = Label(r, "", 22); Anchor(text.rectTransform, 0, 0, 1, 1, 12); text.alignment = TextAnchor.MiddleLeft;
            var hint = Label(r, placeholder, 20, new Color(.55f, .45f, .36f)); Anchor(hint.rectTransform, 0, 0, 1, 1, 12); hint.alignment = TextAnchor.MiddleLeft;
            // A field is one line inside a fixed-height box. Truncating drops a line that does not fit
            // whole, which with the tall display face left the PIN and its placeholder invisible.
            text.verticalOverflow = hint.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            input.textComponent = text; input.placeholder = hint;
            input.contentType = secret ? InputField.ContentType.Password : InputField.ContentType.Standard;
            if (secret) { input.characterValidation = InputField.CharacterValidation.Integer; input.keyboardType = TouchScreenKeyboardType.NumberPad; }
            return input;
        }
        public static RectTransform Stack(Transform parent, string name, int padding = 0)
        {
            var r = Rect(name, parent); var layout = r.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding); layout.spacing = 12; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            return r;
        }
        public static RectTransform Scroll(Transform parent)
        {
            var frame = Rect("Scroll", parent); Anchor(frame, 0, 0, 1, 1);
            var view = Rect("Viewport", frame); Anchor(view, 0, 0, 1, 1, 10); view.gameObject.AddComponent<RectMask2D>();
            // The viewport needs a graphic that takes raycasts, otherwise a drag or a wheel over the
            // empty parts of a card reaches nothing and the panel cannot be scrolled - which hid the
            // lower half of the parent area, including "Mulai ulang onboarding".
            var catcher = view.gameObject.AddComponent<Image>(); catcher.color = new Color(0, 0, 0, 0); catcher.raycastTarget = true;
            // Keep text clear of the leaf ornaments on the card corners.
            view.offsetMin = new Vector2(20, 16); view.offsetMax = new Vector2(-20, -40);
            var content = Stack(view, "Content", 22); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = frame.gameObject.AddComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34;
            return content;
        }
        // Wooden title plate with a leaf on each side (for headings on screens and menus).
        public static Text Banner(Transform parent, string text, int size = 40)
        {
            var plate = Rect("Banner", parent); Skinned(plate.gameObject, Skin.Wood);
            plate.gameObject.AddComponent<LayoutElement>().preferredHeight = size * 1.9f;
            for (int side = 0; side < 2; side++)
            {
                var leaf = Rect("Leaf", plate); leaf.anchorMin = leaf.anchorMax = new Vector2(side, .5f); leaf.sizeDelta = new Vector2(size * 1.1f, size * 1.1f);
                leaf.anchoredPosition = new Vector2(side == 0 ? -4 : 4, 0); leaf.localRotation = Quaternion.Euler(0, 0, side == 0 ? 30 : -30);
                var art = Decor("leaves-small");
                if (art != null) { var img = leaf.gameObject.AddComponent<Image>(); img.sprite = art; img.preserveAspect = true; img.raycastTarget = false; leaf.sizeDelta *= 1.3f; }
                else { var g = leaf.gameObject.AddComponent<IntroIcon>(); g.symbol = "leaf"; g.color = new Color32(96, 172, 70, 255); g.raycastTarget = false; }
                leaf.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            var label = Label(plate, text, size, Color.white); label.font = Bold; label.alignment = TextAnchor.MiddleCenter; Anchor(label.rectTransform, 0, 0, 1, 1, 10);
            // Long titles shrink to fit the plate instead of disappearing.
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = size;
            var o = label.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.3f, .16f, .05f, .9f); o.effectDistance = new Vector2(2, -2);
            return label;
        }
    }
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        Rect last;
        Vector2 dimensions;
        void Update()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            if (area == last && dimensions == new Vector2(Screen.width, Screen.height)) return;
            last = area; dimensions = new Vector2(Screen.width, Screen.height);
            var r = (RectTransform)transform; r.anchorMin = area.min / dimensions; r.anchorMax = area.max / dimensions; r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
