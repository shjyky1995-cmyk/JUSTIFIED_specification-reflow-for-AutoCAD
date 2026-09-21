using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.Standards;

// 从目录读取院标和模板。正式目录拒绝 test-fixture；缺文件或未标定都不生成默认值。
public sealed class DirectoryPackageCatalog : IStandardProvider, ILayoutTemplateProvider
{
    private readonly string _root;
    private readonly bool _allowTestFixtures;
    private readonly PackageValidator _validator;

    public DirectoryPackageCatalog(string root, bool allowTestFixtures)
        : this(root, allowTestFixtures, new PackageValidator())
    {
    }

    public DirectoryPackageCatalog(string root, bool allowTestFixtures, PackageValidator validator)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("院标目录不能为空。", nameof(root));
        _root = root;
        _allowTestFixtures = allowTestFixtures;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public StandardLoadResult Load(StandardRef reference, CancellationToken cancellationToken)
    {
        if (reference == null) throw new ArgumentNullException(nameof(reference));
        if (cancellationToken.IsCancellationRequested) return new StandardLoadResult(null, new[] { Cancelled("读取院标已取消。") });
        var path = PackagePath("standards", reference.Id, reference.Version, out var diagnostics);
        if (path == null) return new StandardLoadResult(null, diagnostics);
        var standard = ReadStandard(path, diagnostics);
        return new StandardLoadResult(HasError(diagnostics) ? null : standard, diagnostics);
    }

    public TemplateLoadResult Load(TemplateRef reference, CancellationToken cancellationToken)
    {
        if (reference == null) throw new ArgumentNullException(nameof(reference));
        if (cancellationToken.IsCancellationRequested) return new TemplateLoadResult(null, new[] { Cancelled("读取模板已取消。") });
        var path = PackagePath("templates", reference.Id, reference.Version, out var diagnostics);
        if (path == null) return new TemplateLoadResult(null, diagnostics);
        var template = ReadTemplate(path, diagnostics, out var standard);
        if (template != null)
            diagnostics.AddRange(_validator.ValidateTemplate(template, standard));
        return new TemplateLoadResult(HasError(diagnostics) ? null : template, diagnostics);
    }

    private InstitutionStandard? ReadStandard(string path, List<Diagnostic> diagnostics)
    {
        if (!TryReadObject(path, diagnostics, out var token)) return null;
        if (!AcceptClassification(token, diagnostics, "standard")) return null;
        InstitutionStandard standard;
        try
        {
            standard = token.ToObject<InstitutionStandard>(JsonSerializer.Create(StandardJson.Settings)) ?? new InstitutionStandard();
        }
        catch (JsonException error)
        {
            diagnostics.Add(Invalid(path, error.Message));
            return null;
        }

        diagnostics.AddRange(_validator.ValidateStandard(standard));
        return standard;
    }

    private LayoutTemplate? ReadTemplate(string path, List<Diagnostic> diagnostics, out InstitutionStandard? standard)
    {
        standard = null;
        if (!TryReadObject(path, diagnostics, out var token)) return null;
        if (!AcceptClassification(token, diagnostics, "template")) return null;
        var snapshot = token["resolvedRowPitch"];
        token.Remove("resolvedRowPitch");
        LayoutTemplate template;
        try
        {
            template = token.ToObject<LayoutTemplate>(JsonSerializer.Create(StandardJson.Settings)) ?? new LayoutTemplate();
        }
        catch (JsonException error)
        {
            diagnostics.Add(Invalid(path, error.Message));
            return null;
        }

        if (template.StandardRef != null && IsSafe(template.StandardRef.Id) && IsSafe(template.StandardRef.Version))
        {
            var standardPath = Path.Combine(_root, "standards", template.StandardRef.Id, template.StandardRef.Version + ".json");
            if (File.Exists(standardPath))
            {
                var standardDiagnostics = new List<Diagnostic>();
                standard = ReadStandard(standardPath, standardDiagnostics);
                diagnostics.AddRange(standardDiagnostics);
                if (HasError(standardDiagnostics)) standard = null;
            }
            else
            {
                diagnostics.Add(Error("找不到模板引用的院标 " + template.StandardRef.Id + "/" + template.StandardRef.Version + "。", "standardRef"));
            }
        }

        if (snapshot != null)
        {
            if (snapshot.Type != JTokenType.Float && snapshot.Type != JTokenType.Integer)
                diagnostics.Add(Error("resolvedRowPitch 必须是数值，不能留空后填 0。", "resolvedRowPitch"));
            else if (standard == null || Math.Abs(snapshot.Value<double>() - standard.RowPitch) > 1e-9)
                diagnostics.Add(Error("resolvedRowPitch 与院标 rowPitch 不一致。", "resolvedRowPitch"));
        }

        return template;
    }

    private string? PackagePath(string kind, string id, string version, out List<Diagnostic> diagnostics)
    {
        diagnostics = new List<Diagnostic>();
        if (!IsSafe(id) || !IsSafe(version))
        {
            diagnostics.Add(Error("包标识只能是文件名，不能包含路径。", kind));
            return null;
        }

        var path = Path.Combine(_root, kind, id, version + ".json");
        if (!File.Exists(path))
        {
            diagnostics.Add(Error("没有已标定的" + (kind == "standards" ? "院标" : "模板") + "：" + id + "/" + version + "。", kind));
            return null;
        }

        return path;
    }

    private bool AcceptClassification(JObject token, List<Diagnostic> diagnostics, string path)
    {
        var classification = token["classification"]?.Type == JTokenType.String ? token.Value<string>("classification") : null;
        token.Remove("classification");
        if (classification == "test-fixture" && _allowTestFixtures) return true;
        if (classification == "production") return true;
        diagnostics.Add(Error(classification == "test-fixture"
            ? "测试夹具不能作为正式院标或模板发布。"
            : "包缺少可发布的 classification。测试夹具必须标明 test-fixture。", path + ".classification"));
        return false;
    }

    private static bool TryReadObject(string path, List<Diagnostic> diagnostics, out JObject token)
    {
        token = new JObject();
        try
        {
            var parsed = JToken.Parse(File.ReadAllText(path));
            if (parsed is JObject obj)
            {
                token = obj;
                return true;
            }

            diagnostics.Add(Invalid(path, "根节点必须是对象。"));
            return false;
        }
        catch (Exception error) when (error is IOException || error is JsonException)
        {
            diagnostics.Add(Invalid(path, error.Message));
            return false;
        }
    }

    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..") return false;
        return value!.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static bool HasError(IReadOnlyList<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == Severity.Error) return true;
        }

        return false;
    }

    private static Diagnostic Invalid(string path, string message)
    {
        return Error("无法读取 " + path + "：" + message, path);
    }

    private static Diagnostic Error(string message, string path)
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.ETemplateInvalid,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Standard,
            Message = message,
            Details = new Dictionary<string, string> { ["path"] = path }
        };
    }

    private static Diagnostic Cancelled(string message)
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.Cancelled,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Standard,
            Message = message
        };
    }
}
