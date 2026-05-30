using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using SbScene.Core.Output;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Tests;

public sealed class VtbfParserTests
{
    [Fact]
    public void RejectsNonVtbfHeader()
    {
        var parser = new VtbfParser();

        Assert.Throws<VtbfParseException>(() => parser.Parse(Encoding.ASCII.GetBytes("NOPE")));
    }

    [Fact]
    public void RejectsTruncatedBlock()
    {
        var bytes = new byte[24];
        Encoding.ASCII.GetBytes("VTBF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("vtc0").CopyTo(bytes, 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), 1024);

        var parser = new VtbfParser();

        Assert.Throws<VtbfParseException>(() => parser.Parse(bytes));
    }

    [Fact]
    public void ParsesSyntheticSceneTreeAndAnimation()
    {
        var data = SyntheticSceneBuilder.BuildMinimalScene();
        var parser = new SbSceneParser();
        var file = parser.ParseFile(data.Path);

        Assert.Equal("VTBF", file.Vtbf.Magic);
        Assert.Equal(data.Bytes.Length, file.Vtbf.Length);
        Assert.Equal(1, file.Summary.RootBlockCount);
        Assert.True(file.Summary.BlockCounts.ContainsKey("SRFF"));
        Assert.True(file.Summary.BlockCounts.ContainsKey("NODE"));
        Assert.True(file.Summary.BlockCounts.ContainsKey("ANIM"));
        Assert.True(file.Summary.BlockCounts.ContainsKey("MOT "));
        Assert.True(file.Summary.BlockCounts.ContainsKey("TRK "));
        Assert.True(file.Summary.BlockCounts.ContainsKey("KEY "));
        Assert.Equal(2, file.Summary.NodeCount);
        Assert.Single(file.Surfboard.Animations);
        Assert.Contains(file.Surfboard.NodeGroups, group => group.Name == "plain" && group.Count == 1);
        Assert.Contains(file.Surfboard.NodeGroups, group => group.Name == "uniform" && group.Count == 1);
        Assert.Contains(file.Surfboard.AnimationBindings, binding => binding.AnimationName == "Change_FashionV01" && binding.NodeName == "plain_body");
        Assert.Contains(file.Surfboard.VariantHints, hint => hint.Category == "Fashion" && hint.Name == "Change_FashionV01");
        Assert.Contains(file.Surfboard.VariantHints, hint => hint.SourceKind == "TrackState");
    }

    [Fact]
    public void ParsesCompactType04RawByteAndFollowingFields()
    {
        var payload = CombineTestBytes(
            [0xFC, 0x00],
            [0x0E, 0x06, 0x01, 0x00],
            [0x0D, 0x04, 0x09],
            [0x0F, 0x02, 0x04],
            Encoding.ASCII.GetBytes("test"),
            [0xFD, 0x00]);
        var block = CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes("NCAT"),
            UInt16Test(0),
            UInt16Test(5),
            payload);
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            block);

        var document = new VtbfParser().Parse(bytes);
        var ncat = document.Blocks.Single().Children.Single(static child => child.Tag == "NCAT");

        Assert.Empty(document.Warnings);
        var rawByte = Assert.Single(ncat.Fields.Where(static field => field.Id == 0x0D));
        Assert.Equal("RawByte", rawByte.TypeName);
        Assert.Equal([9], rawByte.Int64Values);
        var followingString = Assert.Single(ncat.Fields.Where(static field => field.Id == 0x0F));
        Assert.Equal("test", followingString.StringValue);
        Assert.Null(ncat.TrailingBytes);
    }

    [Fact]
    public void ParsesCompactType03RawByteAndFollowingRecord()
    {
        var payload = CombineTestBytes(
            [0xFC, 0x00],
            [0x03, 0x02, 0x0A],
            Encoding.ASCII.GetBytes("fontScroll"),
            [0x0D, 0x04, 0x00],
            [0x0F, 0x03, 0x01],
            [0xFE, 0x00],
            [0x0E, 0x06, 0x05, 0x00],
            [0xFD, 0x00]);
        var block = CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes("NCAT"),
            UInt16Test(0),
            UInt16Test(5),
            payload);
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            block);

        var document = new VtbfParser().Parse(bytes);
        var ncat = document.Blocks.Single().Children.Single(static child => child.Tag == "NCAT");

        Assert.Empty(document.Warnings);
        var rawByte = Assert.Single(ncat.Fields.Where(static field => field.Id == 0x0F));
        Assert.Equal("RawByte03", rawByte.TypeName);
        Assert.Equal([1], rawByte.Int64Values);
        Assert.Contains(ncat.Fields, static field => field.Id == 0x0E && field.Int64Values?.SingleOrDefault() == 5);
        Assert.Null(ncat.TrailingBytes);
    }

    [Fact]
    public void NodeCategoryDetailsKeepMixedParameterFieldOccurrences()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("NCAT", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "ExtParamData"),
                    CompactRawByteField(0x0D, 9),
                    CompactUInt16Field(0x0E, 1),
                    CompactStringField(0x0F, "alpha"),
                    CompactRawByte03Field(0x0F, 1),
                    CompactInt32Field(0x0F, -2),
                    CompactFloatField(0x0F, 1.5f)))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var record = Assert.Single(model.NodeCategoryDetails);

        Assert.Empty(document.Warnings);
        Assert.Equal("ExtParamData", record.KindName);
        Assert.Equal(9, record.TypeByte);
        Assert.Equal(1, record.CategoryId);
        Assert.Equal("alpha", record.ParameterPreview);
        Assert.Equal("alpha", record.ParameterString);

        var parameterFields = record.Fields.Where(static field => field.IdHex == "0x000F").ToArray();
        Assert.Equal(["String", "RawByte03", "Int32", "Float32"], parameterFields.Select(static field => field.TypeName));
        Assert.Equal("alpha", parameterFields[0].StringValue);
        Assert.Equal([1], parameterFields[1].Int64Values);
        Assert.Equal([-2], parameterFields[2].Int64Values);
        Assert.Equal([1.5], parameterFields[3].Float64Values);
    }

    [Fact]
    public void UnknownCompactFieldKeepsHeaderInTrailingBytes()
    {
        var payload = CombineTestBytes(
            [0x0D, 0x7F],
            [0xAA]);
        var block = CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes("CNUM"),
            UInt16Test(1),
            UInt16Test(1),
            payload);
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            block);

        var document = new VtbfParser().Parse(bytes);
        var cnum = document.Blocks.Single().Children.Single(static child => child.Tag == "CNUM");

        Assert.Contains(document.Warnings, warning => warning.Contains("unknown compact field id=0x0D, type=0x7F", StringComparison.Ordinal));
        Assert.Empty(cnum.Fields);
        Assert.Equal(payload, cnum.TrailingBytes);
    }

    [Fact]
    public void ParsesCompactType00AndSupplementalCnumFields()
    {
        var payload = CombineTestBytes(
            [0x0D, 0x00],
            [0xA1, 0x02, 0x02],
            Encoding.ASCII.GetBytes("88"));
        var block = CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes("CNUM"),
            UInt16Test(1),
            UInt16Test(1),
            payload);
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            block);

        var document = new VtbfParser().Parse(bytes);
        var cnum = document.Blocks.Single().Children.Single(static child => child.Tag == "CNUM");

        Assert.Empty(document.Warnings);
        Assert.Null(cnum.TrailingBytes);
        var marker = Assert.Single(cnum.Fields.Where(static field => field.Id == 0x0D));
        Assert.Equal("ZeroLengthMarker", marker.TypeName);
        Assert.Empty(marker.Raw);
        var supplemental = Assert.Single(cnum.Fields.Where(static field => field.Id == 0xA1));
        Assert.Equal("88", supplemental.StringValue);
    }

    [Fact]
    public void ParsesSlicRecordMarkers()
    {
        var payload = CombineTestBytes(
            [0x83, 0x09, 0x03, 0x00, 0x00, 0x00],
            [0x40, 0x06, 0x28, 0x00],
            [0xFE, 0x00],
            [0x83, 0x09, 0x02, 0x00, 0x00, 0x00],
            [0x40, 0x06, 0x27, 0x00],
            [0xFD, 0x00]);
        var block = CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes("SLIC"),
            UInt16Test(0),
            UInt16Test(4),
            payload);
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            block);

        var document = new VtbfParser().Parse(bytes);
        var slic = document.Blocks.Single().Children.Single(static child => child.Tag == "SLIC");

        Assert.Empty(document.Warnings);
        Assert.Null(slic.TrailingBytes);
        Assert.Contains(slic.Fields, static field => field.TypeName == "RecordStart");
        Assert.Contains(slic.Fields, static field => field.TypeName == "RecordEnd");
        Assert.Equal(2, slic.Fields.Count(static field => field.Id == 0x83));
    }

    [Fact]
    public void AggregatesRecordNodesTransformsAndCategoriesAcrossBlocks()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("NODE", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "first"),
                    CompactUInt32Field(0x30, 0x101)),
                CombineTestBytes(
                    CompactStringField(0x03, "second"),
                    CompactUInt32Field(0x30, 0x102)))),
            LinearCompactBlock("TRS2", 0, 0, CompactRecords(
                CompactByteField(0x3A, 0),
                CompactByteField(0x3A, 1))),
            LinearCompactBlock("NCAT", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "kind-a"),
                    CompactByteField(0x0D, 0),
                    CompactUInt16Field(0x0E, 1)),
                CombineTestBytes(
                    CompactStringField(0x03, "kind-b"),
                    CompactByteField(0x0D, 0),
                    CompactUInt16Field(0x0E, 2)))),
            LinearCompactBlock("NODE", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "third"),
                    CompactUInt32Field(0x30, 0x103)))),
            LinearCompactBlock("TRS2", 0, 0, CompactRecords(
                CompactByteField(0x3A, 1))),
            LinearCompactBlock("NCAT", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "kind-c"),
                    CompactByteField(0x0D, 0),
                    CompactUInt16Field(0x0E, 4)))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);

        Assert.Empty(document.Warnings);
        Assert.Equal([1, 2, 4], model.NodeCategoryRecords);
        Assert.Equal(3, model.NodeCategoryDetails.Count);
        Assert.Equal(3, model.Nodes.Count);
        Assert.Equal([0, 1, 2], model.Nodes.Select(static node => node.Index));
        Assert.Equal(["first", "second", "third"], model.Nodes.Select(static node => node.Name));
        Assert.Equal([1, 2, 4], model.Nodes.Select(static node => node.CategoryId));
        Assert.Equal([0, 1, 2], model.Transform2DRecords.Select(static transform => transform.Index));
        Assert.Equal([false, true, true], model.Nodes.Select(static node => node.Transform2D?.Display));
    }

    [Fact]
    public void ParsesTextureAtlasStateWordAndCropRecords()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("TEXL", 1, 2, CombineTestBytes(
                CompactStringField(0x03, "main_textures"),
                CompactUInt16Field(0x60, 1))),
            LinearCompactBlock("TEX ", 0, 5, CombineTestBytes(
                CompactUInt16Field(0x40, 256),
                CompactUInt16Field(0x41, 128),
                CompactStringField(0x61, "atlas_a"),
                CompactUInt32Field(0x62, 0x110),
                CompactUInt16Field(0x63, 1))),
            LinearCompactBlock("CROP", 0, 1, CompactPackedRecordField(0x65, 1, 2, 3, 130, 67)));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var atlas = Assert.Single(model.Resources.Atlases);
        var crop = Assert.Single(atlas.Crops);

        Assert.Empty(document.Warnings);
        Assert.Equal("main_textures", model.Resources.TextureListName);
        Assert.Equal(1, model.Resources.DeclaredTextureCount);
        Assert.Equal("atlas_a", atlas.Name);
        Assert.Equal(256, atlas.Width);
        Assert.Equal(128, atlas.Height);
        Assert.Equal(0x110, atlas.Field62);
        Assert.Equal([4, 8], atlas.Field62Bits);
        Assert.Equal(1, atlas.DeclaredCropCount);
        Assert.Equal(1, crop.Kind);
        Assert.Equal(2, crop.Left);
        Assert.Equal(3, crop.Top);
        Assert.Equal(130, crop.Right);
        Assert.Equal(67, crop.Bottom);

        var json = JsonSerializer.Serialize(model.Resources.Atlases, SbSceneJson.CreateOptions(indented: true));
        using var jsonDocument = JsonDocument.Parse(json);
        var jsonAtlas = jsonDocument.RootElement[0];
        Assert.Equal(0x110, jsonAtlas.GetProperty("field62").GetInt32());
        Assert.Equal([4, 8], jsonAtlas.GetProperty("field62Bits").EnumerateArray().Select(static item => item.GetInt32()));
    }

    [Fact]
    public void ParsesCsliAndSlicResourceRecords()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("NODE", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "slice_node"),
                    CompactUInt32Field(0x30, 0xF01)))),
            LinearCompactBlock("CSLI", 2, 13, CombineTestBytes(
                CompactFloatField(0x40, 10f),
                CompactFloatField(0x41, 20f),
                CompactFloatField(0x42, 0f),
                CompactFloatField(0x43, 5f),
                CompactUInt16Field(0x44, 2),
                CompactUInt16Field(0x51, 0),
                CompactUInt32Field(0x80, 0x8000),
                CompactRawByteField(0x81, 3),
                CompactRawByteField(0x82, 1),
                CompactRawByteField(0x84, 3),
                CompactRawByteField(0x85, 1),
                CompactFloatField(0x86, 30f),
                CompactFloatField(0x87, 40f))),
            LinearCompactBlock("CREF", 0, 2, CombineTestBytes(
                CompactCrefField(0, 0, 0),
                CompactCrefField(0, 0, 1))),
            LinearCompactBlock("SLIC", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactUInt32Field(0x83, 3),
                    CompactUInt16Field(0x40, 10),
                    CompactUInt16Field(0x41, 20),
                    CompactInt16Field(0x45, 0),
                    CompactColorField(0x37, 255, 255, 255, 255),
                    CompactColorField(0x39, 255, 1, 2, 3),
                    CompactColorField(0x39, 255, 4, 5, 6),
                    CompactColorField(0x39, 255, 7, 8, 9),
                    CompactColorField(0x39, 255, 10, 11, 12),
                    CompactColorField(0x38, 255, 0, 0, 0)),
                CombineTestBytes(
                    CompactUInt32Field(0x83, 3),
                    CompactUInt16Field(0x40, 30),
                    CompactUInt16Field(0x41, 20),
                    CompactInt16Field(0x45, 1),
                    CompactColorField(0x37, 255, 128, 64, 32),
                    CompactColorField(0x39, 254, 1, 2, 3),
                    CompactColorField(0x39, 253, 4, 5, 6),
                    CompactColorField(0x39, 252, 7, 8, 9),
                    CompactColorField(0x39, 251, 10, 11, 12),
                    CompactColorField(0x38, 255, 0, 0, 0)))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var sliceCast = Assert.Single(model.Resources.SliceCasts);

        Assert.Empty(document.Warnings);
        Assert.Equal("slice_node", sliceCast.NodeName);
        Assert.Equal(2, sliceCast.Field44Count);
        Assert.Equal(10f, sliceCast.Field40);
        Assert.Equal(20f, sliceCast.Field41);
        Assert.Equal(0f, sliceCast.Field42);
        Assert.Equal(5f, sliceCast.Field43);
        Assert.Equal(0x8000, sliceCast.Field80);
        Assert.Equal(3, sliceCast.Field81);
        Assert.Equal(1, sliceCast.Field82);
        Assert.Equal(3, sliceCast.Field84);
        Assert.Equal(1, sliceCast.Field85);
        Assert.Equal(30f, sliceCast.Field86);
        Assert.Equal(40f, sliceCast.Field87);
        Assert.True(sliceCast.SlicRecordCountMatchesField44);
        Assert.True(sliceCast.CropReferenceCountMatchesField44);
        Assert.Equal(2, sliceCast.CropReferences.Count);
        Assert.Equal(["01000000000000", "01000000000100"], sliceCast.CropReferences.Select(static reference => reference.RawHex));
        Assert.Equal(2, sliceCast.Slices.Count);
        Assert.Equal([3, 3], sliceCast.Slices.Select(static slice => slice.Field83));
        Assert.Equal([0, 1], sliceCast.Slices.Select(static slice => slice.Field45));
        Assert.Equal("#FF804020", sliceCast.Slices[1].Field37Color?.Hex);
        Assert.Equal("FF804020", sliceCast.Slices[1].Field37RawHex);
        Assert.Equal("#FF000000", sliceCast.Slices[1].Field38Color?.Hex);
        Assert.Equal("FF000000", sliceCast.Slices[1].Field38RawHex);
        Assert.Equal(["#FE010203", "#FD040506", "#FC070809", "#FB0A0B0C"], sliceCast.Slices[1].Field39Colors.Select(static color => color.Hex));
        Assert.Equal(["FE010203", "FD040506", "FC070809", "FB0A0B0C"], sliceCast.Slices[1].Field39RawHexValues);

        var json = JsonSerializer.Serialize(model.Resources.SliceCasts, SbSceneJson.CreateOptions(indented: true));
        using var jsonDocument = JsonDocument.Parse(json);
        var jsonSlice = jsonDocument.RootElement[0].GetProperty("slices")[1];
        Assert.Equal("FF804020", jsonSlice.GetProperty("field37RawHex").GetString());
        Assert.Equal("FF000000", jsonSlice.GetProperty("field38RawHex").GetString());
        Assert.Equal("FE010203", jsonSlice.GetProperty("field39RawHexValues")[0].GetString());
    }

    [Fact]
    public void ParsesCrfdRawResourceRecords()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("NODE", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "crfd_node"),
                    CompactUInt32Field(0x30, 0xF01)))),
            LinearCompactBlock("CRFD", 0, 7, CombineTestBytes(
                CompactUInt16Field(0x51, 0),
                CompactStringField(0x90, "MM_UI_Test"),
                CompactStringField(0x91, "Reference_Test"),
                CompactInt16Field(0x92, 2),
                CompactInt16Field(0x93, -1),
                CompactFloatField(0x94, 0f),
                CompactByteField(0x95, 0))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var record = Assert.Single(model.Resources.CrfdRecords);

        Assert.Empty(document.Warnings);
        Assert.Equal(0, record.Field51);
        Assert.Equal("crfd_node", record.NodeName);
        Assert.Equal("MM_UI_Test", record.Field90);
        Assert.Equal("4D4D5F55495F54657374", record.Field90RawHex);
        Assert.Equal("Reference_Test", record.Field91);
        Assert.Equal("5265666572656E63655F54657374", record.Field91RawHex);
        Assert.Equal(2, record.Field92);
        Assert.Equal(-1, record.Field93);
        Assert.Equal(0f, record.Field94);
        Assert.Equal(0, record.Field95);
        Assert.Equal(7, record.Fields.Count);

        var json = JsonSerializer.Serialize(model.Resources.CrfdRecords, SbSceneJson.CreateOptions(indented: true));
        using var jsonDocument = JsonDocument.Parse(json);
        var jsonRecord = jsonDocument.RootElement[0];
        Assert.Equal("4D4D5F55495F54657374", jsonRecord.GetProperty("field90RawHex").GetString());
        Assert.Equal("5265666572656E63655F54657374", jsonRecord.GetProperty("field91RawHex").GetString());
    }

    [Fact]
    public void ParsesCnumRawResourceRecordsWithFollowingCref()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("NODE", 0, 0, CompactRecords(
                CombineTestBytes(
                    CompactStringField(0x03, "cnum_node"),
                    CompactUInt32Field(0x30, 0xF01)))),
            LinearCompactBlock("CNUM", 1, 26, CombineTestBytes(
                CompactUInt32Field(0x48, 0x8000),
                CompactUInt16Field(0x51, 0),
                CompactFloatField(0x40, 10f),
                CompactFloatField(0x42, 20f),
                CompactFloatField(0x43, 30f),
                CompactUInt16Field(0x44, 2),
                CompactColorField(0x39, 0xFF, 0x01, 0x02, 0x03),
                CompactColorField(0x39, 0xFE, 0x04, 0x05, 0x06),
                CompactColorField(0x39, 0xFD, 0x07, 0x08, 0x09),
                CompactColorField(0x39, 0xFC, 0x0A, 0x0B, 0x0C),
                CompactUInt32Field(0xA0, 65),
                CompactUInt16Field(0xA2, 1),
                CompactUInt16Field(0xA3, 2),
                CompactUInt16Field(0xA4, 3),
                CompactUInt16Field(0xA5, 4),
                CompactRawByteField(0xA6, 5),
                CompactRawByte03Field(0xA7, 6),
                CompactRawByteField(0xA8, 7),
                CompactRawByte03Field(0xAA, 8),
                CompactRawByte03Field(0xAB, 9),
                CompactRawByte03Field(0xA9, 10),
                CompactRawByte03Field(0xAC, 11),
                CompactRawByteField(0xAD, 12),
                CompactRawVector2Field(0xAE, 1.25f, -3.5f),
                CompactPackedRecordField(0xAF, 3, 7, 8, 9),
                [0x0D, 0x00],
                CompactStringField(0xA1, "12"))),
            LinearCompactBlock("CREF", 0, 2, CombineTestBytes(
                CompactCrefField(0, 0, 10),
                CompactCrefField(0, 0, 11))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var record = Assert.Single(model.Resources.CnumRecords);

        Assert.Empty(document.Warnings);
        Assert.Equal("cnum_node", record.NodeName);
        Assert.Equal(2, record.Field44Count);
        Assert.Equal(0x8000, record.Field48);
        Assert.Equal(10f, record.Field40);
        Assert.Equal(20f, record.Field42);
        Assert.Equal(30f, record.Field43);
        Assert.Equal(["#FF010203", "#FE040506", "#FD070809", "#FC0A0B0C"], record.Field39Colors?.Select(static color => color.Hex));
        Assert.Equal(["FF010203", "FE040506", "FD070809", "FC0A0B0C"], record.Field39RawHexValues);
        Assert.Equal(65, record.FieldA0);
        Assert.Equal("12", record.FieldA1);
        Assert.Equal("3132", record.FieldA1RawHex);
        Assert.Equal(1, record.FieldA2);
        Assert.Equal(2, record.FieldA3);
        Assert.Equal(3, record.FieldA4);
        Assert.Equal(4, record.FieldA5);
        Assert.Equal(5, record.FieldA6);
        Assert.Equal(6, record.FieldA7);
        Assert.Equal(7, record.FieldA8);
        Assert.Equal(10, record.FieldA9);
        Assert.Equal(8, record.FieldAA);
        Assert.Equal(9, record.FieldAB);
        Assert.Equal(11, record.FieldAC);
        Assert.Equal(12, record.FieldAD);
        Assert.Equal("020000A03F000060C0", record.FieldAERawHex);
        Assert.Equal([1.25f, -3.5f], record.FieldAEFloatValues);
        Assert.Equal("03070008000900", record.FieldAFRawHex);
        Assert.Equal([3, 7, 8, 9], record.FieldAFPackedValues);
        Assert.Equal([0x0D], record.ZeroLengthMarkerFieldIds);
        Assert.True(record.CropReferenceCountMatchesField44);
        Assert.Equal([10, 11], record.CropReferences.Select(static reference => reference.CropIndex));
        Assert.Equal(["01000000000A00", "01000000000B00"], record.CropReferences.Select(static reference => reference.RawHex));

        var json = JsonSerializer.Serialize(model.Resources.CnumRecords, SbSceneJson.CreateOptions(indented: true));
        using var jsonDocument = JsonDocument.Parse(json);
        var jsonRecord = jsonDocument.RootElement[0];
        Assert.Equal("FF010203", jsonRecord.GetProperty("field39RawHexValues")[0].GetString());
        Assert.Equal("#FF010203", jsonRecord.GetProperty("field39Colors")[0].GetProperty("hex").GetString());
        Assert.Equal("020000A03F000060C0", jsonRecord.GetProperty("fieldAERawHex").GetString());
        Assert.Equal([1.25, -3.5], jsonRecord.GetProperty("fieldAEFloatValues").EnumerateArray().Select(static item => item.GetDouble()));
        Assert.Equal("03070008000900", jsonRecord.GetProperty("fieldAFRawHex").GetString());
        Assert.Equal([3, 7, 8, 9], jsonRecord.GetProperty("fieldAFPackedValues").EnumerateArray().Select(static item => item.GetInt32()));
    }

    [Fact]
    public void ParsesTextRawRecords()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("TEXT", 0, 7, CombineTestBytes(
                [0x00, 0x00],
                CompactRawVector2Field(0x33, 1.5f, -2.25f),
                CompactInt16Field(0x41, 16),
                CompactUInt32Field(0x78, 23),
                CompactInt16Field(0x79, -1),
                CompactStringField(0x7A, "hello"),
                CompactPackedRecordField(0x7B, 2, 0, 0, 0),
                CompactInt16Field(0x7C, 0))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var record = Assert.Single(model.Resources.TextRecords);

        Assert.Empty(document.Warnings);
        Assert.Equal([0x00], record.ZeroLengthMarkerFieldIds);
        Assert.Equal("020000C03F000010C0", record.Field33RawHex);
        Assert.Equal(1.5f, record.Field33Vector?.X);
        Assert.Equal(-2.25f, record.Field33Vector?.Y);
        Assert.Equal(16, record.Field41);
        Assert.Equal(23, record.Field78);
        Assert.Equal(-1, record.Field79);
        Assert.Equal("hello", record.Field7A);
        Assert.Equal("hello", record.Field7AShiftJis);
        Assert.Equal("68656C6C6F", record.Field7ARawHex);
        Assert.Equal("02000000000000", record.Field7BRawHex);
        Assert.Equal([2, 0, 0, 0], record.Field7BPackedValues);
        Assert.Equal(0, record.Field7C);
    }

    [Fact]
    public void ParsesTextShiftJisCandidate()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("TEXT", 0, 7, CombineTestBytes(
                [0x00, 0x00],
                CompactRawStringField(0x7A, 0x82, 0xA0, 0x82, 0xA2))));

        var document = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(document);
        var record = Assert.Single(model.Resources.TextRecords);

        Assert.Empty(document.Warnings);
        Assert.Equal("\u3042\u3044", record.Field7AShiftJis);
        Assert.Equal("82A082A2", record.Field7ARawHex);
    }

    [Fact]
    public void JsonAndMarkdownExportsContainExpectedSections()
    {
        var data = SyntheticSceneBuilder.BuildMinimalScene();
        var file = new SbSceneParser().ParseFile(data.Path);

        var json = JsonSerializer.Serialize(file, SbSceneJson.CreateOptions(indented: true));
        using var document = JsonDocument.Parse(json);

        Assert.Equal(2, document.RootElement.GetProperty("summary").GetProperty("nodeCount").GetInt32());

        var markdown = MarkdownExporter.ToMarkdown(file);
        Assert.Contains("## 动画列表", markdown);
        Assert.Contains("## 字段目录", markdown);
        Assert.Contains("ParamRawHex", markdown);
        Assert.Contains("`NODE.0x30` bit 分布", markdown);
        Assert.Contains("## 纹理与 Image Cast", markdown);
        Assert.Contains("## Track flags 与 key value 存储", markdown);
        Assert.Contains("## TRK.0x57 key count", markdown);
        Assert.Contains("## KEY 插值候选", markdown);
        Assert.Contains("## 动画到节点绑定样例", markdown);
        Assert.Contains("Change_FashionV01", markdown);
        Assert.Contains("plain", markdown);
        Assert.Contains("疑似开关与状态", markdown);
    }

    [Fact]
    public void JsonExportIncludesTextField7BPackedRecord()
    {
        var bytes = CombineTestBytes(
            Encoding.ASCII.GetBytes("VTBF"),
            Int32Test(0x10),
            Encoding.ASCII.GetBytes("SRFF"),
            [0x01, 0x00, 0x00, 0x4C],
            LinearCompactBlock("TEXT", 0, 7, CombineTestBytes(
                [0x00, 0x00],
                CompactRawVector2Field(0x33, 1.5f, -2.25f),
                CompactInt16Field(0x41, 16),
                CompactUInt32Field(0x78, 23),
                CompactInt16Field(0x79, -1),
                CompactStringField(0x7A, "hello"),
                CompactPackedRecordField(0x7B, 2, 0, 0, 0),
                CompactInt16Field(0x7C, 0))));

        var parsed = new VtbfParser().Parse(bytes);
        var model = new SbSceneParser().Analyze(parsed);
        var json = JsonSerializer.Serialize(model.Resources.TextRecords, SbSceneJson.CreateOptions(indented: true));
        using var document = JsonDocument.Parse(json);
        var record = document.RootElement[0];

        Assert.Equal("020000C03F000010C0", record.GetProperty("field33RawHex").GetString());
        Assert.Equal(1.5, record.GetProperty("field33Vector").GetProperty("x").GetDouble());
        Assert.Equal(-2.25, record.GetProperty("field33Vector").GetProperty("y").GetDouble());
        Assert.Equal("hello", record.GetProperty("field7AShiftJis").GetString());
        Assert.Equal("02000000000000", record.GetProperty("field7BRawHex").GetString());
        Assert.Equal([2, 0, 0, 0], record.GetProperty("field7BPackedValues").EnumerateArray().Select(static item => item.GetInt32()));
    }

    private static byte[] Int32Test(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt16Test(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt32Test(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LinearCompactBlock(string tag, ushort low, ushort high, byte[] payload)
    {
        return CombineTestBytes(
            Encoding.ASCII.GetBytes("vtc0"),
            Int32Test(8 + payload.Length),
            Encoding.ASCII.GetBytes(tag),
            UInt16Test(low),
            UInt16Test(high),
            payload);
    }

    private static byte[] CompactRecords(params byte[][] records)
    {
        var parts = new List<byte[]> { new byte[] { 0xFC, 0x00 } };
        for (var i = 0; i < records.Length; i++)
        {
            if (i > 0)
            {
                parts.Add(new byte[] { 0xFE, 0x00 });
            }

            parts.Add(records[i]);
        }

        parts.Add(new byte[] { 0xFD, 0x00 });
        return CombineTestBytes(parts.ToArray());
    }

    private static byte[] CompactStringField(byte id, string value)
    {
        var raw = Encoding.ASCII.GetBytes(value);
        return CombineTestBytes([id, 0x02, (byte)raw.Length], raw);
    }

    private static byte[] CompactRawStringField(byte id, params byte[] raw)
    {
        return CombineTestBytes([id, 0x02, (byte)raw.Length], raw);
    }

    private static byte[] CompactByteField(byte id, byte value)
    {
        return [id, 0x01, value];
    }

    private static byte[] CompactUInt16Field(byte id, ushort value)
    {
        return CombineTestBytes([id, 0x06], UInt16Test(value));
    }

    private static byte[] CompactInt16Field(byte id, short value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return CombineTestBytes([id, 0x05], bytes);
    }

    private static byte[] CompactInt32Field(byte id, int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return CombineTestBytes([id, 0x08], bytes);
    }

    private static byte[] CompactUInt32Field(byte id, uint value)
    {
        return CombineTestBytes([id, 0x09], UInt32Test(value));
    }

    private static byte[] CompactFloatField(byte id, float value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        return CombineTestBytes([id, 0x0A], bytes);
    }

    private static byte[] CompactRawVector2Field(byte id, float x, float y)
    {
        var bytes = new byte[9];
        bytes[0] = 2;
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(1, 4), x);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(5, 4), y);
        return CombineTestBytes([id, 0x4A], bytes);
    }

    private static byte[] CompactRawByteField(byte id, byte value)
    {
        return [id, 0x04, value];
    }

    private static byte[] CompactRawByte03Field(byte id, byte value)
    {
        return [id, 0x03, value];
    }

    private static byte[] CompactColorField(byte id, byte a, byte r, byte g, byte b)
    {
        return [id, 0x0C, a, r, g, b];
    }

    private static byte[] CompactCrefField(ushort textureListIndex, ushort textureIndex, ushort cropIndex)
    {
        return CombineTestBytes(
            [0x49, 0x45, 0x01],
            UInt16Test(textureListIndex),
            UInt16Test(textureIndex),
            UInt16Test(cropIndex));
    }

    private static byte[] CompactPackedRecordField(byte id, byte prefix, params ushort[] values)
    {
        var payload = new byte[1 + values.Length * 2];
        payload[0] = prefix;
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1 + i * 2, 2), values[i]);
        }

        return CombineTestBytes([id, 0x45], payload);
    }

    private static byte[] CombineTestBytes(params byte[][] parts)
    {
        var total = parts.Sum(static part => part.Length);
        var output = new byte[total];
        var cursor = 0;
        foreach (var part in parts)
        {
            part.CopyTo(output.AsSpan(cursor));
            cursor += part.Length;
        }

        return output;
    }
}

internal static class SyntheticSceneBuilder
{
    public static SyntheticSceneData BuildMinimalScene()
    {
        var nodePlain = Block(
            "NODE",
            [StringField(0x0100, "plain_body"), IntField(0x0030, 0xF01)],
            [Block("TRS2", [], []), Block("DATA", [], [])]);

        var nodeUniform = Block(
            "NODE",
            [StringField(0x0100, "uniform_body"), IntField(0x0030, 0xE01)],
            [Block("TRS2", [], []), Block("DATA", [], [])]);

        var cast = Block("CAST", [], [nodePlain, nodeUniform]);
        var layer = Block("LAYR", [StringField(0x0100, "main")], [cast]);
        var scene = Block("SCN ", [StringField(0x0100, "scene")], [layer]);
        var project = Block("PROJ", [StringField(0x0100, "project")], [scene]);
        var source = Block("SRCK", [], [project]);

        var keyOff = Block("KEY ", [FloatField(0x0300, 0f, 0f)], []);
        var keyOn = Block("KEY ", [FloatField(0x0300, 1f, 1f)], []);
        var track = Block(
            "TRK ",
            [StringField(0x0100, "visibility"), IntField(0x0200, 7, 1, 0, 2)],
            [keyOff, keyOn]);
        var motion = Block("MOT ", [StringField(0x0100, "plain_body"), IntField(0x0200, 0)], [track]);
        var animation = Block("ANIM", [StringField(0x0100, "Change_FashionV01")], [motion]);

        var root = Block("SRFF", [], [source, animation]);
        var bytes = Combine(Encoding.ASCII.GetBytes("VTBF"), root);
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-{Guid.NewGuid():N}.sbscene");
        File.WriteAllBytes(path, bytes);
        return new SyntheticSceneData(path, bytes);
    }

    private static byte[] Block(string tag, byte[][] fields, byte[][] children)
    {
        if (tag.Length != 4)
        {
            throw new ArgumentException("Tags must be exactly four ASCII characters.", nameof(tag));
        }

        var payload = Combine(
            Encoding.ASCII.GetBytes(tag),
            Int32(fields.Length),
            Int32(children.Length),
            Combine(fields),
            Combine(children));

        return Combine(Encoding.ASCII.GetBytes("vtc0"), Int32(payload.Length), payload);
    }

    private static byte[] StringField(ushort id, string value)
    {
        var raw = Encoding.UTF8.GetBytes(value + "\0");
        return Field(id, VtbfFieldTypes.String, raw.Length, 1, raw);
    }

    private static byte[] IntField(ushort id, params int[] values)
    {
        var raw = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(i * 4, 4), values[i]);
        }

        return Field(id, VtbfFieldTypes.Int32, values.Length, 4, raw);
    }

    private static byte[] FloatField(ushort id, params float[] values)
    {
        var raw = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i * 4, 4), values[i]);
        }

        return Field(id, VtbfFieldTypes.Float32, values.Length, 4, raw);
    }

    private static byte[] Field(ushort id, int typeCode, int count, int stride, byte[] raw)
    {
        var bytes = new byte[12 + raw.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), (ushort)typeCode);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), stride);
        raw.CopyTo(bytes.AsSpan(12));
        return bytes;
    }

    private static byte[] Int32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var total = parts.Sum(static part => part.Length);
        var output = new byte[total];
        var cursor = 0;
        foreach (var part in parts)
        {
            part.CopyTo(output.AsSpan(cursor));
            cursor += part.Length;
        }

        return output;
    }
}

internal sealed record SyntheticSceneData(string Path, byte[] Bytes);
