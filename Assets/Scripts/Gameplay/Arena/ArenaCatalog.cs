using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// How each arena looks and what it is called. The numbers of a round on it, and the level that opens it, are in
    /// <see cref="GameTuning.Maps"/> under the same id.
    /// </summary>
    [CreateAssetMenu(fileName = "data_arena_catalog", menuName = "Bonk Arena/Arena Catalog")]
    public sealed class ArenaCatalog : ScriptableObject
    {
        [Tooltip("Arenas in rotation order; each id matches an entry of GameTuning.Maps")]
        [SerializeField]
        private Entry[] arenas = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Arenas => arenas;

        public Entry Find(string id)
        {
            foreach (var arena in arenas) {
                if (arena.Id == id) {
                    return arena;
                }
            }

            return null;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Permanent identity used by storage and analytics; changing it would lose that identity")]
            [SerializeField]
            private string id;

            [Tooltip("Uppercase name shown in the lobby")]
            [SerializeField]
            private string displayName;

            [Tooltip("Material of the floor; empty keeps the one the scene has")]
            [SerializeField]
            private Material floorMaterial;

            [Tooltip("Colour behind the arena; applied together with the floor material, so an entry without a material keeps the scene's sky")]
            [SerializeField]
            private Color background = new(0.53f, 0.81f, 0.92f, 1f);

            [Header("World below")]
            [Tooltip("Metres from the floor's top down to the ground far below; 0 builds no world, only the props")]
            [Min(0f)]
            [SerializeField]
            private float groundDepth = 40f;

            [Tooltip("Material of the ground that fills the view far below the arena")]
            [SerializeField]
            private Material groundMaterial;

            [Tooltip("Metres of view depth past the arena's middle where the fog starts; the fog colour is the background")]
            [Min(0f)]
            [SerializeField]
            private float fogStart = 8f;

            [Tooltip("Metres of view depth past the arena's middle where the fog is thick and hides everything")]
            [Min(0f)]
            [SerializeField]
            private float fogEnd = 90f;

            [Tooltip("What the arena stands on, under its floor: always narrower than the floor, so the rim and the drop show")]
            [SerializeField]
            private WorldPiece[] pedestal = Array.Empty<WorldPiece>();

            [Tooltip("Large flat or solid pieces of the world below, such as a table top or a rug; their base height is " +
                "measured from the floor's top like the pedestal's")]
            [SerializeField]
            private WorldPiece[] groundPieces = Array.Empty<WorldPiece>();

            [Header("Props")]
            [Tooltip("Props standing on the ground and round the rim; empty leaves the arena bare")]
            [SerializeField]
            private DecorItem[] decor = Array.Empty<DecorItem>();

            public string Id => id;
            public string DisplayName => displayName;
            public Material FloorMaterial => floorMaterial;
            public Color Background => background;
            public IReadOnlyList<DecorItem> Decor => decor;
            public float GroundDepth => groundDepth;
            public Material GroundMaterial => groundMaterial;
            public float FogStart => fogStart;
            public float FogEnd => fogEnd;
            public IReadOnlyList<WorldPiece> Pedestal => pedestal;
            public IReadOnlyList<WorldPiece> GroundPieces => groundPieces;
        }

        /// <summary>The shape of a <see cref="WorldPiece"/>.</summary>
        public enum PieceShape
        {
            Cylinder,
            Box,
            Model,
        }

        /// <summary>
        /// One solid of the world below the arena: a tier of the pedestal, a table top, a rug. Built from a plain
        /// cylinder or box, or from a model, and placed by <see cref="ArenaWorld"/>.
        /// </summary>
        [Serializable]
        public sealed class WorldPiece
        {
            [Tooltip("Cylinder or Box for a plain solid in the material below, Model for a prop with its own colours")]
            [SerializeField]
            private PieceShape shape;

            [Tooltip("The model of a Model piece")]
            [SerializeField]
            private GameObject model;

            [Tooltip("Colour of a cylinder or box; a model keeps its own")]
            [SerializeField]
            private Material material;

            [Tooltip("Metres: width, height and depth of a box; across, height and across of a cylinder; for a model, X is " +
                "its size multiplier")]
            [SerializeField]
            private Vector3 size = Vector3.one;

            [Tooltip("Metres from the arena's middle, as the camera sees it: X to the right of the screen, Y away from the camera")]
            [SerializeField]
            private Vector2 offset;

            [Tooltip("Metres of the piece's base above the floor's top; negative is below it")]
            [SerializeField]
            private float baseHeight;

            [Tooltip("Degrees of turn about the vertical")]
            [SerializeField]
            private float yaw;

            public PieceShape Shape => shape;
            public GameObject Model => model;
            public Material Material => material;
            public Vector3 Size => size;
            public Vector2 Offset => offset;
            public float BaseHeight => baseHeight;
            public float Yaw => yaw;
        }

        /// <summary>One prop of an arena's surroundings, placed round the floor as <see cref="ArenaDecor"/> describes.</summary>
        [Serializable]
        public sealed class DecorItem
        {
            [Tooltip("The prop; its colours come from the material of its own pack")]
            [SerializeField]
            private GameObject model;

            [Tooltip("Degrees round the arena: 0 straight behind it as the camera looks, 90 to the right of the screen, 180 in front")]
            [SerializeField]
            private float angle;

            [Tooltip("Metres beyond the arena's radius to the prop's middle")]
            [SerializeField]
            private float distance = 3f;

            [Tooltip("Metres of the prop's base above the floor's top; negative stands it on the ground far below")]
            [SerializeField]
            private float height;

            [Tooltip("Multiplier of the model's own size")]
            [Min(0.01f)]
            [SerializeField]
            private float scale = 1f;

            [Tooltip("Extra turn in degrees from facing the arena's middle")]
            [SerializeField]
            private float yaw;

            [Tooltip("Degrees the prop's front tips down (positive) or back, after the yaw: stands a model that lies flat, or " +
                "turns a face up to the camera that looks down on it")]
            [SerializeField]
            private float pitch;

            public GameObject Model => model;
            public float Angle => angle;
            public float Distance => distance;
            public float Height => height;
            public float Scale => scale;
            public float Yaw => yaw;
            public float Pitch => pitch;
        }
    }
}
