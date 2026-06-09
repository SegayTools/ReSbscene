namespace SbScene.Core.Rendering;

internal static class SbSceneRenderOptionsValidator
{
    public static void Validate(SbSceneRenderOptions options)
    {
        if (options.Scale <= 0 || double.IsNaN(options.Scale) || double.IsInfinity(options.Scale))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Render scale must be a positive finite number.");
        }

        if (options.Padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Render padding must be non-negative.");
        }

        if (options.Supersample is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Supersample factor must be between 1 and 8.");
        }
    }
}
