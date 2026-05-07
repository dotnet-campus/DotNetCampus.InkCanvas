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
        _dirty = true;
    }

    public void RemoveWhere(Func<StrokeData, bool> predicate)
    {
        if (_strokes.RemoveAll(s => predicate(s)) > 0)
        {
            _dirty = true;
        }
    }

    public int StrokeCount => _strokes.Count;

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
