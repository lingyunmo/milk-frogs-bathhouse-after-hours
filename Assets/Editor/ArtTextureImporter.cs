using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class ArtTextureImporter : AssetPostprocessor
{
    // Bump when the rules below change so existing imports are rebuilt.
    public override uint GetVersion() => 3;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Props/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Resources/Finishes/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Resources/Creature/", StringComparison.Ordinal)) return;

        string name = Path.GetFileNameWithoutExtension(assetPath);
        bool normal = name.EndsWith("Normal", StringComparison.OrdinalIgnoreCase) || name.Contains("_nor_gl_");
        bool packed = name.EndsWith("Finish", StringComparison.Ordinal) || name.EndsWith("_Unity", StringComparison.Ordinal);
        bool height = name.EndsWith("Relief", StringComparison.Ordinal);
        var importer = (TextureImporter)assetImporter;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal && !packed && !height;
        importer.alphaSource = packed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 4096;
        // BC7 colour and packed maps, BC5 normals: visually lossless, and about a quarter of the video memory,
        // which keeps the whole set resident on 4 GB laptop GPUs.
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.crunchedCompression = false;
    }
}
