using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace SpaceGame.Drawing;

public static class DrawUtils
{
    public static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, float x1, float y1, float x2, float y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;

        float angle = MathF.Atan2(dy, dx);
        spriteBatch.Draw(pixel, new Vector2(x1, y1), null, color, angle,
            Vector2.Zero, new Vector2(len / pixel.Width, 1f),
            SpriteEffects.None, 0);
    }

    public static void DrawRect(SpriteBatch spriteBatch, Texture2D pixel, float x, float y, float w, float h, Color color)
    {
        DrawLine(spriteBatch, pixel, x, y, x + w, y, color);
        DrawLine(spriteBatch, pixel, x + w, y, x + w, y + h, color);
        DrawLine(spriteBatch, pixel, x + w, y + h, x, y + h, color);
        DrawLine(spriteBatch, pixel, x, y + h, x, y, color);
    }

    public static void DrawCircle(SpriteBatch spriteBatch, Texture2D pixel, float cx, float cy, float r, Color color)
    {
        int segments = Math.Max(8, (int)(r * 0.5f));
        for (int i = 0; i < segments; i++)
        {
            float a1 = MathF.PI * 2 * i / segments;
            float a2 = MathF.PI * 2 * (i + 1) / segments;
            DrawLine(spriteBatch, pixel,
                cx + MathF.Cos(a1) * r, cy + MathF.Sin(a1) * r,
                cx + MathF.Cos(a2) * r, cy + MathF.Sin(a2) * r,
                color);
        }
    }

    public static void FillCircle(SpriteBatch spriteBatch, Texture2D pixel, float cx, float cy, float r, Color color)
    {
        int segments = Math.Max(16, (int)(r * 0.8f));
        float angleStep = MathF.PI * 2 / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = angleStep * i;
            float a2 = angleStep * (i + 1);
            float x1 = cx + MathF.Cos(a1) * r;
            float y1 = cy + MathF.Sin(a1) * r;
            float x2 = cx + MathF.Cos(a2) * r;
            float y2 = cy + MathF.Sin(a2) * r;
            DrawLine(spriteBatch, pixel, cx, cy, x1, y1, color);
            DrawLine(spriteBatch, pixel, x1, y1, x2, y2, color);
        }
    }

    public static void DrawSpacedText(SpriteBatch spriteBatch, SpriteFont font, string text, float x, float y, Color color, float scale = 1f)
    {
        if (text.Length == 0) return;

        string[] parts = text.Split(' ');
        if (parts.Length <= 1)
        {
            spriteBatch.DrawString(font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0);
            return;
        }

        float spaceW = 8f * scale;
        float cx = x;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                spriteBatch.DrawString(font, parts[i], new Vector2(cx, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0);
                cx += font.MeasureString(parts[i]).X * scale + spaceW;
            }
            else
            {
                cx += spaceW;
            }
        }
    }

    public static List<string> WordWrap(SpriteFont font, string text, float maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        text = text.Replace("\r\n", "\n");
        var paragraphs = text.Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (paragraphs[p].Length == 0)
            {
                if (p < paragraphs.Length - 1)
                    lines.Add("");
                continue;
            }

            var words = paragraphs[p].Split(' ');
            var currentLine = new List<string>();
            float lineWidth = 0;

            void FlushLine()
            {
                if (currentLine.Count > 0)
                    lines.Add(string.Join(" ", currentLine));
                currentLine.Clear();
                lineWidth = 0;
            }

            foreach (var word in words)
            {
                float wordWidth = font.MeasureString(word).X;
                if (currentLine.Count > 0)
                    wordWidth += 8f;

                if (lineWidth + wordWidth > maxWidth && currentLine.Count > 0)
                    FlushLine();

                if (currentLine.Count > 0) lineWidth += 8f;
                currentLine.Add(word);
                lineWidth += font.MeasureString(word).X;
            }

            if (currentLine.Count > 0)
                lines.Add(string.Join(" ", currentLine));
        }

        return lines;
    }

    public static Color GetFactionColor(string? faction)
    {
        return faction switch
        {
            "Atlas Federation" => new Color(60, 130, 255),
            "Trigor Empire" => new Color(255, 60, 30),
            "Independent" => new Color(60, 200, 60),
            _ => Color.Gray
        };
    }

    public static Color ParseColor(string hex)
    {
        if (hex.StartsWith("#")) hex = hex[1..];
        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..], 16);
            return new Color(r, g, b);
        }
        return Color.White;
    }

    public static string SanitizeText(string text)
    {
        return text.Replace('\u2014', '-').Replace('\u2013', '-')
            .Replace('\u201C', '"').Replace('\u201D', '"')
            .Replace('\u2018', '\'').Replace('\u2019', '\'')
            .Replace('\u2026', '.').Replace('\u00A0', ' ');
    }

    public static string GetDirection(Systems.Vector2 from, Systems.Vector2 to)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float angle = MathF.Atan2(dy, dx);

        if (angle < -MathF.PI * 0.75f) return "W";
        if (angle < -MathF.PI * 0.25f) return "NW";
        if (angle < MathF.PI * 0.25f) return "N";
        if (angle < MathF.PI * 0.75f) return "NE";
        return "E";
    }
}
