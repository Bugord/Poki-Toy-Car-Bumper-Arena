using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// Names shared by the build, which moves heavy leaf assets out of the player into bundles in StreamingAssets, and
    /// the player, which downloads them after it starts (<see cref="ContentBundles"/>). Only textures and the music
    /// move: they depend on nothing, so a bundle holds no copy of a shader or material from the player. The first
    /// round's floor stays in the player, so the first round never waits for a download.
    /// </summary>
    public static class ContentNames
    {
        /// <summary>Folder under StreamingAssets the bundles are copied to.</summary>
        public const string Folder = "Bundles";

        /// <summary>The file a bundle is copied to, which the player downloads.</summary>
        public static string FileOf(string bundle) => bundle + ".bundle";

        public const string MusicBundle = "music";
        public const string CarsBundle = "cars";

        private const string FloorMaterialPrefix = "mat_floor_";
        private const string FloorTexturePrefix = "tex_floor_";

        /// <summary>The bundle with an arena's floor textures, but the first round's.</summary>
        public static string FloorBundleOf(string arenaId) => "floors-" + arenaId;

        /// <summary>
        /// The texture a floor material draws (<c>mat_floor_tabletop_cloth</c> draws <c>tex_floor_tabletop_cloth</c>),
        /// or null for a material outside that rule.
        /// </summary>
        public static string FloorTextureOf(Material material) =>
            material != null && material.name.StartsWith(FloorMaterialPrefix)
                ? FloorTexturePrefix + material.name.Substring(FloorMaterialPrefix.Length)
                : null;

        public static string PortraitOf(CarCatalog.Entry car) => "tex_car_" + car.Id.Replace('-', '_');

        public static string BodyPortraitOf(CarCatalog.Entry car) => PortraitOf(car) + "_body";

        /// <summary>The floor the visit's first round is played on, kept in the player.</summary>
        public static Material FirstFloor(GameTuning tuning, ArenaCatalog arenas)
        {
            var map = tuning.Maps[0];
            var variant = map.Variants.Count > 0 ? map.Variants[0].Id : null;
            return arenas.Find(map.Id)?.FloorMaterialOf(variant);
        }
    }
}
