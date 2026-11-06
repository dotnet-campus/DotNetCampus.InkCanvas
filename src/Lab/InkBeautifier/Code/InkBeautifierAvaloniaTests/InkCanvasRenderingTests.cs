using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using InkBeautifierAvaloniaDemo;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;

namespace InkBeautifierAvaloniaTests;

[TestClass]
[DoNotParallelize]
public sealed class InkCanvasRenderingTests
{
    private static bool _applicationInitialized;

    [TestMethod]
    public void WhenProblemInkIsExportedThenRawInputCanBeReplayed()
    {
        EnsureApplication();
        var canvas = new InkCanvas();
        canvas.AddPoint(new InkInputPoint(new InkPoint2D(10.25, 20.5), 1_000, 0.25, 1, InkPointerStatus.Down));
        canvas.AddPoint(new InkInputPoint(new InkPoint2D(10.5, 20.75), 1_001, 0.5, 1, InkPointerStatus.Move));
        canvas.AddPoint(new InkInputPoint(new InkPoint2D(30.75, 40.125), 1_020, 0.75, 1, InkPointerStatus.Up));
        using var stream = new MemoryStream();

        canvas.ExportProblemInk(stream);

        stream.Position = 0;
        XDocument document = XDocument.Load(stream);
        XElement[] points = document.Root!
            .Element("ArrayOfInkDataModel")!
            .Elements("InkDataModel")
            .ToArray();
        Assert.AreEqual(
            "Down,Move,Up",
            string.Join(',', points.Select(point => point.Element("Status")!.Value)));
    }

    [TestMethod]
    [DataRow("Ink0.txt")]
    [DataRow("Ink1.txt")]
    [DataRow("Ink2.txt")]
    [DataRow("Ink3.txt")]
    [DataRow("InkProblem-1.txt")]
    [DataRow("InkProblem-2.txt")]
    [DataRow("InkProblem-3.txt")]
    [DataRow("InkProblem-4.txt")]
    [DataRow("InkProblem-20261106-093537.txt")]
    public void WhenDeviceInkIsRenderedThenScreenshotIsCreated(string assetName)
    {
        EnsureApplication();
        const int width = 1000;
        const int height = 760;
        var canvas = new InkCanvas();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = canvas
        };
        window.Show();

        try
        {
            IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes = LoadDeviceStrokes(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName));
            DeviceInkReplay replay = DrawDeviceStrokes(canvas, strokes, width, height);

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(canvas);

            Assert.IsGreaterThan(1_000, CountNonWhitePixels(bitmap));
            AssertStrokeCoverage(bitmap, replay.AcceptedStrokes, replay.Transform);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"{assetName}-coverage.txt"), replay.Diagnostics);
            if (assetName == "Ink2.txt")
            {
                PixelBuffer pixels = ReadPixels(bitmap);
                foreach (DeviceInkPoint point in replay.AcceptedStrokes.SelectMany(stroke => stroke))
                {
                    (int x, int y) = replay.Transform.ToPixel(point);
                    Assert.IsTrue(ContainsInkNear(pixels, x, y, 12), $"Ink2 accepted input ({x},{y}) has no ink.");
                }
                foreach (DeviceInkPoint point in strokes.SelectMany(stroke => stroke))
                {
                    (int x, int y) = replay.Transform.ToPixel(point);
                    Assert.IsTrue(x >= 30 && x <= width - 30 && y >= 30 && y <= height - 30,
                        $"Ink2 raw input does not fit the replay viewport: ({x},{y}).");
                }
            }
            if (assetName == "Ink3.txt")
            {
                AssertInk3InputLeavesBottomQuarterEmpty(replay, height);
            }

            string screenshotFileName = $"{Path.GetFileNameWithoutExtension(assetName)}-Beautified.png";
            SaveBitmap(bitmap, Path.Combine(AppContext.BaseDirectory, screenshotFileName));

        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenExportedProblemInkIsRenderedThenKnownHoleRemainsFilled()
    {
        EnsureApplication();
        const int width = 1000;
        const int height = 760;
        var canvas = new InkCanvas();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = canvas
        };
        window.Show();

        try
        {
            IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes = LoadDeviceStrokes(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", "InkProblem-1.txt"));
            DrawDeviceStrokes(canvas, strokes, width, height);
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsTrue(IsInkPixel(pixelBuffer.Pixels, (147 * pixelBuffer.Stride) + (495 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenSecondExportedProblemInkIsRenderedThenKnownHoleRemainsFilled()
    {
        EnsureApplication();
        const int width = 1000;
        const int height = 760;
        var canvas = new InkCanvas();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = canvas
        };
        window.Show();

        try
        {
            IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes = LoadDeviceStrokes(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", "InkProblem-2.txt"));
            DrawDeviceStrokes(canvas, strokes, width, height);
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsTrue(IsInkPixel(pixelBuffer.Pixels, (337 * pixelBuffer.Stride) + (578 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenThirdExportedProblemInkIsRenderedThenKnownHoleRemainsFilled()
    {
        EnsureApplication();
        const int width = 1000;
        const int height = 760;
        var canvas = new InkCanvas();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = canvas
        };
        window.Show();

        try
        {
            IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes = LoadDeviceStrokes(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", "InkProblem-3.txt"));
            DrawDeviceStrokes(canvas, strokes, width, height);
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsTrue(IsInkPixel(pixelBuffer.Pixels, (128 * pixelBuffer.Stride) + (754 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenStrokeRepeatedlyCrossesThenCrossingRemainsFilled()
    {
        EnsureApplication();
        const int size = 240;
        var canvas = new InkCanvas
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        };
        var window = new Window
        {
            Width = size,
            Height = size,
            Content = canvas
        };
        window.Show();

        try
        {
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(40, 120), 0, 0.5, 1, InkPointerStatus.Down));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(80, 120), 10, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(120, 120), 20, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(160, 120), 30, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(200, 120), 40, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(160, 120), 50, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(120, 120), 60, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(80, 120), 70, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(40, 120), 80, 0.5, 1, InkPointerStatus.Up));

            using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsTrue(IsInkPixel(pixelBuffer.Pixels, (120 * pixelBuffer.Stride) + (120 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenTurningStrokeOverlapsEarlierSweepThenAcceptedCentersRemainFilled()
    {
        EnsureApplication();
        const int size = 160;
        var canvas = new InkCanvas
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        };
        var window = new Window
        {
            Width = size,
            Height = size,
            Content = canvas
        };
        window.Show();

        try
        {
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(30, 40), 0, 0.5, 1, InkPointerStatus.Down));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(60, 40), 10, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(80, 70), 20, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(30, 70), 30, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(30, 45), 40, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(60, 10), 50, 0.5, 1, InkPointerStatus.Up));

            using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsTrue(IsInkPixel(pixelBuffer.Pixels, (45 * pixelBuffer.Stride) + (30 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void WhenClosedLoopIsRenderedThenInteriorRemainsEmpty()
    {
        EnsureApplication();
        const int size = 240;
        var canvas = new InkCanvas();
        var window = new Window
        {
            Width = size,
            Height = size,
            Content = canvas
        };
        window.Show();

        try
        {
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(40, 120), 0, 0.5, 1, InkPointerStatus.Down));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(60, 60), 10, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(120, 40), 20, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(180, 60), 30, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(200, 120), 40, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(180, 180), 50, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(120, 200), 60, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(60, 180), 70, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(40, 120), 80, 0.5, 1, InkPointerStatus.Move));
            canvas.AddPoint(new InkInputPoint(new InkPoint2D(60, 120), 90, 0.5, 1, InkPointerStatus.Up));

            using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixelBuffer = ReadPixels(bitmap);

            Assert.IsFalse(IsInkPixel(pixelBuffer.Pixels, (120 * pixelBuffer.Stride) + (120 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    [DataRow("clear", false, false)]
    [DataRow("same-id", false, false)]
    [DataRow("filtered-move", true, false)]
    [DataRow("transform", false, true)]
    public void WhenCachedGeometryChangesThenRenderedPixelsFollowLifecycle(string change, bool originalInk, bool translatedInk)
    {
        EnsureApplication();
        var canvas = new InkCanvas { VelocityResponse = 0 };
        var window = new Window { Width = 200, Height = 160, Content = canvas };
        window.Show();
        try
        {
            canvas.AddPoint(new(new(20, 40), 0, .5, 1, InkPointerStatus.Down));
            canvas.AddPoint(new(new(80, 40), 10, .5, 1, InkPointerStatus.Move));
            using (var initial = new RenderTargetBitmap(new PixelSize(200, 160), new Vector(96, 96)))
            {
                initial.Render(canvas);
                PixelBuffer pixels = ReadPixels(initial);
                Assert.IsTrue(IsInkPixel(pixels.Pixels, 40 * pixels.Stride + 50 * 4));
            }
            switch (change)
            {
                case "clear": canvas.Clear(); break;
                case "same-id": canvas.AddPoint(new(new(120, 100), 20, .5, 1, InkPointerStatus.Down)); break;
                case "filtered-move": canvas.AddPoint(new(new(150, 100), 10, .5, 1, InkPointerStatus.Move)); break;
                case "transform": canvas.OutlineTransform = Matrix.CreateTranslation(0, 60); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize(200, 160), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer actual = ReadPixels(bitmap);
            Assert.AreEqual((originalInk, translatedInk),
                (IsInkPixel(actual.Pixels, 40 * actual.Stride + 50 * 4),
                 IsInkPixel(actual.Pixels, 100 * actual.Stride + 50 * 4)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    [DataRow("render", true, false)]
    [DataRow("filtered-move", true, false)]
    [DataRow("other-stroke", true, true)]
    [DataRow("transform", false, true)]
    public void WhenUnchangedStrokeIsRedrawnThenInkIsPreserved(string change, bool originalInk, bool lowerInk)
    {
        EnsureApplication();
        var canvas = new InkCanvas { VelocityResponse = 0 };
        var window = new Window { Width = 200, Height = 160, Content = canvas };
        window.Show();
        try
        {
            canvas.AddPoint(new(new(20, 40), 0, .5, 1, InkPointerStatus.Down));
            canvas.AddPoint(new(new(80, 40), 10, .5, 1, InkPointerStatus.Move));
            using (var initial = new RenderTargetBitmap(new PixelSize(200, 160), new Vector(96, 96)))
            {
                initial.Render(canvas);
                Assert.IsTrue(ContainsInkNear(ReadPixels(initial), 50, 40, 0));
            }

            switch (change)
            {
                case "render": break;
                case "filtered-move": canvas.AddPoint(new(new(150, 100), 10, .5, 1, InkPointerStatus.Move)); break;
                case "other-stroke":
                    canvas.AddPoint(new(new(20, 100), 0, .5, 2, InkPointerStatus.Down));
                    canvas.AddPoint(new(new(80, 100), 10, .5, 2, InkPointerStatus.Up));
                    break;
                case "transform": canvas.OutlineTransform = Matrix.CreateTranslation(0, 60); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }

            canvas.InvalidateVisual();
            using var bitmap = new RenderTargetBitmap(new PixelSize(200, 160), new Vector(96, 96));
            bitmap.Render(canvas);
            PixelBuffer pixels = ReadPixels(bitmap);
            Assert.AreEqual((originalInk, lowerInk),
                (ContainsInkNear(pixels, 50, 40, 0), ContainsInkNear(pixels, 50, 100, 0)));
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertInk3InputLeavesBottomQuarterEmpty(DeviceInkReplay replay, int height)
    {
        int maximumAcceptedY = replay.AcceptedStrokes
            .SelectMany(stroke => stroke)
            .Select(point => replay.Transform.ToPixel(point).Y)
            .Max();

        Assert.IsLessThan(height * 3 / 4, maximumAcceptedY);
    }

    private static void AssertStrokeCoverage(
        RenderTargetBitmap bitmap,
        IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes,
        DeviceInkTransform transform)
    {
        const int partitionCount = 3;
        const int searchRadius = 48;
        PixelBuffer pixelBuffer = ReadPixels(bitmap);

        for (int strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++)
        {
            IReadOnlyList<DeviceInkPoint> stroke = strokes[strokeIndex];
            int actualPartitionCount = Math.Min(partitionCount, stroke.Count);
            for (int partitionIndex = 0; partitionIndex < actualPartitionCount; partitionIndex++)
            {
                int startIndex = partitionIndex * stroke.Count / actualPartitionCount;
                int endIndex = ((partitionIndex + 1) * stroke.Count / actualPartitionCount) - 1;
                bool containsInk = false;
                for (int pointIndex = startIndex; pointIndex <= endIndex && !containsInk; pointIndex++)
                {
                    (int x, int y) = transform.ToPixel(stroke[pointIndex]);
                    containsInk = ContainsInkNear(pixelBuffer, x, y, searchRadius);
                }

                Assert.IsTrue(
                    containsInk,
                    $"Stroke {strokeIndex + 1}, partition {partitionIndex + 1} has no rendered ink near its input path.");
            }
        }
    }

    private static bool ContainsInkNear(PixelBuffer pixelBuffer, int centerX, int centerY, int radius)
    {
        int minimumX = Math.Max(0, centerX - radius);
        int maximumX = Math.Min(pixelBuffer.Width - 1, centerX + radius);
        int minimumY = Math.Max(0, centerY - radius);
        int maximumY = Math.Min(pixelBuffer.Height - 1, centerY + radius);
        int squaredRadius = radius * radius;

        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                int offsetX = x - centerX;
                int offsetY = y - centerY;
                if (((offsetX * offsetX) + (offsetY * offsetY)) <= squaredRadius &&
                    IsInkPixel(pixelBuffer.Pixels, (y * pixelBuffer.Stride) + (x * 4)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountNonWhitePixels(RenderTargetBitmap bitmap)
    {
        PixelBuffer pixelBuffer = ReadPixels(bitmap);
        int count = 0;
        for (int index = 0; index < pixelBuffer.Pixels.Length; index += 4)
        {
            if (IsInkPixel(pixelBuffer.Pixels, index))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsInkPixel(byte[] pixels, int index)
    {
        byte blue = pixels[index];
        byte green = pixels[index + 1];
        byte red = pixels[index + 2];
        byte alpha = pixels[index + 3];
        return alpha > 0 && (red < 245 || green < 245 || blue < 245);
    }

    private static PixelBuffer ReadPixels(RenderTargetBitmap bitmap)
    {
        const int bytesPerPixel = 4;
        int stride = bitmap.PixelSize.Width * bytesPerPixel;
        int bufferSize = stride * bitmap.PixelSize.Height;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, bufferSize, stride);
            var pixels = new byte[bufferSize];
            Marshal.Copy(buffer, pixels, 0, bufferSize);
            return new PixelBuffer(pixels, bitmap.PixelSize.Width, bitmap.PixelSize.Height, stride);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<IReadOnlyList<DeviceInkPoint>> LoadDeviceStrokes(string path)
    {
        XDocument document = XDocument.Parse(File.ReadAllText(path));
        return document.Root!
            .Elements("ArrayOfInkDataModel")
            .Select(stroke => (IReadOnlyList<DeviceInkPoint>) stroke
                .Elements("InkDataModel")
                .Select(point => new DeviceInkPoint(
                    System.Xml.XmlConvert.ToTimeSpan(point.Element("Time")!.Value),
                    double.Parse(point.Element("X")!.Value, System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(point.Element("Y")!.Value, System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(point.Element("PressureFactor")!.Value, System.Globalization.CultureInfo.InvariantCulture),
                    Enum.TryParse(point.Element("Status")?.Value, out InkPointerStatus status) ? status : null))
                .ToArray())
            .Where(stroke => stroke.Count > 0)
            .ToArray();
    }

    private static DeviceInkReplay DrawDeviceStrokes(
        InkCanvas canvas,
        IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes,
        int width,
        int height)
    {
        const double margin = 30;
        DeviceInkPoint[] allPoints = strokes.SelectMany(stroke => stroke).ToArray();
        double minimumX = allPoints.Min(point => point.X);
        double maximumX = allPoints.Max(point => point.X);
        double minimumY = allPoints.Min(point => point.Y);
        double maximumY = allPoints.Max(point => point.Y);
        double scale = Math.Min(
            (width - (margin * 2)) / Math.Max(1, maximumX - minimumX),
            (height - (margin * 2)) / Math.Max(1, maximumY - minimumY));
        canvas.MaximumWidth = 18 / scale;
        canvas.VelocityResponse = 1.8 * scale;
        canvas.OutlineTransform = new Matrix(
            scale,
            0,
            0,
            scale,
            margin - (minimumX * scale),
            margin - (minimumY * scale));

        var acceptedStrokes = new List<IReadOnlyList<DeviceInkPoint>>(strokes.Count);
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine(FormattableString.Invariant($"input=({minimumX},{minimumY})-({maximumX},{maximumY}); scale={scale}; margin={margin}"));
        Rect outputBounds = default;
        long strokeId = 1;
        foreach (IReadOnlyList<DeviceInkPoint> stroke in strokes)
        {
            var acceptedPoints = new List<DeviceInkPoint>(stroke.Count);
            InkBeautifierResult finalResult = new(strokeId, [], false, false);
            long firstTimestamp = (long)stroke[0].Time.TotalMilliseconds;
            for (int index = 0; index < stroke.Count; index++)
            {
                DeviceInkPoint point = stroke[index];
                InkPointerStatus status = point.Status ??
                    (index == 0 ? InkPointerStatus.Down : InkPointerStatus.Move);
                InkBeautifierResult result = canvas.AddPoint(new InkInputPoint(
                    new InkPoint2D(point.X, point.Y),
                    Math.Max(0, (long)point.Time.TotalMilliseconds - firstTimestamp),
                    Math.Clamp(point.Pressure / 1024, 0, 1),
                    strokeId,
                    status));
                finalResult = result;
                if (result.WasAccepted)
                {
                    acceptedPoints.Add(point);
                }
            }

            DeviceInkPoint last = stroke[^1];
            if (last.Status is null)
            {
                InkBeautifierResult upResult = canvas.AddPoint(new InkInputPoint(
                    new InkPoint2D(last.X, last.Y),
                    Math.Max(1, (long)last.Time.TotalMilliseconds - firstTimestamp + 1),
                    Math.Clamp(last.Pressure / 1024, 0, 1),
                    strokeId,
                    InkPointerStatus.Up));
                finalResult = upResult;
                if (upResult.WasAccepted && !acceptedPoints.Contains(last))
                {
                    acceptedPoints.Add(last);
                }
            }

            InkPoint2D[] vertices = finalResult.Outlines.SelectMany(outline => outline).ToArray();
            if (vertices.Length > 0)
            {
                var bounds = new Rect(
                    margin + (vertices.Min(p => p.X) - minimumX) * scale,
                    margin + (vertices.Min(p => p.Y) - minimumY) * scale,
                    (vertices.Max(p => p.X) - vertices.Min(p => p.X)) * scale,
                    (vertices.Max(p => p.Y) - vertices.Min(p => p.Y)) * scale);
                outputBounds = outputBounds == default ? bounds : outputBounds.Union(bounds);
                diagnostics.AppendLine($"stroke={strokeId}; outputBounds={bounds}");
            }
            double spanX = (stroke.Max(p => p.X) - stroke.Min(p => p.X)) * scale;
            double spanY = (stroke.Max(p => p.Y) - stroke.Min(p => p.Y)) * scale;
            double maxJump = stroke.Zip(stroke.Skip(1), (a, b) =>
                Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)) * scale).DefaultIfEmpty().Max();
            diagnostics.AppendLine(FormattableString.Invariant(
                $"stroke={strokeId}; raw={stroke.Count}; accepted={acceptedPoints.Count}; start=({margin + (stroke[0].X - minimumX) * scale:F2},{margin + (stroke[0].Y - minimumY) * scale:F2}); span=({spanX:F2},{spanY:F2}); maxRawJump={maxJump:F2}; contours={finalResult.Outlines.Count}"));
            diagnostics.AppendLine("acceptedPixels=" + string.Join(";", acceptedPoints.Select(p =>
                FormattableString.Invariant($"({margin + (p.X - minimumX) * scale:F1},{margin + (p.Y - minimumY) * scale:F1})"))));
            acceptedStrokes.Add(acceptedPoints);
            strokeId++;
        }

        return new DeviceInkReplay(
            new DeviceInkTransform(scale, minimumX, minimumY, margin),
            acceptedStrokes, outputBounds, diagnostics.AppendLine($"outputBounds={outputBounds}").ToString());
    }

    private readonly record struct DeviceInkPoint(
        TimeSpan Time,
        double X,
        double Y,
        double Pressure,
        InkPointerStatus? Status);

    private readonly record struct DeviceInkReplay(
        DeviceInkTransform Transform,
        IReadOnlyList<IReadOnlyList<DeviceInkPoint>> AcceptedStrokes,
        Rect OutputBounds,
        string Diagnostics);

    private readonly record struct DeviceInkTransform(double Scale, double MinimumX, double MinimumY, double Margin)
    {
        public (int X, int Y) ToPixel(DeviceInkPoint point) =>
            ((int)Math.Round(Margin + ((point.X - MinimumX) * Scale)),
             (int)Math.Round(Margin + ((point.Y - MinimumY) * Scale)));
    }

    private readonly record struct PixelBuffer(byte[] Pixels, int Width, int Height, int Stride);

    private static void SaveBitmap(RenderTargetBitmap bitmap, string path)
    {
        using FileStream stream = File.Create(path);
        bitmap.Save(stream, new PngBitmapEncoderOptions());
    }

    private static void EnsureApplication()
    {
        if (_applicationInitialized)
        {
            return;
        }

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .SetupWithoutStarting();
        _applicationInitialized = true;
    }
}