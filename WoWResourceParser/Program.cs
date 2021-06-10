using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WoWResourceParser
{
    class Program
    {
        static void Main(string[] args)
        {
            string adtFolder = "E:\\_NewProjectWoW\\Client\\World\\Maps\\DragonIsles";
            string dataFolder = "E:\\_NewProjectWoW\\Client";
            string assetsFilePath = "assets.txt";

            Console.WriteLine("-------------------");
            var banner = @"
Starting program:
 For each ADT:
  Extract every M2 path
  Extract every WMO path
 For each WMO:
  Extract every M2 path
  Extract every BLP path
 For each M2:
  Extract every BLP path
 return all M2, WMO, and BLP paths required

ADT Folder:" + $"\n {adtFolder}\n" +
"Data Folder:" + $"\n {dataFolder}\n";
            Console.WriteLine(banner);
            Console.WriteLine("-------------------");

            var wmos = new List<string>();
            var m2s = new List<string>();
            var blps = new List<string>();

            Console.WriteLine("Parsing all ADTs...");
            ParseAllADTs(ref wmos, ref m2s, adtFolder);

            Console.WriteLine("Parsing all WMOs...");
            ParseAllWMOs(wmos, ref m2s, ref blps, dataFolder);

            Console.WriteLine("Parsing all M2s...");
            ParseAllM2s(m2s, ref blps, dataFolder);

            Console.WriteLine("-------------------");
            Console.WriteLine("Totals parsed:\n " +
                $"{wmos.Count} WMOs\n " +
                $"{m2s.Count} M2s\n " +
                $"{blps.Count} BLPs");

            SaveFileOutput(assetsFilePath, wmos, m2s, blps);
            Console.WriteLine("\nSaved results to: " + assetsFilePath);
            Console.WriteLine("-------------------");

            Console.WriteLine("Done.");
        }

        static void SaveFileOutput(string path, List<string> wmos, List<string> m2s, List<string> blps)
        {
            var output = new List<string>(wmos.Count + m2s.Count + blps.Count);
            output.AddRange(wmos);
            output.AddRange(m2s);
            output.AddRange(blps);
            File.WriteAllLines(path, output);
        }

        static void ParseAllADTs(ref List<string> wmos, ref List<string> m2s, string adtFolder)
        {
            foreach (var filePath in Directory.EnumerateFiles(adtFolder))
            {
                if (!filePath.ToLower().EndsWith(".adt"))
                    continue;

                m2s.AddRange(ExtractM2PathsFromADT(filePath));
                wmos.AddRange(ExtractWMOPathsFromADT(filePath));

                Console.WriteLine($" {filePath}:\n  {m2s.Count} M2 paths\n  {wmos.Count} WMO paths");
            }
        }

        static void ParseAllWMOs(List<string> wmos, ref List<string> m2s, ref List<string> blps, string dataFolder)
        {
            foreach (var wmoPath in wmos)
            {
                string fullWmoPath = $"{dataFolder}\\{wmoPath}";
                if (File.Exists(fullWmoPath))
                {
                    ExtractAssetsFromWMO(ref m2s, ref blps, fullWmoPath);
                }
                else
                {
                    Console.WriteLine(" [ERROR] Unable to find file path: " + fullWmoPath);
                }
            }
        }

        static void ParseAllM2s(List<string> m2s, ref List<string> blps, string dataFolder)
        {
            foreach (var m2Path in m2s)
            {
                string fullM2Path = $"{dataFolder}\\{m2Path}";
                if (File.Exists(fullM2Path))
                {
                    ExtractAssetsFromM2(ref blps, fullM2Path);
                }
                else
                {
                    Console.WriteLine(" [ERROR] Unable to find file path: " + fullM2Path);
                }
            }
        }

        static void ExtractAssetsFromM2(ref List<string> blps, string path)
        {
            // First read number of textures and location
            var offset = 80;
            uint numTextures = 0;
            uint textLookupOffset = 0;
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                fileStream.Position = offset;
                using (var binReader = new BinaryReader(fileStream))
                {
                    numTextures = binReader.ReadUInt32();
                    textLookupOffset = binReader.ReadUInt32();
                }
            }

            // Now read each texture block (we only care about the location and length of each str)
            var fileOffsets = new List<KeyValuePair<uint, uint>>();
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                fileStream.Position = textLookupOffset;
                using (var binReader = new BinaryReader(fileStream))
                {
                    for (int i = 0; i < numTextures; ++i)
                    {
                        /*
                        struct M2Texture
                        {
                            uint type;
                            uint flags;
                            uint fileNameLength;
                            uint fileNameOffset;
                        }
                        */
                        // uint _type;
                        binReader.ReadUInt32();
                        // uint flags;
                        binReader.ReadUInt32();
                        fileOffsets.Add(new KeyValuePair<uint, uint>(
                            // uint fileNameLength;
                            binReader.ReadUInt32(),
                            // uint fileNameOffset;
                            binReader.ReadUInt32()));
                    }
                }
            }

            // Now read the BLP path
            foreach (var pair in fileOffsets)
            {
                using (var fileStream = new FileStream(path, FileMode.Open))
                {
                    fileStream.Position = pair.Value;
                    using (var binReader = new BinaryReader(fileStream))
                    {
                        var blp = Encoding.UTF8.GetString(binReader.ReadBytes((int)pair.Key));
                        blp = blp.Substring(0, blp.Length - 1); // Remove string terminator
                        blps.Add(blp);
                    }
                }
            }
        }

        static void ExtractAssetsFromWMO(ref List<string> m2s, ref List<string> blps, string path)
        {
            m2s.AddRange(ExtractStringListFromChunk(
                path,
                // NDOM = MODN chunk
                new int[] { 'N', 'D', 'O', 'M' },
                new string[] { ".m2", ".mdx" }.ToList()));

            blps.AddRange(ExtractStringListFromChunk(
                path,
                // XTOM = MOTX chunk
                new int[] { 'X', 'T', 'O', 'M' },
                new string[] { ".blp" }.ToList()));
        }

        static List<string> ExtractM2PathsFromADT(string path)
        {
            return ExtractStringListFromChunk(
                path,
                // XDMM = MMDX chunk
                new int[] { 'X', 'D', 'M', 'M' },
                new string[] { ".m2", ".mdx" }.ToList());
        }

        static List<string> ExtractWMOPathsFromADT(string path)
        {
            return ExtractStringListFromChunk(
                path,
                // OMWM = MWMO chunk
                new int[] { 'O', 'M', 'W', 'M' },
                new string[] { ".wmo" }.ToList());
        }

        static List<string> ExtractStringListFromChunk(string path, int[] chunk, List<string> extensionsValidated)
        {
            var list = new List<string>();
            long offset = FindChunkOffset(path, chunk);
            if (offset == -1)
                return list;

            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                fileStream.Position = offset + 4;
                using (var binReader = new BinaryReader(fileStream))
                {
                    var continueReading = true;
                    while (continueReading)
                    {
                        var str = ReadNullTerminatedString(binReader);
                        if (str.Length == 0 || !ValidFileExtension(str, extensionsValidated))
                            continueReading = false;
                        else
                            list.Add(str);
                    }
                }
            }
            return list;
        }

        static bool ValidFileExtension(string path, List<string> allowed)
        {
            var str = path.ToLower();
            return allowed.Any(ext => str.EndsWith(ext));
        }

        static string ExtractTextureFromOffset()
        {
            return "";
        }

        static string ReadNullTerminatedString(BinaryReader stream)
        {
            string str = "";
            try
            {
                char ch;
                while ((ch = stream.ReadChar()) != 0)
                    str = str + ch;
            }
            catch (ArgumentException e)
            {
                //Console.WriteLine($"[WARNING] Failed to parse position: '{stream.BaseStream.Position}', {str}: {e.GetType()}: {e.Message}");
            }
            return str;
        }

        static long FindChunkOffset(string filePath, int[] searchPattern)
        {
            using (var stream = File.OpenRead(filePath))
            {
                int searchPosition = 0;
                while (true)
                {
                    var latestbyte = stream.ReadByte();
                    if (latestbyte == -1)
                        break;

                    if (latestbyte == searchPattern[searchPosition])
                    {
                        searchPosition++;
                        if (searchPosition == searchPattern.Length)
                        {
                            return stream.Position;
                        }
                    }
                    else
                    {
                        searchPosition = 0;
                    }
                }
            }
            return -1;
        }
    }
}
