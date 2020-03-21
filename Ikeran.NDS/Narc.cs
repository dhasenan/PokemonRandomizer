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

            FileTable = new NarcFileTable(); 
            FileTable.Load(
                segment.Sections[0].Data.After(8),
                segment.Sections[1].Data,
                segment.Sections[2].Data);
        }

        public Entry Root { get => FileTable.Root; }
        public FileTable FileTable { get; }
    }
}
