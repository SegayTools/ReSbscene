using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Rendering;

namespace SbScene.Core.Images;

/// <summary>
/// 提供GIF写出器，负责把内存数据编码并写入目标文件。
/// </summary>
public static class GifWriter
{
    private const int GlobalColorCount = 256;
    private const int QuantizedChannelBits = 5;
    private const int QuantizedChannelCount = 1 << QuantizedChannelBits;
    private const int QuantizedColorCount = QuantizedChannelCount * QuantizedChannelCount * QuantizedChannelCount;

    /// <summary>
    /// 将 RGBA 帧序列编码为 GIF 文件，并按指定背景色处理透明像素。
    /// </summary>
    /// <param name="path">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="frames">要采样或渲染的动画帧位置。</param>
    /// <param name="delayCentiseconds">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="matteColor">参与颜色、透明度或混合计算的通道值。</param>
    /// <param name="loopCount">参与本次处理的循环数量。</param>
    /// <param name="compressFrames">要采样或渲染的动画帧位置。</param>
    public static void Write(
        string path,
        IReadOnlyList<RgbaImage> frames,
        int delayCentiseconds,
        RgbaColor matteColor,
        ushort loopCount = 0,
        bool compressFrames = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = File.Create(path);
        Write(stream, frames, delayCentiseconds, matteColor, loopCount, compressFrames);
    }

    /// <summary>
    /// 将 RGBA 帧序列编码为 GIF 数据流，供 CLI 和 Viewer 导出动画。
    /// </summary>
    /// <param name="stream">承载输入或输出字节的流。</param>
    /// <param name="frames">要采样或渲染的动画帧位置。</param>
    /// <param name="delayCentiseconds">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="matteColor">参与颜色、透明度或混合计算的通道值。</param>
    /// <param name="loopCount">参与本次处理的循环数量。</param>
    /// <param name="compressFrames">要采样或渲染的动画帧位置。</param>
    public static void Write(
        Stream stream,
        IReadOnlyList<RgbaImage> frames,
        int delayCentiseconds,
        RgbaColor matteColor,
        ushort loopCount = 0,
        bool compressFrames = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            throw new ArgumentException("GIF requires at least one frame.", nameof(frames));
        }

        if (delayCentiseconds <= 0 || delayCentiseconds > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(delayCentiseconds), "GIF frame delay must fit in an unsigned 16-bit centisecond value.");
        }

        var width = frames[0].Width;
        var height = frames[0].Height;
        if (width > ushort.MaxValue || height > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), "GIF dimensions must be 65535 pixels or smaller.");
        }

        foreach (var frame in frames)
        {
            if (frame.Width != width || frame.Height != height)
            {
                throw new ArgumentException("All GIF frames must have the same dimensions.", nameof(frames));
            }
        }

        var palette = BuildPalette(frames, matteColor);
        var indexedFrames = frames
            .Select(frame => new IndexedFrame(frame.Width, frame.Height, QuantizeFrame(frame, palette, matteColor)))
            .ToArray();
        WriteHeader(stream, width, height, palette);
        WriteLoopExtension(stream, loopCount);
        if (compressFrames)
        {
            WriteCompressedFrames(stream, indexedFrames, (ushort)delayCentiseconds);
        }
        else
        {
            foreach (var frame in indexedFrames)
            {
                WriteGraphicControlExtension(stream, (ushort)delayCentiseconds);
                WriteImageDescriptor(stream, 0, 0, width, height);
                WriteImageData(stream, frame.Indices);
            }
        }

        stream.WriteByte(0x3B);
    }

    private static void WriteCompressedFrames(Stream stream, IReadOnlyList<IndexedFrame> frames, ushort delayCentiseconds)
    {
        IndexedFrame? previous = null;
        foreach (var frame in frames)
        {
            var rect = previous is null
                ? GifRect.Full(frame.Width, frame.Height)
                : FindChangedRect(previous.Indices, frame.Indices, frame.Width, frame.Height);
            if (rect.Width == 0 || rect.Height == 0)
            {
                rect = new GifRect(0, 0, 1, 1);
            }

            WriteGraphicControlExtension(stream, delayCentiseconds);
            WriteImageDescriptor(stream, rect.Left, rect.Top, rect.Width, rect.Height);
            WriteImageData(stream, CopyRect(frame.Indices, frame.Width, rect));
            previous = frame;
        }
    }

    private static GifRect FindChangedRect(byte[] previous, byte[] current, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + x;
                if (previous[offset] == current[offset])
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left || bottom < top
            ? new GifRect(0, 0, 0, 0)
            : new GifRect(left, top, right - left + 1, bottom - top + 1);
    }

    private static byte[] CopyRect(byte[] indices, int sourceWidth, GifRect rect)
    {
        var output = new byte[rect.Width * rect.Height];
        for (var y = 0; y < rect.Height; y++)
        {
            Buffer.BlockCopy(indices, ((rect.Top + y) * sourceWidth + rect.Left), output, y * rect.Width, rect.Width);
        }

        return output;
    }

    private static byte[] BuildPalette(IReadOnlyList<RgbaImage> frames, RgbaColor matteColor)
    {
        var histogram = BuildHistogram(frames, matteColor);
        var colors = new List<QuantizedColor>();
        for (var key = 0; key < histogram.Length; key++)
        {
            var count = histogram[key].Count;
            if (count == 0)
            {
                continue;
            }

            colors.Add(new QuantizedColor(
                key,
                QuantizedChannelCenter((key >> 10) & 0x1F),
                QuantizedChannelCenter((key >> 5) & 0x1F),
                QuantizedChannelCenter(key & 0x1F),
                count,
                histogram[key].SumR,
                histogram[key].SumG,
                histogram[key].SumB));
        }

        if (colors.Count == 0)
        {
            colors.Add(new QuantizedColor(0, matteColor.R, matteColor.G, matteColor.B, 1, matteColor.R, matteColor.G, matteColor.B));
        }

        var buckets = new List<ColorBucket> { new(colors) };
        while (buckets.Count < GlobalColorCount)
        {
            var index = FindBucketToSplit(buckets);
            if (index < 0)
            {
                break;
            }

            var bucket = buckets[index];
            buckets.RemoveAt(index);
            var (left, right) = bucket.Split();
            buckets.Add(left);
            buckets.Add(right);
        }

        var palette = new byte[GlobalColorCount * 3];
        for (var i = 0; i < Math.Min(buckets.Count, GlobalColorCount); i++)
        {
            var color = buckets[i].AverageColor();
            palette[i * 3] = color.R;
            palette[i * 3 + 1] = color.G;
            palette[i * 3 + 2] = color.B;
        }

        return palette;
    }

    private static HistogramEntry[] BuildHistogram(IReadOnlyList<RgbaImage> frames, RgbaColor matteColor)
    {
        var histogram = new HistogramEntry[QuantizedColorCount];
        foreach (var frame in frames)
        {
            var pixels = frame.Pixels;
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                CompositePixel(pixels, offset, matteColor, out var r, out var g, out var b);
                var key = QuantizedKey(r, g, b);
                histogram[key].Count++;
                histogram[key].SumR += r;
                histogram[key].SumG += g;
                histogram[key].SumB += b;
            }
        }

        return histogram;
    }

    private static int FindBucketToSplit(IReadOnlyList<ColorBucket> buckets)
    {
        var bestIndex = -1;
        var bestScore = -1L;
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            if (bucket.Colors.Count < 2)
            {
                continue;
            }

            var score = bucket.RangeScore;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static void WriteHeader(Stream stream, int width, int height, byte[] palette)
    {
        stream.Write(Encoding.ASCII.GetBytes("GIF89a"));
        WriteUInt16(stream, width);
        WriteUInt16(stream, height);
        stream.WriteByte(0xF7);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.Write(palette);
    }

    private static void WriteLoopExtension(Stream stream, ushort loopCount)
    {
        stream.WriteByte(0x21);
        stream.WriteByte(0xFF);
        stream.WriteByte(0x0B);
        stream.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        stream.WriteByte(0x03);
        stream.WriteByte(0x01);
        WriteUInt16(stream, loopCount);
        stream.WriteByte(0);
    }

    private static void WriteGraphicControlExtension(Stream stream, ushort delayCentiseconds)
    {
        stream.WriteByte(0x21);
        stream.WriteByte(0xF9);
        stream.WriteByte(0x04);
        stream.WriteByte(0x04);
        WriteUInt16(stream, delayCentiseconds);
        stream.WriteByte(0);
        stream.WriteByte(0);
    }

    private static void WriteImageDescriptor(Stream stream, int left, int top, int width, int height)
    {
        stream.WriteByte(0x2C);
        WriteUInt16(stream, left);
        WriteUInt16(stream, top);
        WriteUInt16(stream, width);
        WriteUInt16(stream, height);
        stream.WriteByte(0);
    }

    private static void WriteImageData(Stream stream, byte[] indices)
    {
        const int lzwMinimumCodeSize = 8;
        stream.WriteByte(lzwMinimumCodeSize);

        var compressed = LzwEncode(indices, lzwMinimumCodeSize);
        for (var offset = 0; offset < compressed.Length; offset += 255)
        {
            var count = Math.Min(255, compressed.Length - offset);
            stream.WriteByte((byte)count);
            stream.Write(compressed, offset, count);
        }

        stream.WriteByte(0);
    }

    private static byte[] QuantizeFrame(RgbaImage frame, byte[] palette, RgbaColor matteColor)
    {
        var indices = new byte[frame.Width * frame.Height];
        var cache = new byte[QuantizedColorCount];
        var cacheSet = new bool[QuantizedColorCount];
        var pixels = frame.Pixels;
        for (var sourceOffset = 0; sourceOffset < pixels.Length; sourceOffset += 4)
        {
            CompositePixel(pixels, sourceOffset, matteColor, out var r, out var g, out var b);
            var key = QuantizedKey(r, g, b);
            if (!cacheSet[key])
            {
                cache[key] = FindNearestPaletteIndex(r, g, b, palette);
                cacheSet[key] = true;
            }

            indices[sourceOffset / 4] = cache[key];
        }

        return indices;
    }

    private static byte FindNearestPaletteIndex(byte r, byte g, byte b, byte[] palette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < GlobalColorCount; index++)
        {
            var paletteOffset = index * 3;
            var dr = r - palette[paletteOffset];
            var dg = g - palette[paletteOffset + 1];
            var db = b - palette[paletteOffset + 2];
            var distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
                if (distance == 0)
                {
                    break;
                }
            }
        }

        return (byte)bestIndex;
    }

    private static byte[] LzwEncode(byte[] indices, int minimumCodeSize)
    {
        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;
        var nextCode = endCode + 1;
        var codeSize = minimumCodeSize + 1;
        var dictionary = new Dictionary<(int Prefix, byte Suffix), int>();
        using var output = new MemoryStream();
        var writer = new LzwBitWriter(output);

        writer.Write(clearCode, codeSize);
        if (indices.Length == 0)
        {
            writer.Write(endCode, codeSize);
            writer.Flush();
            return output.ToArray();
        }

        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var suffix = indices[i];
            if (dictionary.TryGetValue((prefix, suffix), out var code))
            {
                prefix = code;
                continue;
            }

            writer.Write(prefix, codeSize);
            if (nextCode >= 4095)
            {
                writer.Write(clearCode, codeSize);
                dictionary.Clear();
                nextCode = endCode + 1;
                codeSize = minimumCodeSize + 1;
            }
            else
            {
                dictionary[(prefix, suffix)] = nextCode++;
                if (nextCode > 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }

            prefix = suffix;
        }

        writer.Write(prefix, codeSize);
        writer.Write(endCode, codeSize);
        writer.Flush();
        return output.ToArray();
    }

    private static void CompositePixel(byte[] pixels, int offset, RgbaColor matteColor, out byte r, out byte g, out byte b)
    {
        var alpha = pixels[offset + 3];
        if (alpha == byte.MaxValue)
        {
            r = pixels[offset];
            g = pixels[offset + 1];
            b = pixels[offset + 2];
            return;
        }

        if (alpha == 0)
        {
            r = matteColor.R;
            g = matteColor.G;
            b = matteColor.B;
            return;
        }

        r = CompositeChannel(pixels[offset], matteColor.R, alpha);
        g = CompositeChannel(pixels[offset + 1], matteColor.G, alpha);
        b = CompositeChannel(pixels[offset + 2], matteColor.B, alpha);
    }

    private static byte CompositeChannel(byte source, byte matte, byte alpha)
    {
        return (byte)((source * alpha + matte * (255 - alpha) + 127) / 255);
    }

    private static int QuantizedKey(byte r, byte g, byte b)
    {
        return (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3);
    }

    private static byte QuantizedChannelCenter(int value)
    {
        return (byte)Math.Clamp(value * 8 + 4, 0, 255);
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
        stream.Write(bytes);
    }

    private struct HistogramEntry
    {
        /// <summary>
        /// 表示数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public long Count;
        /// <summary>
        /// 表示累计值红色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public long SumR;
        /// <summary>
        /// 表示累计值绿色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public long SumG;
        /// <summary>
        /// 表示累计值蓝色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public long SumB;
    }

    private sealed record QuantizedColor(
        int Key,
        byte R,
        byte G,
        byte B,
        long Count,
        long SumR,
        long SumG,
        long SumB);

    private sealed record IndexedFrame(int Width, int Height, byte[] Indices);

    private readonly record struct GifRect(int Left, int Top, int Width, int Height)
    {
        /// <summary>
        /// 创建覆盖完整图像区域的矩形，用于 GIF 帧差分和写出。
        /// </summary>
        /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
        /// <param name="height">目标高度或参与尺寸计算的高度。</param>
        /// <returns>覆盖左上角到指定宽高的完整 GIF 帧矩形。</returns>
        public static GifRect Full(int width, int height)
        {
            return new GifRect(0, 0, width, height);
        }
    }

    private sealed class ColorBucket
    {
        /// <summary>
        /// 初始化颜色桶，并计算调色板切分所需的通道范围和像素权重。
        /// </summary>
        /// <param name="colors">参与颜色、透明度或混合计算的通道值。</param>
        public ColorBucket(List<QuantizedColor> colors)
        {
            Colors = colors;
            Count = colors.Sum(static color => color.Count);
            MinR = colors.Min(static color => color.R);
            MaxR = colors.Max(static color => color.R);
            MinG = colors.Min(static color => color.G);
            MaxG = colors.Max(static color => color.G);
            MinB = colors.Min(static color => color.B);
            MaxB = colors.Max(static color => color.B);
        }

        /// <summary>
        /// 获取颜色集合，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public List<QuantizedColor> Colors { get; }

        /// <summary>
        /// 获取数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public long Count { get; }

        /// <summary>
        /// 获取最小值红色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MinR { get; }

        /// <summary>
        /// 获取最大值红色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MaxR { get; }

        /// <summary>
        /// 获取最小值绿色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MinG { get; }

        /// <summary>
        /// 获取最大值绿色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MaxG { get; }

        /// <summary>
        /// 获取最小值蓝色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MinB { get; }

        /// <summary>
        /// 获取最大值蓝色通道值，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public int MaxB { get; }

        /// <summary>
        /// 获取颜色桶的最大通道跨度加权分数，用于选择下一次切分的桶。
        /// </summary>
        public long RangeScore => Math.Max(MaxR - MinR, Math.Max(MaxG - MinG, MaxB - MinB)) * Count;

        /// <summary>
        /// 拆分颜色桶，用于调色板量化过程。
        /// </summary>
        /// <param name="Left">参与几何边界、坐标或变换计算的位置值。</param>
        /// <param name="Right">参与几何边界、坐标或变换计算的位置值。</param>
        /// <returns>按主变化通道拆出的左右两个颜色桶。</returns>
        public (ColorBucket Left, ColorBucket Right) Split()
        {
            var channel = SelectSplitChannel();
            var ordered = channel switch
            {
                0 => Colors.OrderBy(static color => color.R).ThenBy(static color => color.G).ThenBy(static color => color.B).ToList(),
                1 => Colors.OrderBy(static color => color.G).ThenBy(static color => color.R).ThenBy(static color => color.B).ToList(),
                _ => Colors.OrderBy(static color => color.B).ThenBy(static color => color.R).ThenBy(static color => color.G).ToList(),
            };

            var half = Math.Max(1, Count / 2);
            var running = 0L;
            var splitIndex = 1;
            for (; splitIndex < ordered.Count; splitIndex++)
            {
                running += ordered[splitIndex - 1].Count;
                if (running >= half)
                {
                    break;
                }
            }

            splitIndex = Math.Clamp(splitIndex, 1, ordered.Count - 1);
            return (new ColorBucket(ordered.Take(splitIndex).ToList()), new ColorBucket(ordered.Skip(splitIndex).ToList()));
        }

        /// <summary>
        /// 计算颜色桶的平均 RGB 值，用于 GIF 调色板量化。
        /// </summary>
        /// <param name="R">参与颜色、透明度或混合计算的通道值。</param>
        /// <param name="G">参与颜色、透明度或混合计算的通道值。</param>
        /// <param name="B">参与颜色、透明度或混合计算的通道值。</param>
        /// <returns>当前颜色桶按像素权重计算得到的平均 RGB 值。</returns>
        public (byte R, byte G, byte B) AverageColor()
        {
            var count = Math.Max(1, Count);
            var r = Colors.Sum(static color => color.SumR) / count;
            var g = Colors.Sum(static color => color.SumG) / count;
            var b = Colors.Sum(static color => color.SumB) / count;
            return ((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        }

        private int SelectSplitChannel()
        {
            var rangeR = MaxR - MinR;
            var rangeG = MaxG - MinG;
            var rangeB = MaxB - MinB;
            if (rangeR >= rangeG && rangeR >= rangeB)
            {
                return 0;
            }

            return rangeG >= rangeB ? 1 : 2;
        }
    }

    private sealed class LzwBitWriter
    {
        private readonly Stream _stream;
        private int _bitBuffer;
        private int _bitCount;

        /// <summary>
        /// 初始化LzwBit写出器 实例，并保存调用方提供的核心数据。
        /// </summary>
        /// <param name="stream">承载输入或输出字节的流。</param>
        public LzwBitWriter(Stream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// 写入 GIF LZW 码流中的一个变长编码值。
        /// </summary>
        /// <param name="code">参与本次处理的代码。</param>
        /// <param name="bitCount">写入该编码值时占用的有效位数。</param>
        public void Write(int code, int bitCount)
        {
            _bitBuffer |= code << _bitCount;
            _bitCount += bitCount;
            while (_bitCount >= 8)
            {
                _stream.WriteByte((byte)(_bitBuffer & 0xFF));
                _bitBuffer >>= 8;
                _bitCount -= 8;
            }
        }

        /// <summary>
        /// 刷新缓冲位流，确保 GIF LZW 编码数据完整写出。
        /// </summary>
        public void Flush()
        {
            if (_bitCount > 0)
            {
                _stream.WriteByte((byte)(_bitBuffer & 0xFF));
                _bitBuffer = 0;
                _bitCount = 0;
            }
        }
    }
}
