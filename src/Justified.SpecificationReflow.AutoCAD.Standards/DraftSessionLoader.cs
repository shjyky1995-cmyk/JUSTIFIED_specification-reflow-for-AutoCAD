using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.Standards;

public sealed class DraftSession
{
    public InstitutionStandard? Standard { get; set; }

    public LayoutTemplate? Template { get; set; }

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();

    public bool Success
    {
        get
        {
            if (Standard == null || Template == null) return false;
            foreach (var diagnostic in Diagnostics)
            {
                if (diagnostic != null && diagnostic.Severity == Severity.Error) return false;
            }

            return true;
        }
    }
}

// 只在内存里补齐草案的 pending 字段，方便生成演示。不改磁盘上的草案，也不等于已经发布。
public sealed class DraftSessionLoader
{
    private readonly PackageValidator _validator = new PackageValidator();

    public DraftSession Load(string directory, string paperCode, string implementedLineBreakVersion, CancellationToken cancellationToken)
    {
        var session = new DraftSession();
        if (cancellationToken.IsCancellationRequested)
        {
            session.Diagnostics.Add(Error(DiagnosticCodes.Cancelled, "读取草案已取消。", "draft"));
            return session;
        }

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            session.Diagnostics.Add(Error("找不到标准目录。", "directory"));
            return session;
        }

        var templateName = TemplateFile(paperCode);
        if (templateName == null)
        {
            session.Diagnostics.Add(Error("图幅只能是 A1、A2 或 A3。", "paper"));
            return session;
        }

        if (string.IsNullOrWhiteSpace(implementedLineBreakVersion) || IsPending(implementedLineBreakVersion))
        {
            session.Diagnostics.Add(Error("没有可使用的换行规则版本。", "lineBreakRuleVersion"));
            return session;
        }

        if (!TryRead(Path.Combine(directory, "jsr-note-1.0.0.json"), session, out var standardToken)) return session;
        PrepareStandard(standardToken!, implementedLineBreakVersion, session);
        session.Standard = Deserialize<InstitutionStandard>(standardToken!, session, "jsr-note-1.0.0.json");
        if (session.Standard == null) return session;
        session.Diagnostics.AddRange(_validator.ValidateStandard(session.Standard));

        if (!TryRead(Path.Combine(directory, templateName), session, out var templateToken)) return session;
        var snapshot = templateToken!["resolvedRowPitch"];
        templateToken.Remove("resolvedRowPitch");
        templateToken.Remove("classification");
        session.Template = Deserialize<LayoutTemplate>(templateToken, session, templateName);
        if (session.Template == null) return session;
        if (session.Template.Status == TemplateStatus.Uncalibrated)
        {
            session.Template.Status = TemplateStatus.Calibrated;
            session.Diagnostics.Add(Notice("模板仍是草案。本次按项目几何生成，打印和字形还没有验收。", "status"));
        }

        if (snapshot != null && snapshot.Type != JTokenType.Null && Math.Abs(snapshot.Value<double>() - session.Standard.RowPitch) > 1e-9)
            session.Diagnostics.Add(Error("resolvedRowPitch 与院标行距不一致。", "resolvedRowPitch"));
        session.Diagnostics.AddRange(_validator.ValidateTemplate(session.Template, session.Standard));
        if (!session.Success)
        {
            session.Standard = null;
            session.Template = null;
        }

        return session;
    }

    public static string? TemplateFile(string? paperCode)
    {
        if (paperCode == null) return null;
        switch (paperCode.Trim().ToUpperInvariant())
        {
            case "A1": return "jsr-A1-three-column-1.0.0.json";
            case "A2": return "jsr-A2-three-column-1.0.0.json";
            case "A3": return "jsr-A3-two-column-1.0.0.json";
            default: return null;
        }
    }

    private static void PrepareStandard(JObject token, string implementedLineBreakVersion, DraftSession session)
    {
        token.Remove("classification");
        var lineBreak = token["lineBreakRuleVersion"]?.Type == JTokenType.String ? token.Value<string>("lineBreakRuleVersion") : null;
        if (IsPending(lineBreak))
        {
            token["lineBreakRuleVersion"] = implementedLineBreakVersion;
            session.Diagnostics.Add(Notice("换行规则版本仍是 pending。本次使用 " + implementedLineBreakVersion + "，没有改草案文件。", "lineBreakRuleVersion"));
        }

        var symbols = token["symbolMapVersion"]?.Type == JTokenType.String ? token.Value<string>("symbolMapVersion") : null;
        if (IsPending(symbols))
        {
            token["symbolMapVersion"] = "passthrough-1.0.0";
            session.Diagnostics.Add(Notice("符号映射仍是 pending。字符按原样保留，不替换。", "symbolMapVersion"));
        }

        if (token["measurementTolerance"] == null || token["measurementTolerance"]!.Type == JTokenType.Null)
        {
            token["measurementTolerance"] = 0;
            session.Diagnostics.Add(Notice("测量公差仍是空的。本次按 0 使用，不把越界当成合格。", "measurementTolerance"));
        }
    }

    private static bool TryRead(string path, DraftSession session, out JObject? token)
    {
        token = null;
        if (!File.Exists(path))
        {
            session.Diagnostics.Add(Error("找不到草案 " + path + "。", "file"));
            return false;
        }

        try
        {
            var parsed = JToken.Parse(File.ReadAllText(path));
            if (parsed is JObject obj)
            {
                token = obj;
                return true;
            }

            session.Diagnostics.Add(Error("草案必须是 JSON 对象：" + path, "file"));
            return false;
        }
        catch (Exception error) when (error is IOException || error is JsonException)
        {
            session.Diagnostics.Add(Error("无法读取草案：" + error.Message, "file"));
            return false;
        }
    }

    private static T? Deserialize<T>(JObject token, DraftSession session, string name) where T : class
    {
        try
        {
            return token.ToObject<T>(JsonSerializer.Create(StandardJson.Settings));
        }
        catch (JsonException error)
        {
            session.Diagnostics.Add(Error(name + " 无法读取：" + error.Message, name));
            return null;
        }
    }

    private static bool IsPending(string? value)
    {
        if (value == null) return true;
        var text = value.Trim();
        return text.Length == 0 || string.Equals(text, "pending", StringComparison.OrdinalIgnoreCase);
    }

    private static Diagnostic Error(string message, string path)
    {
        return Error(DiagnosticCodes.ETemplateInvalid, message, path);
    }

    private static Diagnostic Error(string code, string message, string path)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Standard,
            Message = message,
            Details = new Dictionary<string, string> { ["path"] = path }
        };
    }

    private static Diagnostic Notice(string message, string path)
    {
        return new Diagnostic
        {
            Code = "DRAFT",
            Severity = Severity.Info,
            Stage = DiagnosticStage.Standard,
            Message = message,
            Details = new Dictionary<string, string> { ["path"] = path }
        };
    }
}
