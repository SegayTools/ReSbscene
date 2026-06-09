namespace SbScene.Core.Rendering;

/// <summary>
/// 表示受光颜色状态，用于同时传递材质色和照明色。
/// </summary>
/// <param name="R">参与颜色、透明度或混合计算的通道值。</param>
/// <param name="G">参与颜色、透明度或混合计算的通道值。</param>
/// <param name="B">参与颜色、透明度或混合计算的通道值。</param>
/// <param name="A">参与颜色、透明度或混合计算的通道值。</param>
public readonly record struct SbSceneLitColor(double R, double G, double B, double A);

/// <summary>
/// 提供sbscene 场景颜色约定，统一封装项目内约定的格式转换和命名规则。
/// </summary>
public static class SbSceneColorConventions
{
    /// <summary>
    /// 获取完全不透明的白色，用作缺省材质色和顶点色。
    /// </summary>
    public static RgbaColor OpaqueWhite { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>
    /// 获取完全不透明的黑色，用作缺省照明色。
    /// </summary>
    public static RgbaColor OpaqueBlack { get; } = new(byte.MinValue, byte.MinValue, byte.MinValue, byte.MaxValue);

    /// <summary>
    /// 按顶点颜色和插值比例计算混合后的颜色通道，用于节点着色。
    /// </summary>
    /// <param name="vertexColors">按左上、左下、右上、右下顺序提供的顶点颜色。</param>
    /// <param name="u">水平方向插值比例，超出范围时会被限制到 0 到 1。</param>
    /// <param name="v">垂直方向插值比例，超出范围时会被限制到 0 到 1。</param>
    /// <returns>根据四个顶点颜色双线性插值得到的 RGBA 颜色。</returns>
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

    /// <summary>
    /// 将材质色、照明色和顶点色应用到纹理采样结果，得到最终受光颜色。
    /// </summary>
    /// <param name="textureR">纹理采样的红色通道。</param>
    /// <param name="textureG">纹理采样的绿色通道。</param>
    /// <param name="textureB">纹理采样的蓝色通道。</param>
    /// <param name="textureA">纹理采样的透明度通道。</param>
    /// <param name="material">节点材质色，用于调制纹理颜色。</param>
    /// <param name="illumination">节点照明色，用于按透明度叠加发光分量。</param>
    /// <param name="vertex">插值得到的顶点色，用于调制纹理颜色和透明度。</param>
    /// <returns>经过材质、照明和顶点色计算后的 RGBA 通道。</returns>
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
