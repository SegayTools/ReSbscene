using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SbScene.Core.Images;

/// <summary>
/// 提供PNG写出器，负责把内存数据编码并写入目标文件。
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// 将 RGBA 图像编码为 PNG 文件，供渲染和导出流程写出静态帧。
    /// </summary>
    /// <param name="path">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="image">参与本次处理的图像或输入对象。</param>
    /// <example>
    /// <code>
    /// var image = new RgbaImage(1, 1, new byte[] { 255, 255, 255, 255 });
    /// PngWriter.Write("pixel.png", image);
    /// </code>
    /// </example>
    public static void Write(string path, RgbaImage image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = File.Create(path);
        stream.Write(Signature);
        WriteChunk(stream, "IHDR", BuildIhdr(image.Width, image.Height));
        WriteChunk(stream, "IDAT", CompressScanlines(image));
        WriteChunk(stream, "IEND", []);
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = 8;
        data[9] = 6;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        return data;
    }

    private static byte[] CompressScanlines(RgbaImage image)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < image.Height; y++)
        {
            raw.WriteByte(0);
            raw.Write(image.Pixels, y * image.Width * 4, image.Width * 4);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        var crc = Crc32.Compute(crcInput);

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        /// <summary>
        /// 计算 PNG chunk 的 CRC32 校验值，用于写出符合格式要求的数据块。
        /// </summary>
        /// <param name="data">待解析、解码或写出的原始字节数据。</param>
        /// <returns>输入字节序列对应的 CRC32 值。</returns>
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }

                table[i] = crc;
            }

            return table;
        }
    }
}
