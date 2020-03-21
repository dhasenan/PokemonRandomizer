using System.Collections.Generic;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    public class NarcFileTable : FileTable
    {
        public NarcFileTable()
        {
            log = LogManager.GetCurrentClassLogger();
        }

        protected override List<Entry> ReadDirectories()
        {
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
                firstFileIndex = fnt.ReadUShort(4),
                nameListStart = fnt.ReadUInt(0),
            };
            dirs.Add(Root);

            for (int i = 1; i < dirCount; i++)
            {
                dirs.Add(new Entry
                {
                    ID = (ushort)(0xF000 | i),
                    nameListStart = fnt.ReadUInt(i * 8),
                    firstFileIndex = fnt.ReadUShort(i * 8 + 4),
                    parentDirectory = fnt.ReadUShort(i * 8 + 6),
                    IsFile = false,
                });
            }

            return dirs;
        }
    }
}
