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
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public FileTable(Slice<byte> fat, Slice<byte> fnt, Slice<byte> data)
        {
            Entry GetFile(ushort fileId, string name)
            {
                if (fileId >= fat.Count / 8)
                {
                    throw new Exception($"invalid file name {name} id {fileId:X}");
                }
                var start = fat.ReadUInt(8 * fileId);
                var end = fat.ReadUInt(8 * fileId + 4);
                if (end > data.Count)
                {
                    throw new Exception($"file {name} {fileId:X}: have {data.Count} bytes of data, file data from {start:X}-{end:X}");
                }
                var fileData = data[start, end];
                return new Entry
                {
                    ID = (ushort)(fileId | 0xF000),
                    Name = name,
                    IsFile = true,
                    Data = fileData,
                };
            }

            // first read the root directory
            var root = new Entry(fnt, 0);
            var numFiles = root.parentDirectory;
            root.parentDirectory = 0;
            root.ID = 0xF000;
            Root = root;
            var dirs = new List<Entry> { root };
            for (ushort i = 1; i < numFiles; i++)
            {
                dirs.Add(new Entry(fnt, i));
            }
            dirs.Sort((a, b) => a.firstFileIndex.CompareTo(b.firstFileIndex));

            // TODO Not all filename tables have a namelist. Autopopulate names as a, b, c ?
            var nameSection = fnt.After(root.nameListStart);
            uint minFileId = uint.MaxValue;
            uint maxFileId = 0;
            for (int i = 0; i < dirs.Count; i++)
            {
                var dir = dirs[i];
                uint endOfMyNames = (i < dirs.Count - 1) ? dirs[i + 1].nameListStart : (uint)fnt.Count;
                var names = ParseNames(fnt[dir.nameListStart, endOfMyNames]);
                ushort fileNum = 0;
                for (int j = 0; j < names.Count; j++)
                {
                    var name = names[j];
                    if (name.IsFile)
                    {
                        ushort fileId = (ushort)(fileNum + dir.firstFileIndex);
                        log.Info($"dir {dir.ID:X}: adding file {fileId:X} {name.Name}");
                        minFileId = Math.Min(minFileId, fileId);
                        maxFileId = Math.Max(maxFileId, fileId);
                        var file = GetFile(fileId, name.Name);
                        file.Parent = dir;
                        dir.Entries.Add(file);
                        fileNum++;
                    }
                    else
                    {
                        var childDir = dirs.FirstOrDefault(x => (x.ID | 0xF000) == name.DirectoryId);
                        if (childDir == null)
                        {
                            throw new Exception($"failed to find directory id {name.DirectoryId:X} name {name.Name}");
                        }
                        childDir.Name = name.Name;
                        childDir.Parent = dir;
                        dir.Entries.Add(childDir);
                    }
                }
            }

            log.Trace($"We've found names for files between {minFileId} and {maxFileId} inclusive");
            AnonymousFiles = new List<Entry>();
            for (ushort fileId = 0; fileId < minFileId; fileId++)
            {
                AnonymousFiles.Add(GetFile(fileId, $"_anon_${fileId}"));
            }
            for (ushort fileId = (ushort)(maxFileId + 1); fileId < fat.Count / 8; fileId++)
            {
                AnonymousFiles.Add(GetFile(fileId, $"_anon_${fileId}"));
            }
        }

        public Entry TryLookUp(string extractedFile)
        {
            Entry current = Root;
            var parts = extractedFile.Trim('/').Split('/');
            foreach (var part in parts)
            {
                foreach (var child in current.Entries)
                {
                    if (child.Name == part)
                    {
                        current = child;
                        goto next;
                    }
                }
                return null;
                next: { }
            }
            return current;
        }

        public Entry Root { get; }
        public List<Entry> AnonymousFiles { get; }

        private static List<NameEntry> ParseNames(Slice<byte> nameSection)
        {
            var names = new List<NameEntry>();
            while (nameSection.Count > 0)
            {
                var name = new NameEntry();
                var length = nameSection[0];
                // Each blob of names ends with a NUL.
                if (length == 0) break;
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
    }

    internal class NameEntry
    {
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
