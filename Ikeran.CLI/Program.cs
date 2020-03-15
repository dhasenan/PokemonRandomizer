using System;
using System.Collections.Generic;
using Ikeran.NDS;
using Ikeran.PokemonShuffler;
using Mono.Options;
using NLog;

namespace Ikeran.CLI
{
    class MainClass
    {
        private static void ShowHelp(OptionSet options, System.IO.TextWriter writer)
        {
            writer.WriteLine("");
        }

        public static int Main(string[] args)
        {
            bool verbose = false;
            bool showHelp = false;
            string configPath = "";
            string romPath = "";
            string outputPath = "";
            int seed = Seed.Arbitrary();
            var p = new OptionSet()
            {
                {"v|verbose", "enable verbose logging", v => verbose = true},
                {"h|help", "show help message and exit", v => showHelp = true},
                {"c|config=", "config file", (string v) => configPath = v},
                {"i|input=", "input ROM", (string v) => romPath = v},
                {"o|output=", "output ROM", (string v) => outputPath = v},
                {"s|seed=", "random number seed (overrides config)", (string v) => seed = Seed.From(v)},
            };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                p.WriteOptionDescriptions(Console.Error);
                return 1;
            }
            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            var logConfig = new NLog.Config.LoggingConfiguration();
            var console = new NLog.Targets.ConsoleTarget("console");
            logConfig.AddRule(verbose ? LogLevel.Trace : LogLevel.Warn, LogLevel.Fatal, console);
            LogManager.Configuration = logConfig;

            var log = LogManager.GetCurrentClassLogger();
            log.Info("loading rom {0}", romPath);
            var rom = new NintendoDSRom(romPath);
            log.Info("rom loaded");
            foreach (var segment in rom.Segments)
            {
                Console.WriteLine(
                    "segment {0} at {1:X}:{2:X} ({3} bytes)",
                    segment.Magic,
                    segment.Data.Offset,
                    segment.Data.End,
                    segment.Data.Count);
                var narc = Narc.TryParseNarc(segment);
                if (narc != null)
                {
                    Console.WriteLine("NARC at {0}", segment.Data.Offset);
                    PrintDir(narc.Root, "  ");
                }
                else if (segment.Magic == "NARC")
                {
                    Console.WriteLine("invalid NARC");
                }
            }
            return 0;
        }

        private static void PrintDir(Entry dir, string indent)
        {
            indent += " ";
            string type = dir.IsFile ? "f" : "d";
            Console.WriteLine("{0}{1} /{2}", indent, type, dir.Name);
            foreach (var entry in dir.Entries)
            {
                PrintDir(entry, indent);
            }
        }
    }
}
