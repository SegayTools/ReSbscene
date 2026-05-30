using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Resources;

namespace SbScene.Core.Tests;

public sealed class SvoResourceParserTests
{
    [Fact]
    public void ParsesAvtsDirectoryReservedOffsets()
    {
        var data = BuildMinimalAvts();

        var header = SvoResourceParser.ParseHeader(data);
        var entries = SvoResourceParser.ParseDirectory(data);

        Assert.Equal("AVTS", header.Magic);
        Assert.Equal(2, header.DirectoryCount);
        Assert.Equal(0x80, header.HeaderSize);
        Assert.Equal(0x400, header.DirectoryEntrySize);
        Assert.Equal(2, entries.Count);
        Assert.Equal("YABX", entries[0].DataMagic);
        Assert.Equal("DDS ", entries[1].DataMagic);
        Assert.True(entries[1].IsDds);
        Assert.Equal(2, entries[1].ReservedNonZeroByteCount);
        Assert.Equal([0x210, 0x3FF], entries[1].ReservedNonZeroByteOffsets);
    }

    private static byte[] BuildMinimalAvts()
    {
        const int headerSize = 0x80;
        const int entrySize = 0x400;
        const int entryCount = 2;
        const int payloadOffset = headerSize + entrySize * entryCount;

        var data = new byte[payloadOffset + 8];
        Encoding.ASCII.GetBytes("AVTS").CopyTo(data, 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), entryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x08, 4), 0x15);

        WriteEntry(data, 0, "meta.svo", 0, 0, payloadOffset, 4);
        WriteEntry(data, 1, "atlas.dds", 1, 1, payloadOffset + 4, 4);
        data[headerSize + entrySize + 0x210] = 0xAA;
        data[headerSize + entrySize + 0x3FF] = 0xBB;

        Encoding.ASCII.GetBytes("YABX").CopyTo(data, payloadOffset);
        Encoding.ASCII.GetBytes("DDS ").CopyTo(data, payloadOffset + 4);
        return data;
    }

    private static void WriteEntry(byte[] data, int index, string name, int kind, int sequence, int dataOffset, int dataLength)
    {
        const int headerSize = 0x80;
        const int entrySize = 0x400;

        var offset = headerSize + entrySize * index;
        Encoding.ASCII.GetBytes(name).CopyTo(data, offset);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x200, 4), kind);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x204, 4), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x208, 4), dataLength);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x20C, 4), dataOffset);
    }
}
