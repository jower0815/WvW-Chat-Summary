using System.Drawing.Drawing2D;

namespace WvWSummaryTool;

internal sealed class FightMapControl : Control
{
    private readonly Dictionary<uint, Bitmap> _backgrounds = [];
    private FightLocation? _location;
    private MapDisplay? _map;
    private double _seconds = double.MaxValue;

    public FightMapControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(24, 29, 35);
        ForeColor = Color.WhiteSmoke;
        Dock = DockStyle.Fill;
        MinimumSize = new Size(250, 220);
    }

    public void SetTime(double seconds) { _seconds = seconds; Invalidate(); }

    public void ShowFight(FightSummary? fight)
    {
        _location = fight?.Location;
        _map = _location is null ? null : FightLocationResolver.GetMapDisplay(_location.MapId);
        _seconds = double.MaxValue;
        if (_location is not null && _map is not null)
        {
            var id = _location.MapId == 96 ? 95U : _location.MapId;
            if (!_backgrounds.ContainsKey(id))
            {
                var extension = id == 968 ? "jpg" : "png";
                using var stream = typeof(FightMapControl).Assembly.GetManifestResourceStream($"WvWSummaryTool.data.maps.{id}.{extension}");
                if (stream is not null)
                {
                    using var image = Image.FromStream(stream);
                    _backgrounds.Add(id, new Bitmap(image));
                }
            }
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_map is null || _location is null)
        {
            TextRenderer.DrawText(g, "Für diesen Fight sind keine Kartendaten verfügbar.", Font,
                ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        var area = new RectangleF(18, 38, Math.Max(1, Width - 36), Math.Max(1, Height - 100));
        var imageId = _location.MapId == 96 ? 95U : _location.MapId;
        _backgrounds.TryGetValue(imageId, out var image);
        var ratio = image is null ? _map.Width / _map.Height : (double)image.Width / image.Height;
        if (area.Width / area.Height > ratio)
        {
            var width = (float)(area.Height * ratio);
            area.X += (area.Width - width) / 2;
            area.Width = width;
        }
        else
        {
            var height = (float)(area.Width / ratio);
            area.Y += (area.Height - height) / 2;
            area.Height = height;
        }
        PointF Project(double x, double y)
        {
            if (image is not null) (x, y) = FightLocationResolver.ToBackgroundCoordinates(_location.MapId, x, y);
            return new(area.Left + (float)x * area.Width, area.Top + (float)y * area.Height);
        }
        using var grid = new Pen(Color.FromArgb(60, 130, 145, 155));
        if (image is not null) g.DrawImage(image, area);
        else
        {
            for (var i = 0; i <= 10; i++)
            {
                g.DrawLine(grid, Project(i / 10.0, 0), Project(i / 10.0, 1));
                g.DrawLine(grid, Project(0, i / 10.0), Project(1, i / 10.0));
            }
        }
        g.DrawRectangle(Pens.Gray, area.X, area.Y, area.Width, area.Height);
        TextRenderer.DrawText(g, _location.Label + "  ·  Norden ↑", Font, new Point(10, 10), ForeColor);
        var overlay = g.Save();
        g.SetClip(area, CombineMode.Intersect);
        foreach (var marker in _map.Markers)
        {
            var point = Project(marker.X, marker.Y);
            g.FillEllipse(Brushes.Gold, point.X - 3, point.Y - 3, 6, 6);
            var text = TextRenderer.MeasureText(marker.Name, Font);
            var x = (int)Math.Clamp(point.X + 4, area.Left, Math.Max(area.Left, area.Right - text.Width));
            var y = (int)Math.Clamp(point.Y - text.Height, area.Top, Math.Max(area.Top, area.Bottom - text.Height));
            TextRenderer.DrawText(g, marker.Name, Font, new Point(x, y), Color.White, Color.FromArgb(35, 40, 45));
        }
        var track = (_location.Track ?? []).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)
            && double.IsFinite(p.Seconds) && p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1)
            .OrderBy(p => p.Seconds).ToArray();
        using var route = new Pen(Color.Cyan, 3);
        using var outline = new Pen(Color.Black, 5);
        var visible = track.Where(p => p.Seconds <= _seconds).ToArray();
        for (var i = 1; i < visible.Length; i++)
        {
            var a = visible[i - 1]; var b = visible[i];
            // Never imply a continuous movement through missing samples or teleports.
            if (b.Seconds - a.Seconds > 5 || Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y) > 0.15) continue;
            g.DrawLine(outline, Project(a.X, a.Y), Project(b.X, b.Y));
            g.DrawLine(route, Project(a.X, a.Y), Project(b.X, b.Y));
        }
        if (visible.Length > 0)
        {
            var first = Project(visible[0].X, visible[0].Y);
            var last = Project(visible[^1].X, visible[^1].Y);
            g.FillEllipse(Brushes.LimeGreen, first.X - 6, first.Y - 6, 12, 12);
            g.DrawEllipse(Pens.Black, first.X - 6, first.Y - 6, 12, 12);
            g.FillRectangle(Brushes.OrangeRed, last.X - 5, last.Y - 5, 10, 10);
            g.DrawRectangle(Pens.Black, last.X - 5, last.Y - 5, 10, 10);
        }
        g.Restore(overlay);
        var caption = track.Length == 0 ? "Keine Positionsdaten im Log / älterer History-Eintrag."
            : $"{_location.TrackSource} · Grün: Start · Rot: Position/Ende · Cyan: Verlauf";
        TextRenderer.DrawText(g, caption, Font, new Rectangle(10, Height - 55, Width - 20, 32), ForeColor, TextFormatFlags.WordBreak);
        TextRenderer.DrawText(g, image is not null ? "Kartenbild © ArenaNet · Quelle: GW2 Elite Insights" : "Schematische Ansicht",
            Font, new Point(10, Height - 20), Color.LightGray);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var image in _backgrounds.Values) image.Dispose();
            _backgrounds.Clear();
        }
        base.Dispose(disposing);
    }
}
