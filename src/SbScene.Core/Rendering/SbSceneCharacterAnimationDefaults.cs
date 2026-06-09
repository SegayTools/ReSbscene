namespace SbScene.Core.Rendering;

/// <summary>
/// 提供 NaviChara 角色默认动画选择，用于初始化预览和导出姿态。
/// </summary>
public static class SbSceneCharacterAnimationDefaults
{
    /// <summary>
    /// 构建 NaviChara 角色默认动画槽位选择，用于初始姿态渲染。
    /// </summary>
    /// <returns>按默认槽位顺序排列的动画选择集合。</returns>
    public static IReadOnlyList<SbSceneAnimationSelection> BuildSelections()
    {
        return
        [
            new SbSceneAnimationSelection("Change_Fashion", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Change_Position", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Change_Accessory", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Action_Wait1", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Mouth_Wait1", 0) { HasExplicitFrame = true },
        ];
    }
}
