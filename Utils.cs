using System.Text;

namespace GodotDecode;

public static class Utils
{
    public static bool CheckMagic(int magic)
    {
        if (magic != Consts.MagicPackage)
            throw new Exception("Not a valid Godot package or resource file.");

        return true;
    }
    
    public static int CheckPckFormatVersion(BinaryReader reader)
    {
        var pckFormatVersion = reader.ReadInt32();
        Console.WriteLine($"Package format version: {pckFormatVersion}");
        Console.WriteLine($"Godot version: {reader.ReadInt32()}.{reader.ReadInt32()}.{reader.ReadInt32()}");
        
        switch (pckFormatVersion)
        {
            case 1:
                return pckFormatVersion;
            case 2 or 3:
                var packFlags = reader.ReadUInt32();
                if ((packFlags & 1) != 0) // PACK_DIR_ENCRYPTED
                    throw new Exception("Encrypted directory not supported.");
                return pckFormatVersion;
            default:
                throw new Exception($"Package format version {pckFormatVersion} is not supported.");
        }
    }
    
    public static string Read32BitPrefixedString(BinaryReader reader)
    {
        return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())).TrimEnd('\0');
    }
}