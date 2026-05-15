using DotNetCampus.Inking;
using Jalium.UI;
using System.Diagnostics;

namespace JaliumInkCanvasDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("JALIUM_RENDER_BACKEND", "d3d12");
        Debug.WriteLine("[JaliumInkCanvasDemo] Preferred backend=d3d12 (via env)");

        var builder = AppBuilder.CreateBuilder(args);
        using var app = builder.Build();
        app.UseApplication<Application>();
        app.MainWindow = new MainWindow();
        return app.Run();
    }
}
