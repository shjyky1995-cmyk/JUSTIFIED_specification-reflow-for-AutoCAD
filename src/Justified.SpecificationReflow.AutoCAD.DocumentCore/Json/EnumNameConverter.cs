using System;
using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Newtonsoft.Json;

namespace Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;

// PRD 的枚举 JSON 取值并非统一 camelCase（paperCode 为 "A1"，anchor.kind 为 "note_top_right"），
// 这两个枚举使用显式名称表；其余枚举由 camelCase StringEnumConverter 处理。
internal abstract class EnumNameConverter<T> : JsonConverter
    where T : struct
{
    private readonly Dictionary<T, string> _names = new Dictionary<T, string>();
    private readonly Dictionary<string, T> _values = new Dictionary<string, T>(StringComparer.Ordinal);

    protected EnumNameConverter(IReadOnlyList<KeyValuePair<T, string>> names)
    {
        foreach (var pair in names)
        {
            _names.Add(pair.Key, pair.Value);
            _values.Add(pair.Value, pair.Key);
        }
    }

    public override bool CanConvert(Type objectType) => objectType == typeof(T) || objectType == typeof(T?);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(_names[(T)value]);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            if (objectType == typeof(T?)) return null;
            throw new JsonSerializationException("枚举 " + typeof(T).Name + " 不允许为空。");
        }
        var text = reader.Value as string;
        if (text != null && _values.TryGetValue(text, out var parsed)) return parsed;
        throw new JsonSerializationException("枚举 " + typeof(T).Name + " 存在未知取值：" + (text ?? reader.Value?.ToString() ?? "null"));
    }
}

internal sealed class PaperCodeConverter : EnumNameConverter<PaperCode>
{
    public PaperCodeConverter()
        : base(new[]
        {
            new KeyValuePair<PaperCode, string>(PaperCode.A1, "A1"),
            new KeyValuePair<PaperCode, string>(PaperCode.A2, "A2"),
            new KeyValuePair<PaperCode, string>(PaperCode.A3, "A3")
        })
    {
    }
}

internal sealed class AnchorKindConverter : EnumNameConverter<AnchorKind>
{
    public AnchorKindConverter()
        : base(new[]
        {
            new KeyValuePair<AnchorKind, string>(AnchorKind.NoteTopLeft, "note_top_left"),
            new KeyValuePair<AnchorKind, string>(AnchorKind.NoteTopRight, "note_top_right")
        })
    {
    }
}
