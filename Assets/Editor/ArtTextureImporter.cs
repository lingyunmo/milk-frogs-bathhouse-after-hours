using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class ArtTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Props/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Resources/Finishes/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Resources/Creature/", StringComparison.Ordinal)) return;

        string name = Path.GetFileNameWithoutExtension(assetPath);
        bool normal = name.EndsWith("Normal", StringComparison.OrdinalIgnoreCase) || name.Contains("_nor_gl_");
        bool packed = name.EndsWith("Finish", StringComparison.Ordinal) || name.EndsWith("_Unity", StringComparison.Ordinal);
        var importer = (TextureImporter)assetImporter;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal && !packed;
        importer.alphaSource = packed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
