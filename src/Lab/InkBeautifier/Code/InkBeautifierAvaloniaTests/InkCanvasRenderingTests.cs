using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using InkBeautifierAvaloniaDemo;
using InkBeautifierLib;

namespace InkBeautifierAvaloniaTests;

[TestClass]
[DoNotParallelize]
public sealed class InkCanvasRenderingTests
{
    private static bool _applicationInitialized;

    [TestMethod]
    public void WhenPointSetIsRenderedThenScreenshotIsCreated()
    {
        EnsureApplication();
        const int width = 1000;
        const int height = 760;
        var canvas = new InkCanvas
        {
            MaximumWidth = 18,
            VelocityResponse = 1.8
        };
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = canvas
        };
        window.Show();

        string sourceDirectory = Path.GetDirectoryName(GetSourceFilePath())!;
        foreach (string assetName in new[] { "Ink0.txt", "Ink1.txt", "Ink2.txt", "Ink3.txt" })
        {
            canvas.Clear();
            IReadOnlyList<IReadOnlyList<DeviceInkPoint>> strokes = LoadDeviceStrokes(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName));
            DrawDeviceStrokes(canvas, strokes, width, height);

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(canvas);

            Assert.IsGreaterThan(1_000, CountNonWhitePixels(bitmap));

            string screenshotPath = Path.Combine(
                sourceDirectory,
                $"{Path.GetFileNameWithoutExtension(assetName)}-Beautified.png");
            using FileStream stream = File.Create(screenshotPath);
            bitmap.Save(stream);
        }

        window.Close();
    }

    private static string GetSourceFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;

    private static int CountNonWhitePixels(RenderTargetBitmap bitmap)
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

            int count = 0;
            for (int index = 0; index < pixels.Length; index += bytesPerPixel)
            {
                byte blue = pixels[index];
                byte green = pixels[index + 1];
                byte red = pixels[index + 2];
                byte alpha = pixels[index + 3];
                if (alpha > 0 && (red < 245 || green < 245 || blue < 245))
                {
                    count++;
                }
            }

            return count;
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
                    double.Parse(point.Element("PressureFactor")!.Value, System.Globalization.CultureInfo.InvariantCulture)))
                .ToArray())
            .Where(stroke => stroke.Count > 0)
            .ToArray();
    }

    private static void DrawDeviceStrokes(
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

        long strokeId = 1;
        foreach (IReadOnlyList<DeviceInkPoint> stroke in strokes)
        {
            long firstTimestamp = (long)stroke[0].Time.TotalMilliseconds;
            for (int index = 0; index < stroke.Count; index++)
            {
                DeviceInkPoint point = stroke[index];
                InkPointerStatus status = index == 0 ? InkPointerStatus.Down : InkPointerStatus.Move;
                canvas.AddPoint(new InkInputPoint(
                    new InkPoint2D(
                        margin + ((point.X - minimumX) * scale),
                        margin + ((point.Y - minimumY) * scale)),
                    Math.Max(0, (long)point.Time.TotalMilliseconds - firstTimestamp),
                    Math.Clamp(point.Pressure / 1024, 0, 1),
                    strokeId,
                    status));
            }

            DeviceInkPoint last = stroke[^1];
            canvas.AddPoint(new InkInputPoint(
                new InkPoint2D(
                    margin + ((last.X - minimumX) * scale),
                    margin + ((last.Y - minimumY) * scale)),
                Math.Max(1, (long)last.Time.TotalMilliseconds - firstTimestamp + 1),
                Math.Clamp(last.Pressure / 1024, 0, 1),
                strokeId,
                InkPointerStatus.Up));
            strokeId++;
        }
    }

    private readonly record struct DeviceInkPoint(TimeSpan Time, double X, double Y, double Pressure);

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