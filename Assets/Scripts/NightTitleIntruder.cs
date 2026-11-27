using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class NightTitleIntruder : VisualElement
    {
        // The source is cropped on its right and bottom edges. Feather those cuts before rotation.
        static readonly float[] Stops = { 0, .77f, .90f, 1 };
        static readonly byte[] Alpha = { 255, 255, 155, 0 };

        internal Texture2D Artwork { get; }

        internal NightTitleIntruder(Texture2D artwork)
        {
            Artwork = artwork;
            pickingMode = PickingMode.Ignore;
            AddToClassList("title-intruder");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            if (!Artwork || contentRect.width <= 0 || contentRect.height <= 0) return;

            float scale = Mathf.Min(contentRect.width / Artwork.width, contentRect.height / Artwork.height);
            var size = new Vector2(Artwork.width, Artwork.height) * scale;
            Vector2 origin = contentRect.center - size * .5f;
            int grid = Stops.Length;
            int cells = grid - 1;
            var mesh = context.Allocate(grid * grid, cells * cells * 6, Artwork);
            if (mesh.vertexCount == 0) return;

            for (int row = 0; row < grid; row++)
            for (int column = 0; column < grid; column++)
            {
                float x = Stops[column];
                float y = Stops[row];
                mesh.SetNextVertex(new Vertex
                {
                    position = new Vector3(origin.x + size.x * x, origin.y + size.y * y, Vertex.nearZ),
                    uv = new Vector2(x, 1 - y),
                    tint = new Color32(187, 198, 194, (byte)(Alpha[row] * Alpha[column] / 255))
                });
            }
            for (int row = 0; row < cells; row++)
            for (int column = 0; column < cells; column++)
            {
                ushort topLeft = (ushort)(row * grid + column);
                ushort topRight = (ushort)(topLeft + 1);
                ushort bottomLeft = (ushort)(topLeft + grid);
                ushort bottomRight = (ushort)(bottomLeft + 1);
                mesh.SetNextIndex(topLeft);
                mesh.SetNextIndex(topRight);
                mesh.SetNextIndex(bottomRight);
                mesh.SetNextIndex(topLeft);
                mesh.SetNextIndex(bottomRight);
                mesh.SetNextIndex(bottomLeft);
            }
        }
    }
}
