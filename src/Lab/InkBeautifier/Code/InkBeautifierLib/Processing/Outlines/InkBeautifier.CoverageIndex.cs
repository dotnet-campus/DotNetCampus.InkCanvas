using System.Collections;
using InkBeautifierLib.Geometry;

namespace InkBeautifierLib.Processing;

public sealed partial class InkBeautifier
{
    private sealed class CoverageIndex(IReadOnlyList<InkPoint2D> polygon) : IReadOnlyList<InkPoint2D>
    {
        private const int BucketCount = 128;
        private const double Padding = 0.00001;
        private int _indexedCount = -1;
        private List<int>[]? _buckets;
        private double _minimumY;
        private double _maximumY;
        private double _step;

        public int Count => polygon.Count;
        public InkPoint2D this[int index] => polygon[index];
        public IEnumerator<InkPoint2D> GetEnumerator() => polygon.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal IReadOnlyList<int>? GetEdges(double y)
        {
            if (_indexedCount != Count) Rebuild();
            if (_buckets is null || !double.IsFinite(y)) return null;
            if (y < _minimumY || y > _maximumY) return Array.Empty<int>();
            return _buckets[Bucket(y)];
        }

        private int Bucket(double y) => Math.Clamp((int)((y - _minimumY) / _step), 0, BucketCount - 1);

        private void Rebuild()
        {
            _indexedCount = Count;
            _buckets = null;
            if (Count < 32) return;
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (InkPoint2D point in polygon)
            {
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || Math.Abs(point.X) > 1_000_000 || Math.Abs(point.Y) > 1_000_000) return;
                min = Math.Min(min, point.Y);
                max = Math.Max(max, point.Y);
            }
            _minimumY = min - Padding;
            _maximumY = max + Padding;
            _step = (max - min + 2 * Padding) / BucketCount;
            var buckets = new List<int>[BucketCount];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = [];
            for (int i = 0; i < Count; i++)
            {
                double start = polygon[i].Y, end = polygon[(i + 1) % Count].Y;
                int last = Bucket(Math.Max(start, end) + Padding);
                for (int bucket = Bucket(Math.Min(start, end) - Padding); bucket <= last; bucket++) buckets[bucket].Add(i);
            }
            _buckets = buckets;
        }
    }
}
