using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;

namespace WoWResourceParser
{
    class Program
    {
        private const string LogPath = "log.txt";

        static void Log(string message)
        {
            Console.WriteLine(message);
            using (var file = File.AppendText(LogPath))
            {
                file.WriteLine(message);
            }
        }

        public class Options
        {
            [Option('e', "extract", Required = false, HelpText = "Set whether to extract the assets used to assets.txt.")]
            public bool Extract { get; set; }

            [Option('p', "package", Required = false, HelpText = "Set whether to package the assets.txt to the destination folder.")]
            public bool Package { get; set; }

            [Option("assetsFilePath", Required = false, HelpText = "Override the assets.txt file path")]
            public string AssetsFilePath { get; set; }

            [Option("adtFolder", Required = true, HelpText = "Sets the ADT (map) folder to read from")]
            public string AdtPath { get; set; }

            [Option("dataFolder", Required = true, HelpText = "Sets the data folder to read assets from")]
            public string DataPath { get; set; }

            [Option("destFolder", Required = true, HelpText = "Sets the destination folder for packaging to")]
            public string DestPath { get; set; }

            [Option("ignoreDataFolder", Required = false, HelpText = "When packaging any files that are contained in this data folder will not be copied over to the destination directory.")]
            public string IgnoreDataPath { get; set; }
        }

        static void Main(string[] args)
        {
            bool extract = false;
            bool package = false;

            string adtFolder = "E:\\_NewProjectWoW\\_HOCKA\\mpqs\\world\\maps\\DungeonMode";
            string dataFolder = "E:\\_NewProjectWoW\\_HOCKA\\mpqs";
            string assetsFilePath = "assets.txt";
            string ignoreDataFolder = "";

            string destFolder = "D:\\WoW 3.3.5a\\Data\\patch-5.MPQ";

            File.Delete(LogPath);

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (o.Extract)
                    {
                        extract = true;
                        Log("--Extract = true");
                    }
                    if (o.Package)
                    {
                        package = true;
                        Log("--Package = true");
                    }
                    if (o.AssetsFilePath?.Length > 0)
                    {
                        assetsFilePath = o.AssetsFilePath;
                        Log("--AssetsPath = " + assetsFilePath);
                    }
                    if (o.AdtPath?.Length > 0)
                    {
                        adtFolder = o.AdtPath;
                        Log("--AdtPath = " + adtFolder);
                    }
                    if (o.DataPath?.Length > 0)
                    {
                        dataFolder = o.DataPath;
                        Log("--DataPath = " + dataFolder);
                    }
                    if (o.DestPath?.Length > 0)
                    {
                        destFolder = o.DestPath;
                        Log("--DestPath = " + destFolder);
                    }
                    if (o.IgnoreDataPath?.Length > 0)
                    {
                        ignoreDataFolder = o.IgnoreDataPath;
                        Log("--IgnoreDataPath = " + ignoreDataFolder);
                    }
                });

            if (extract)
            {
                ExtractAssets(adtFolder, dataFolder, assetsFilePath);
            }

            if (package)
            {
                FetchAssets(dataFolder, destFolder, assetsFilePath, ignoreDataFolder);
            }

            Log("Done.");
        }

        static void FetchAssets(string dataFolder, string destFolder, string assetsFilePath, string ignoreDataFolder)
        {
            Log("Packaging...");
            Log("-------------------");
            foreach (var line in File.ReadAllLines(assetsFilePath))
            {
                var friendlyLine = line.Replace(".MDX", ".M2").Replace('/', '\\'); ;
                var fullPath = dataFolder + "\\" + friendlyLine;
                if (ignoreDataFolder.Length > 0)
                {
                    var ignorePath = ignoreDataFolder + "\\" + friendlyLine;
                    if (File.Exists(ignorePath))
                    {
                        Console.WriteLine($" [INFO] Skipping file in ignore folder: {ignorePath}");
                        continue;
                    }
                }
                if (File.Exists(fullPath))
                {
                    var destFullPath = destFolder + "\\" + friendlyLine;
                    var containingDir = destFullPath.Substring(0, destFullPath.LastIndexOf('\\'));
                    Directory.CreateDirectory(containingDir);
                    if (File.Exists(destFullPath))
                    {
                        File.Delete(destFullPath);
                    }
                    File.Copy(fullPath, destFullPath);
                }
                else
                {
                    Log($" [ERROR] Unable to find path: {fullPath}");
                }
            }
            Log("-------------------");
        }

        static void ExtractAssets(string adtFolder, string dataFolder, string assetsFilePath)
        {
            Log("-------------------");
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
            Log(banner);
            Log("-------------------");

            var wmos = new List<string>();
            var m2s = new List<string>();
            var blps = new List<string>();

            Log("Parsing all ADTs...");
            ParseAllADTs(ref wmos, ref m2s, ref blps, adtFolder);

            Log("Parsing all WMOs...");
            ParseAllWMOs(wmos, ref m2s, ref blps, dataFolder);

            Log("Parsing all M2s...");
            ParseAllM2s(m2s, ref blps, dataFolder);

            Log("-------------------");
            Log("Totals parsed:\n " +
                $"{wmos.Count} WMOs\n " +
                $"{m2s.Count} M2s\n " +
                $"{blps.Count} BLPs");

            SaveFileOutput(assetsFilePath, wmos, m2s, blps);
            Log("\nSaved results to: " + assetsFilePath);
            Log("-------------------");
        }

        static void SaveFileOutput(string path, List<string> wmos, List<string> m2s, List<string> blps)
        {
            var output = new List<string>(wmos.Count + m2s.Count + blps.Count);
            output.AddRange(wmos);
            output.AddRange(m2s);
            output.AddRange(blps);
            File.WriteAllLines(path, output);
        }

        static void ParseAllADTs(ref List<string> wmos, ref List<string> m2s, ref List<string> blps, string adtFolder)
        {
            foreach (var filePath in Directory.EnumerateFiles(adtFolder))
            {
                if (!filePath.ToLower().EndsWith(".adt"))
                    continue;

                var newM2s = ExtractM2PathsFromADT(filePath);
                m2s.AddRange(newM2s);
                var newWMOs = ExtractWMOPathsFromADT(filePath);
                wmos.AddRange(newWMOs);
                blps.AddRange(ExtractBLPPathsFromADT(filePath));

                Log($" {filePath}:\n  {newM2s.Count} M2 paths\n  {newWMOs.Count} WMO paths");
            }
        }

        static void ParseAllWMOs(List<string> wmos, ref List<string> m2s, ref List<string> blps, string dataFolder)
        {
            var subWmos = new List<string>();
            foreach (var wmoPath in wmos)
            {
                string fullWmoPath = $"{dataFolder}\\{wmoPath}";
                if (File.Exists(fullWmoPath))
                {
                    ExtractAssetsFromWMO(ref m2s, ref blps, fullWmoPath);
                    var pathPartA = wmoPath.Substring(wmoPath.LastIndexOf('\\') + 1);
                    var file = pathPartA.Substring(0, pathPartA.LastIndexOf('.')).ToUpper();
                    var folder = fullWmoPath.Substring(0, fullWmoPath.LastIndexOf('\\'));
                    var parentShortFolder = folder.Substring(dataFolder.Length + 1);
                    foreach (var filePath in Directory.EnumerateFiles(folder))
                    {
                        var subFilePath = filePath.Substring(filePath.LastIndexOf('\\') + 1);
                        if (subFilePath.ToUpper().Contains(file))
                        {
                            subWmos.Add(parentShortFolder + "\\" + subFilePath);
                            // Check for x_000.wmo - a sub wmo with no useful data for us
                            if (!Regex.IsMatch(subFilePath, @"_\d{3}\.\wmo"))
                            {
                                ExtractAssetsFromWMO(ref m2s, ref blps, filePath);
                            }
                        }
                    }
                }
                else
                {
                    Log(" [ERROR] Unable to find file path: " + fullWmoPath);
                }
            }
            wmos.AddRange(subWmos);
        }

        static void ParseAllM2s(List<string> m2s, ref List<string> blps, string dataFolder)
        {
            foreach (var m2Path in m2s)
            {
                string fullM2Path = $"{dataFolder}\\{m2Path.Replace(".MDX", ".M2")}";
                if (File.Exists(fullM2Path))
                {
                    ExtractAssetsFromM2(ref blps, fullM2Path);
                    var pathPartA = m2Path.Substring(m2Path.LastIndexOf('\\') + 1);
                    var file = pathPartA.Substring(0, pathPartA.LastIndexOf('.')).ToUpper();
                    var folder = fullM2Path.Substring(0, fullM2Path.LastIndexOf('\\'));
                    var parentShortFolder = folder.Substring(dataFolder.Length + 1);
                    foreach (var filePath in Directory.EnumerateFiles(folder))
                    {
                        // Don't have anything to store .skins, hack by inserting into blps which we don't do any processing on
                        var subFilePath = filePath.Substring(filePath.LastIndexOf('\\') + 1).ToUpper();
                        if (subFilePath.Contains(file) && 
                            (subFilePath.EndsWith(".SKIN") || subFilePath.EndsWith(".BLP")))
                        {
                            blps.Add(parentShortFolder + "\\" + subFilePath);
                        }
                    }

                }
                else
                {
                    Log(" [ERROR] Unable to find file path: " + fullM2Path);
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
                        // Factor in string terminator
                        if (blp.Length > 1)
                        {
                            // Remove string terminator
                            if (blp[blp.Length - 1] == '\0')
                            {
                                blp = blp.Substring(0, blp.Length - 1);
                            }
                            blps.Add(blp);
                        }
                    }
                }
            }
        }

        static void ExtractAssetsFromWMO(ref List<string> m2s, ref List<string> blps, string path)
        {
            var newM2s = ExtractStringListFromChunk(
                path,
                // NDOM = MODN chunk
                new int[] { 'N', 'D', 'O', 'M' },
                new string[] { ".m2", ".mdx" }.ToList());
            m2s.AddRange(newM2s);

            // Read The MOMT chunk for offsets into MOTX chunk
            var motxOffsets = GetWMOMOTXOffsetsFromMOMT(path);
            blps.AddRange(GetWMOBLPFromMOTX(path, motxOffsets));
        }

        static List<string> GetWMOBLPFromMOTX(string path, List<uint> motxOffsets)
        {
            var blps = new List<string>();
            // XTOM = MOTX chunk
            long offset = FindChunkOffset(path, new int[] { 'X', 'T', 'O', 'M' }) + 4;
            if (offset > 0)
            {
                using (var fileStream = new FileStream(path, FileMode.Open))
                {
                    using (var binReader = new BinaryReader(fileStream))
                    {
                        foreach (var motxOffset in motxOffsets)
                        {
                            fileStream.Position = offset + motxOffset;
                            // Some offsets are near uint max, not sure why
                            if (fileStream.Position < fileStream.Length)
                            {
                                var blp = ReadNullTerminatedString(binReader);
                                // Sometimes reads rubbish, this algorithm isn't quite right
                                if (blp.ToUpper().EndsWith(".BLP"))
                                {
                                    blps.Add(blp);
                                }
                                else
                                {
                                    Log(" [ERROR]: Invalid MOTX BLP: " + blp);
                                }
                            }
                        }
                    }
                }
            }
            return blps;
        }

        static List<uint> GetWMOMOTXOffsetsFromMOMT(string path)
        {
            var motxOffsets = new List<uint>();
            long offset = FindChunkOffset(path, new int[] { 'T', 'M', 'O', 'M' }) + 4;
            if (offset > 0)
            {
                var numTextures = GetNumTexturesFromWMO(path);
                using (var fileStream = new FileStream(path, FileMode.Open))
                {
                    fileStream.Position = offset;
                    using (var binReader = new BinaryReader(fileStream))
                    {
                        for (var i = 0; i < numTextures; ++i)
                        {
                            if ((fileStream.Position + 64) > fileStream.Length)
                                break;
                            fileStream.Position = fileStream.Position + 12;
                            var tex1 = binReader.ReadUInt32();
                            if (tex1 > 0)
                                motxOffsets.Add(tex1);
                            fileStream.Position = fileStream.Position + 8;
                            var tex2 = binReader.ReadUInt32();
                            if (tex2 > 0)
                                motxOffsets.Add(tex2);
                            fileStream.Position = fileStream.Position + 36;
                        }
                    }
                }
            }
            return motxOffsets;
        }

        static uint GetNumTexturesFromWMO(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                fileStream.Position = 20;
                using (var binReader = new BinaryReader(fileStream))
                {
                    return binReader.ReadUInt32();
                }
            }
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

        static List<string> ExtractBLPPathsFromADT(string path)
        {
            return ExtractStringListFromChunk(
                path,
                // XETM = MTEX chunk
                new int[] { 'X', 'E', 'T', 'M' },
                new string[] { ".blp" }.ToList());
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
                        if (fileStream.Position >= fileStream.Length || 
                            (str.Length > 0 && !ValidFileExtension(str, extensionsValidated)))
                        {
                            continueReading = false;
                        }
                        else if (str.Length > 0)
                        {
                            list.Add(str);
                        }
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
                //Log($"[WARNING] Failed to parse position: '{stream.BaseStream.Position}', {str}: {e.GetType()}: {e.Message}");
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
