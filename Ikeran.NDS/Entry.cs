using System.Collections.Generic;
using System.Linq;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    public class Entry
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        const byte DirMask = 0b1000_0000;

        public ushort ID { get; internal set; }
        internal ushort firstFileIndex;
        internal ushort parentDirectory;
        internal RelativeRange fileData;
        internal uint nameListStart;

        public Entry Parent { get; internal set; }
        public string Name { get; internal set; }
        public List<Entry> Entries { get; } = new List<Entry>();
        public bool IsFile { get; internal set; }
        public bool IsDir { get => !IsFile; }
        public Slice<byte>? Data { get; internal set; }
        public string Magic
        {
            get
            {
                if (Data.HasValue && Data.Value.Count > 4)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (!(Data.Value[i] >= (byte)'A' && Data.Value[i] <= (byte)'Z') &&
                            !(Data.Value[i] >= (byte)'a' && Data.Value[i] <= (byte)'z'))
                        {
                            return null;
                        }
                    }
                    return Data.Value.ReadString(0, 4);
                }
                return null;
            }
        }

        public string Path
        {
            get
            {
                if (Parent == null)
                {
                    return Name;
                }
                return Parent.Path + "/" + Name;
            }
        }

        public Entry() { }

        internal Entry(Slice<byte> filenameTable, ushort id, int skipFntBytes)
        {
            this.ID = id;
            nameListStart = filenameTable.ReadUInt(id * 8 + skipFntBytes);
            firstFileIndex = filenameTable.ReadUShort(id * 8 + 4 + skipFntBytes);
            parentDirectory = filenameTable.ReadUShort(id * 8 + 6 + skipFntBytes);
        }

        public Entry LookUp(string path)
        {
            return LookUpImpl(path, 0);
        }

        private Entry LookUpImpl(string path, int start)
        {
            if (path.Length <= start)
            {
                return this;
            }
            if (IsFile)
            {
                return null;
            }
            var s = path.IndexOf('/', start + 1);
            var nextElement = path.Substring(start + 1, start - s);
            foreach (var entry in Entries)
            {
                if (entry.Name == nextElement)
                {
                    return entry.LookUpImpl(path, s);
                }
            }
            return null;
        }
    }
}
