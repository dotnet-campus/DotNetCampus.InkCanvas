using DotNetCampus.Inking.Primitive;
using Jalium.UI.Media;

namespace DotNetCampus.Inking;

public readonly record struct InkStrokeSnapshot(Color Color, double Thickness, IReadOnlyList<InkStylusPoint> Points);
