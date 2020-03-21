using System;
using System.Collections.Generic;
using System.IO;
using Ikeran.NDS;
using Ikeran.PokemonShuffler;
using Ikeran.Util;
using Mono.Options;
using NLog;

namespace Ikeran.CLI
{
    class MainClass
    {
        private static Logger log;

        private static void ShowHelp(OptionSet options, System.IO.TextWriter writer)
        {
            writer.WriteLine("");
        }

        public static int Main(string[] args)
        {
            bool verbose = false;
            bool showHelp = false;
            string configPath = "";
            string romPath = null;
            string outputPath = null;
            int seed = Seed.Arbitrary();
            string extractedFile = null;
            var p = new OptionSet
            {
                {"v|verbose", "enable verbose logging", v => verbose = true},
                {"h|help", "show help message and exit", v => showHelp = true},
                {"c|config=", "config file", (string v) => configPath = v},
                {"i|input=", "input ROM", (string v) => romPath = v},
                {"o|output=", "output ROM", (string v) => outputPath = v},
                {"s|seed=", "random number seed (overrides config)", (string v) => seed = Seed.From(v)},
                {"extract-to=", "extract ROM data to files", (string v) => extractedFile = v},
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

            log = LogManager.GetCurrentClassLogger();
            log.Info("loading rom {0}", romPath);
            var rom = new NintendoDSRom(romPath);
            log.Info("rom loaded");

            if (extractedFile != null)
            {
                SaveAll(extractedFile, rom.FileTable.Root);
            }

            return 0;
        }

        private static void SaveAll(string prefix, Entry entry)
        {
            var name = string.IsNullOrEmpty(entry.Name) ? prefix : Path.Combine(prefix, entry.Name);
            if (entry.IsFile)
            {
                Console.WriteLine($"saving {name}");
                Directory.CreateDirectory(prefix);
                var f = new FileStream(name, FileMode.Create);
                var data = (Slice<byte>)entry.Data;
                f.Write(data.Array, data.Offset, data.Count);
                f.Flush();
                f.Close();
                if (entry.Magic == "NARC")
                {
                    var narcPath = name + "_narc";
                    var narcData = entry.Data.Value;
                    narcData.BigEndian = false;
                    try
                    {
                        var narc = new Narc(new Segment(narcData));
                        SaveAll(narcPath, narc.FileTable.Root);
                    }
                    catch (Exception e)
                    {
                        log.Error($"failed to parse NARC {name}");
                        log.Error(e);
                    }
                }
            }

            foreach (var e in entry.Entries)
            {
                SaveAll(name, e);
            }
        }

        private static void PrintDir(Entry dir, string indent)
        {
            if (dir.IsFile)
            {
                Console.WriteLine("{0}: {1}", dir.Path, dir.Magic ?? "unknown");
            }
            //indent += " ";
            //string type = dir.IsFile ? "f" : "d";
            //Console.WriteLine("{0}{1} /{2}", indent, type, dir.Name);
            foreach (var entry in dir.Entries)
            {
                PrintDir(entry, indent);
            }
        }
    }
}
