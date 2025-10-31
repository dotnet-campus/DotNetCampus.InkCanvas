using Avalonia;

namespace DotNetCampus.Inking.Erasing;

/// <summary>
/// 橡皮擦的视图接口
/// </summary>
public interface IEraserView
{
    void Move(Point position);
    void SetEraserSize(Size size);
}