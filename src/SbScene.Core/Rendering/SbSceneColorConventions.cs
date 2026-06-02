namespace SbScene.Core.Rendering;

public readonly record struct SbSceneLitColor(double R, double G, double B, double A);

public static class SbSceneColorConventions
{
    public static RgbaColor OpaqueWhite { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public static RgbaColor OpaqueBlack { get; } = new(byte.MinValue, byte.MinValue, byte.MinValue, byte.MaxValue);

    public static RgbaColor InterpolateVertexColor(IReadOnlyList<RgbaColor> vertexColors, double u, double v)
    {
        if (vertexColors.Count < 4)
        {
            return OpaqueWhite;
        }

        var x = ClampUnit(u);
        var y = ClampUnit(v);
        var top = Lerp(vertexColors[0], vertexColors[2], x);
        var bottom = Lerp(vertexColors[1], vertexColors[3], x);
        return Lerp(top, bottom, y);
    }

    public static SbSceneLitColor ApplyLighting(
        double textureR,
        double textureG,
        double textureB,
        double textureA,
        RgbaColor material,
        RgbaColor illumination,
        RgbaColor vertex)
    {
        var vertexR = vertex.R / 255.0;
        var vertexG = vertex.G / 255.0;
        var vertexB = vertex.B / 255.0;
        var illuminationA = illumination.A / 255.0;

        return new SbSceneLitColor(
            ClampByte(textureR * (material.R / 255.0) * vertexR + illumination.R * illuminationA),
            ClampByte(textureG * (material.G / 255.0) * vertexG + illumination.G * illuminationA),
            ClampByte(textureB * (material.B / 255.0) * vertexB + illumination.B * illuminationA),
            ClampByte(textureA * (vertex.A / 255.0)));
    }

    private static RgbaColor Lerp(RgbaColor left, RgbaColor right, double amount)
    {
        return new RgbaColor(
            ToByte(left.R + (right.R - left.R) * amount),
            ToByte(left.G + (right.G - left.G) * amount),
            ToByte(left.B + (right.B - left.B) * amount),
            ToByte(left.A + (right.A - left.A) * amount));
    }

    private static double ClampUnit(double value)
    {
        return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    }

    private static double ClampByte(double value)
    {
        return double.IsFinite(value) ? Math.Clamp(value, byte.MinValue, byte.MaxValue) : 0;
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);
    }
}
