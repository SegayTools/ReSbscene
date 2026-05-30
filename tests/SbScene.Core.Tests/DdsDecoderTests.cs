using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Images;

namespace SbScene.Core.Tests;

public sealed class DdsDecoderTests
{
    [Fact]
    public void DecodesDxt1Dds()
    {
        Span<byte> block = stackalloc byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(block[0..2], 0xF800);
        BinaryPrimitives.WriteUInt16LittleEndian(block[2..4], 0x07E0);
        BinaryPrimitives.WriteUInt32LittleEndian(block[4..8], 0);
        var dds = BuildFourCcDds(4, 4, "DXT1", block.ToArray());

        var image = DdsDecoder.Decode(dds);

        Assert.Equal("DXT1", DdsDecoder.GetFormatName(dds));
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal([0xFF, 0x00, 0x00, 0xFF], image.Pixels.Take(4).ToArray());
    }

    [Fact]
    public void DecodesA8R8G8B8Dds()
    {
        var dds = BuildA8R8G8B8Dds(1, 1, [0x33, 0x22, 0x11, 0x44]);

        var image = DdsDecoder.Decode(dds);

        Assert.Equal("A8R8G8B8", DdsDecoder.GetFormatName(dds));
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([0x11, 0x22, 0x33, 0x44], image.Pixels);
    }

    [Fact]
    public void DecodesR8G8B8Dds()
    {
        var dds = BuildR8G8B8Dds(1, 1, [0x33, 0x22, 0x11]);

        var image = DdsDecoder.Decode(dds);

        Assert.Equal("R8G8B8", DdsDecoder.GetFormatName(dds));
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([0x11, 0x22, 0x33, 0xFF], image.Pixels);
    }

    [Fact]
    public void DecodesA4R4G4B4Dds()
    {
        Span<byte> pixel = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(pixel, 0xF123);
        var dds = BuildA4R4G4B4Dds(1, 1, pixel.ToArray());

        var image = DdsDecoder.Decode(dds);

        Assert.Equal("A4R4G4B4", DdsDecoder.GetFormatName(dds));
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([0x11, 0x22, 0x33, 0xFF], image.Pixels);
    }

    [Fact]
    public void DecodesR5G6B5Dds()
    {
        Span<byte> pixel = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(pixel, 0x07E0);
        var dds = BuildR5G6B5Dds(1, 1, pixel.ToArray());

        var image = DdsDecoder.Decode(dds);

        Assert.Equal("R5G6B5", DdsDecoder.GetFormatName(dds));
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([0x00, 0xFF, 0x00, 0xFF], image.Pixels);
    }

    private static byte[] BuildFourCcDds(int width, int height, string fourCc, byte[] pixels)
    {
        var data = BuildDdsHeader(width, height, pixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x04);
        Encoding.ASCII.GetBytes(fourCc).CopyTo(data, 84);
        pixels.CopyTo(data.AsSpan(128));
        return data;
    }

    private static byte[] BuildA8R8G8B8Dds(int width, int height, byte[] bgraPixels)
    {
        var data = BuildDdsHeader(width, height, bgraPixels.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x41);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92, 4), 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96, 4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100, 4), 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104, 4), 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(108, 4), 0x1000);
        bgraPixels.CopyTo(data.AsSpan(128));
        return data;
    }

    private static byte[] BuildR8G8B8Dds(int width, int height, byte[] bgrPixels)
    {
        var data = BuildDdsHeader(width, height, bgrPixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88, 4), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92, 4), 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96, 4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100, 4), 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104, 4), 0);
        bgrPixels.CopyTo(data.AsSpan(128));
        return data;
    }

    private static byte[] BuildA4R4G4B4Dds(int width, int height, byte[] pixels)
    {
        var data = BuildDdsHeader(width, height, pixels.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x41);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88, 4), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92, 4), 0x00000F00);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96, 4), 0x000000F0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100, 4), 0x0000000F);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104, 4), 0x0000F000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(108, 4), 0x1000);
        pixels.CopyTo(data.AsSpan(128));
        return data;
    }

    private static byte[] BuildR5G6B5Dds(int width, int height, byte[] pixels)
    {
        var data = BuildDdsHeader(width, height, pixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88, 4), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92, 4), 0x0000F800);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96, 4), 0x000007E0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100, 4), 0x0000001F);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104, 4), 0);
        pixels.CopyTo(data.AsSpan(128));
        return data;
    }

    private static byte[] BuildDdsHeader(int width, int height, int pixelLength)
    {
        var data = new byte[128 + pixelLength];
        Encoding.ASCII.GetBytes("DDS ").CopyTo(data, 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8, 4), 0x00081007);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12, 4), height);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(20, 4), pixelLength);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(108, 4), 0x1000);
        return data;
    }
}
