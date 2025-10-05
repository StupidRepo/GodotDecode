using System.Security.Cryptography;
using System.Text;

namespace GodotDecode;

public static class Functions
{
    public static List<FileIndex> MakeFileIndex(BinaryReader reader, int pckFormatVersion)
    {
        long fileBaseOffset = 0;
        if (pckFormatVersion >= 2) fileBaseOffset = reader.ReadInt64();
            
        // Skip reserved bytes (16 x Int32)
        reader.BaseStream.Seek(16 * 4, SeekOrigin.Current);
            
        var fileCount = reader.ReadInt32();
        Console.WriteLine($"Found {fileCount} files.");
        
        var fileIndex = new List<FileIndex>();
        for (var i = 0; i < fileCount; i++)
		{
			fileIndex.Add(new FileIndex(
				Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()))
					.TrimEnd('\0'), 
				reader.ReadInt64() + fileBaseOffset, reader.ReadInt64(), 
				reader.ReadBytes(16)
			));
			
			if (pckFormatVersion < 2) continue;
			var encryptedBool = reader.ReadUInt32();
			if ((encryptedBool & 1) != 0)
				throw new Exception("Encrypted files not supported.");
		}
        
        if(fileIndex.Count < 1)
			throw new Exception("No files were found inside the archive.");
        
        fileIndex.Sort((a, b) => (int)(a.Offset - b.Offset));
		return fileIndex;
    }
    public static void ExtractFiles(BinaryReader reader, List<FileIndex> fileIndex, string outputDir, bool convert, bool verify)
	{
		Directory.CreateDirectory(outputDir);

		var indexEnd = reader.BaseStream.Position;
		foreach (var entry in fileIndex)
        {
            if (entry.Offset < indexEnd)
            {
                Console.WriteLine($"Skipping invalid entry '{entry.Path}'. (offset {entry.Offset} < index end {indexEnd})");
                continue;
            }

            if (convert)
            {
	            if (new Converter(reader, entry, outputDir, verify).Convert()) continue;
            }
            
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
            var fileData = reader.ReadBytes((int)entry.Size);
            if (verify && !convert)
            {
	            Console.WriteLine($"Verifying: {entry.Path} (hash code {entry.GetHashCode()})"); // for simplicity’s sake, we use GetHashCode to avoid printing the full hash in most cases
	            
	            var hash = MD5.HashData(fileData);
	            if (!hash.SequenceEqual(entry.Hash))
		            Console.WriteLine($"WARNING: Hash mismatch for '{entry.Path}'. Got MD5 of {Convert.ToHexStringLower(hash)}, expected {Convert.ToHexStringLower(entry.Hash)}!");
            }

            var outputPath = Path.Combine(outputDir, entry.Path.TrimStart('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var outputFile = File.Create(outputPath);
            outputFile.Write(fileData);
        }
    }

    public record FileIndex(string Path, long Offset, long Size, byte[] Hash)
    {
	    public string Path { get; set; } = Path;

	    public long Offset { get; set; } = Offset;
	    public long Size { get; set; } = Size;
	    
	    public void ChangeExtension(string from, string to)
	    {
		    if(Path.EndsWith(from, StringComparison.OrdinalIgnoreCase))
			    Path = Path[..^from.Length] + to;
	    }

	    public void Shrink(long by, int stripEnd = 0)
	    {
		    Offset += by; // move start forward
		    Size -= by + stripEnd; // reduce size by how much we moved start forward, and how much we want to strip from the end
	    }
    }
}