using System.Buffers.Binary;
using System.Text;

namespace SbScene.Core.Vtbf;

public sealed class VtbfParser
{
    private const int RootHeaderSize = 4;
    private const int BlockPrefixSize = 8;
    private const int BlockHeaderSize = 20;
    private const int FieldHeaderSize = 12;
    private static readonly Encoding TextEncoding = CreateTextEncoding();

    private readonly List<string> _warnings = [];
    private ReadOnlyMemory<byte> _buffer;

    public static VtbfDocument ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new VtbfParser().Parse(File.ReadAllBytes(path));
    }

    public VtbfDocument Parse(byte[] data) => Parse((ReadOnlyMemory<byte>)data);

    public VtbfDocument Parse(ReadOnlyMemory<byte> data)
    {
        _warnings.Clear();
        _buffer = data;

        if (data.Length < RootHeaderSize)
        {
            throw new VtbfParseException("File is too small to contain a VTBF header.");
        }

        var magic = ReadAscii(0, 4);
        if (magic != "VTBF")
        {
            throw new VtbfParseException($"Invalid VTBF magic '{magic}'.");
        }

        if (LooksLikeLinearRoot())
        {
            return ParseLinearDocument(magic);
        }

        var blocks = new List<VtbfBlock>();
        var cursor = RootHeaderSize;
        var rootIndex = 0;
        while (cursor < data.Length)
        {
            if (IsPadding(cursor, data.Length - cursor))
            {
                break;
            }

            var block = ParseBlock(ref cursor, data.Length, $"/{rootIndex}", 0);
            blocks.Add(block);
            rootIndex++;
        }

        var counts = blocks
            .SelectMany(Flatten)
            .GroupBy(static block => block.Tag)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return new VtbfDocument
        {
            Magic = magic,
            Length = data.Length,
            Blocks = blocks,
            BlockCounts = counts,
            Warnings = _warnings.ToArray(),
        };
    }

    private bool LooksLikeLinearRoot()
    {
        if (_buffer.Length < 16)
        {
            return false;
        }

        if (ReadAscii(4, 4) == "vtc0")
        {
            return false;
        }

        var rootLength = ReadInt32(4);
        if (rootLength < 16 || rootLength > _buffer.Length)
        {
            return false;
        }

        if (!LooksLikeTag(8))
        {
            return false;
        }

        return rootLength == _buffer.Length || ReadAscii(rootLength, 4) == "vtc0";
    }

    private VtbfDocument ParseLinearDocument(string magic)
    {
        var rootLength = ReadInt32(4);
        var rootTag = ReadAscii(8, 4);
        var rootParamLow = ReadUInt16(12);
        var rootParamHigh = ReadUInt16(14);
        var rootParamRawHex = Convert.ToHexString(_buffer.Span.Slice(12, 4));
        var cursor = rootLength;
        var blocks = new List<VtbfBlock>();
        var index = 0;

        while (cursor < _buffer.Length)
        {
            if (IsPadding(cursor, _buffer.Length - cursor))
            {
                break;
            }

            blocks.Add(ParseLinearBlock(ref cursor, $"/0/{index}"));
            index++;
        }

        var root = new VtbfBlock
        {
            Tag = rootTag,
            Offset = 0,
            ContentOffset = rootLength,
            EndOffset = cursor,
            Length = rootLength,
            PropertyCount = 0,
            ChildCount = blocks.Count,
            ParamLow = rootParamLow,
            ParamHigh = rootParamHigh,
            ParamRawHex = rootParamRawHex,
            Path = "/0",
            Fields = Array.Empty<VtbfField>(),
            Children = blocks,
        };

        var counts = Flatten(root)
            .GroupBy(static block => block.Tag)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return new VtbfDocument
        {
            Magic = magic,
            Length = _buffer.Length,
            Blocks = new[] { root },
            BlockCounts = counts,
            Warnings = _warnings.ToArray(),
        };
    }

    private VtbfBlock ParseLinearBlock(ref int cursor, string path)
    {
        var blockOffset = cursor;
        RequireAvailable(cursor, 16, _buffer.Length, "linear VTBF block header");
        var marker = ReadAscii(cursor, 4);
        if (marker != "vtc0")
        {
            throw new VtbfParseException($"Expected block marker 'vtc0' at 0x{cursor:X}, got '{marker}'.");
        }

        var declaredLength = ReadInt32(cursor + 4);
        if (declaredLength < 8)
        {
            throw new VtbfParseException($"Invalid linear vtc0 block length {declaredLength} at 0x{blockOffset:X}.");
        }

        var fieldEnd = blockOffset + BlockPrefixSize + declaredLength;
        if (fieldEnd > _buffer.Length)
        {
            throw new VtbfParseException(
                $"Block at 0x{blockOffset:X} declares length {declaredLength}, which exceeds file end 0x{_buffer.Length:X}.");
        }

        var tag = ReadAscii(cursor + 8, 4);
        var childCount = ReadUInt16(cursor + 12);
        var fieldCount = ReadUInt16(cursor + 14);
        var paramRawHex = Convert.ToHexString(_buffer.Span.Slice(cursor + 12, 4));
        var payloadStart = cursor + 16;
        var fields = ParseCompactFields(tag, payloadStart, fieldEnd, childCount, fieldCount, out var trailing);

        cursor = fieldEnd;
        var children = new List<VtbfBlock>(childCount);
        for (var i = 0; i < childCount; i++)
        {
            if (cursor >= _buffer.Length || ReadAscii(cursor, 4) != "vtc0")
            {
                _warnings.Add($"{tag} at 0x{blockOffset:X} declares {childCount} child block(s), but child {i} is missing.");
                break;
            }

            children.Add(ParseLinearBlock(ref cursor, $"{path}/{tag}[{i}]"));
        }

        return new VtbfBlock
        {
            Tag = tag,
            Offset = blockOffset,
            ContentOffset = payloadStart,
            EndOffset = cursor,
            Length = declaredLength,
            PropertyCount = fieldCount,
            ChildCount = childCount,
            ParamLow = childCount,
            ParamHigh = fieldCount,
            ParamRawHex = paramRawHex,
            Path = path,
            Fields = fields,
            Children = children,
            TrailingBytes = trailing,
        };
    }

    private IReadOnlyList<VtbfField> ParseCompactFields(string tag, int start, int end, int low, int high, out byte[]? trailing)
    {
        trailing = null;
        var fields = new List<VtbfField>();
        var cursor = start;

        if (cursor >= end)
        {
            return fields;
        }

        if (tag is "NODE" or "TRS2" or "NCAT" or "SLIC")
        {
            while (cursor < end)
            {
                if (cursor + 2 <= end && (_buffer.Span[cursor] is 0xFC or 0xFD or 0xFE) && _buffer.Span[cursor + 1] == 0)
                {
                    fields.Add(CreateCompactMarkerField(cursor, _buffer.Span[cursor]));
                    cursor += 2;
                    continue;
                }

                if (!TryParseCompactField(tag, ref cursor, end, fields, out var error))
                {
                    _warnings.Add($"{tag} at 0x{start - 16:X}: stopped compact field parse at 0x{cursor:X}: {error}");
                    trailing = _buffer.Slice(cursor, end - cursor).ToArray();
                    break;
                }
            }

            return fields;
        }

        var expectedFieldCount = high;
        for (var i = 0; i < expectedFieldCount && cursor < end; i++)
        {
            if (!TryParseCompactField(tag, ref cursor, end, fields, out var error))
            {
                _warnings.Add($"{tag} at 0x{start - 16:X}: stopped compact field parse at 0x{cursor:X}: {error}");
                break;
            }
        }

        if (ShouldParseExtraCompactFields(tag))
        {
            while (cursor < end)
            {
                if (!TryParseCompactField(tag, ref cursor, end, fields, out var error))
                {
                    _warnings.Add($"{tag} at 0x{start - 16:X}: stopped compact field parse at 0x{cursor:X}: {error}");
                    break;
                }
            }
        }

        if (cursor < end)
        {
            trailing = _buffer.Slice(cursor, end - cursor).ToArray();
            _warnings.Add($"{tag} at 0x{start - 16:X} has {end - cursor} compact trailing byte(s); low={low}, high={high}.");
        }

        return fields;
    }

    private bool TryParseCompactField(string ownerTag, ref int cursor, int end, List<VtbfField> fields, out string? error)
    {
        error = null;
        var fieldOffset = cursor;
        if (cursor + 2 > end)
        {
            error = "field header exceeds block payload";
            cursor = fieldOffset;
            return false;
        }

        var id = _buffer.Span[cursor];
        var typeCode = _buffer.Span[cursor + 1];
        cursor += 2;

        int rawLength;
        int payloadOffset;
        if (typeCode == 0x02)
        {
            if (cursor >= end)
            {
                error = "string length byte exceeds block payload";
                cursor = fieldOffset;
                return false;
            }

            rawLength = _buffer.Span[cursor];
            payloadOffset = cursor + 1;
            if (rawLength == 0x80 && cursor + 1 < end)
            {
                rawLength = _buffer.Span[cursor + 1];
                payloadOffset = cursor + 2;
            }

            cursor = payloadOffset + rawLength;
        }
        else
        {
            rawLength = GetCompactPayloadLength(ownerTag, id, typeCode);
            if (rawLength < 0)
            {
                error = $"unknown compact field id=0x{id:X2}, type=0x{typeCode:X2}";
                cursor = fieldOffset;
                return false;
            }

            payloadOffset = cursor;
            cursor += rawLength;
        }

        if (cursor > end)
        {
            error = $"field id=0x{id:X2}, type=0x{typeCode:X2} exceeds block payload";
            cursor = fieldOffset;
            return false;
        }

        var raw = _buffer.Slice(payloadOffset, rawLength).ToArray();
        fields.Add(CreateCompactField(fieldOffset, payloadOffset, id, typeCode, raw));
        return true;
    }

    private static int GetCompactPayloadLength(string ownerTag, int id, int typeCode)
    {
        return typeCode switch
        {
            0x00 => 0,
            0x01 => 1,
            0x03 => 1,
            0x04 => 1,
            0x05 => 2,
            0x06 => 2,
            0x08 => 4,
            0x09 => 4,
            0x0A => 4,
            0x0B => 4,
            0x0C => 4,
            0x45 when ownerTag == "CROP" => 9,
            0x45 => 7,
            0x4A when id is 0x12 or 0x13 => 13,
            0x4A => 9,
            _ => -1,
        };
    }

    private VtbfField CreateCompactMarkerField(int offset, int marker)
    {
        var name = marker switch
        {
            0xFC => "RecordStartFirst",
            0xFD => "RecordEnd",
            0xFE => "RecordStart",
            _ => "RecordMarker",
        };

        return new VtbfField
        {
            Offset = offset,
            PayloadOffset = offset,
            Id = 0xFF00 | marker,
            TypeCode = marker,
            TypeNameOverride = name,
            IsKnownTypeOverride = true,
            Count = 0,
            Stride = 0,
            Raw = Array.Empty<byte>(),
            DecodedKind = name,
            Preview = name,
        };
    }

    private static VtbfField CreateCompactField(int offset, int payloadOffset, int id, int typeCode, byte[] raw)
    {
        var decoded = DecodeCompactField(typeCode, raw);
        return new VtbfField
        {
            Offset = offset,
            PayloadOffset = payloadOffset,
            Id = id,
            TypeCode = typeCode,
            TypeNameOverride = GetCompactTypeName(typeCode),
            IsKnownTypeOverride = true,
            Count = GetCompactCount(typeCode, raw),
            Stride = GetCompactStride(typeCode, raw),
            Raw = raw,
            DecodedKind = decoded.Kind,
            StringValue = decoded.StringValue,
            Int64Values = decoded.Int64Values,
            Float64Values = decoded.Float64Values,
            Preview = decoded.Preview,
        };
    }

    private static string GetCompactTypeName(int typeCode)
    {
        return typeCode switch
        {
            0x00 => "ZeroLengthMarker",
            0x01 => "Byte/Bool",
            0x02 => "String",
            0x03 => "RawByte03",
            0x04 => "RawByte",
            0x05 => "Int16",
            0x06 => "UInt16",
            0x08 => "Int32",
            0x09 => "UInt32",
            0x0A => "Float32",
            0x0B => "Int32/PackedAngleCandidate",
            0x0C => "Color32",
            0x45 => "PackedRecord",
            0x4A => "VectorFloat32",
            _ => $"Compact0x{typeCode:X2}",
        };
    }

    private static int GetCompactCount(int typeCode, byte[] raw)
    {
        return typeCode switch
        {
            0x00 => 0,
            0x01 or 0x03 or 0x04 => raw.Length,
            0x02 => raw.Length,
            0x05 or 0x06 => raw.Length / 2,
            0x08 or 0x09 or 0x0A or 0x0B or 0x0C or 0x4A => raw.Length / 4,
            0x45 => raw.Length,
            _ => raw.Length,
        };
    }

    private static int GetCompactStride(int typeCode, byte[] raw)
    {
        return typeCode switch
        {
            0x00 => 0,
            0x01 or 0x02 or 0x03 or 0x04 or 0x45 => 1,
            0x05 or 0x06 => 2,
            0x08 or 0x09 or 0x0A or 0x0B or 0x0C or 0x4A => 4,
            _ => Math.Max(1, raw.Length),
        };
    }

    private static DecodedField DecodeCompactField(int typeCode, byte[] raw)
    {
        string? stringValue = null;
        long[]? intValues = null;
        double[]? floatValues = null;
        var kind = GetCompactTypeName(typeCode);

        switch (typeCode)
        {
            case 0x01:
                intValues = raw.Select(static value => (long)value).ToArray();
                break;
            case 0x03:
            case 0x04:
                intValues = raw.Select(static value => (long)value).ToArray();
                break;
            case 0x02:
                stringValue = DecodeString(raw);
                break;
            case 0x05:
                intValues = DecodeInt16Array(raw);
                break;
            case 0x06:
                intValues = DecodeUInt16Array(raw);
                break;
            case 0x08:
                intValues = DecodeInt32Array(raw);
                break;
            case 0x09:
                intValues = DecodeUInt32Array(raw);
                break;
            case 0x0A:
                floatValues = DecodeFloat32Array(raw);
                break;
            case 0x0B:
                intValues = DecodeInt32Array(raw);
                break;
            case 0x0C:
                intValues = raw.Select(static value => (long)value).ToArray();
                break;
            case 0x45:
                intValues = DecodeUInt16ArrayWithBytePrefix(raw);
                break;
            case 0x4A:
                var vectorRaw = raw.Length % 4 == 1 ? raw[1..] : raw;
                floatValues = DecodeFloat32Array(vectorRaw);
                if (raw.Length % 4 == 1)
                {
                    intValues = [raw[0]];
                }

                break;
        }

        return new DecodedField(kind, stringValue, intValues, floatValues, BuildPreview(stringValue, intValues, floatValues, raw));
    }

    private static bool ShouldParseExtraCompactFields(string tag) => tag is "CNUM" or "TEXT";

    private VtbfBlock ParseBlock(ref int cursor, int containerEnd, string path, int depth)
    {
        if (depth > 512)
        {
            throw new VtbfParseException($"Block nesting is deeper than 512 at 0x{cursor:X}.");
        }

        var blockOffset = cursor;
        RequireAvailable(cursor, BlockHeaderSize, containerEnd, "VTBF block header");

        var marker = ReadAscii(cursor, 4);
        if (marker != "vtc0")
        {
            throw new VtbfParseException($"Expected block marker 'vtc0' at 0x{cursor:X}, got '{marker}'.");
        }

        var declaredLength = ReadInt32(cursor + 4);
        if (declaredLength < 12)
        {
            throw new VtbfParseException($"Invalid vtc0 block length {declaredLength} at 0x{blockOffset:X}.");
        }

        var blockEnd = blockOffset + BlockPrefixSize + declaredLength;
        if (blockEnd > containerEnd)
        {
            var alternateEnd = blockOffset + declaredLength;
            if (alternateEnd <= containerEnd && alternateEnd >= blockOffset + BlockHeaderSize)
            {
                _warnings.Add($"Block at 0x{blockOffset:X} uses total-size length semantics; accepted as fallback.");
                blockEnd = alternateEnd;
            }
            else
            {
                throw new VtbfParseException(
                    $"Block at 0x{blockOffset:X} declares length {declaredLength}, which exceeds container end 0x{containerEnd:X}.");
            }
        }

        var tag = ReadAscii(cursor + 8, 4);
        var propertyCount = ReadInt32(cursor + 12);
        var childCount = ReadInt32(cursor + 16);
        if (propertyCount < 0 || childCount < 0)
        {
            throw new VtbfParseException($"Negative property or child count in {tag} at 0x{blockOffset:X}.");
        }

        cursor += BlockHeaderSize;
        var fields = new List<VtbfField>(propertyCount);
        for (var i = 0; i < propertyCount; i++)
        {
            fields.Add(ParseField(ref cursor, blockEnd));
        }

        var children = new List<VtbfBlock>(childCount);
        for (var i = 0; i < childCount; i++)
        {
            children.Add(ParseBlock(ref cursor, blockEnd, $"{path}/{tag}[{i}]", depth + 1));
        }

        byte[]? trailing = null;
        if (cursor < blockEnd)
        {
            var trailingLength = blockEnd - cursor;
            trailing = _buffer.Slice(cursor, trailingLength).ToArray();
            _warnings.Add($"{tag} at 0x{blockOffset:X} has {trailingLength} trailing byte(s).");
            cursor = blockEnd;
        }

        if (cursor != blockEnd)
        {
            throw new VtbfParseException($"Parser overran {tag} at 0x{blockOffset:X}; cursor=0x{cursor:X}, end=0x{blockEnd:X}.");
        }

        return new VtbfBlock
        {
            Tag = tag,
            Offset = blockOffset,
            ContentOffset = blockOffset + BlockHeaderSize,
            EndOffset = blockEnd,
            Length = declaredLength,
            PropertyCount = propertyCount,
            ChildCount = childCount,
            Path = path,
            Fields = fields,
            Children = children,
            TrailingBytes = trailing,
        };
    }

    private VtbfField ParseField(ref int cursor, int blockEnd)
    {
        var offset = cursor;
        RequireAvailable(cursor, FieldHeaderSize, blockEnd, "VTBF field header");

        var id = ReadUInt16(cursor);
        var typeCode = ReadUInt16(cursor + 2);
        var count = ReadInt32(cursor + 4);
        var stride = ReadInt32(cursor + 8);

        if (count < 0 || stride < 0)
        {
            throw new VtbfParseException($"Negative field count or stride at 0x{offset:X}.");
        }

        var payloadLength64 = (long)count * stride;
        if (payloadLength64 > int.MaxValue)
        {
            throw new VtbfParseException($"Field at 0x{offset:X} is too large: {payloadLength64} bytes.");
        }

        var payloadLength = (int)payloadLength64;
        var payloadOffset = offset + FieldHeaderSize;
        RequireAvailable(payloadOffset, payloadLength, blockEnd, "VTBF field payload");
        var raw = _buffer.Slice(payloadOffset, payloadLength).ToArray();
        cursor = payloadOffset + payloadLength;

        var decoded = DecodeField(typeCode, count, stride, raw);
        return new VtbfField
        {
            Offset = offset,
            PayloadOffset = payloadOffset,
            Id = id,
            TypeCode = typeCode,
            Count = count,
            Stride = stride,
            Raw = raw,
            DecodedKind = decoded.Kind,
            StringValue = decoded.StringValue,
            Int64Values = decoded.Int64Values,
            Float64Values = decoded.Float64Values,
            Preview = decoded.Preview,
        };
    }

    private static DecodedField DecodeField(int typeCode, int count, int stride, byte[] raw)
    {
        string? stringValue = null;
        long[]? intValues = null;
        double[]? floatValues = null;
        var kind = "Raw";

        switch (typeCode)
        {
            case VtbfFieldTypes.String:
                stringValue = DecodeString(raw);
                kind = "String";
                break;
            case VtbfFieldTypes.Int32 when stride == 4:
                intValues = DecodeInt32Array(raw);
                kind = "Int32";
                break;
            case VtbfFieldTypes.Float32 when stride == 4:
                floatValues = DecodeFloat32Array(raw);
                kind = "Float32";
                break;
            case VtbfFieldTypes.Bytes when stride == 1:
                intValues = raw.Select(static value => (long)value).ToArray();
                kind = "Bytes";
                break;
            case VtbfFieldTypes.Int16 when stride == 2:
                intValues = DecodeInt16Array(raw);
                kind = "Int16";
                break;
            case VtbfFieldTypes.Int8 when stride == 1:
                intValues = raw.Select(static value => unchecked((long)(sbyte)value)).ToArray();
                kind = "Int8";
                break;
            case VtbfFieldTypes.Float64 when stride == 8:
                floatValues = DecodeFloat64Array(raw);
                kind = "Float64";
                break;
            case VtbfFieldTypes.Int64 when stride == 8:
                intValues = DecodeInt64Array(raw);
                kind = "Int64";
                break;
        }

        if (stringValue is null && LooksLikeText(raw))
        {
            stringValue = DecodeString(raw);
            kind = VtbfFieldTypes.IsKnown(typeCode) ? $"{kind}+StringCandidate" : "StringCandidate";
        }

        if (intValues is null)
        {
            intValues = stride switch
            {
                1 when raw.Length == count => raw.Select(static value => (long)value).ToArray(),
                2 when raw.Length % 2 == 0 => DecodeInt16Array(raw),
                4 when raw.Length % 4 == 0 => DecodeInt32Array(raw),
                8 when raw.Length % 8 == 0 => DecodeInt64Array(raw),
                _ => null,
            };
        }

        if (floatValues is null)
        {
            floatValues = stride switch
            {
                4 when raw.Length % 4 == 0 => DecodeFloat32Array(raw),
                8 when raw.Length % 8 == 0 => DecodeFloat64Array(raw),
                _ => null,
            };
        }

        var preview = BuildPreview(stringValue, intValues, floatValues, raw);
        return new DecodedField(kind, stringValue, intValues, floatValues, preview);
    }

    private static string? BuildPreview(string? stringValue, long[]? intValues, double[]? floatValues, byte[] raw)
    {
        if (!string.IsNullOrWhiteSpace(stringValue))
        {
            return stringValue.Length <= 96 ? stringValue : stringValue[..96] + "...";
        }

        if (floatValues is { Length: > 0 })
        {
            return string.Join(", ", floatValues.Take(6).Select(static value => value.ToString("G6")));
        }

        if (intValues is { Length: > 0 })
        {
            return string.Join(", ", intValues.Take(8));
        }

        if (raw.Length == 0)
        {
            return string.Empty;
        }

        return Convert.ToHexString(raw.AsSpan(0, Math.Min(raw.Length, 24)));
    }

    private static bool LooksLikeText(byte[] raw)
    {
        if (raw.Length == 0)
        {
            return false;
        }

        var nonZero = raw.Where(static value => value != 0).ToArray();
        if (nonZero.Length == 0)
        {
            return false;
        }

        var printable = nonZero.Count(static value => value is >= 0x20 and <= 0x7E || value >= 0x80);
        return printable >= Math.Max(1, nonZero.Length * 9 / 10);
    }

    private static string DecodeString(byte[] raw)
    {
        var terminator = Array.IndexOf(raw, (byte)0);
        var length = terminator >= 0 ? terminator : raw.Length;
        return TextEncoding.GetString(raw, 0, length);
    }

    private static Encoding CreateTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }

    private static long[] DecodeInt16Array(byte[] raw)
    {
        var values = new long[raw.Length / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(i * 2, 2));
        }

        return values;
    }

    private static long[] DecodeUInt16Array(byte[] raw)
    {
        var values = new long[raw.Length / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(i * 2, 2));
        }

        return values;
    }

    private static long[] DecodeInt32Array(byte[] raw)
    {
        var values = new long[raw.Length / 4];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(i * 4, 4));
        }

        return values;
    }

    private static long[] DecodeUInt32Array(byte[] raw)
    {
        var values = new long[raw.Length / 4];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i * 4, 4));
        }

        return values;
    }

    private static long[] DecodeUInt16ArrayWithBytePrefix(byte[] raw)
    {
        if (raw.Length < 3)
        {
            return raw.Select(static value => (long)value).ToArray();
        }

        var values = new List<long> { raw[0] };
        var cursor = 1;
        while (cursor + 1 < raw.Length)
        {
            values.Add(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(cursor, 2)));
            cursor += 2;
        }

        if (cursor < raw.Length)
        {
            values.Add(raw[cursor]);
        }

        return values.ToArray();
    }

    private static long[] DecodeInt64Array(byte[] raw)
    {
        var values = new long[raw.Length / 8];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(i * 8, 8));
        }

        return values;
    }

    private static double[] DecodeFloat32Array(byte[] raw)
    {
        var values = new List<double>(raw.Length / 4);
        for (var i = 0; i < raw.Length / 4; i++)
        {
            var value = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(i * 4, 4));
            if (double.IsFinite(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static double[] DecodeFloat64Array(byte[] raw)
    {
        var values = new List<double>(raw.Length / 8);
        for (var i = 0; i < raw.Length / 8; i++)
        {
            var value = BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(i * 8, 8));
            if (double.IsFinite(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private bool IsPadding(int offset, int length)
    {
        var span = _buffer.Slice(offset, length).Span;
        foreach (var value in span)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void RequireAvailable(int offset, int length, int end, string context)
    {
        if (length < 0 || offset < 0 || offset + length > end || offset + length > _buffer.Length)
        {
            throw new VtbfParseException($"{context} at 0x{offset:X} exceeds boundary 0x{end:X}.");
        }
    }

    private string ReadAscii(int offset, int length)
    {
        RequireAvailable(offset, length, _buffer.Length, "ASCII read");
        return Encoding.ASCII.GetString(_buffer.Slice(offset, length).Span);
    }

    private int ReadInt32(int offset)
    {
        RequireAvailable(offset, 4, _buffer.Length, "Int32 read");
        return BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(offset, 4).Span);
    }

    private int ReadUInt16(int offset)
    {
        RequireAvailable(offset, 2, _buffer.Length, "UInt16 read");
        return BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(offset, 2).Span);
    }

    private bool LooksLikeTag(int offset)
    {
        RequireAvailable(offset, 4, _buffer.Length, "tag read");
        var span = _buffer.Slice(offset, 4).Span;
        foreach (var value in span)
        {
            if (value != 0x20 && (value < 0x41 || value > 0x5A) && (value < 0x30 || value > 0x39))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<VtbfBlock> Flatten(VtbfBlock block)
    {
        yield return block;
        foreach (var child in block.Children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private readonly record struct DecodedField(
        string Kind,
        string? StringValue,
        long[]? Int64Values,
        double[]? Float64Values,
        string? Preview);
}
