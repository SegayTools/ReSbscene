namespace SbScene.Core.Rendering;

public static class SbSceneCharacterAnimationDefaults
{
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
