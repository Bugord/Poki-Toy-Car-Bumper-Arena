using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.Build;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Reads the files out of a built bundle (Unity's UnityFS archive), for <see cref="DeferredContent"/>: the music's
    /// raw copy is the encoded audio the WebGL build put in the bundle's <c>.resource</c> file, which the browser decodes
    /// itself. Only what an uncompressed bundle needs: a block directory compressed with LZ4 or not at all, and data
    /// blocks stored as they are.
    /// </summary>
    internal static class BundleArchive
    {
        private const int CompressionMask = 0x3F;
        private const int CompressionNone = 0;
        private const int CompressionLz4 = 2;
        private const int CompressionLz4Hc = 3;
        private const int BlocksInfoAtTheEnd = 0x80;
        private const int BlockInfoNeedPaddingAtStart = 0x200;

        /// <summary>The bytes of every file in the archive, by its name.</summary>
        public static Dictionary<string, byte[]> Files(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var reader = new BigEndianReader(bytes);
            if (reader.String() != "UnityFS") {
                throw new BuildFailedException($"{path} is not a UnityFS bundle.");
            }

            var version = reader.Int32();
            reader.String();
            reader.String();
            reader.Int64();
            var compressedInfo = reader.Int32();
            var uncompressedInfo = reader.Int32();
            var flags = reader.Int32();
            if (version >= 7) {
                reader.Align(16);
            }

            byte[] packedInfo;
            if ((flags & BlocksInfoAtTheEnd) != 0) {
                packedInfo = new byte[compressedInfo];
                Array.Copy(bytes, bytes.Length - compressedInfo, packedInfo, 0, compressedInfo);
            } else {
                packedInfo = reader.Bytes(compressedInfo);
            }

            if ((flags & BlockInfoNeedPaddingAtStart) != 0) {
                reader.Align(16);
            }

            var info = Unpack(packedInfo, uncompressedInfo, flags & CompressionMask, path);
            using var directory = new BigEndianReader(info);
            directory.Bytes(16);
            var blockCount = directory.Int32();
            var data = new MemoryStream();
            for (var i = 0; i < blockCount; i++) {
                var size = directory.Int32();
                var packed = directory.Int32();
                var blockFlags = directory.Int16();
                data.Write(Unpack(reader.Bytes(packed), size, blockFlags & CompressionMask, path));
            }

            var all = data.ToArray();
            var files = new Dictionary<string, byte[]>();
            var nodeCount = directory.Int32();
            for (var i = 0; i < nodeCount; i++) {
                var offset = directory.Int64();
                var size = directory.Int64();
                directory.Int32();
                var name = directory.String();
                var file = new byte[size];
                Array.Copy(all, offset, file, 0, size);
                files[name] = file;
            }

            return files;
        }

        private static byte[] Unpack(byte[] packed, int size, int compression, string path) => compression switch {
            CompressionNone => packed,
            CompressionLz4 or CompressionLz4Hc => Lz4(packed, size),
            _ => throw new BuildFailedException($"{path}: compression {compression} is not read here."),
        };

        /// <summary>One LZ4 block (the format, not the frame): runs of literals, then matches back into the output.</summary>
        private static byte[] Lz4(byte[] source, int size)
        {
            var output = new byte[size];
            var read = 0;
            var write = 0;
            while (read < source.Length) {
                var token = source[read++];
                var literals = token >> 4;
                if (literals == 15) {
                    byte more;
                    do {
                        more = source[read++];
                        literals += more;
                    } while (more == 255);
                }

                Array.Copy(source, read, output, write, literals);
                read += literals;
                write += literals;
                if (read >= source.Length) {
                    break;
                }

                var offset = source[read] | (source[read + 1] << 8);
                read += 2;
                var match = (token & 15) + 4;
                if ((token & 15) == 15) {
                    byte more;
                    do {
                        more = source[read++];
                        match += more;
                    } while (more == 255);
                }

                // Byte by byte: a match may overlap the bytes it copies.
                for (var i = 0; i < match; i++) {
                    output[write] = output[write - offset];
                    write++;
                }
            }

            if (write != size) {
                throw new BuildFailedException($"LZ4 block unpacked to {write} bytes, not {size}.");
            }

            return output;
        }

        private sealed class BigEndianReader : IDisposable
        {
            private readonly byte[] bytes;
            private int position;

            public BigEndianReader(byte[] bytes) => this.bytes = bytes;

            public void Dispose()
            {
            }

            public string String()
            {
                var end = Array.IndexOf(bytes, (byte)0, position);
                var text = Encoding.UTF8.GetString(bytes, position, end - position);
                position = end + 1;
                return text;
            }

            public short Int16() => (short)((Byte() << 8) | Byte());

            public int Int32() => (Byte() << 24) | (Byte() << 16) | (Byte() << 8) | Byte();

            public long Int64() => ((long)Int32() << 32) | (uint)Int32();

            public byte[] Bytes(int count)
            {
                var result = new byte[count];
                Array.Copy(bytes, position, result, 0, count);
                position += count;
                return result;
            }

            public void Align(int to) => position = (position + to - 1) / to * to;

            private int Byte() => bytes[position++];
        }
    }
}
