using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using UnityEngine;

/// <summary>
/// Renders text to a Texture2D using System.Drawing (plain GDI+ / .NET
/// Framework text rendering) instead of relying on Unity's Font system.
///
/// This game replaced Unity's built-in text pipeline with its own Cocos2d-
/// derived CCText/CCFont system, so Unity's Font objects never got any
/// glyphs loaded into them - OnGUI text is a dead end no matter which
/// Font we hand it. System.Drawing is a completely separate rendering
/// path (part of the .NET Framework, not Unity or the game), so it works
/// regardless of what either engine's font system is doing.
///
/// Usage: GUI.DrawTexture(rect, TextRasterizer.GetTexture("Host", 14, Color.White));
/// </summary>
public static class TextRasterizer
{
    private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

    public static Texture2D GetTexture(string text, int fontSize, UnityEngine.Color color)
    {
        if (string.IsNullOrEmpty(text)) text = " ";

        var key = text + "|" + fontSize + "|" + color.r + "," + color.g + "," + color.b + "," + color.a;

        Texture2D cached;
        if (_cache.TryGetValue(key, out cached) && cached != null)
            return cached;

        var drawingColor = System.Drawing.Color.FromArgb(
            (int)(color.a * 255),
            (int)(color.r * 255),
            (int)(color.g * 255),
            (int)(color.b * 255)
        );

        Texture2D tex = RenderToTexture(text, fontSize, drawingColor);
        _cache[key] = tex;
        return tex;
    }

    private static Texture2D RenderToTexture(string text, int fontSize, System.Drawing.Color color)
    {
        using (var font = new System.Drawing.Font("Arial", fontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel))
        {
            System.Drawing.SizeF measured;
            using (var measureBmp = new System.Drawing.Bitmap(1, 1))
            using (var measureG = System.Drawing.Graphics.FromImage(measureBmp))
            {
                measured = measureG.MeasureString(text, font);
            }

            int width = Math.Max(1, (int)Math.Ceiling(measured.Width));
            int height = Math.Max(1, (int)Math.Ceiling(measured.Height));

            byte[] pngBytes;
            using (var bmp = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    using (var brush = new System.Drawing.SolidBrush(color))
                    {
                        g.DrawString(text, font, brush, 0, 0);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngBytes = ms.ToArray();
                }
            }

            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            tex.LoadImage(pngBytes);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }

    /// <summary>Clears the texture cache (e.g. if you want to force a re-render).</summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}
