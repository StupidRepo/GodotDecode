namespace GodotDecode;

internal static class Program
{
    private static void Main(string[] args)
    {
        // parse input file and output dir from args
        if (args.Length < 1)
        {
            Console.WriteLine(Consts.Usage);
            return;
        }
        
        var convert = args.Contains("--convert") || args.Contains("-c");
        var verify = args.Contains("--verify") || args.Contains("-v");
        if (convert && verify)
        {
            Console.WriteLine("Cannot use --convert and --verify together due to converted data being different from packed data.");
            return;
        }
        
        args = args.Where(arg => arg != "--convert" && arg != "-c" && arg != "--verify" && arg != "-v").ToArray();
        
        var inputFile = args[0];
        var outputDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetDirectoryName(inputFile)!, Path.GetFileNameWithoutExtension(inputFile));
        
        Console.WriteLine($"Input file: {inputFile}");
        Console.WriteLine($"Output directory: {outputDir}");
        
        if (!File.Exists(inputFile))
        {
            Console.WriteLine($"Input file '{inputFile}' does not exist.");
            return;
        }

        using var reader = new BinaryReader(File.OpenRead(inputFile));
        if (!Utils.CheckMagic(reader.ReadInt32())) {
            reader.BaseStream.Seek(-4, SeekOrigin.End);
            Utils.CheckMagic(reader.ReadInt32());
                
            reader.BaseStream.Seek(-12, SeekOrigin.Current);
                
            var offset = reader.ReadInt64();
            reader.BaseStream.Seek(-offset - 8, SeekOrigin.Current);
            Utils.CheckMagic(reader.ReadInt32());
        }
        
        var pckFormatVersion = Utils.CheckPckFormatVersion(reader);
            
        var fileIndex = Functions.MakeFileIndex(reader, pckFormatVersion);
        Functions.ExtractFiles(reader, fileIndex, outputDir, convert, verify);
    }
}
