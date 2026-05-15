using DotNetCampus.Inking.Model;
using Jalium.UI;
using Jalium.UI.Media;

namespace DotNetCampus.Inking.Caching;

internal sealed class JaliumCommittedLayerCache
{
    private readonly List<StrokeData> _strokes = [];
    private readonly Dictionary<uint, GeometryGroup> _groups = [];
    private bool _dirty = true;

    private uint BrushKey(SolidColorBrush brush) => brush.Color.ToArgb();

    public int CacheHitCount { get; private set; }
    public int CacheMissCount { get; private set; }

    public void Reset()
    {
        _strokes.Clear();
        _groups.Clear();
        _dirty = true;
    }

    public void Add(StrokeData stroke)
    {
        _strokes.Add(stroke);
        if (_dirty)
        {
            return;
        }

        var key = BrushKey(stroke.Brush);
        if (!_groups.TryGetValue(key, out var group))
        {
            group = new GeometryGroup();
            _groups[key] = group;
        }

        group.Children.Add(stroke.Geometry.Clone());
    }

    public IReadOnlyList<InkStrokeSnapshot> ReplaceWhere(
        Func<StrokeData, bool> candidate,
        Func<StrokeData, IReadOnlyList<StrokeData>> rewrite)
    {
        if (_strokes.Count == 0)
        {
            return Array.Empty<InkStrokeSnapshot>();
        }

        var erased = new List<InkStrokeSnapshot>();
        var replacement = new List<StrokeData>(_strokes.Count);
        var changed = false;

        foreach (var stroke in _strokes)
        {
            if (!candidate(stroke))
            {
                replacement.Add(stroke);
                continue;
            }

            erased.Add(new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList()));
            var rewritten = rewrite(stroke);
            if (rewritten.Count > 0)
            {
                replacement.AddRange(rewritten);
            }

            changed = true;
        }

        if (!changed)
        {
            return Array.Empty<InkStrokeSnapshot>();
        }

        _strokes.Clear();
        _strokes.AddRange(replacement);
        _dirty = true;
        return erased;
    }

    public IReadOnlyList<InkStrokeSnapshot> RemoveWhere(Func<StrokeData, bool> predicate)
    {
        return ReplaceWhere(predicate, _ => Array.Empty<StrokeData>());
    }

    public int StrokeCount => _strokes.Count;

    public IReadOnlyList<InkStrokeSnapshot> GetStrokeSnapshots()
    {
        var snapshots = new List<InkStrokeSnapshot>(_strokes.Count);
        foreach (var stroke in _strokes)
        {
            snapshots.Add(new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList()));
        }

        return snapshots;
    }

    public void Draw(DrawingContext drawingContext)
    {
        if (_dirty)
        {
            RebuildGroups();
            CacheMissCount++;
            _dirty = false;
        }
        else
        {
            CacheHitCount++;
        }

        foreach (var group in _groups)
        {
            var color = Color.FromArgb(group.Key);
            drawingContext.DrawGeometry(new SolidColorBrush(color), null, group.Value);
        }
    }

    private void RebuildGroups()
    {
        _groups.Clear();
        foreach (var stroke in _strokes)
        {
            var key = BrushKey(stroke.Brush);
            if (!_groups.TryGetValue(key, out var group))
            {
                group = new GeometryGroup();
                _groups[key] = group;
            }

            group.Children.Add(stroke.Geometry.Clone());
        }
    }
}
