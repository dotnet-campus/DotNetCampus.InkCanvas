namespace DotNetCampus.Inking.Rendering;

internal sealed class RenderBackendSelector(Func<bool> nativeAvailableProbe)
{
    public string LastSelectedRenderer { get; private set; } = "native";

    public IStrokeRenderer Select(
        JaliumRenderBackendMode mode,
        IStrokeRenderer nativeRenderer,
        IStrokeRenderer skiaFallbackRenderer)
    {
        var nativeAvailable = nativeAvailableProbe();
        IStrokeRenderer renderer = mode switch
        {
            JaliumRenderBackendMode.ForceNative => nativeAvailable ? nativeRenderer : skiaFallbackRenderer,
            JaliumRenderBackendMode.ForceSkiaFallback => skiaFallbackRenderer,
            JaliumRenderBackendMode.AutoFallback => nativeAvailable ? nativeRenderer : skiaFallbackRenderer,
            _ => nativeAvailable ? nativeRenderer : skiaFallbackRenderer
        };

        LastSelectedRenderer = renderer.Name;
        return renderer;
    }
}
