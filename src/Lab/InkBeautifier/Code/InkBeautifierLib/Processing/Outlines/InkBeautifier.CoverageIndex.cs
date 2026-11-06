using System.Collections;
using InkBeautifierLib.Geometry;

namespace InkBeautifierLib.Processing;

public sealed partial class InkBeautifier
{
    private sealed class CoverageIndex(IReadOnlyList<InkPoint2D> polygon) : IReadOnlyList<InkPoint2D>
    {
        private List<int>[]? _buckets;
        private double _minimumY;
        private double _step;
        private int _indexedCount = -1;
        public int Count => polygon.Count;
        public InkPoint2D this[int index] => polygon[index];
        public IEnumerator<InkPoint2D> GetEnumerator() => polygon.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal IReadOnlyList<int>? Candidates(double y)
        {
            if (_indexedCount != Count) Rebuild();
            if (_buckets is null || !double.IsFinite(y)) return null;
            if (y < _minimumY || y > _minimumY + _step * _buckets.Length) return [];
            return _buckets[Bucket(y)];
        }

        private int Bucket(double y) => Math.Clamp((int)((y - _minimumY) / _step), 0, 127);

        private void Rebuild()
        {
            _indexedCount = Count;
            _buckets = null;
            if (Count < 32) return;
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            foreach (InkPoint2D point in polygon)
            {
                // Keep extreme/non-finite arithmetic on the original exact path.
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
                    Math.Abs(point.X) > 1_000_000 || Math.Abs(point.Y) > 1_000_000) return;
                minimum = Math.Min(minimum, point.Y);
                maximum = Math.Max(maximum, point.Y);
            }
            const double padding = 0.00001;
            _minimumY = minimum - padding;
            _step = (maximum - minimum + 2 * padding) / 128;
            _buckets = new List<int>[128];
            for (int b = 0; b < _buckets.Length; b++) _buckets[b] = [];
            for (int i = 0; i < Count; i++)
            {
                InkPoint2D start = polygon[i], end = polygon[(i + 1) % Count];
                int first = Bucket(Math.Min(start.Y, end.Y) - padding);
                int last = Bucket(Math.Max(start.Y, end.Y) + padding);
                for (int b = first; b <= last; b++) _buckets[b].Add(i);
            }
        }
    }
}
