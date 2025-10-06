namespace GodotDecode;

public class Converter(BinaryReader reader, Functions.FileIndex entry, string outputDir, bool verify)
{
    private SourceFormat DetermineSourceFormat()
    {
        var ext = Path.GetExtension(entry.Path).ToLowerInvariant();
        return ext switch
        {
            ".ctex" => // Compressed TEXture (Godot 4.x)
                SourceFormat.CompressedTexture,
            ".stex" => // Stream TEXture (Godot 3.x)
                SourceFormat.StreamTexture,
            ".oggstr" => // OGG STReam
                SourceFormat.Ogg,
            ".sample" => // wav sample
                SourceFormat.Wav,
            _ => SourceFormat.Unknown
        };
    }

    /**
     * Returns true if this function dealt with the file saving and such. If so, anything calling this function should not further process file data. False, if the file should be saved as-is.
     */
    public bool Convert()
    {
        var sourceFormat = DetermineSourceFormat();
        reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

        switch (sourceFormat)
        {
            case SourceFormat.CompressedTexture:
            {
                // for this bit, we start from https://github.com/godotengine/godot/blob/4.2/scene/resources/compressed_texture.cpp#L35
                // skip:
                // - header (4 bytes)
                // - version (4 bytes)
                // - width (4 bytes)
                // - height (4 bytes)
                // - data format (4 bytes)
                // - mipmap limit (4 bytes)
                // - reserved (12 bytes)
                reader.BaseStream.Seek(36, SeekOrigin.Current);
                
                // now start from https://github.com/godotengine/godot/blob/4.2/scene/resources/compressed_texture.cpp#L299
                var format = (TextureFormat) reader.ReadInt32();
                
                // skip:
                // - width (2 bytes)
                // - height (2 bytes)
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                
                var mipmaps = Math.Max(1, reader.ReadInt32());
                reader.BaseStream.Seek(4, SeekOrigin.Current); // skip some kind of format thing
                
                if(format is TextureFormat.Png or TextureFormat.WebP)
                {
                    Console.WriteLine($"Found {mipmaps} mipmaps in '{entry.Path}'.");
                    
                    for (var i = 0; i < mipmaps; i++)
                    {
                        var mipmapSize = reader.ReadInt32();
                        var mipmapData = reader.ReadBytes(mipmapSize);
                        Console.WriteLine($" Mipmap {i+1}: {mipmapSize} bytes");
                        
                        using var mipmapFile = File.Create(Path.Combine(outputDir,
                            $"{entry.Path.TrimStart('/')}._mipmap_{i+1}{(format == TextureFormat.Png ? ".png" : ".webp")}"));
                        mipmapFile.Write(mipmapData, 0, mipmapSize);
                    }
                }

                switch (format)
                {
                    case TextureFormat.Png:
                    case TextureFormat.WebP:
                        return true;
                    case TextureFormat.BasisUniversal: // seems to be ktx 2.0, so we will convert to png
                    {
                        // TODO: ktx 2.0 basis universal to png conversion
                        return true;
                    }

                    case TextureFormat.Image:
                    default:
                        Console.WriteLine($"I can't convert the CompressedTexture '{entry.Path}' because it uses a format that is not supported by this tool ('{format}').");
                        return true;
                }
            }
            case SourceFormat.StreamTexture:
            {
                // skip:
                // - header (4 bytes)
                // - texture width (2 bytes)
                // - texture width custom (2 bytes)
                // - texture height (2 bytes)
                // - texture height custom (2 bytes)
                reader.BaseStream.Seek(12, SeekOrigin.Current);
                var format = reader.ReadInt32();
                if ((format & Consts.TextureV1FormatBitPng) != 0)
                {
                    entry.ChangeExtension(".stex", ".png");
                }
                else if ((format & Consts.TextureV1FormatBitWebp) != 0)
                {
                    entry.ChangeExtension(".stex", ".webp");
                }
                else
                {
                    Console.WriteLine($"Encountered unknown StreamTexture format for '{entry.Path}'.");
                    // skip:
                    // - data format (4 bytes)
                    // - mipmaps (4 bytes)
                    // - size (4 bytes)
                    reader.BaseStream.Seek(12, SeekOrigin.Current);
                    
                    var magic = reader.ReadInt32();
                    entry.ChangeExtension(".stex", magic == Consts.MagicWebp ? ".webp" : ".png");
                }

                entry.Shrink(32); // 12 skipped bytes + format int + 12 skipped bytes + magic int
                break;
            }
            case SourceFormat.Ogg:
            {
                entry.Shrink(279, 4); // TODO: figure out what any of this means
                entry.ChangeExtension(".oggstr", ".ogg");
                break;
            }
            case SourceFormat.Wav:
            {
                var properties = ParseResource(reader, entry);
                if (properties == null) return false;
                
                if (properties.Name != "AudioStreamWAV" && properties.Name != "AudioStreamSample") {
                    throw new Exception("Resource is not of type AudioStreamWAV or AudioStreamSample. Conversion is not possible.");
                }
                
                var props = properties.Properties;
                if (!props.TryGetValue("data", out dynamic? rawData)) {
                    throw new Exception("Failed to get audio data, conversion not possible");
                }
                props.TryGetValue("stereo", out dynamic? stereo);
                
                var data = (MemoryStream) rawData;
                var dataLength = (int) data.Length;
                
                var formatCode = (WavFormat) props["format"];
                if(formatCode is WavFormat.QuiteOkAudio or WavFormat.IMA_AdPCM)
                    Console.WriteLine($"WARNING: '{entry.Path}' uses a WAV format ({formatCode}) that is not supported by this tool. The output file may not be playable.");
                
                var channels = stereo ?? false ? 2 : 1;
                var sampleRate = props.TryGetValue("mix_rate", out var sampleRateRaw) ? (int) sampleRateRaw : 44100;
                var bytesPerSample = formatCode switch
                {
                    WavFormat.EightBits => 1,
                    WavFormat.SixteenBits => 2,
                    WavFormat.IMA_AdPCM => 4,
                    WavFormat.QuiteOkAudio => 2, // QOA uses int16 samples
                    _ => throw new Exception($"Unknown WAV format: {formatCode}")
                };
                
                entry.ChangeExtension(".sample", ".wav");
                var memStream = new MemoryStream(44); // WAV header is 44 bytes
                var writer = new BinaryWriter(memStream);
                
                writer.Write("RIFF"u8.ToArray());
                writer.Write(36 + dataLength); // file size + 36 bytes for the rest of the header
                writer.Write("WAVE"u8.ToArray());
                writer.Write("fmt "u8.ToArray());
                writer.Write(16); // size of the fmt chunk
                writer.Write((short) formatCode);
                writer.Write((short) channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bytesPerSample); // byte rate
                writer.Write((short) (channels * bytesPerSample)); // block align
                writer.Write((short) (bytesPerSample * 8)); // bits per sample
                writer.Write("data"u8.ToArray());
                writer.Write(dataLength);
                
                memStream.Write(data.ToArray(), 0, dataLength); // write the actual audio data
                memStream.Seek(0, SeekOrigin.Begin); // rewind to the start so it can be saved
                
                var outputPath = Path.Combine(outputDir, entry.Path.TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using var outputFile = File.Create(outputPath);
                memStream.CopyTo(outputFile);

                return true;
            }
        }

        return false;
    }

    private enum SourceFormat
    {
        Unknown,
        
        CompressedTexture, // https://github.com/godotengine/godot/blob/4.2/scene/resources/compressed_texture.cpp#L299
        StreamTexture, // https://github.com/godotengine/godot/blob/3.5/scene/resources/texture.cpp#L464
        
        Ogg, // https://github.com/godotengine/godot/blob/3.0/core/io/resource_format_binary.cpp#L836
        Wav, // https://github.com/godotengine/godot/blob/3.5/scene/resources/audio_stream_sample.cpp#L552
    }
    
    // https://github.com/godotengine/godot/blob/4.2/scene/resources/compressed_texture.h#L42
    private enum TextureFormat {
        Image,
        Png,
        WebP,
        BasisUniversal, // this seems to be KTX 2.0 which i, luckily, have experience with...
                        // okay i kid you not i swear i remember doing stuff with ktx 2.0
    };
    
    // https://github.com/godotengine/godot/blob/master/scene/resources/audio_stream_wav.h#L100
    private enum WavFormat {
        EightBits,
        SixteenBits,
        IMA_AdPCM,
        QuiteOkAudio, // no, it is quite not okay. >:(
    };
    
    #region Resources
    // https://github.com/godotengine/godot/blob/master/core/io/resource_format_binary.cpp#L982
    private SerializedObject? ParseResource(BinaryReader reader, Functions.FileIndex entry)
    {
        var magic = reader.ReadInt32();
        if (magic == Consts.MagicRscc)
            throw new Exception($"Compressed resources aren't not supported. Cannot convert '{entry.Path}'.");
        if (magic != Consts.MagicRsrc)
            throw new Exception($"Invalid resource magic for '{entry.Path}'.");

        var bigEndian = reader.ReadInt32();
        if (bigEndian > 0) Console.WriteLine("Big endian resources are currently not supported. Extracted file might not be readable.");
        
        reader.BaseStream.Seek(4, SeekOrigin.Current); // skip use_real64
        var versionMajor = reader.ReadInt32();
        var versionMinor = reader.ReadInt32();
        var resourceVersion = reader.ReadInt32();

        var resourceType = Utils.Read32BitPrefixedString(reader);
        Console.WriteLine($"Resource type: {resourceType}, engine v{versionMajor}.{versionMinor} (resource v{resourceVersion})");
        if (resourceVersion != 3) // we only support 3 (Godot 3.6)
        {
            Console.WriteLine($"Resource version {resourceVersion} is not supported.");
            return null;
        }

        // skip:
        // - importmd_ofs (8 bytes)
        // - reserved (14 x Int32)
        reader.BaseStream.Seek(8 + 14 * 4, SeekOrigin.Current);
        
        var stringTableSize = reader.ReadInt32();
        var stringTable = new string[stringTableSize];
        for (var i = 0; i < stringTableSize; i++) {
            stringTable[i] = Utils.Read32BitPrefixedString(reader);
        }
        
        var externalResourceCount = reader.ReadInt32();
        for (var i = 0; i < externalResourceCount; i++) {
            Utils.Read32BitPrefixedString(reader); // type
            Utils.Read32BitPrefixedString(reader); // path
        }
        
        var internalResourceCount = reader.ReadInt32();
        var internalResourceOffsets = new long[internalResourceCount];
        for (var i = 0; i < internalResourceCount; i++) {
            Utils.Read32BitPrefixedString(reader); // path
            internalResourceOffsets[i] = reader.ReadInt64();
            // Console.WriteLine($"Internal resource '{path}' at {internalResourceOffsets[i]}");
        }
        
        if (internalResourceOffsets.Length < 1) {
            throw new Exception("No internal resources found in RSRC file. Conversion is not possible.");
        }
        
        reader.BaseStream.Seek(entry.Offset + internalResourceOffsets[0], SeekOrigin.Begin);
        var name = Utils.Read32BitPrefixedString(reader);
        var propertyCount = reader.ReadInt32();
        
        var properties = new Dictionary<string, dynamic>();
        for (var i = 0; i < propertyCount; i++)
        {
            var nameIndex = reader.ReadInt32();
            var value = ParseVariant(reader);
            
            properties.Add(stringTable[nameIndex], value);
        }
        
        return new SerializedObject(name, properties);
    }
    private dynamic? ParseVariant(BinaryReader reader)
    {
        var type = (Variant) reader.ReadInt32();
        dynamic? value = null;

        switch (type)
        {
            case Variant.Nil: break;
            
            case Variant.Bool: value = reader.ReadInt32() != 0; break;
            
            case Variant.Int: value = reader.ReadInt32(); break;
            case Variant.Int64: value = reader.ReadInt64(); break;
            case Variant.Float: value = reader.ReadSingle(); break;
            
            case Variant.Array:
            {
                var size = Math.Abs(reader.ReadInt32());
                value = new dynamic[size];
                for (var i = 0; i < size; i++)
                    value[i] = ParseVariant(reader);
                break;
            }
            case Variant.RawArray:
            {
                var size = reader.ReadInt32();
                value = reader.ReadBytes(size);
                var padding = 4 - (size % 4);
                if (padding < 4) reader.BaseStream.Seek(padding, SeekOrigin.Current);
                break;
            }
            
            case Variant.PackedInt64Array:
            {
                var size = reader.ReadInt32();
                value = reader.ReadBytes(size * 8);
                break;
            }
            
            default:
                throw new Exception($"Variant type '{type}' is not supported.");
        }
        
        return value;
    }
    
    private record SerializedObject(string Name, Dictionary<string, dynamic> Properties);
    
    // https://github.com/godotengine/godot/blob/master/core/io/resource_format_binary.cpp#L45
    private enum Variant {
        Nil = 1,
        Bool = 2,
        Int = 3,
        Float = 4,
        // ...
        Array = 30,
        RawArray = 31,
        // ...
        Int64 = 40,
        PackedInt64Array = 48
    }
    #endregion
}