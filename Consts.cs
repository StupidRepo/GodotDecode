namespace GodotDecode;

public static class Consts
{
    public const int MagicPackage = 0x43504447;
    public const int MagicRsrc = 0x43525352;
    public const int MagicRscc = 0x43435352; // compressed resource
    public const int MagicWebp = 0x50424557;
    
    public const int TextureV1FormatBitPng = 1 << 20;
    public const int TextureV1FormatBitWebp = 1 << 21;

    public const string Usage =
        "A Godot Engine package unpacker.\n" +
        "Usage: GodotDecode <input_file> [output_dir]\n" +
        "Options:\n" +
        "--convert (-c)\tConvert common formats.\n" +
        "--verify (-v)\tVerify extracted files against their MD5 hashes.";
}