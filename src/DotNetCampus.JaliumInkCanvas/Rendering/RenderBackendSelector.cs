namespace DotNetCampus.Inking.Rendering;

internal sealed class RenderBackendSelector(Func<bool> nativeAvailableProbe)
{
    public string LastSelectedRenderer { get; private set; } = "native";

    public bool LastNativeAvailable { get; private set; } = true;

    public IStrokeRenderer Select(
        JaliumRenderBackendMode mode,
        IStrokeRenderer nativeRenderer,
        IStrokeRenderer skiaFallbackRenderer)
    {
        var nativeAvailable = nativeAvailableProbe();
        LastNativeAvailable = nativeAvailable;
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
