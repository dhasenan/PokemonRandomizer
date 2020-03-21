using System.Collections.Generic;
using NLog;

namespace Ikeran.NDS
{
    public class RomFileTable : FileTable
    {
        public RomFileTable()
        {
            log = LogManager.GetCurrentClassLogger();
        }

        protected override List<Entry> ReadDirectories()
        {
            var fat = AllocTable;
            var fnt = NameTable;
            var root = new Entry(fnt, 0, 0)
            {
                IsFile = false
            };
            int numFiles = root.parentDirectory;
            root.parentDirectory = 0;
            root.ID = 0xF000;
            Root = root;
            var dirs = new List<Entry> { root };
            for (ushort i = 1; i < numFiles; i++)
            {
                dirs.Add(new Entry(fnt, i, 0));
            }
            return dirs;
        }
    }
}
