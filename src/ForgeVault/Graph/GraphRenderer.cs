using ForgeVault.Core;
using SkiaSharp;
using System.Windows.Media.Imaging;

namespace ForgeVault.Graph;

public sealed class GraphRenderer
{
    public BitmapSource RenderGraph(List<NoteModel> notes, int width, int height)
    {
        if (width <= 0 || height <= 0) width = height = 600;

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(30, 30, 30));

        var nodes = notes.Select(n => new Node
        {
            Title = n.Title,
            X = (float)(Random.Shared.NextDouble() * width),
            Y = (float)(Random.Shared.NextDouble() * height)
        }).ToList();

        var titleToNode = nodes.ToDictionary(n => n.Title, StringComparer.OrdinalIgnoreCase);

        using var linePaint = new SKPaint { Color = new SKColor(80, 80, 80), StrokeWidth = 1, IsAntialias = true };
        using var nodePaint = new SKPaint { Color = new SKColor(0, 122, 204), IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 12, IsAntialias = true };

        // Simple force-directed-ish relaxation
        for (int step = 0; step < 100; step++)
        {
            foreach (var a in nodes)
            {
                foreach (var b in nodes)
                {
                    if (a == b) continue;
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy) + 0.1f;
                    var force = 2000f / (dist * dist);
                    a.Dx -= force * dx / dist;
                    a.Dy -= force * dy / dist;
                }
            }

            for (int i = 0; i < notes.Count; i++)
            {
                foreach (var link in notes[i].OutgoingLinks)
                {
                    if (titleToNode.TryGetValue(link, out var target))
                    {
                        var source = nodes[i];
                        var dx = target.X - source.X;
                        var dy = target.Y - source.Y;
                        var dist = MathF.Sqrt(dx * dx + dy * dy) + 0.1f;
                        var force = (dist - 100f) * 0.02f;
                        source.Dx += force * dx / dist;
                        source.Dy += force * dy / dist;
                        target.Dx -= force * dx / dist;
                        target.Dy -= force * dy / dist;
                    }
                }
            }

            foreach (var node in nodes)
            {
                node.X = Math.Clamp(node.X + node.Dx * 0.1f, 20, width - 20);
                node.Y = Math.Clamp(node.Y + node.Dy * 0.1f, 20, height - 20);
                node.Dx = 0;
                node.Dy = 0;
            }
        }

        // Draw edges
        for (int i = 0; i < notes.Count; i++)
        {
            var source = nodes[i];
            foreach (var link in notes[i].OutgoingLinks)
            {
                if (titleToNode.TryGetValue(link, out var target))
                {
                    canvas.DrawLine(source.X, source.Y, target.X, target.Y, linePaint);
                }
            }
        }

        // Draw nodes
        foreach (var node in nodes)
        {
            canvas.DrawCircle(node.X, node.Y, 6, nodePaint);
            canvas.DrawText(node.Title, node.X + 8, node.Y + 4, textPaint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private class Node
    {
        public string Title { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Dx { get; set; }
        public float Dy { get; set; }
    }
}
