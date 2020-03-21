using System.Collections.Generic;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    public class NarcFileTable : FileTable
    {
        private uint _numFiles;

        public NarcFileTable()
        {
            log = LogManager.GetCurrentClassLogger();
        }

        public override void Load(Slice<byte> allocTable, Slice<byte> nameTable, Slice<byte> data)
        {
            _numFiles = allocTable.ReadUInt(0);
            base.Load(allocTable.After(4), nameTable, data);
        }

        protected override List<Entry> ReadDirectories()
        {
            // NARCs don't usually have directories.
            var dirs = new List<Entry>();

            var fnt = NameTable;
            // Does this start with the magic bytes 'BTNF' ?
            if (fnt[0] == 0x42 && fnt[1] == 0x54 && fnt[2] == 0x4e && fnt[3] == 0x46)
            {
                fnt = fnt.After(8);
            }
            var dirCount = fnt.ReadUShort(6);
            Root = new Entry
            {
                IsFile = false,
                ID = 0xF000,
                firstFileIndex = (ushort)fnt.ReadUInt(0),
                nameListStart = fnt.ReadUShort(4),
            };
            dirs.Add(Root);

            for (int i = 1; i < dirCount; i++)
            {
                dirs.Add(new Entry
                {
                    ID = (ushort)(0xF000 | i),
                    firstFileIndex = (ushort)fnt.ReadUInt(i * 8),
                    nameListStart = fnt.ReadUShort(i * 8 + 4),
                    parentDirectory = fnt.ReadUShort(i * 8 + 6),
                    IsFile = false,
                });
            }

            return dirs;
        }
    }
}
