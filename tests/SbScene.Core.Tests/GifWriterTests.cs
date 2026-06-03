using System.Text;
using SbScene.Core.Images;
using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class GifWriterTests
{
    [Fact]
    public void WriteEmitsGif89aLoopExtensionFramesAndDelay()
    {
        var frames = new[]
        {
            SolidImage(2, 2, 255, 0, 0, 255),
            SolidImage(2, 2, 0, 0, 255, 255),
        };
        using var stream = new MemoryStream();

        GifWriter.Write(stream, frames, delayCentiseconds: 3, new RgbaColor(255, 255, 255, 255));

        var bytes = stream.ToArray();
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.True(IndexOf(bytes, Encoding.ASCII.GetBytes("NETSCAPE2.0")) >= 0);
        Assert.Equal(2, CountImageDescriptors(bytes));
        Assert.Equal([3, 3], ReadGraphicControlDelays(bytes));
    }

    [Fact]
    public void WriteCompressedFramesUsesChangedRectangles()
    {
        var first = SolidImage(4, 4, 255, 0, 0, 255);
        var second = SolidImage(4, 4, 255, 0, 0, 255);
        var offset = (1 * 4 + 2) * 4;
        second.Pixels[offset] = 0;
        second.Pixels[offset + 1] = 0;
        second.Pixels[offset + 2] = 255;
        using var stream = new MemoryStream();

        GifWriter.Write(stream, [first, second], delayCentiseconds: 3, new RgbaColor(255, 255, 255, 255), compressFrames: true);

        Assert.Equal(
            [
                new ImageDescriptor(0, 0, 4, 4),
                new ImageDescriptor(2, 1, 1, 1),
            ],
            ReadImageDescriptors(stream.ToArray()));
    }

    private static RgbaImage SolidImage(int width, int height, byte r, byte g, byte b, byte a)
    {
        var pixels = new byte[width * height * 4];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = r;
            pixels[offset + 1] = g;
            pixels[offset + 2] = b;
            pixels[offset + 3] = a;
        }

        return new RgbaImage(width, height, pixels);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static int CountImageDescriptors(byte[] bytes)
    {
        var count = 0;
        WalkBlocks(bytes, onGraphicControlDelay: null, onImageDescriptor: _ => count++);
        return count;
    }

    private static IReadOnlyList<ImageDescriptor> ReadImageDescriptors(byte[] bytes)
    {
        var descriptors = new List<ImageDescriptor>();
        WalkBlocks(bytes, onGraphicControlDelay: null, onImageDescriptor: descriptor => descriptors.Add(descriptor));
        return descriptors;
    }

    private static IReadOnlyList<int> ReadGraphicControlDelays(byte[] bytes)
    {
        var delays = new List<int>();
        WalkBlocks(bytes, delay => delays.Add(delay), onImageDescriptor: null);
        return delays;
    }

    private static void WalkBlocks(byte[] bytes, Action<int>? onGraphicControlDelay, Action<ImageDescriptor>? onImageDescriptor)
    {
        var offset = 13 + 256 * 3;
        while (offset < bytes.Length)
        {
            switch (bytes[offset])
            {
                case 0x21:
                    var label = bytes[offset + 1];
                    if (label == 0xF9)
                    {
                        onGraphicControlDelay?.Invoke(bytes[offset + 4] | bytes[offset + 5] << 8);
                    }

                    offset += 2;
                    while (offset < bytes.Length)
                    {
                        var blockSize = bytes[offset++];
                        if (blockSize == 0)
                        {
                            break;
                        }

                        offset += blockSize;
                    }

                    break;
                case 0x2C:
                    onImageDescriptor?.Invoke(new ImageDescriptor(
                        bytes[offset + 1] | bytes[offset + 2] << 8,
                        bytes[offset + 3] | bytes[offset + 4] << 8,
                        bytes[offset + 5] | bytes[offset + 6] << 8,
                        bytes[offset + 7] | bytes[offset + 8] << 8));
                    offset += 10;
                    offset++;
                    while (offset < bytes.Length)
                    {
                        var blockSize = bytes[offset++];
                        if (blockSize == 0)
                        {
                            break;
                        }

                        offset += blockSize;
                    }

                    break;
                case 0x3B:
                    return;
                default:
                    throw new InvalidDataException($"Unexpected GIF block byte 0x{bytes[offset]:X2} at {offset}.");
            }
        }
    }

    private sealed record ImageDescriptor(int Left, int Top, int Width, int Height);
}
