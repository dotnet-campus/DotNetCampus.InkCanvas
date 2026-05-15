namespace DotNetCampus.Inking;

public sealed class JaliumInkCanvasRenderingOptions
{
    public JaliumRenderBackendMode BackendMode { get; set; } = JaliumRenderBackendMode.NativePreferred;

    public Func<IJaliumEraserOverlay>? EraserOverlayCreator { get; set; }

    public bool ShowEraserOverlay { get; set; } = true;

    public bool EnablePressureSmoothing { get; set; } = true;

    public bool EnableIntermediatePoints { get; set; } = true;

    public bool EnablePerformanceCounters { get; set; } = true;

    public bool EnableCommittedLayerCache { get; set; } = true;

    public bool IgnorePressure { get; set; }

    public bool EnableStylusSizeAsEraserSize { get; set; } = true;

    public bool CanEraserAlwaysFollowsTouchSize { get; set; }

    public bool LockMinEraserSize { get; set; } = true;

    public float PressureSmoothingFactor { get; set; } = 0.25f;

    public double MinPointDistance { get; set; } = 0.2d;

    public double EraserSize { get; set; } = 18d;

    public TimeSpan EraserCanResizeDuringTimeSpan { get; set; } = TimeSpan.FromMilliseconds(600);

    public double MinEraserSize { get; set; } = 18d;

    public double MaxEraserSize { get; set; } = 600d;
}
