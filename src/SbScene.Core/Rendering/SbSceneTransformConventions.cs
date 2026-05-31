namespace SbScene.Core.Rendering;

public static class SbSceneTransformConventions
{
    public static double ToScreenRotationDegrees(double sceneRotationDegrees)
    {
        return -sceneRotationDegrees;
    }
}
