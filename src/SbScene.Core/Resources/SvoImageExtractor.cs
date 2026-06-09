using System.Text.Json;
using SbScene.Core.Images;
using SbScene.Core.Output;

namespace SbScene.Core.Resources;

/// <summary>
/// 从 SVO 资源包提取图像、裁剪图和清单，供命令行导出使用。
/// </summary>
public static class SvoImageExtractor
{
    /// <summary>
    /// 从 SVO 中提取可渲染图像资源，并返回解码后的纹理集合。
    /// </summary>
    /// <param name="sbscenePath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="svoPath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="outputDirectory">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="writeAtlases">指示是否同时写出完整 atlas 图像；为 false 时仅写出裁剪图和清单。</param>
    /// <returns>包含写出数量、清单路径和非致命警告的图像提取结果。</returns>
    public static ImageExtractionResult Extract(string sbscenePath, string svoPath, string outputDirectory, bool writeAtlases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var warnings = new List<string>();
        var header = SvoResourceParser.ParseHeaderFile(svoPath);
        var directory = SvoResourceParser.ParseDirectoryFile(svoPath);
        var metadata = SvoResourceParser.ParseMetadataFile(svoPath);
        var textures = SvoResourceParser.ParseFile(svoPath);
        var resourceMap = SbSceneTextureParser.ParseResourceMap(sbscenePath);
        var atlases = resourceMap.Atlases;

        var textureByName = textures
            .Where(static texture => !string.IsNullOrWhiteSpace(texture.AtlasName))
            .ToDictionary(static texture => texture.AtlasName!, static texture => texture, StringComparer.OrdinalIgnoreCase);

        if (textures.Count != atlases.Count)
        {
            warnings.Add($"SVO DDS count ({textures.Count}) does not match sbscene TEX count ({atlases.Count}); matching by index.");
        }

        Directory.CreateDirectory(outputDirectory);
        var atlasOut = Path.Combine(outputDirectory, "atlases");
        var cropOut = Path.Combine(outputDirectory, "crops");
        var cropCount = 0;
        var manifestAtlases = new List<object>();

        for (var i = 0; i < atlases.Count; i++)
        {
            var atlas = atlases[i];
            var texture = textureByName.TryGetValue(atlas.Name, out var namedTexture)
                ? namedTexture
                : i < textures.Count
                    ? textures[i]
                    : null;
            if (texture is null)
            {
                warnings.Add($"No SVO DDS texture found for atlas {atlas.Name}.");
                continue;
            }

            if (texture.Width != atlas.Width || texture.Height != atlas.Height)
            {
                warnings.Add($"Atlas {i} dimension mismatch: SVO {texture.Width}x{texture.Height}, sbscene {atlas.Width}x{atlas.Height}.");
            }

            var image = DdsDecoder.Decode(texture.DdsBytes);
            var safeName = MakeSafeName(atlas.Name);
            if (writeAtlases)
            {
                PngWriter.Write(Path.Combine(atlasOut, $"{i:D3}_{safeName}.png"), image);
            }

            var atlasCropOut = Path.Combine(cropOut, $"{i:D3}_{safeName}");
            Directory.CreateDirectory(atlasCropOut);
            foreach (var crop in atlas.Crops)
            {
                if (crop.Width <= 0 || crop.Height <= 0)
                {
                    warnings.Add($"Skipped invalid crop {atlas.Name}[{crop.Index}] = {crop.Left},{crop.Top},{crop.Right},{crop.Bottom}.");
                    continue;
                }

                if (crop.Left < 0 || crop.Top < 0 || crop.Right > image.Width || crop.Bottom > image.Height)
                {
                    warnings.Add($"Crop {atlas.Name}[{crop.Index}] extends outside atlas bounds and was padded with transparency.");
                }

                var cropped = image.CropWithTransparentPadding(crop.Left, crop.Top, crop.Width, crop.Height);
                PngWriter.Write(Path.Combine(atlasCropOut, $"{crop.Index:D3}.png"), cropped);
                cropCount++;
            }

            manifestAtlases.Add(new
            {
                atlas.Index,
                atlas.Name,
                atlas.Width,
                atlas.Height,
                atlas.DeclaredCropCount,
                ActualCropCount = atlas.Crops.Count,
                SvoDirectoryIndex = texture.DirectoryIndex,
                SvoOffset = texture.Offset,
                SvoLength = texture.Length,
                SvoFileName = texture.FileName,
                SvoAtlasName = texture.AtlasName,
                texture.Format,
                Crops = atlas.Crops,
            });
        }

        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        var manifest = new
        {
            SvoHeader = header,
            SvoDirectory = directory,
            SvoMetadata = metadata,
            Atlases = manifestAtlases,
            ImageCasts = resourceMap.ImageCasts,
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SbSceneJson.CreateOptions(indented: true)), new System.Text.UTF8Encoding(false));

        return new ImageExtractionResult
        {
            OutputDirectory = outputDirectory,
            AtlasCount = manifestAtlases.Count,
            CropCount = cropCount,
            ImageCastCount = resourceMap.ImageCasts.Count,
            Warnings = warnings,
        };
    }

    private static string MakeSafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
