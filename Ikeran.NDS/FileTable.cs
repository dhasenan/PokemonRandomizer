using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{

    public class FileTable
    {
        public FileTable(Slice<byte> fat, Slice<byte> fnt)
        {
            var files = new List<RelativeRange>();
            while (fat.Count >= 8)
            {
                files.Add(new RelativeRange { Start = fat.ReadUInt(0), End = fat.ReadUInt(4) });
                fat = fat.After(8);
            }

            // first read the root directory
            var root = new Entry(fnt, 0);
            var numFiles = root.parentDirectory;
            root.parentDirectory = 0;
            root.ID = 0xF000;  // root directory is always 0xF000
            // and now the rest
            var dirs = new List<Entry>{root};
            for (ushort i = 1; i < numFiles; i++)
            {
                dirs.Add(new Entry(fnt, i));
            }
            dirs.Sort((a, b) => a.firstFileIndex.CompareTo(b.firstFileIndex));
            // The filename section has a slightly weird format.
            // For files, the format is just length followed by the string data. Fine.
            // For directories, the first byte is `length | 0x80`, then string data,
            // then a two-byte value indicating a directory ID.
            // So for instance with PkmnBlk, `/a` has `81 61 01-f0` and `/dl_rom` has
            // `86 64-6c-5f-72-6f-6d 1d-f0`. That means directory IDs 0xf001 and 0xf01d.
            // I think?

            var nameSection = fnt.After(root.nameListStart);

            for (int i = 0; i < dirs.Count; i++)
            {
                var dir = dirs[i];
                uint endOfMyNames = (i < dirs.Count - 1) ? dirs[i + 1].nameListStart: (uint)fnt.Count;
                var names = ParseNames(fnt[dir.nameListStart, endOfMyNames]);
                int name = 0;
                foreach (var kid in dirs.Where(x => x.parentDirectory == dir.ID).ToList())
                {
                    var ent = names[name];
                    if (!ent.IsFile)
                    {
                        kid.IsFile = false;
                        kid.Name = ent.Name;
                        continue;
                    }
                    // Block of files.
                    while (names[name].IsFile)
                    {
                        dir.Entries.Add(new Entry { Name = names[name].Name, IsFile = true });
                    }
                }
            }

            // Set things' names
            //SetNames(root, names);

            // Now let's add files into directories.
            // This structure requires each directory tree to be contiguous in memory.
            // A directory encompasses a range of bytes, which we get by its start
            // and the start of its next sibling. There's also the "file" type of
            // directory, which means "these are files that are siblings to a folder".
            // I think that can also be implicit maybe?

            // okay, let's grab the filenames
            // filenames are length-prefixed strings
        }

        private static List<NameEntry> ParseNames(Slice<byte> nameSection)
        {
            var names = new List<NameEntry>();
            while (nameSection.Count > 0)
            {
                var name = new NameEntry();
                var length = nameSection[0];
                if ((length & 0x80) == 0x80)
                {
                    length -= 0x80;
                }
                else
                {
                    name.IsFile = true;
                }
                nameSection = nameSection.After(1);
                name.Name = nameSection.ReadString(0, length);
                nameSection = nameSection.After(length);
                if (!name.IsFile)
                {
                    name.DirectoryId = nameSection.ReadUShort(0);
                    nameSection = nameSection.After(2);
                }
                names.Add(name);
            }
            return names;
        }

        private static void SetChildExtents(Entry parent)
        {
            if (parent.Entries.Count == 0)
            {
                return;
            }
            for (int i = 0; i < parent.Entries.Count - 1; i++)
            {
                var e = parent.Entries[i];
                e.lastFileIndex = parent.Entries[i + 1].firstFileIndex;
                SetChildExtents(e);
            }
            var last = parent.Entries.Last();
            last.lastFileIndex = parent.lastFileIndex;
            SetChildExtents(last);
        }

        private static void ExpandFiles(Entry parent, List<RelativeRange> files, Slice<byte> data)
        {
            var entries = new List<Entry>();
            foreach (var entry in parent.Entries)
            {
                if (!entry.IsFile)
                {
                    ExpandFiles(entry, files, data);
                    entries.Add(entry);
                    continue;
                }
                for (int i = entry.firstFileIndex; i < entry.lastFileIndex; i++)
                {
                    entries.Add(new Entry
                    {
                        ID = (ushort)i,
                        firstFileIndex = (ushort)i,
                        lastFileIndex = (ushort)(i + 1),
                        Data = data[files[i].Start, files[i].End]
                    });
                }
            }
            parent.Entries.Clear();
            parent.Entries.AddRange(entries);
        }
    }

    internal class NameEntry
    {
        internal int Offset;
        internal string Name;
        internal bool IsFile;
        internal ushort DirectoryId;
    }

    public class Entry
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        const byte DirMask = 0b1000_0000;

        public ushort ID { get; internal set; }
        internal ushort firstFileIndex;
        internal ushort parentDirectory;
        internal RelativeRange fileData;
        internal Slice<byte> names;
        internal ushort lastFileIndex;
        internal uint nameListStart;

        public Entry Parent { get; internal set; }
        public string Name { get; internal set; }
        public List<Entry> Entries { get; } = new List<Entry>();
        public bool IsFile { get; internal set; }
        public bool IsDir { get => !IsFile; }
        public Slice<byte>? Data { get; internal set; }

        public Entry() { }

        internal Entry(Slice<byte> filenameTable, ushort id)
        {
            this.ID = id;
            nameListStart = filenameTable.ReadUInt(id * 8);
            firstFileIndex = filenameTable.ReadUShort(id * 8 + 4);
            parentDirectory = filenameTable.ReadUShort(id * 8 + 6);
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

    public struct RelativeRange
    {
        public uint Start, End;
    }
}
