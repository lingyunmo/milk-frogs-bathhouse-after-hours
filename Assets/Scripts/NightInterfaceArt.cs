using UnityEngine;

namespace AfterHours
{
    // Soft gradients and film textures for the interface. UI Toolkit has no gradient fill, so these are
    // generated once at startup instead of being shipped as flat images.
    internal static class NightInterfaceArt
    {
        static Texture2D shade, left, bottom, vignette, grain, haze, glow;

        // Black that fades out radially. Sits behind text that floats directly over the world.
        internal static Texture2D Shade => shade ? shade : shade = Radial("Text shade", 128, (r, _) =>
        {
            float a = 1 - Mathf.SmoothStep(0, 1, r);
            return a * a * .92f;
        });

        // Black on the left edge for menu columns, gone by the middle of the screen.
        internal static Texture2D LeftScrim => left ? left : left = Ramp("Left scrim", 256, 4, (x, _) =>
            Mathf.Pow(1 - Mathf.SmoothStep(0, 1, x), 1.5f) * .9f);

        // Black rising from the bottom edge.
        internal static Texture2D BottomScrim => bottom ? bottom : bottom = Ramp("Bottom scrim", 4, 256, (_, y) =>
            Mathf.Pow(1 - Mathf.SmoothStep(0, 1, y), 1.7f) * .78f);

        internal static Texture2D Vignette => vignette ? vignette : vignette = Radial("Vignette", 256, (r, _) =>
            Mathf.SmoothStep(.55f, 1.32f, r) * .72f);

        // White glow for practical lamps on the title art. Tinted in the style sheet.
        internal static Texture2D Glow => glow ? glow : glow = Radial("Lamp glow", 128, (r, _) =>
        {
            float core = Mathf.Exp(-r * r * 9);
            float halo = Mathf.Exp(-r * r * 2.2f) * .45f;
            return Mathf.Clamp01((core + halo) * (1 - Mathf.SmoothStep(.75f, 1, r)));
        });

        // Monochrome film grain, tiled and shifted every frame on screens without the 3D camera.
        internal static Texture2D Grain
        {
            get
            {
                if (grain) return grain;
                const int size = 192;
                var random = new System.Random(911);
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; ++i)
                {
                    float n = (float)(random.NextDouble() + random.NextDouble()) - 1;
                    byte value = (byte)(n > 0 ? 255 : 0);
                    pixels[i] = new Color32(value, value, value, (byte)(Mathf.Abs(n) * 30));
                }
                grain = Make("Film grain", size, size, pixels, TextureWrapMode.Repeat, FilterMode.Point);
                return grain;
            }
        }

        // Slow drifting steam for the title corridor: tileable value noise with a few octaves.
        internal static Texture2D Haze
        {
            get
            {
                if (haze) return haze;
                const int width = 256, height = 128;
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    float u = (float)x / width, v = (float)y / height;
                    float n = 0, amplitude = .55f, total = 0;
                    for (int octave = 0; octave < 4; ++octave)
                    {
                        int period = 3 << octave;
                        n += Tileable(u * period, v * period * .5f, period, Mathf.Max(1, period / 2), octave) * amplitude;
                        total += amplitude;
                        amplitude *= .5f;
                    }
                    n /= total;
                    float wisps = Mathf.SmoothStep(.38f, .82f, n);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(wisps * 255));
                }
                haze = Make("Steam haze", width, height, pixels, TextureWrapMode.Repeat, FilterMode.Bilinear);
                return haze;
            }
        }

        static float Tileable(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float Corner(int cx, int cy)
            {
                cx = ((cx % periodX) + periodX) % periodX;
                cy = ((cy % periodY) + periodY) % periodY;
                uint h = (uint)(cx * 374761393 + cy * 668265263 + seed * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
            float a = Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx);
            float b = Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        delegate float Shape(float a, float b);

        static float Dither(int x, int y)
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h & 1023) / 1023f - .5f) * 1.6f;
        }

        static Texture2D Radial(string name, int size, Shape alpha)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
            {
                float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)Mathf.Clamp(alpha(r, 0) * 255 + Dither(x, y), 0, 255);
                pixels[y * size + x] = name == "Lamp glow" ? new Color32(255, 255, 255, a) : new Color32(0, 0, 0, a);
            }
            return Make(name, size, size, pixels, TextureWrapMode.Clamp, FilterMode.Bilinear);
        }

        static Texture2D Ramp(string name, int width, int height, Shape alpha)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                // Texture rows start at the bottom. The bottom scrim measures y from the bottom edge.
                float u = (x + .5f) / width, v = (y + .5f) / height;
                pixels[y * width + x] = new Color32(0, 0, 0, (byte)Mathf.Clamp(alpha(u, v) * 255 + Dither(x, y), 0, 255));
            }
            return Make(name, width, height, pixels, TextureWrapMode.Clamp, FilterMode.Bilinear);
        }

        static Texture2D Make(string name, int width, int height, Color32[] pixels, TextureWrapMode wrap, FilterMode filter)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = filter,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
