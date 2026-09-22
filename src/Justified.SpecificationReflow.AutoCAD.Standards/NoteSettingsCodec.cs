using System;
using System.Collections.Generic;
using System.Text;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Newtonsoft.Json;

namespace Justified.SpecificationReflow.AutoCAD.Standards;

// 工程设置的编码。图内字典用 Xrecord 存储，单个字符串上限远小于一份 JSON，
// 所以先 Base64 再分块；解码丢失任何一块都算损坏，不留半份设置。
public static class NoteSettingsCodec
{
    public const int ChunkSize = 200;

    public static List<string> Encode(NoteSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        var json = JsonConvert.SerializeObject(settings, StandardJson.Settings);
        var text = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var chunks = new List<string>();
        for (var offset = 0; offset < text.Length; offset += ChunkSize)
            chunks.Add(text.Substring(offset, Math.Min(ChunkSize, text.Length - offset)));
        return chunks;
    }

    public static NoteSettings? Decode(IReadOnlyList<string>? chunks)
    {
        if (chunks == null || chunks.Count == 0) return null;
        var builder = new StringBuilder();
        foreach (var chunk in chunks) builder.Append(chunk ?? string.Empty);
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(builder.ToString()));
            var settings = JsonConvert.DeserializeObject<NoteSettings>(json, StandardJson.Settings);
            if (settings == null) return null;
            if (settings.Problems().Count > 0) return null;
            return settings;
        }
        catch (Exception error) when (error is FormatException || error is JsonException || error is ArgumentException)
        {
            return null;
        }
    }
}
