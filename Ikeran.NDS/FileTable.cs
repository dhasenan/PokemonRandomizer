using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    /// <summary>
    /// A file table is a means to access files, typically by path.
    /// </summary>
    public interface IFileTable
    {
        /// <summary>
        /// The root directory.
        /// </summary>
        Entry Root { get; }

        /// <summary>
        /// Anonymous files -- files not associated with any directory, without any name.
        /// </summary>
        IList<Entry> AnonymousFiles { get; }

        /// <summary>
        /// Look up a file by path.
        /// </summary>
        /// <returns>The matching file, or null if not found.</returns>
        /// <param name="path">The path, with '/' as the path element separator</param>
        Entry TryLookUp(string path);

        /// <summary>
        /// Load a filesystem
        /// </summary>
        /// <param name="allocTable">The file allocation table (FAT)</param>
        /// <param name="nameTable">The file name table (FNT)</param>
        /// <param name="data">The data (eg GMIF)</param>
        void Load(Slice<byte> allocTable, Slice<byte> nameTable, Slice<byte> data);
    }

    public abstract class FileTable : IFileTable
    {
        public Entry Root { get; protected set; }
        public IList<Entry> AnonymousFiles { get; } = new List<Entry>();
        protected Slice<byte> Data, NameTable, AllocTable;
        protected Logger log;

        /// <summary>
        /// Look up a file by path.
        /// </summary>
        /// <returns>The matching file, or null if not found.</returns>
        /// <param name="path">The path, with '/' as the path element separator</param>
        public Entry TryLookUp(string path)
        {
            Entry current = Root;
            var parts = path.Trim('/').Split('/');
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

        /// <summary>
        /// Load a filesystem
        /// </summary>
        /// <param name="allocTable">The file allocation table (FAT)</param>
        /// <param name="nameTable">The file name table (FNT)</param>
        /// <param name="data">The data (eg GMIF)</param>
        public void Load(Slice<byte> allocTable, Slice<byte> nameTable, Slice<byte> data)
        {
            AllocTable = allocTable;
            NameTable = nameTable;
            Data = data;

            var fnt = nameTable;
            var fat = allocTable;

            var dirs = ReadDirectories();
            dirs.Sort((a, b) => a.firstFileIndex.CompareTo(b.firstFileIndex));
            Root = dirs[0];
            Root.ID = 0xF000;

            uint minFileId = uint.MaxValue;
            uint maxFileId = 0;
            if (Root.nameListStart == 0)
            {
                // We don't have any explicitly named files.
                minFileId = 1;
                maxFileId = 0;
                goto nameListRead;
            }
            log.Info("root name list starts at {0:x}", Root.nameListStart);
            var nameSection = fnt.After(Root.nameListStart);
            for (int i = 0; i < dirs.Count; i++)
            {
                var dir = dirs[i];
                uint endOfMyNames = (i < dirs.Count - 1) ? dirs[i + 1].nameListStart : (uint)fnt.Count;
                log.Info($"name list {i} data: {dir.nameListStart:x} - {endOfMyNames}; fnt size: {fnt.Count:x}; num dirs: {dirs.Count}");
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
        nameListRead:
            for (ushort fileId = 0; fileId < minFileId; fileId++)
            {
                AnonymousFiles.Add(GetFile(fileId, $"_anon_${fileId}"));
            }
            for (ushort fileId = (ushort)(maxFileId + 1); fileId < fat.Count / 8; fileId++)
            {
                AnonymousFiles.Add(GetFile(fileId, $"_anon_${fileId}"));
            }
        }

        protected Entry GetFile(ushort fileId, string name)
        {
            if (fileId >= AllocTable.Count / 8)
            {
                throw new Exception($"invalid file, name {name}, id {fileId:X}");
            }
            var start = AllocTable.ReadUInt(8 * fileId);
            var end = AllocTable.ReadUInt(8 * fileId + 4);
            if (end > Data.Count)
            {
                throw new Exception($"file {name} {fileId:X}: have {Data.Count} bytes of data, file data from {start:X}-{end:X}");
            }
            var fileData = Data[start, end];
            return new Entry
            {
                ID = (ushort)(fileId | 0xF000),
                Name = name,
                IsFile = true,
                Data = fileData,
            };
        }

        protected abstract List<Entry> ReadDirectories();

        internal static List<NameEntry> ParseNames(Slice<byte> nameSection)
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
}
