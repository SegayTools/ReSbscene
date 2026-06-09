using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Tests;

public sealed class SbSceneGifRendererTests
{
    [Fact]
    public void RenderUsesFrameRangeFpsAndProportionalTargetSize()
    {
        var scene = EmptyScene();
        using var svo = new TemporaryFile(BuildMinimalSvo());

        var result = SbSceneGifRenderer.Render(
            scene,
            svo.Path,
            new SbSceneRenderOptions(),
            new SbSceneGifRenderOptions
            {
                Fps = 30,
                FrameRange = new SbSceneGifFrameRange(5, 65),
                TargetWidth = 240,
            });

        Assert.Equal(30, result.Frames.Count);
        Assert.Equal(240, result.Width);
        Assert.Equal(200, result.Height);
        Assert.Equal(30, result.Fps);
        Assert.Equal(5, result.StartFrame);
        Assert.Equal(65, result.EndFrame);
        Assert.Equal(0, result.RenderedItemCount);
        Assert.Equal(0, result.CandidateItemCount);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void RenderDeduplicatesAnimationWarnings()
    {
        var scene = EmptyScene();
        using var svo = new TemporaryFile(BuildMinimalSvo());
        var renderOptions = new SbSceneRenderOptions
        {
            Animations =
            [
                new SbSceneAnimationSelection("Missing", 0),
                new SbSceneAnimationSelection("Missing", 0),
            ],
        };

        var result = SbSceneGifRenderer.Render(scene, svo.Path, renderOptions, new SbSceneGifRenderOptions());

        Assert.Equal(["Animation 'Missing' was not found."], result.Warnings);
    }

    private static SbSceneFile EmptyScene()
    {
        return new SbSceneFile
        {
            SourcePath = "test.sbscene",
            SourceSize = 0,
            Vtbf = new VtbfDocument
            {
                Magic = "VTBF",
                Length = 0,
                Blocks = [],
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
            Surfboard = new SurfboardModel
            {
                Objects = [],
                Nodes = [],
                Transform2DRecords = [],
                NodeCategoryRecords = [],
                NodeCategoryDetails = [],
                NodeGroups = [],
                Resources = new SbSceneResourceMap
                {
                    Atlases = [],
                    ImageCasts = [],
                    CnumRecords = [],
                    CrfdRecords = [],
                    TextRecords = [],
                    SliceCasts = [],
                },
                Camera = null,
                Animations = [],
                AnimationBindings = [],
                VariantHints = [],
                UnknownFields = [],
            },
            Summary = new ParseSummary
            {
                RootBlockCount = 0,
                TotalBlockCount = 0,
                NodeCount = 0,
                AnimationCount = 0,
                VariantHintCount = 0,
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
        };
    }

    private static byte[] BuildMinimalSvo()
    {
        const int headerSize = 0x80;
        const int entrySize = 0x400;
        const int payloadOffset = headerSize + entrySize;
        var dds = BuildA8R8G8B8Dds();
        var data = new byte[payloadOffset + dds.Length];
        Encoding.ASCII.GetBytes("AVTS").CopyTo(data, 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x08, 4), 0x15);

        Encoding.ASCII.GetBytes("atlas.dds").CopyTo(data, headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(headerSize + 0x200, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(headerSize + 0x204, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(headerSize + 0x208, 4), dds.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(headerSize + 0x20C, 4), payloadOffset);
        dds.CopyTo(data.AsSpan(payloadOffset));
        return data;
    }

    private static byte[] BuildA8R8G8B8Dds()
    {
        var data = new byte[132];
        Encoding.ASCII.GetBytes("DDS ").CopyTo(data, 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8, 4), 0x00081007);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(20, 4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80, 4), 0x41);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92, 4), 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96, 4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100, 4), 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104, 4), 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(108, 4), 0x1000);
        data[128] = 0;
        data[129] = 0;
        data[130] = 0;
        data[131] = byte.MaxValue;
        return data;
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(byte[] bytes)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.svo");
            File.WriteAllBytes(Path, bytes);
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
