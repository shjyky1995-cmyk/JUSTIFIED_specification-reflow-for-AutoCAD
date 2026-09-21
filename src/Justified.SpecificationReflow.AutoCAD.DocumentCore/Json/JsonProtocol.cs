using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;

// JSON 协议门面：Schema 校验 + 序列化 + 往返。序列化成功不等于 Schema 验证通过，
// Load 必须先通过 Schema（含 NaN/Infinity 全局拒绝）才物化模型；Save 反向保证不产出违协议 JSON。
public sealed class JsonProtocol
{
    private const string ResourcePrefix = "Justified.SpecificationReflow.AutoCAD.DocumentCore.Schemas.";

    private readonly Dictionary<string, JObject> _schemaRoots;
    private readonly JsonSchemaValidator _documentValidator;
    private readonly JsonSchemaValidator _layoutValidator;
    private readonly JsonSchemaValidator _renderValidator;

    public static JsonProtocol Default { get; } = new JsonProtocol();

    private JsonProtocol()
    {
        _schemaRoots = new Dictionary<string, JObject>(StringComparer.Ordinal)
        {
            ["document.schema.json"] = LoadSchema("document.schema.json"),
            ["layout-result.schema.json"] = LoadSchema("layout-result.schema.json"),
            ["render-request.schema.json"] = LoadSchema("render-request.schema.json")
        };
        _documentValidator = new JsonSchemaValidator(_schemaRoots["document.schema.json"], ResolveSchemaFile);
        _layoutValidator = new JsonSchemaValidator(_schemaRoots["layout-result.schema.json"], ResolveSchemaFile);
        _renderValidator = new JsonSchemaValidator(_schemaRoots["render-request.schema.json"], ResolveSchemaFile);
    }

    public LoadResult<Document> LoadDocument(string json) =>
        Load(json, _documentValidator, token => token.ToObject<Document>(CreateSerializer())!);

    public LoadResult<LayoutResult> LoadLayoutResult(string json) =>
        Load(json, _layoutValidator, token => token.ToObject<LayoutResult>(CreateSerializer())!);

    public LoadResult<RenderRequest> LoadRenderRequest(string json) =>
        Load(json, _renderValidator, token => token.ToObject<RenderRequest>(CreateSerializer())!);

    public string SaveDocument(Document document) => Save(document, _documentValidator);

    public string SaveLayoutResult(LayoutResult result) => Save(result, _layoutValidator);

    public string SaveRenderRequest(RenderRequest request) => Save(request, _renderValidator);

    public IReadOnlyList<Diagnostic> ValidateDocument(string json) => Validate(json, _documentValidator);

    public IReadOnlyList<Diagnostic> ValidateLayoutResult(string json) => Validate(json, _layoutValidator);

    public IReadOnlyList<Diagnostic> ValidateRenderRequest(string json) => Validate(json, _renderValidator);

    public string ComputeDocumentHash(Document document)
    {
        var json = SaveDocument(document);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }
        return builder.ToString();
    }

    private static LoadResult<T> Load<T>(string json, JsonSchemaValidator validator, Func<JToken, T> materialize)
        where T : class
    {
        JToken token;
        try
        {
            token = JToken.Parse(json);
        }
        catch (JsonException error)
        {
            return new LoadResult<T>(null, new[] { ProtocolDiagnostic("JSON 解析失败：" + error.Message, "$") });
        }
        var diagnostics = ValidateToken(token, validator);
        if (diagnostics.Count > 0)
            return new LoadResult<T>(null, diagnostics);
        try
        {
            return new LoadResult<T>(materialize(token), Array.Empty<Diagnostic>());
        }
        catch (JsonException error)
        {
            return new LoadResult<T>(null, new[] { ProtocolDiagnostic("模型物化失败：" + error.Message, token.Path) });
        }
    }

    private static string Save<T>(T value, JsonSchemaValidator validator)
        where T : class
    {
        // 走完整序列化管线（TimeSpan 等需要转换器路径），再解析回 JToken 做禁止项与 Schema 校验。
        var token = JToken.Parse(JsonConvert.SerializeObject(value, ProtocolSerializerSettings.Default));
        foreach (var violation in JsonSchemaValidator.FindNonFiniteNumbers(token))
        {
            throw new InvalidOperationException("序列化禁止 NaN/Infinity：" + violation);
        }
        var schemaViolations = validator.Validate(token);
        if (schemaViolations.Count > 0)
        {
            throw new InvalidOperationException("模型不符合协议 Schema：" + string.Join("；", schemaViolations));
        }
        return token.ToString(Formatting.None);
    }

    private static IReadOnlyList<Diagnostic> Validate(string json, JsonSchemaValidator validator)
    {
        try
        {
            return ValidateToken(JToken.Parse(json), validator);
        }
        catch (JsonException error)
        {
            return new[] { ProtocolDiagnostic("JSON 解析失败：" + error.Message, "$") };
        }
    }

    private static IReadOnlyList<Diagnostic> ValidateToken(JToken token, JsonSchemaValidator validator)
    {
        var violations = new List<SchemaViolation>();
        violations.AddRange(JsonSchemaValidator.FindNonFiniteNumbers(token));
        violations.AddRange(validator.Validate(token));
        if (violations.Count == 0) return Array.Empty<Diagnostic>();
        var diagnostics = new List<Diagnostic>(violations.Count);
        foreach (var violation in violations)
        {
            diagnostics.Add(ProtocolDiagnostic(violation.Message, violation.Path, MapCode(violation.Path)));
        }
        return diagnostics;
    }

    // 未知主版本与未知必需块类型按 PRD 第 10 节映射为 E_SCHEMA_VERSION；其余协议违例为 E_SCHEMA_INVALID。
    private static string MapCode(string path)
    {
        if (path == "$.schemaVersion") return DiagnosticCodes.ESchemaVersion;
        if (path.StartsWith("$.blocks[", StringComparison.Ordinal) && path.EndsWith("].type", StringComparison.Ordinal))
            return DiagnosticCodes.ESchemaVersion;
        return DiagnosticCodes.ESchemaInvalid;
    }

    private static Diagnostic ProtocolDiagnostic(string message, string path, string? code = null) =>
        new Diagnostic
        {
            Code = code ?? DiagnosticCodes.ESchemaInvalid,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Protocol,
            Message = message,
            Details = new Dictionary<string, string> { ["jsonPath"] = path }
        };

    private static JsonSerializer CreateSerializer() => JsonSerializer.Create(ProtocolSerializerSettings.Default);

    private JObject ResolveSchemaFile(string file) =>
        _schemaRoots.TryGetValue(file, out var root)
            ? root
            : throw new InvalidOperationException("Schema 引用了未注册的文件：" + file);

    private static JObject LoadSchema(string fileName)
    {
        var assembly = typeof(JsonProtocol).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException("缺少嵌入的 Schema 资源：" + fileName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JObject.Parse(reader.ReadToEnd());
    }
}
