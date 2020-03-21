using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ikeran.Util;
using NLog;

namespace Ikeran.NDS
{
    public class Section
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Magic { get; }
        public Slice<byte> Header { get; }
        public Slice<byte> Body { get; }

        public Section(Slice<byte> data)
        {
            // The header is constrained to <= 255 bytes.
            // The body must be a multiple of 256 bytes.
            var headerLength = data.Count & 0xFF;
            
            Header = data.Until(headerLength);
            Body = data.After(headerLength);
            Magic = data.ReadString(0, 4);
        }
    }

    public class Segment
    {
        public static readonly string[] KnownMagic =
        {
            // Some magic appears in reverse as well as forward.
            "NARC",
            "NCLR", "RLCN",
            "NCGR", "RGCN",
            "NSCR", "RCSN",
            "NANR", "RNAN",
            "NCER", "RECN",
            "SDAT",
        };
        public static readonly IList<byte[]> KnownMagicBytes = KnownMagic.Select(Encoding.ASCII.GetBytes).ToList();
        private static Logger log = LogManager.GetCurrentClassLogger();

        public Segment(Slice<byte> data)
        {
            // Generic header:
            // - [0:4] magic number, eg 'NARC'
            // - [4:8] constant 0xFEFF0001
            // - [8:12] byte length of segment
            // - [12:14] size of header, always 0x10
            // - [14:16] number of sub-segments
            var remainder = data;
            Magic = remainder.ReadString(0, 4);
            var c = remainder.ReadUInt(4);
            remainder.BigEndian = !remainder.BigEndian;
            var length = remainder.ReadUInt(8);
            remainder = remainder.Until(length);
            var headerLength = remainder.ReadUShort(0xC);
            var numSections = remainder.ReadUShort(0xE);
            log.Trace("nds rom segment at {3:X}, magic {0}, length {1:X}, sections {2:X}", Magic, length, numSections, remainder.Offset);
            Header = remainder.Until(headerLength);
            Data = remainder[headerLength, length - headerLength];

            remainder = remainder.After(headerLength);

            // SDAT has nonstandard format
            if (Magic == "SDAT") return;
            for (int i = 0; i < numSections; i++)
            {
                if (remainder.Count < 8)
                {
                    log.Trace("malformed segment at {0}; skipping child sections", data.Offset);
                    break;
                }
                length = remainder.ReadUInt(4);
                log.Info($"segment at {remainder.Offset:x} length {length:x}");
                if (length > remainder.Count)
                {
                    log.Trace("malformed segment at {0}; skipping child sections", data.Offset);
                    break;
                }
                var section = new Section(remainder[0, length]);
                Sections.Add(section);
                remainder = remainder.After(length);
                log.Trace("nds rom section, magic {0}, length {1:X}", section.Magic, length);
            }
        }

        public string Magic { get; }
        public Slice<byte> Header { get; }
        public List<Section> Sections { get; } = new List<Section>();
        public Slice<byte> Data { get; }

        private static bool HasSections(string magic)
        {
            switch (magic)
            {
                case "NARC":
                case "NSCR":
                case "RCSN":
                    return true;
                default:
                    return false;
            }
        }
    }
}
