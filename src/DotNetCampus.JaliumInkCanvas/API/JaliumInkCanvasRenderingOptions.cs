namespace DotNetCampus.Inking;

public sealed class JaliumInkCanvasRenderingOptions
{
    public JaliumRenderBackendMode BackendMode { get; set; } = JaliumRenderBackendMode.NativePreferred;

    public bool EnablePressureSmoothing { get; set; } = true;

    public bool EnablePerformanceCounters { get; set; } = true;

    public bool EnableCommittedLayerCache { get; set; } = true;

    public float PressureSmoothingFactor { get; set; } = 0.25f;
}
