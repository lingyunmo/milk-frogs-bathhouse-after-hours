using System.IO;
using System.IO.Compression;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace AfterHours.Editor
{
    [ScriptedImporter(1, "brinevolume")]
    public sealed class BrineVolumeImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            using var file = File.OpenRead(context.assetPath);
            using var header = new BinaryReader(file);
            int size = header.ReadInt32();
            int channels = header.ReadByte();
            if (size < 4 || size > 256 || (channels != 2 && channels != 4))
                throw new InvalidDataException("Invalid Brine volume header.");
            var bytes = new byte[size * size * size * channels];
            using var compressed = new GZipStream(file, CompressionMode.Decompress);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = compressed.Read(bytes, offset, bytes.Length - offset);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
            var format = channels == 4 ? GraphicsFormat.R8G8B8A8_UNorm : GraphicsFormat.R8G8_UNorm;
            var texture = new Texture3D(size, size, size, format, TextureCreationFlags.None)
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath),
                wrapMode = channels == 4 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixelData(bytes, 0);
            texture.Apply(false, false);
            context.AddObjectToAsset("volume", texture);
            context.SetMainObject(texture);
        }
    }
}
