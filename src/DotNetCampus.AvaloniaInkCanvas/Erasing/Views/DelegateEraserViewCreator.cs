namespace DotNetCampus.Inking.Erasing;

/// <summary>
/// 使用委托的方式创建 <see cref="IEraserView"/> 实例的创建器
/// </summary>
/// <param name="Creator"></param>
public record DelegateEraserViewCreator(Func<IEraserView> Creator) : IEraserViewCreator
{
    public IEraserView CreateEraserView()
    {
        return Creator();
    }
}