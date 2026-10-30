using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SonicDeck.Visuals;

public static class AppIconGenerator
{
    public static byte[] RenderPngFrame(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas c = surface.Canvas;
        c.Clear(SKColors.Transparent);

        float s = size;
        float pad = s * 0.05f;
        float corner = s * 0.24f;
        SKRect badgeRect = new(pad, pad, s - pad, s - pad);

        // 1. Deep Obsidian/Cyberpunk Squircle Background
        using (var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(s, s),
                new[]
                {
                    new SKColor(15, 23, 42),
                    new SKColor(7, 10, 20),
                    new SKColor(22, 12, 38)
                },
                new[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            c.DrawRoundRect(badgeRect, corner, corner, bgPaint);
        }

        // 2. Outer Neon Cyan-to-Violet Glowing Border Ring
        using (var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1.5f, s * 0.042f),
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(s, s),
                new[]
                {
                    new SKColor(6, 182, 212),
                    new SKColor(139, 92, 246),
                    new SKColor(244, 63, 94)
                },
                null,
                SKShaderTileMode.Clamp)
        })
        {
            c.DrawRoundRect(badgeRect, corner, corner, borderPaint);
        }

        // 3. Subtle Inner Sonic Pulse Ring
        float cx = s * 0.5f;
        float cy = s * 0.5f;
        using (var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, s * 0.02f),
            Color = new SKColor(56, 189, 248, 55)
        })
        {
            c.DrawCircle(cx, cy, s * 0.34f, ringPaint);
        }

        // 4. 7 Glowing Equalizer Spectrum Bars (Winamp + Modern Studio style)
        float[] barHeights = { 0.32f, 0.58f, 0.84f, 0.48f, 0.92f, 0.68f, 0.38f };
        int barCount = barHeights.Length;
        float totalBarAreaW = s * 0.64f;
        float startX = (s - totalBarAreaW) * 0.5f;
        float slotW = totalBarAreaW / barCount;
        float barW = slotW * 0.66f;
        float baseLine = s * 0.78f;
        float maxBarH = s * 0.56f;

        using var barGlow = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            MaskFilter = size >= 48 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.03f) : null
        };

        using var barFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, baseLine),
                new SKPoint(0, baseLine - maxBarH),
                new[]
                {
                    new SKColor(6, 182, 212),    // Bottom Cyan
                    new SKColor(168, 85, 247),   // Mid Purple
                    new SKColor(244, 63, 94),    // Top Neon Rose
                    new SKColor(250, 204, 21)    // Peak Gold
                },
                new[] { 0f, 0.45f, 0.82f, 1f },
                SKShaderTileMode.Clamp)
        };

        using var capPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(254, 240, 138)
        };

        for (int i = 0; i < barCount; i++)
        {
            float bh = barHeights[i] * maxBarH;
            float x = startX + i * slotW + (slotW - barW) * 0.5f;
            float y = baseLine - bh;
            float rad = Math.Max(1f, barW * 0.35f);

            if (size >= 48)
            {
                barGlow.Color = new SKColor(56, 189, 248, 90);
                c.DrawRoundRect(x, y, barW, bh, rad, rad, barGlow);
            }

            c.DrawRoundRect(x, y, barW, bh, rad, rad, barFill);

            // Floating Peak Hold Cap above each bar
            float capH = Math.Max(1.5f, s * 0.035f);
            float capY = y - capH * 1.65f;
            c.DrawRoundRect(x, capY, barW, capH, rad * 0.5f, rad * 0.5f, capPaint);
        }

        // 5. Golden Parametric EQ S-Curve cutting across the bars
        using var eqPath = new SKPath();
        eqPath.MoveTo(startX, cy + s * 0.06f);
        eqPath.CubicTo(
            startX + totalBarAreaW * 0.33f, cy - s * 0.24f,
            startX + totalBarAreaW * 0.66f, cy + s * 0.20f,
            startX + totalBarAreaW, cy - s * 0.12f);

        using var eqStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1.5f, s * 0.042f),
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(250, 204, 21)
        };
        c.DrawPath(eqPath, eqStroke);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void WriteMultiSizeIcoFile(string icoPath)
    {
        int[] sizes = { 256, 64, 48, 32, 16 };
        byte[][] pngFrames = sizes.Select(RenderPngFrame).ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);
        using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR header
        bw.Write((ushort)0);               // Reserved
        bw.Write((ushort)1);               // Type: 1 = ICO
        bw.Write((ushort)sizes.Length);    // Image count

        int offset = 6 + (16 * sizes.Length);
        for (int i = 0; i < sizes.Length; i++)
        {
            int sz = sizes[i];
            byte[] png = pngFrames[i];

            bw.Write((byte)(sz >= 256 ? 0 : sz)); // Width (0 means 256)
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // Height
            bw.Write((byte)0);                    // Color palette
            bw.Write((byte)0);                    // Reserved
            bw.Write((ushort)1);                  // Color planes
            bw.Write((ushort)32);                 // Bits per pixel
            bw.Write((uint)png.Length);           // Data size
            bw.Write((uint)offset);               // Data offset

            offset += png.Length;
        }

        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write(pngFrames[i]);
        }
    }

    public static ImageSource CreateWindowIconSource()
    {
        byte[] png = RenderPngFrame(256);
        using var ms = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
