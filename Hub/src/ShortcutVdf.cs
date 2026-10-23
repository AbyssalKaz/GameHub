using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GameHub
{
    internal sealed class VdfField
    {
        public byte Type { get; set; }
        public string Key { get; set; }
        public object Value { get; set; }
    }

    internal sealed class VdfNode
    {
        public List<VdfField> Fields { get; } = new List<VdfField>();

        public VdfField Find(string key) => Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));

        public VdfNode Child(string key) => Find(key)?.Value as VdfNode;

        public string GetString(string key) => Find(key)?.Value as string;

        public uint? GetUInt(string key) => Find(key)?.Value is uint value ? value : (uint?)null;

        public void SetString(string key, string value) => Set(key, ShortcutVdf.TypeString, value ?? "");

        public void SetUInt(string key, uint value) => Set(key, ShortcutVdf.TypeInt, value);

        public void SetNode(string key, VdfNode value) => Set(key, ShortcutVdf.TypeMap, value);

        private void Set(string key, byte type, object value)
        {
            VdfField field = Find(key);
            if (field == null)
            {
                Fields.Add(new VdfField { Key = key, Type = type, Value = value });
                return;
            }
            field.Type = type;
            field.Value = value;
        }
    }

    internal static class ShortcutVdf
    {
        public const byte TypeMap = 0;
        public const byte TypeString = 1;
        public const byte TypeInt = 2;
        public const byte TypeFloat = 3;
        public const byte TypeUInt64 = 7;
        public const byte TypeEnd = 8;
        public const byte TypeInt64 = 10;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static VdfNode Read(byte[] bytes)
        {
            int position = 0;
            return ReadNode(bytes, ref position);
        }

        public static VdfNode ReadFile(string path)
        {
            return File.Exists(path) ? Read(File.ReadAllBytes(path)) : new VdfNode();
        }

        private static VdfNode ReadNode(byte[] bytes, ref int position)
        {
            var node = new VdfNode();
            while (position < bytes.Length)
            {
                byte type = bytes[position++];
                if (type == TypeEnd) break;

                string key = ReadString(bytes, ref position);
                object value;
                switch (type)
                {
                    case TypeMap:
                        value = ReadNode(bytes, ref position);
                        break;
                    case TypeString:
                        value = ReadString(bytes, ref position);
                        break;
                    case TypeInt:
                        value = BitConverter.ToUInt32(bytes, position);
                        position += 4;
                        break;
                    case TypeFloat:
                        value = BitConverter.ToSingle(bytes, position);
                        position += 4;
                        break;
                    case TypeUInt64:
                        value = BitConverter.ToUInt64(bytes, position);
                        position += 8;
                        break;
                    case TypeInt64:
                        value = BitConverter.ToInt64(bytes, position);
                        position += 8;
                        break;
                    default:
                        throw new InvalidDataException($"Unknown field type {type} in shortcuts.vdf");
                }
                node.Fields.Add(new VdfField { Type = type, Key = key, Value = value });
            }
            return node;
        }

        private static string ReadString(byte[] bytes, ref int position)
        {
            int start = position;
            while (position < bytes.Length && bytes[position] != 0) position++;
            string text = Utf8.GetString(bytes, start, position - start);
            position++;
            return text;
        }

        public static byte[] Write(VdfNode root)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                WriteNode(writer, root);
            }
            return stream.ToArray();
        }

        private static void WriteNode(BinaryWriter writer, VdfNode node)
        {
            foreach (VdfField field in node.Fields)
            {
                writer.Write(field.Type);
                WriteString(writer, field.Key);
                switch (field.Type)
                {
                    case TypeMap:
                        WriteNode(writer, field.Value as VdfNode ?? new VdfNode());
                        break;
                    case TypeString:
                        WriteString(writer, field.Value as string ?? "");
                        break;
                    case TypeInt:
                        writer.Write(Convert.ToUInt32(field.Value));
                        break;
                    case TypeFloat:
                        writer.Write(Convert.ToSingle(field.Value));
                        break;
                    case TypeUInt64:
                        writer.Write(Convert.ToUInt64(field.Value));
                        break;
                    case TypeInt64:
                        writer.Write(Convert.ToInt64(field.Value));
                        break;
                }
            }
            writer.Write(TypeEnd);
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            writer.Write(Utf8.GetBytes(text));
            writer.Write((byte)0);
        }

        public static uint ShortcutAppId(string exe, string name)
        {
            return Crc32(Utf8.GetBytes(exe + name)) | 0x80000000;
        }

        public static ulong GameId(uint appId) => ((ulong)appId << 32) | 0x02000000;

        private static uint[] crcTable;

        private static uint Crc32(byte[] bytes)
        {
            if (crcTable == null)
            {
                var table = new uint[256];
                for (uint index = 0; index < 256; index++)
                {
                    uint value = index;
                    for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
                    table[index] = value;
                }
                crcTable = table;
            }

            uint crc = 0xFFFFFFFF;
            foreach (byte current in bytes) crc = crcTable[(crc ^ current) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }
    }
}
