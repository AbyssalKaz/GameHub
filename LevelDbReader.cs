using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class LevelDbReader
{
    private class Record
    {
        public long Sequence;
        public bool Deleted;
        public byte[] Value;
    }

    public static Dictionary<string, string> ReadAll(string directory)
    {
        var records = new Dictionary<string, Record>();

        foreach (string file in Directory.GetFiles(directory))
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".ldb" && extension != ".sst" && extension != ".log") continue;

            try
            {
                byte[] data = ReadShared(file);
                if (extension == ".log") ReadLog(data, records);
                else ReadTable(data, records);
            }
            catch (Exception) { }
        }

        var result = new Dictionary<string, string>();
        foreach (var pair in records)
        {
            if (!pair.Value.Deleted) result[pair.Key] = Encoding.UTF8.GetString(pair.Value.Value);
        }
        return result;
    }

    private static byte[] ReadShared(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    private static void Store(Dictionary<string, Record> records, string key, long sequence, bool deleted, byte[] value)
    {
        Record existing;
        if (records.TryGetValue(key, out existing) && existing.Sequence > sequence) return;
        records[key] = new Record { Sequence = sequence, Deleted = deleted, Value = value };
    }

    private static void ReadTable(byte[] data, Dictionary<string, Record> records)
    {
        int position = data.Length - 48;
        ReadVarint(data, ref position);
        ReadVarint(data, ref position);
        int indexOffset = (int)ReadVarint(data, ref position);
        int indexSize = (int)ReadVarint(data, ref position);

        foreach (var handle in ParseBlock(ReadBlock(data, indexOffset, indexSize)))
        {
            int handlePosition = 0;
            int blockOffset = (int)ReadVarint(handle.Value, ref handlePosition);
            int blockSize = (int)ReadVarint(handle.Value, ref handlePosition);

            foreach (var item in ParseBlock(ReadBlock(data, blockOffset, blockSize)))
            {
                byte[] internalKey = item.Key;
                if (internalKey.Length < 8) continue;

                ulong trailer = BitConverter.ToUInt64(internalKey, internalKey.Length - 8);
                string userKey = Encoding.UTF8.GetString(internalKey, 0, internalKey.Length - 8);
                Store(records, userKey, (long)(trailer >> 8), (trailer & 0xFF) == 0, item.Value);
            }
        }
    }

    private static byte[] ReadBlock(byte[] data, int offset, int size)
    {
        byte compression = data[offset + size];
        byte[] raw = new byte[size];
        Buffer.BlockCopy(data, offset, raw, 0, size);

        if (compression == 0) return raw;
        if (compression == 1) return DecompressSnappy(raw);
        throw new NotSupportedException("Unsupported block compression " + compression);
    }

    private static List<KeyValuePair<byte[], byte[]>> ParseBlock(byte[] block)
    {
        var items = new List<KeyValuePair<byte[], byte[]>>();
        int restartCount = (int)BitConverter.ToUInt32(block, block.Length - 4);
        int end = block.Length - 4 - restartCount * 4;
        int position = 0;
        byte[] previousKey = new byte[0];

        while (position < end)
        {
            int shared = (int)ReadVarint(block, ref position);
            int unshared = (int)ReadVarint(block, ref position);
            int valueLength = (int)ReadVarint(block, ref position);

            byte[] key = new byte[shared + unshared];
            Buffer.BlockCopy(previousKey, 0, key, 0, shared);
            Buffer.BlockCopy(block, position, key, shared, unshared);
            position += unshared;

            byte[] value = new byte[valueLength];
            Buffer.BlockCopy(block, position, value, 0, valueLength);
            position += valueLength;

            items.Add(new KeyValuePair<byte[], byte[]>(key, value));
            previousKey = key;
        }

        return items;
    }

    private static void ReadLog(byte[] data, Dictionary<string, Record> records)
    {
        const int BlockSize = 32768;
        const int HeaderSize = 7;
        var pending = new MemoryStream();
        int position = 0;

        while (position + HeaderSize <= data.Length)
        {
            int remainingInBlock = BlockSize - (position % BlockSize);
            if (remainingInBlock < HeaderSize)
            {
                position += remainingInBlock;
                continue;
            }

            int length = data[position + 4] | (data[position + 5] << 8);
            int type = data[position + 6];
            position += HeaderSize;

            if (type == 0)
            {
                position += remainingInBlock - HeaderSize;
                continue;
            }
            if (position + length > data.Length) break;

            if (type == 1 || type == 2) pending.SetLength(0);
            pending.Write(data, position, length);
            position += length;

            if (type == 1 || type == 4) ReadWriteBatch(pending.ToArray(), records);
        }
    }

    private static void ReadWriteBatch(byte[] batch, Dictionary<string, Record> records)
    {
        if (batch.Length < 12) return;

        long sequence = BitConverter.ToInt64(batch, 0);
        int count = BitConverter.ToInt32(batch, 8);
        int position = 12;

        for (int index = 0; index < count && position < batch.Length; index++)
        {
            byte tag = batch[position++];
            int keyLength = (int)ReadVarint(batch, ref position);
            string key = Encoding.UTF8.GetString(batch, position, keyLength);
            position += keyLength;

            if (tag == 1)
            {
                int valueLength = (int)ReadVarint(batch, ref position);
                byte[] value = new byte[valueLength];
                Buffer.BlockCopy(batch, position, value, 0, valueLength);
                position += valueLength;
                Store(records, key, sequence + index, false, value);
            }
            else
            {
                Store(records, key, sequence + index, true, null);
            }
        }
    }

    private static byte[] DecompressSnappy(byte[] input)
    {
        int position = 0;
        byte[] output = new byte[(int)ReadVarint(input, ref position)];
        int written = 0;

        while (position < input.Length)
        {
            byte tag = input[position++];
            int kind = tag & 3;

            if (kind == 0)
            {
                int length = tag >> 2;
                if (length >= 60)
                {
                    int extraBytes = length - 59;
                    length = 0;
                    for (int i = 0; i < extraBytes; i++) length |= input[position++] << (8 * i);
                }
                length += 1;
                Buffer.BlockCopy(input, position, output, written, length);
                position += length;
                written += length;
                continue;
            }

            int copyLength;
            int copyOffset;
            if (kind == 1)
            {
                copyLength = ((tag >> 2) & 7) + 4;
                copyOffset = ((tag >> 5) << 8) | input[position++];
            }
            else if (kind == 2)
            {
                copyLength = (tag >> 2) + 1;
                copyOffset = input[position] | (input[position + 1] << 8);
                position += 2;
            }
            else
            {
                copyLength = (tag >> 2) + 1;
                copyOffset = input[position] | (input[position + 1] << 8) | (input[position + 2] << 16) | (input[position + 3] << 24);
                position += 4;
            }

            for (int i = 0; i < copyLength; i++)
            {
                output[written] = output[written - copyOffset];
                written++;
            }
        }

        return output;
    }

    private static long ReadVarint(byte[] data, ref int position)
    {
        long result = 0;
        int shift = 0;
        while (true)
        {
            byte current = data[position++];
            result |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) return result;
            shift += 7;
        }
    }
}
