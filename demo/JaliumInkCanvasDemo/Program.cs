using DotNetCampus.Inking;
using Jalium.UI;
using Jalium.UI.Interop;
using System.Diagnostics;

namespace JaliumInkCanvasDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var backend = RenderBackend.D3D12;
        var renderContext = RenderContext.GetOrCreateCurrent(RenderBackend.D3D12);
        renderContext.DefaultRenderingEngine = RenderingEngine.Impeller;
        Debug.WriteLine($"[JaliumInkCanvasDemo] Selected backend={backend}, engine={renderContext.DefaultRenderingEngine}");

        var builder = AppBuilder.CreateBuilder(args);
        using var app = builder.Build();
        app.UseApplication<Application>();
        app.MainWindow = new MainWindow();
        return app.Run();
    }
}
