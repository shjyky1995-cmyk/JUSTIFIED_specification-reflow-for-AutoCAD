using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;

public readonly struct SchemaViolation
{
    public SchemaViolation(string path, string message)
    {
        Path = path;
        Message = message;
    }

    public string Path { get; }

    public string Message { get; }

    public override string ToString() => Path + ": " + Message;
}

// draft-07 子集校验器：type/enum/const/properties/required/additionalProperties/items/
// minimum/maximum/exclusiveMinimum/exclusiveMaximum/minLength/maxLength/pattern/minItems/
// if/then/else/not/$ref（本地与跨文件，$ref 基准文档随引用切换）。未知关键字忽略，保证前向兼容。
public sealed class JsonSchemaValidator
{
    private readonly JObject _root;
    private readonly Func<string, JObject> _refResolver;

    public JsonSchemaValidator(JObject schema, Func<string, JObject>? refResolver = null)
    {
        _root = schema ?? throw new ArgumentNullException(nameof(schema));
        _refResolver = refResolver ?? (_ => throw new InvalidOperationException("Schema 引用了外部文件，但未提供解析器。"));
    }

    public IReadOnlyList<SchemaViolation> Validate(JToken instance)
    {
        var errors = new List<SchemaViolation>();
        ValidateAgainst(instance, _root, _root, "$", errors);
        return errors;
    }

    // JSON 标准不允许 NaN/Infinity；Newtonsoft 会按非标准扩展解析它们，这里全局拒绝。
    public static IReadOnlyList<SchemaViolation> FindNonFiniteNumbers(JToken token)
    {
        var violations = new List<SchemaViolation>();
        CollectNonFinite(token, violations);
        return violations;
    }

    private static void CollectNonFinite(JToken token, List<SchemaViolation> violations)
    {
        if (token.Type == JTokenType.Float)
        {
            var value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                var path = token.Path.Length == 0 ? "$" : "$." + token.Path;
                violations.Add(new SchemaViolation(path, "非法数值 NaN/Infinity：协议禁止非有限数。"));
            }
        }
        foreach (var child in token.Children())
            CollectNonFinite(child, violations);
    }

    private void ValidateAgainst(JToken token, JToken schemaToken, JObject baseRoot, string path, List<SchemaViolation> errors)
    {
        if (schemaToken is not JObject schema) return;

        if (schema["$ref"] is JValue reference && reference.Value<string>() is { Length: > 0 } target)
        {
            var resolved = ResolveRef(target, baseRoot);
            ValidateAgainst(token, resolved.Schema, resolved.Base, path, errors);
            return;
        }

        if (schema["type"] is JToken typeToken && !MatchesType(token, typeToken, out var typeError))
        {
            errors.Add(new SchemaViolation(path, typeError));
            return;
        }

        if (schema["enum"] is JArray allowed)
        {
            var matched = false;
            foreach (var candidate in allowed)
            {
                if (JToken.DeepEquals(token, candidate))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                errors.Add(new SchemaViolation(path, "值不在允许的枚举集合内。"));
                return;
            }
        }

        if (schema["const"] is JToken constant && !JToken.DeepEquals(token, constant))
        {
            errors.Add(new SchemaViolation(path, "值不等于约束的常量。"));
            return;
        }

        switch (token.Type)
        {
            case JTokenType.Integer:
            case JTokenType.Float:
                ValidateNumber(token, schema, path, errors);
                break;
            case JTokenType.String:
                ValidateString(token, schema, path, errors);
                break;
            case JTokenType.Array:
                ValidateArray(token, schema, baseRoot, path, errors);
                break;
            case JTokenType.Object:
                ValidateObject(token, schema, baseRoot, path, errors);
                break;
        }

        if (schema["not"] is JObject notSchema)
        {
            var branch = new List<SchemaViolation>();
            ValidateAgainst(token, notSchema, baseRoot, path, branch);
            if (branch.Count == 0)
                errors.Add(new SchemaViolation(path, "值命中 not 禁止的模式。"));
        }

        if (schema["if"] is JObject ifSchema)
        {
            var condition = new List<SchemaViolation>();
            ValidateAgainst(token, ifSchema, baseRoot, path, condition);
            var branch = condition.Count == 0 ? schema["then"] : schema["else"];
            if (branch != null)
                ValidateAgainst(token, branch, baseRoot, path, errors);
        }
    }

    private static bool MatchesType(JToken token, JToken typeToken, out string error)
    {
        var names = new List<string>();
        if (typeToken.Type == JTokenType.Array)
        {
            foreach (var entry in typeToken)
                names.Add(entry.Value<string>()!);
        }
        else
        {
            names.Add(typeToken.Value<string>()!);
        }

        foreach (var name in names)
        {
            if (MatchesSingleType(token, name))
            {
                error = string.Empty;
                return true;
            }
        }

        error = "类型不匹配：期望 " + string.Join(" 或 ", names) + "，实际 " + Describe(token) + "。";
        return false;
    }

    private static bool MatchesSingleType(JToken token, string name)
    {
        switch (name)
        {
            case "object":
                return token.Type == JTokenType.Object;
            case "array":
                return token.Type == JTokenType.Array;
            case "string":
                return token.Type == JTokenType.String;
            case "boolean":
                return token.Type == JTokenType.Boolean;
            case "null":
                return token.Type == JTokenType.Null;
            case "integer":
                if (token.Type == JTokenType.Integer) return true;
                return token.Type == JTokenType.Float && IsIntegral(token.Value<double>());
            case "number":
                if (token.Type == JTokenType.Integer) return true;
                if (token.Type != JTokenType.Float) return false;
                var finite = token.Value<double>();
                return !double.IsNaN(finite) && !double.IsInfinity(finite);
            default:
                throw new InvalidOperationException("Schema 使用了不支持的 type：" + name);
        }
    }

    private static bool IsIntegral(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && Math.Truncate(value) == value;

    private static string Describe(JToken token)
    {
        if (token.Type == JTokenType.Float)
        {
            var value = token.Value<double>();
            if (double.IsNaN(value)) return "NaN（非法数值）";
            if (double.IsPositiveInfinity(value)) return "Infinity（非法数值）";
            if (double.IsNegativeInfinity(value)) return "-Infinity（非法数值）";
        }
        return token.Type.ToString();
    }

    private static void ValidateNumber(JToken token, JObject schema, string path, List<SchemaViolation> errors)
    {
        var value = token.Type == JTokenType.Integer
            ? Convert.ToDouble(token.Value<long>(), CultureInfo.InvariantCulture)
            : token.Value<double>();
        if (schema["minimum"] is JValue minimum && value < minimum.Value<double>())
            errors.Add(new SchemaViolation(path, "数值小于最小值 " + Format(minimum.Value<double>()) + "。"));
        if (schema["maximum"] is JValue maximum && value > maximum.Value<double>())
            errors.Add(new SchemaViolation(path, "数值大于最大值 " + Format(maximum.Value<double>()) + "。"));
        if (schema["exclusiveMinimum"] is JValue exclusiveMinimum && value <= exclusiveMinimum.Value<double>())
            errors.Add(new SchemaViolation(path, "数值必须大于 " + Format(exclusiveMinimum.Value<double>()) + "。"));
        if (schema["exclusiveMaximum"] is JValue exclusiveMaximum && value >= exclusiveMaximum.Value<double>())
            errors.Add(new SchemaViolation(path, "数值必须小于 " + Format(exclusiveMaximum.Value<double>()) + "。"));
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void ValidateString(JToken token, JObject schema, string path, List<SchemaViolation> errors)
    {
        var value = token.Value<string>() ?? string.Empty;
        if (schema["minLength"] is JValue minLength && value.Length < minLength.Value<int>())
            errors.Add(new SchemaViolation(path, "字符串长度 " + value.Length.ToString(CultureInfo.InvariantCulture) + " 小于最小长度 " + minLength.Value<int>().ToString(CultureInfo.InvariantCulture) + "。"));
        if (schema["maxLength"] is JValue maxLength && value.Length > maxLength.Value<int>())
            errors.Add(new SchemaViolation(path, "字符串长度 " + value.Length.ToString(CultureInfo.InvariantCulture) + " 大于最大长度 " + maxLength.Value<int>().ToString(CultureInfo.InvariantCulture) + "。"));
        if (schema["pattern"] is JValue pattern && !Regex.IsMatch(value, pattern.Value<string>()))
            errors.Add(new SchemaViolation(path, "字符串不匹配模式 " + pattern.Value<string>() + "。"));
    }

    private void ValidateArray(JToken token, JObject schema, JObject baseRoot, string path, List<SchemaViolation> errors)
    {
        var array = (JArray)token;
        if (schema["minItems"] is JValue minItems && array.Count < minItems.Value<int>())
            errors.Add(new SchemaViolation(path, "数组项数 " + array.Count.ToString(CultureInfo.InvariantCulture) + " 小于最小项数 " + minItems.Value<int>().ToString(CultureInfo.InvariantCulture) + "。"));
        if (schema["items"] is JToken items)
        {
            for (var index = 0; index < array.Count; index++)
                ValidateAgainst(array[index], items, baseRoot, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]", errors);
        }
    }

    private void ValidateObject(JToken token, JObject schema, JObject baseRoot, string path, List<SchemaViolation> errors)
    {
        var obj = (JObject)token;
        var properties = schema["properties"] as JObject;
        if (schema["required"] is JArray required)
        {
            foreach (var name in required)
            {
                var field = name.Value<string>()!;
                if (obj[field] == null)
                    errors.Add(new SchemaViolation(path, "缺少必需字段 " + field + "。"));
            }
        }
        if (properties != null)
        {
            foreach (var property in properties)
            {
                var child = obj[property.Key];
                if (child != null)
                    ValidateAgainst(child, property.Value!, baseRoot, path + "." + property.Key, errors);
            }
        }
        if (schema["additionalProperties"] is JValue additional && !additional.Value<bool>())
        {
            foreach (var property in obj.Properties())
            {
                if (properties == null || properties[property.Name] == null)
                    errors.Add(new SchemaViolation(path + "." + property.Name, "未知字段，不在契约内。"));
            }
        }
    }

    private (JToken Schema, JObject Base) ResolveRef(string reference, JObject baseRoot)
    {
        var separator = reference.IndexOf('#');
        var file = separator < 0 ? reference : reference.Substring(0, separator);
        var pointer = separator < 0 ? string.Empty : reference.Substring(separator + 1);
        var root = file.Length == 0 ? baseRoot : _refResolver(file);
        var node = (JToken)root;
        if (pointer.Length == 0) return (node, root);
        foreach (var raw in pointer.TrimStart('/').Split('/'))
        {
            var segment = raw.Replace("~1", "/").Replace("~0", "~");
            if (node is not JObject current || current[segment] == null)
                throw new InvalidOperationException("无法解析 Schema $ref：" + reference);
            node = current[segment]!;
        }
        return (node, root);
    }
}
