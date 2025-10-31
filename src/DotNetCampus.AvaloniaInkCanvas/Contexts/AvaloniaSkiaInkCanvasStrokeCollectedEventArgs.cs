using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetCampus.Inking.Contexts;

public class AvaloniaSkiaInkCanvasStrokeCollectedEventArgs : EventArgs
{
    public AvaloniaSkiaInkCanvasStrokeCollectedEventArgs(int stylusDeviceId, SkiaStroke skiaStroke)
    {
        StylusDeviceId = stylusDeviceId;
        SkiaStroke = skiaStroke;
    }

    public int StylusDeviceId { get; }
    public SkiaStroke SkiaStroke { get; }
}