using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// The textures of a content bundle without the bundle (<see cref="ContentNames.RawFileOf"/>): each texture's raw
    /// GPU data in the format the build gave it (crunched DXT1 floors, DXT5 portraits), with what it takes to make the
    /// texture again. The build writes the file (<c>DeferredContent</c>), the player reads it when the bundle itself
    /// will not open, so the textures come in through no archive reader or image decoder at all: the format's own
    /// unpacking is the one the textures inside the player already go through.
    /// </summary>
    public static class RawTextures
    {
        private const int Version = 1;

        public static void Write(Stream stream, IEnumerable<Texture2D> textures)
        {
            var list = new List<Texture2D>(textures);
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(list.Count);
            foreach (var texture in list) {
                var data = texture.GetRawTextureData();
                writer.Write(texture.name);
                writer.Write(texture.width);
                writer.Write(texture.height);
                writer.Write((int)texture.format);
                writer.Write(texture.mipmapCount);
                writer.Write(!texture.isDataSRGB);
                writer.Write((int)texture.filterMode);
                writer.Write((int)texture.wrapModeU);
                writer.Write((int)texture.wrapModeV);
                writer.Write(texture.anisoLevel);
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        /// <summary>The textures, by name, made from the file's bytes; their CPU copies are dropped once uploaded.</summary>
        public static Dictionary<string, Texture2D> Read(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            var version = reader.ReadInt32();
            if (version != Version) {
                throw new InvalidDataException($"raw textures version {version}, expected {Version}");
            }

            var count = reader.ReadInt32();
            var textures = new Dictionary<string, Texture2D>(count);
            for (var i = 0; i < count; i++) {
                var name = reader.ReadString();
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();
                var format = (TextureFormat)reader.ReadInt32();
                var mips = reader.ReadInt32();
                var linear = reader.ReadBoolean();
                var filter = (FilterMode)reader.ReadInt32();
                var wrapU = (TextureWrapMode)reader.ReadInt32();
                var wrapV = (TextureWrapMode)reader.ReadInt32();
                var aniso = reader.ReadInt32();
                var data = reader.ReadBytes(reader.ReadInt32());
                var texture = new Texture2D(width, height, format, mips, linear) {
                    name = name,
                    filterMode = filter,
                    wrapModeU = wrapU,
                    wrapModeV = wrapV,
                    anisoLevel = aniso,
                };
                texture.LoadRawTextureData(data);
                texture.Apply(false, true);
                textures[name] = texture;
            }

            return textures;
        }
    }
}
