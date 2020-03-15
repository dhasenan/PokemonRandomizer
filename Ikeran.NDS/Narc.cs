using System.Collections.Generic;
using System.IO;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    public class Narc
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public static Narc TryParseNarc(Segment segment)
        {
            if (segment.Magic != "NARC") return null;
            if (segment.Sections.Count != 3) return null;
            if (segment.Sections[0].Magic != "BTAF") return null;
            if (segment.Sections[1].Magic != "BTNF") return null;
            if (segment.Sections[2].Magic != "GMIF") return null;
            return new Narc(segment);
        }

        public Narc(Segment segment)
        {
            Contract.Assert(() => segment.Magic == "NARC");
            Contract.Assert(() => segment.Sections.Count == 3);
            Contract.Assert(() => segment.Sections[0].Magic == "BTAF");
            Contract.Assert(() => segment.Sections[1].Magic == "BTNF");
            Contract.Assert(() => segment.Sections[2].Magic == "GMIF");

            // file allocation table: how big and where each file is
            var fileOffsets = new List<RelativeRange>();
            var fat = segment.Sections[0];
            uint entryCount = fat.Header.ReadUInt(8);
            log.Trace("{0} entries", entryCount);
            var fatBody = fat.Body;
            for (int i = 0; i < entryCount; i++)
            {
                fileOffsets.Add(new RelativeRange { Start = fatBody.ReadUInt(0), End = fatBody.ReadUInt(4) });
                fatBody = fatBody.After(8);
            }

            // file name table
            // technically two sections except they only have one header
            // first part is the directory table
            // second part is the name table
            var fnt = segment.Sections[1];
            var dirs = new List<Entry>();
            var root = new Entry();
            var dirTable = fnt.Body.After(8);
            //root.startOffset = dirTable.ReadInt(0);
            root.firstFileIndex = dirTable.ReadUShort(4);
            dirs.Add(root);
#if false
            // TODO: find a ROM with a non-empty BNTF to figure out what this should look like           
            dirTable = dirTable.After(8);
            for (int i = 1; i < entryCount; i++)
            {
                var dir = new NarcDir();
                dir.id = (ushort)i;
                dir.startOffset = dirTable.ReadInt(0) + root.startOffset;
                dir.firstFileIndex = fnt.Body.ReadUShort(4);
                dir.parentDirectory = fnt.Body.ReadUShort(6);
                dirs.Add(dir);
                dirTable = dirTable.After(8);
            }

            var nameTable = dirTable;
            log.Trace("dir table has {0} entries", dirs.Count);

            while (nameTable.Count > 0)
            {
                var start = nameTable.Offset;
                var length = nameTable[0];
                bool isFile = (length & (0b10000000)) == 0;
                length &= 0b01111111;
                nameTable = nameTable.After(1);
                var name = nameTable.ReadString(0, length);
                nameTable = nameTable.After(length);
                var directoryId = nameTable.ReadUShort(0);
                nameTable = nameTable.After(2);
                log.Trace("{2:x}: directory {0} name {1}", directoryId, name, start);
                dirs[directoryId].Name = name;
                dirs[directoryId].IsFile = isFile;
            }
#endif

            Contract.Assert(() => root.Entries.Count == 0);

            // Okay, now that we have the directories with their names, let's turn them into a proper tree.
            foreach (Entry dir in dirs)
            {
                if (dir.ID == 0) continue;
                var parent = dirs[dir.parentDirectory];
                Contract.Assert(parent != dir, "tried to make directory its own child");
                Contract.Assert(!parent.IsFile, "tried to add directory as child of file");
                dir.Parent = parent;
                parent.Entries.Add(dir);
            }
            Root = root;
            Contract.Assert(() => root.Entries.Count == 0);

            // And let's add the files where they should go.
            // TODO figure out where the files should go
            int index = 0;
            foreach (var file in fileOffsets)
            {
                Root.Entries.Add(new Entry
                {
                    Name = index.ToString("x"),
                    fileData = file,
                    IsFile = true,
                    Parent = root,
                });
                index++;
            }
            Contract.Assert(() => Root.Entries.Count == entryCount);
            Contract.Assert(() => Root.Entries.Count == fileOffsets.Count);

            // TODO decompress
            data = segment.Sections[2].Body;
        }

        public Entry Root { get; internal set; }

        private Slice<byte> data;

        public Slice<byte> ReadFile(string path)
        {
            Entry current = Root;
            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0)
                {
                    continue;
                }
                foreach (Entry child in current.Entries)
                {
                    if (child.Name == part)
                    {
                        current = child;
                        continue;
                    }
                }
                throw new FileNotFoundException("NARC archive did not contain file " + path);
            }
            return data[current.fileData.Start, current.fileData.End];
        }
    }
}
