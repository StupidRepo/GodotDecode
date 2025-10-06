# GodotDecode
<sub>There's probably another project with this name...</sub>

A C# tool which extracts files from Godot Engine resource packs (`.pck` files).

## Features
- Extracts all files from a `.pck` file.
- Optionally converts Godot-specific file formats to more common formats:
  - [x] Textures (`.ctex`, `.stex`) → `.png` or `.webp` (depending on the original format)
    - Proper mipmap support too, if present in the file.
    - **KTX 2.0 support is not implemented yet. Files with this format will be discarded.**
      - _(Also there is some kind of weird bug where KTX 2.0 files will have like 8 bytes at the start for no reason?)_
  - [x] Audio (`.oggstr`, `.sample`) → `.ogg`, `.wav` (respectively)
    - `.wav` support:
      - [x] PCM8
      - [x] PCM16
      - [ ] ADPCM _(support unknown)_
      - [ ] Quite OK Audio _(work in progress)_
  - [ ] Meshes → `.obj`
  - [ ] Binary GDScript → `.gd`

## Usage
1. Download [the .NET SDK](https://dotnet.microsoft.com/en-us/download) from Microsoft.
2. Clone this repository or download the source code as a ZIP file and extract it.
3. Open a terminal in the project directory and run:
   ```bash
   dotnet run <path_to_pck_file> [output_directory] [--convert] [--verify]
   ```
   - `<path_to_pck_file>`: Path to the `.pck` file you want to extract.
   - `[output_directory]`: _(Optional)_ Directory where extracted files will be saved. Defaults to the `.pck` file location.
   - `--convert`: _(Optional)_ Convert Godot-specific formats to common formats.
   - `--verify`: _(Optional)_ Verify file integrity using checksums. **Cannot be used with `--convert`!**

# Credits
- [Godot Engine](https://godotengine.org/) - The amazing open-source game engine.
- [godotdec](https://github.com/Bioruebe/godotdec) - Another C# tool for extracting `.pck` files - some parts of the `godotdec` codebase was used for reference.