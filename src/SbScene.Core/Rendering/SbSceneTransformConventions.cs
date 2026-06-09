namespace SbScene.Core.Rendering;

/// <summary>
/// 提供sbscene 场景变换约定，统一封装项目内约定的格式转换和命名规则。
/// </summary>
public static class SbSceneTransformConventions
{
    /// <summary>
    /// 格式化Screen旋转Degrees，将模型转换为可展示、保存或比较的文本内容。
    /// </summary>
    /// <param name="sceneRotationDegrees">已解析的 sbscene 场景模型。</param>
    /// <returns>格式化后的文本内容。</returns>
    public static double ToScreenRotationDegrees(double sceneRotationDegrees)
    {
        return -sceneRotationDegrees;
    }
}
