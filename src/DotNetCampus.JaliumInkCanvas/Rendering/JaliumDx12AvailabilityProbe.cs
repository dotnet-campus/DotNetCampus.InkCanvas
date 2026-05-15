using System.Reflection;

namespace DotNetCampus.Inking.Rendering;

internal static class JaliumDx12AvailabilityProbe
{
    private static readonly Lazy<bool> CachedResult = new(ProbeCore);

    public static bool IsAvailable()
    {
        return CachedResult.Value;
    }

    private static bool ProbeCore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // Jalium native DX12 backend is provided by native companion binaries.
        // If they are not discoverable at runtime, native renderer should fallback.
        var backend = Environment.GetEnvironmentVariable("JALIUM_RENDER_BACKEND");
        if (!string.IsNullOrWhiteSpace(backend) && !backend.Equals("d3d12", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (HasDx12NativeBinary(baseDirectory))
        {
            return true;
        }

        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return HasDx12NativeBinary(assemblyDirectory);
    }

    private static bool HasDx12NativeBinary(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        foreach (var name in new[]
                 {
                     "jalium.native.d3d12.dll",
                     "jalium.native.core.dll",
                 })
        {
            if (!File.Exists(Path.Combine(directory, name)))
            {
                return false;
            }
        }

        return true;
    }
}
