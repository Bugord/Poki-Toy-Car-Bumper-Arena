using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How each league (a <see cref="PlayerRank"/> tier) looks in the main menu: its sky behind the car, the top of the
    /// car's pedestal inside the gold rim, and the little plinth the leagues' cards draw. Only the look; the buttons, the
    /// stickers and PLAY keep their colours in every league, so they read the same wherever the player is. The colours
    /// are the MenuB league boards' in Claude Design.
    /// </summary>
    internal static class LeagueLook
    {
        private readonly struct Look
        {
            public readonly string Id;
            // The sky's stops: the middle, at 45% and at the rim.
            public readonly Color32 SkyMiddle;
            public readonly Color32 SkyHalf;
            public readonly Color32 SkyRim;
            // The plinth's top, light middle and edge, and its side, as the cards' little plinths draw them.
            public readonly Color32 TopLight;
            public readonly Color32 Top;
            public readonly Color32 Side;
            // The 3D pedestal's top: between the two above, as a lit material makes the light one of the two anyway.
            public readonly Color32 Pedestal;

            public Look(string id, uint middle, uint half, uint rim, uint topLight, uint top, uint side, uint pedestal)
            {
                Id = id;
                SkyMiddle = Rgb(middle);
                SkyHalf = Rgb(half);
                SkyRim = Rgb(rim);
                TopLight = Rgb(topLight);
                Top = Rgb(top);
                Side = Rgb(side);
                Pedestal = Rgb(pedestal);
            }
        }

        private static readonly Look[] Looks = {
            new("rookie", 0x7fd2ff, 0x3db5ff, 0x2aa6f6, 0xffffff, 0xeef3fb, 0xc7d3ea, 0xfafaff),
            new("bronze", 0xffd8bf, 0xe9875a, 0xa5452c, 0xf6c194, 0xe09056, 0xb9602a, 0xeba26f),
            new("silver", 0xf2f6ff, 0xb9cbec, 0x6f86bd, 0xffffff, 0xcdd8ea, 0x93a4c4, 0xe2e9f4),
            new("gold", 0xfff8d2, 0xffd54f, 0xf39a1c, 0xfff0a8, 0xffc93c, 0xe08a12, 0xffe07a),
            new("legend", 0xffd6f1, 0xc97cff, 0x5a3cc9, 0xffb3cf, 0xff7aa8, 0xd94a86, 0xff94bb),
        };

        // The sky's texture: square, scaled to cover the menu. Its middle is at 50% across and 60% down, and its rim
        // stop about the farthest visible corner of an upright or a wide screen cropped from it (CSS farthest-corner).
        private const int SkySize = 128;
        private static readonly Vector2 MenuMiddle = new(0.5f, 0.6f);
        private const float MenuRadius = 0.66f;
        // The leagues' cards and the full-screen list: the light high up, the rim stop past the corners.
        private static readonly Vector2 CardMiddle = new(0.5f, 0.3f);
        private const float CardRadius = 0.85f;

        private static readonly Dictionary<int, Texture2D> Textures = new();

        public static int Count => Looks.Length;

        /// <summary>The league's id in analytics and storage: rookie, bronze, silver, gold, legend.</summary>
        public static string IdOf(int tier) => Looks[Clamp(tier)].Id;

        public static Color PedestalOf(int tier) => Looks[Clamp(tier)].Pedestal;
        public static Color TopOf(int tier) => Looks[Clamp(tier)].Top;
        public static Color TopLightOf(int tier) => Looks[Clamp(tier)].TopLight;
        public static Color SideOf(int tier) => Looks[Clamp(tier)].Side;

        /// <summary>The menu's sky in this league, to cover the screen with.</summary>
        public static Texture2D SkyOf(int tier) => Radial(tier, 0, MenuMiddle, MenuRadius);

        /// <summary>The league's sky lit from high up, for its card in the leagues list and the swatch of the popup.</summary>
        public static Texture2D CardSkyOf(int tier) => Radial(tier, 1, CardMiddle, CardRadius);

        /// <summary>The league's sky running left to right, for its row in the upright leagues list.</summary>
        public static Texture2D RowSkyOf(int tier)
        {
            var key = Clamp(tier) * 4 + 2;
            if (Textures.TryGetValue(key, out var texture) && texture != null) {
                return texture;
            }

            var look = Looks[Clamp(tier)];
            texture = NewTexture(SkySize, 1, $"league_row_{look.Id}");
            var pixels = new Color32[SkySize];
            for (var x = 0; x < SkySize; x++) {
                pixels[x] = Stops(look, x / (SkySize - 1f), 0.55f);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Textures[key] = texture;
            return texture;
        }

        private static Texture2D Radial(int tier, int kind, Vector2 middle, float radius)
        {
            var key = Clamp(tier) * 4 + kind;
            if (Textures.TryGetValue(key, out var texture) && texture != null) {
                return texture;
            }

            var look = Looks[Clamp(tier)];
            texture = NewTexture(SkySize, SkySize, $"league_sky_{look.Id}_{kind}");
            var pixels = new Color32[SkySize * SkySize];
            for (var y = 0; y < SkySize; y++) {
                for (var x = 0; x < SkySize; x++) {
                    // Textures run bottom up, the CSS middle top down.
                    var point = new Vector2((x + 0.5f) / SkySize, 1f - (y + 0.5f) / SkySize);
                    pixels[y * SkySize + x] = Stops(look, Vector2.Distance(point, middle) / radius, 0.45f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Textures[key] = texture;
            return texture;
        }

        private static Color32 Stops(Look look, float t, float half) =>
            t < half
                ? Color32.Lerp(look.SkyMiddle, look.SkyHalf, t / half)
                : Color32.Lerp(look.SkyHalf, look.SkyRim, Mathf.Clamp01((t - half) / (1f - half)));

        private static Texture2D NewTexture(int width, int height, string name) =>
            new(width, height, TextureFormat.RGBA32, false) {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

        private static int Clamp(int tier) => Mathf.Clamp(tier, 0, Looks.Length - 1);

        private static Color32 Rgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xff);
    }
}
