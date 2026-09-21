using System;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// T03 冻结的 LayoutResult / RenderRequest 契约：Schema 往返与违例拒绝。
public class LayoutRenderContractTests
{
    private static readonly JsonProtocol Protocol = JsonProtocol.Default;

    [Test]
    public void LayoutResultFixtureRoundTrips()
    {
        var original = Fixtures.Json("layout-result-valid.json");
        var loaded = Protocol.LoadLayoutResult(original.ToString());
        Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Diagnostics));
        var saved = Protocol.SaveLayoutResult(loaded.Value!);
        Assert.That(JToken.DeepEquals(original, JToken.Parse(saved)), Is.True, saved);
    }

    [Test]
    public void RenderRequestFixtureRoundTripsAcrossSchemaFileReference()
    {
        var original = Fixtures.Json("render-request-valid.json");
        var loaded = Protocol.LoadRenderRequest(original.ToString());
        Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Diagnostics));
        var saved = Protocol.SaveRenderRequest(loaded.Value!);
        Assert.That(JToken.DeepEquals(original, JToken.Parse(saved)), Is.True, saved);
    }

    [Test]
    public void LayoutResultCarriesDiagnosticsStatisticsAndSuperscriptRun()
    {
        var result = Protocol.LoadLayoutResult(Fixtures.Text("layout-result-valid.json")).Value!;
        var warning = result.Diagnostics.Single();
        Assert.That(warning.Code, Is.EqualTo(DiagnosticCodes.WTokenSplit));
        Assert.That(warning.Severity, Is.EqualTo(Severity.Warning));
        Assert.That(warning.SourceRef!.ParagraphIndex, Is.EqualTo(2));
        Assert.That(warning.Details!["jsonPath"], Is.EqualTo("$.blocks[2].runs[0]"));
        Assert.That(result.Statistics.PageCount, Is.EqualTo(1));
        Assert.That(result.Statistics.RowCount, Is.EqualTo(3));
        Assert.That(result.Statistics.ObjectCount, Is.EqualTo(3));
        Assert.That(result.Statistics.WarningCount, Is.EqualTo(1));
        Assert.That(result.Statistics.Elapsed, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
        var rows = result.Pages.Single().Columns.Single().Rows;
        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(rows[2].Occupancy, Is.EqualTo(Occupancy.Spacer));
        Assert.That(rows[2].VisualLine, Is.Null);
        var renderRuns = rows[1].VisualLine!.RenderRuns;
        Assert.That(renderRuns.Count, Is.EqualTo(2));
        Assert.That(renderRuns[1].ResolvedStyle.Semantic, Is.EqualTo(RunSemantic.Superscript));
        Assert.That(renderRuns[1].BaselineOffset, Is.EqualTo(1.2));
        Assert.That(renderRuns[1].RelativeOrigin.X, Is.EqualTo(17.5));
        Assert.That(renderRuns[1].ResolvedStyle.Font.Family, Is.EqualTo("测试仿宋"));
    }

    [Test]
    public void RenderRequestRejectsNonPositiveUnitScaleAndUnknownTargetSpace()
    {
        var request = JObject.Parse(Fixtures.Text("render-request-valid.json"));
        request["unitScale"] = 0.0;
        Assert.That(Protocol.ValidateRenderRequest(request.ToString()).Single().Details!["jsonPath"], Is.EqualTo("$.unitScale"));
        request["unitScale"] = 1.0;
        request["targetSpace"] = "paper";
        Assert.That(Protocol.ValidateRenderRequest(request.ToString()).Single().Details!["jsonPath"], Is.EqualTo("$.targetSpace"));
    }

    [Test]
    public void LayoutResultRejectsNegativeRowIndexAndUnknownProperty()
    {
        var token = JObject.Parse(Fixtures.Text("layout-result-valid.json"));
        token["pages"]![0]!["columns"]![0]!["rows"]![0]!["rowIndex"] = -1;
        Assert.That(Protocol.ValidateLayoutResult(token.ToString()).Single().Details!["jsonPath"],
            Is.EqualTo("$.pages[0].columns[0].rows[0].rowIndex"));
        var extra = JObject.Parse(Fixtures.Text("layout-result-valid.json"));
        extra["vendor:extra"] = 1;
        Assert.That(Protocol.ValidateLayoutResult(extra.ToString()).Single().Details!["jsonPath"], Is.EqualTo("$.vendor:extra"));
    }

    [Test]
    public void LayoutResultRejectsNonFiniteMeasuredWidth()
    {
        var token = JObject.Parse(Fixtures.Text("layout-result-valid.json"));
        token["pages"]![0]!["columns"]![0]!["rows"]![0]!["visualLine"]!["measuredWidth"] = new JValue(double.NaN);
        Assert.That(Protocol.ValidateLayoutResult(token.ToString()), Is.Not.Empty);
        Assert.That(Protocol.LoadLayoutResult(token.ToString()).Success, Is.False);
    }
}
