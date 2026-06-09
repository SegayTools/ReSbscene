using System.Buffers.Binary;
using System.Text;

namespace SbScene.Core.Images;

/// <summary>
/// 提供DDS解码器，负责把压缩或封装后的图像数据解码为像素数据。
/// </summary>
public static class DdsDecoder
{
    /// <summary>
    /// 解码Decode，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="data">待解析、解码或写出的原始字节数据。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static RgbaImage Decode(ReadOnlySpan<byte> data)
    {
        ValidateDdsHeader(data);
        var height = BinaryPrimitives.ReadInt32LittleEndian(data[12..16]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[16..20]);
        var mipMapCount = BinaryPrimitives.ReadInt32LittleEndian(data[28..32]);
        var format = GetFormatName(data);
        if (format == "DXT1")
        {
            var topLevelSize = GetDxt1TopLevelSize(width, height);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected DXT1 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeDxt1(data.Slice(128, topLevelSize), width, height);
        }

        if (format == "DXT5")
        {
            var topLevelSize = GetDxt5TopLevelSize(width, height);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected DXT5 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeDxt5(data.Slice(128, topLevelSize), width, height);
        }

        if (format == "A8R8G8B8")
        {
            var topLevelSize = checked(width * height * 4);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected A8R8G8B8 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeA8R8G8B8(data.Slice(128, topLevelSize), width, height);
        }

        if (format == "R8G8B8")
        {
            var topLevelSize = checked(width * height * 3);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected R8G8B8 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeR8G8B8(data.Slice(128, topLevelSize), width, height);
        }

        if (format == "A4R4G4B4")
        {
            var topLevelSize = checked(width * height * 2);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected A4R4G4B4 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeA4R4G4B4(data.Slice(128, topLevelSize), width, height);
        }

        if (format == "R5G6B5")
        {
            var topLevelSize = checked(width * height * 2);
            if (data.Length < 128 + topLevelSize)
            {
                throw new InvalidDataException("DDS payload is shorter than the expected R5G6B5 top-level mip size.");
            }

            _ = mipMapCount;
            return DecodeR5G6B5(data.Slice(128, topLevelSize), width, height);
        }

        throw new InvalidDataException($"Unsupported DDS format '{format}'.");
    }

    /// <summary>
    /// 读取 DDS 头部中的格式标记，并返回用于诊断和错误消息的格式名称。
    /// </summary>
    /// <param name="data">待解析、解码或写出的原始字节数据。</param>
    /// <returns>DDS 头部声明的像素格式名称；未知格式会返回可诊断的 FourCC 文本。</returns>
    public static string GetFormatName(ReadOnlySpan<byte> data)
    {
        ValidateDdsHeader(data);
        var pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(data[80..84]);
        var fourCc = Encoding.ASCII.GetString(data.Slice(84, 4));
        var rgbBitCount = BinaryPrimitives.ReadUInt32LittleEndian(data[88..92]);
        var redMask = BinaryPrimitives.ReadUInt32LittleEndian(data[92..96]);
        var greenMask = BinaryPrimitives.ReadUInt32LittleEndian(data[96..100]);
        var blueMask = BinaryPrimitives.ReadUInt32LittleEndian(data[100..104]);
        var alphaMask = BinaryPrimitives.ReadUInt32LittleEndian(data[104..108]);

        if ((pixelFormatFlags & 0x04) != 0 && fourCc == "DXT1")
        {
            return "DXT1";
        }

        if ((pixelFormatFlags & 0x04) != 0 && fourCc == "DXT5")
        {
            return "DXT5";
        }

        if ((pixelFormatFlags & 0x40) != 0
            && rgbBitCount == 32
            && redMask == 0x00FF0000
            && greenMask == 0x0000FF00
            && blueMask == 0x000000FF
            && alphaMask == 0xFF000000)
        {
            return "A8R8G8B8";
        }

        if ((pixelFormatFlags & 0x40) != 0
            && rgbBitCount == 24
            && redMask == 0x00FF0000
            && greenMask == 0x0000FF00
            && blueMask == 0x000000FF
            && alphaMask == 0)
        {
            return "R8G8B8";
        }

        if ((pixelFormatFlags & 0x40) != 0
            && rgbBitCount == 16
            && redMask == 0x00000F00
            && greenMask == 0x000000F0
            && blueMask == 0x0000000F
            && alphaMask == 0x0000F000)
        {
            return "A4R4G4B4";
        }

        if ((pixelFormatFlags & 0x40) != 0
            && rgbBitCount == 16
            && redMask == 0x0000F800
            && greenMask == 0x000007E0
            && blueMask == 0x0000001F
            && alphaMask == 0)
        {
            return "R5G6B5";
        }

        return fourCc.All(static ch => ch is >= ' ' and <= '~')
            ? fourCc
            : $"UnknownFourCC({Convert.ToHexString(data.Slice(84, 4))})";
    }

    /// <summary>
    /// 计算DXT1上边界Level大小，用于校验 DDS mip 顶层数据大小。
    /// </summary>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>DXT1 顶层 mip 数据需要占用的字节数。</returns>
    public static int GetDxt1TopLevelSize(int width, int height)
    {
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        return blocksWide * blocksHigh * 8;
    }

    /// <summary>
    /// 计算DXT5上边界Level大小，用于校验 DDS mip 顶层数据大小。
    /// </summary>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>DXT5 顶层 mip 数据需要占用的字节数。</returns>
    public static int GetDxt5TopLevelSize(int width, int height)
    {
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        return blocksWide * blocksHigh * 16;
    }

    private static RgbaImage DecodeDxt1(ReadOnlySpan<byte> blocks, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        var offset = 0;

        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                DecodeDxt1Block(blocks.Slice(offset, 8), pixels, width, height, bx * 4, by * 4);
                offset += 8;
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage DecodeDxt5(ReadOnlySpan<byte> blocks, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        var offset = 0;

        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                DecodeDxt5Block(blocks.Slice(offset, 16), pixels, width, height, bx * 4, by * 4);
                offset += 16;
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage DecodeA8R8G8B8(ReadOnlySpan<byte> data, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var source = i * 4;
            var destination = i * 4;
            pixels[destination] = data[source + 2];
            pixels[destination + 1] = data[source + 1];
            pixels[destination + 2] = data[source];
            pixels[destination + 3] = data[source + 3];
        }

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage DecodeR8G8B8(ReadOnlySpan<byte> data, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var source = i * 3;
            var destination = i * 4;
            pixels[destination] = data[source + 2];
            pixels[destination + 1] = data[source + 1];
            pixels[destination + 2] = data[source];
            pixels[destination + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage DecodeA4R4G4B4(ReadOnlySpan<byte> data, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * 2, 2));
            var destination = i * 4;
            pixels[destination] = Expand4((packed >> 8) & 0xF);
            pixels[destination + 1] = Expand4((packed >> 4) & 0xF);
            pixels[destination + 2] = Expand4(packed & 0xF);
            pixels[destination + 3] = Expand4((packed >> 12) & 0xF);
        }

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage DecodeR5G6B5(ReadOnlySpan<byte> data, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            DecodeRgb565(BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * 2, 2)), out var color);
            var destination = i * 4;
            pixels[destination] = color.R;
            pixels[destination + 1] = color.G;
            pixels[destination + 2] = color.B;
            pixels[destination + 3] = color.A;
        }

        return new RgbaImage(width, height, pixels);
    }

    private static void DecodeDxt1Block(ReadOnlySpan<byte> block, byte[] pixels, int width, int height, int x0, int y0)
    {
        var color0 = BinaryPrimitives.ReadUInt16LittleEndian(block[0..2]);
        var color1 = BinaryPrimitives.ReadUInt16LittleEndian(block[2..4]);
        Span<Rgba32> colors = stackalloc Rgba32[4];
        DecodeRgb565(color0, out colors[0]);
        DecodeRgb565(color1, out colors[1]);
        if (color0 > color1)
        {
            colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
            colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);
        }
        else
        {
            colors[2] = Lerp(colors[0], colors[1], 1, 1, 2);
            colors[3] = new Rgba32(0, 0, 0, 0);
        }

        var colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block[4..8]);
        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                var x = x0 + px;
                var y = y0 + py;
                if (x >= width || y >= height)
                {
                    continue;
                }

                var pixelIndex = py * 4 + px;
                var color = colors[(int)((colorBits >> (pixelIndex * 2)) & 0x3)];
                var destination = (y * width + x) * 4;
                pixels[destination] = color.R;
                pixels[destination + 1] = color.G;
                pixels[destination + 2] = color.B;
                pixels[destination + 3] = color.A;
            }
        }
    }

    private static void DecodeDxt5Block(ReadOnlySpan<byte> block, byte[] pixels, int width, int height, int x0, int y0)
    {
        Span<byte> alphaPalette = stackalloc byte[8];
        alphaPalette[0] = block[0];
        alphaPalette[1] = block[1];
        if (alphaPalette[0] > alphaPalette[1])
        {
            for (var i = 1; i <= 6; i++)
            {
                alphaPalette[i + 1] = (byte)(((7 - i) * alphaPalette[0] + i * alphaPalette[1] + 3) / 7);
            }
        }
        else
        {
            for (var i = 1; i <= 4; i++)
            {
                alphaPalette[i + 1] = (byte)(((5 - i) * alphaPalette[0] + i * alphaPalette[1] + 2) / 5);
            }

            alphaPalette[6] = 0;
            alphaPalette[7] = 255;
        }

        ulong alphaBits = 0;
        for (var i = 0; i < 6; i++)
        {
            alphaBits |= (ulong)block[2 + i] << (8 * i);
        }

        Span<Rgba32> colors = stackalloc Rgba32[4];
        DecodeRgb565(BinaryPrimitives.ReadUInt16LittleEndian(block[8..10]), out colors[0]);
        DecodeRgb565(BinaryPrimitives.ReadUInt16LittleEndian(block[10..12]), out colors[1]);
        colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
        colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);

        var colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block[12..16]);
        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                var x = x0 + px;
                var y = y0 + py;
                if (x >= width || y >= height)
                {
                    continue;
                }

                var pixelIndex = py * 4 + px;
                var alphaIndex = (int)((alphaBits >> (pixelIndex * 3)) & 0x7);
                var colorIndex = (int)((colorBits >> (pixelIndex * 2)) & 0x3);
                var color = colors[colorIndex];
                var destination = (y * width + x) * 4;
                pixels[destination] = color.R;
                pixels[destination + 1] = color.G;
                pixels[destination + 2] = color.B;
                pixels[destination + 3] = alphaPalette[alphaIndex];
            }
        }
    }

    private static void DecodeRgb565(ushort packed, out Rgba32 color)
    {
        var r5 = (packed >> 11) & 0x1F;
        var g6 = (packed >> 5) & 0x3F;
        var b5 = packed & 0x1F;
        color = new Rgba32(
            (byte)((r5 << 3) | (r5 >> 2)),
            (byte)((g6 << 2) | (g6 >> 4)),
            (byte)((b5 << 3) | (b5 >> 2)));
    }

    private static Rgba32 Lerp(Rgba32 a, Rgba32 b, int aw, int bw, int denominator)
    {
        return new Rgba32(
            (byte)((a.R * aw + b.R * bw) / denominator),
            (byte)((a.G * aw + b.G * bw) / denominator),
            (byte)((a.B * aw + b.B * bw) / denominator),
            (byte)((a.A * aw + b.A * bw) / denominator));
    }

    private static byte Expand4(int value)
    {
        return (byte)((value << 4) | value);
    }

    private static void ValidateDdsHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < 128 || Encoding.ASCII.GetString(data[..4]) != "DDS ")
        {
            throw new InvalidDataException("DDS data must start with 'DDS '.");
        }

        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(data[4..8]);
        if (headerSize != 124)
        {
            throw new InvalidDataException($"Unsupported DDS header size {headerSize}.");
        }
    }

    private readonly record struct Rgba32(byte R, byte G, byte B, byte A = 255);
}
