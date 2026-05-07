namespace DotNetCampus.Inking;

public enum JaliumRenderBackendMode
{
    NativePreferred = 0,
    ForceNative = 1,
    ForceSkiaFallback = 2,
    AutoFallback = 3,
}
