using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Ikeran.Util
{
    public struct Slice<T> : IReadOnlyList<T>
    {
        public Slice(Slice<T> data, int start, int end)
        {
            Contract.Assert(() => start >= 0);
            Contract.Assert(() => start <= end);
            Contract.Assert(() => end <= data.Count);

            Array = data.Array;
            Offset = start + data.Offset;
            End = end + data.Offset;
            BigEndian = data.BigEndian;
        }

        public Slice(T[] data, int start, int end)
        {
            Contract.Assert(() => start >= 0);
            Contract.Assert(() => start <= end);
            Contract.Assert(() => end <= data.Length);
            Array = data;
            Offset = start;
            End = end;
            BigEndian = true;
        }

        public Slice(T[] data) : this()
        {
            Array = data;
            Offset = 0;
            End = data.Length;
            BigEndian = true;
        }

        public int Offset { get; }
        public T[] Array { get; }
        public int End { get; }
        public int Count => End - Offset;
        public bool BigEndian;

        public Slice<T> After(int start)
        {
            return new Slice<T>(this, start, Count);
        }

        public Slice<T> After(uint start)
        {
            return After((int)start);
        }

        public Slice<T> Until(int limit)
        {
            return new Slice<T>(this, 0, limit);
        }

        public Slice<T> Until(uint limit)
        {
            return Until((int)limit);
        }

        public Slice<T> this[uint start, uint end]
        {
            get
            {
                return this[(int)start, (int)end];
            }
        }

        public Slice<T> this[int start, int end]
        {
            get
            {
                return new Slice<T>(this, start, end);
            }
        }

        public T this[int index]
        {
            get
            {
                var end = End;
                Contract.Assert(() => index >= 0);
                Contract.Assert(() => index < end);
                return Array[Offset + index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SliceEnumerator<T>(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SliceEnumerator<T>(this);
        }
    }

    struct SliceEnumerator<T> : IEnumerator<T>
    {
        private Slice<T> slice;
        private int i;

        public SliceEnumerator(Slice<T> slice)
        {
            this.slice = slice;
            i = -1;
        }

        public T Current => slice[i];

        object IEnumerator.Current => slice[i];

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            i++;
            return i <= slice.Count;
        }

        public void Reset()
        {
            i = 0;
        }
    }

    public static class ByteUtils
    {
        public static uint ReadUInt(this Slice<byte> data, int start)
        {
            if (data.BigEndian)
            {
                uint result = 0;
                for (int i = 3; i >= 0; i--)
                {
                    result <<= 8;
                    result |= data[i + start];
                }
                return result;
            }
            else
            {
                uint result = 0;
                for (int i = 0; i < 4; i++)
                {
                    result <<= 8;
                    result |= data[i + start];
                }
                return result;
            }
        }

        public static int ReadInt(this Slice<byte> data, int start)
        {
            return (int)ReadUInt(data, start);
        }

        public static ushort ReadUShort(this Slice<byte> data, int start)
        {
            if (data.BigEndian)
            {
                return (ushort)((data[start + 1] << 8) | data[start]);
            }
            return (ushort)((data[start] << 8) | data[start + 1]);
        }

        public static string ReadString(this Slice<byte> data, int start, int length)
        {
            return Encoding.UTF8.GetString(data.Array, data.Offset + start, length);
        }

        public static string ReadLString(this Slice<byte> data, int start)
        {
            var length = data[start];
            return ReadString(data, start + 1, length);
        }

        public static bool StartsWith(this Slice<byte> data, byte[] match)
        {
            if (match.Length > data.Count) return false;
            for (int i = 0; i < match.Length; i++)
            {
                if (match[i] != data[i]) return false;
            }
            return true;
        }
    }
}