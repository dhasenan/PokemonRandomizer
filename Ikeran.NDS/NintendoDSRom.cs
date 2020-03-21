using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Ikeran.Util;
using System;
using NLog;

namespace Ikeran.NDS
{
    // http://dsibrew.org/wiki/NDS_Format
    // https://www.romhacking.net/documents/%5b469%5dnds_formats.htm
    // https://github.com/devkitPro/ndstool
    public class NintendoDSRom
    {
        private const int LogReadBytes = 50 * 1024 * 1024;
        private readonly static Logger log = LogManager.GetCurrentClassLogger();
        private readonly static byte[] _sectionMarker = { 0xff, 0xfe, 0x00, 0x01 };
        private readonly static byte[] _bigEndianSectionMarker = { 0xfe, 0xff, 0x00, 0x01 };
        public string Path { get; }
        public string Name { get; }
        public FileTable FileTable { get; }

        public readonly List<Slice<byte>> FilesById;
        public readonly List<Segment> Segments;
        private Slice<byte> _data;

        public NintendoDSRom() { }

        public NintendoDSRom(string path) : this(path, File.ReadAllBytes(path)) { }

        public NintendoDSRom(string path, byte[] data)
        {
            Path = path;
            _data = new Slice<byte>(data);
            Segments = new List<Segment>();

            // Global file name table: pointer at 0x40, length at 0x44
            // Global file allocation table: pointer at 0x48, length at 0x4c
            var fntOffset = _data.ReadUInt(0x40);
            var fntLength = _data.ReadUInt(0x44);
            var fatOffset = _data.ReadUInt(0x48);
            var fatLength = _data.ReadUInt(0x4C);
            log.Info("FAT at {0:x}..{1:x}", fatOffset, fatOffset + fatLength);
            log.Info("FNT at {0:x}..{1:x}", fntOffset, fntOffset + fntLength);

            var fat = _data[fatOffset, fatOffset + fatLength];
            var fnt = _data[fntOffset, fntOffset + fntLength];

            FileTable = new FileTable(fat, fnt, _data, FileTable.Mode.Rom);
        }
    }
}
