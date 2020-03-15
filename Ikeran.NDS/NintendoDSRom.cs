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

            var fileTable = new FileTable(fat, fnt);
        }

        private void ScanForArchives()
        {
            Segments.Clear();
            var remaining = _data;


            int nextLoggedOffset = LogReadBytes;
            while (remaining.Count > 8)
            {
                if (remaining.Offset >= nextLoggedOffset)
                {
                    nextLoggedOffset += LogReadBytes;
                    log.Trace("progress: {0} out of {1}", remaining.Offset, _data.Count);
                }
                if (remaining.After(4).StartsWith(_sectionMarker))
                {
                    var s = TryReadSegment(remaining, bigEndian: false);
                    if (s != null && s.Data.Count > 0)
                    {
                        log.Trace("found a little-endian segment at {0}", s.Data.Offset);
                        remaining = remaining.After(s.Data.Count);
                        continue;
                    }
                }
                else if (remaining.After(4).StartsWith(_bigEndianSectionMarker))
                {
                    var s = TryReadSegment(remaining, bigEndian: true);
                    if (s != null && s.Data.Count > 0)
                    {
                        log.Trace("found a big-endian segment at {0}", s.Data.Offset);
                        remaining = remaining.After(s.Data.Count);
                        continue;
                    }
                }
                remaining = remaining.After(1);
            }
        }

        private Segment TryReadSegment(Slice<byte> remaining, bool bigEndian)
        {
            if (!Segment.KnownMagicBytes.Any(x => remaining.StartsWith(x)))
            {
                return null;
            }
            remaining.BigEndian = bigEndian;
            // We have a segment! Add it to the list and skip over it.
            var segment = new Segment(remaining);
            Segments.Add(segment);
            return segment;
        }
    }
}
