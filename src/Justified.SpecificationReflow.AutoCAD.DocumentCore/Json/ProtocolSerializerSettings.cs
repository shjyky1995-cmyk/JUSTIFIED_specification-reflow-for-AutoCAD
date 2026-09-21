using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;

// 协议序列化设置：属性名 camelCase、字典键原样（extensions 命名空间 key 不得被改写）、
// null 省略、枚举默认 camelCase；PaperCode 与 AnchorKind 使用显式名称转换器。
public static class ProtocolSerializerSettings
{
    public static JsonSerializerSettings Default { get; } = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        FloatFormatHandling = FloatFormatHandling.Symbol,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false }
        },
        Converters =
        {
            new PaperCodeConverter(),
            new AnchorKindConverter(),
            new StringEnumConverter(new CamelCaseNamingStrategy())
        }
    };
}
