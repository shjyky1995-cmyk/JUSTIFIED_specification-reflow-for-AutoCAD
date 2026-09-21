using System;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// AC-04：JSON 往返语义不丢、未知扩展字段保留、非法数值拒绝。
public class DocumentProtocolTests
{
    private static readonly JsonProtocol Protocol = JsonProtocol.Default;

    [Test]
    public void ValidDocumentLoadsWithFullSemantics()
    {
        var result = Protocol.LoadDocument(Fixtures.Text("document-valid.json"));
        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
        var document = result.Value!;
        Assert.That(document.SchemaVersion, Is.EqualTo("1.0"));
        Assert.That(document.DocumentId, Is.EqualTo("sample-doc-001"));
        Assert.That(document.DisciplineCode, Is.EqualTo("structure"));
        Assert.That(document.Source.Kind, Is.EqualTo(DocumentSourceKind.Docx));
        Assert.That(document.Source.Name, Is.EqualTo("示例.docx"));
        Assert.That(document.Source.ContentHash, Is.EqualTo("example-only"));
        Assert.That(document.Blocks.Count, Is.EqualTo(4));
        Assert.That(document.Blocks[0].Type, Is.EqualTo(BlockType.Heading1));
        Assert.That(document.Blocks[1].Type, Is.EqualTo(BlockType.Paragraph));
        Assert.That(document.Blocks[1].Numbering!.Label, Is.EqualTo("1."));
        Assert.That(document.Blocks[1].Numbering!.SourceKind, Is.EqualTo(NumberingSourceKind.Automatic));
        Assert.That(document.Blocks[1].Numbering!.Level, Is.EqualTo(0));
        Assert.That(document.Blocks[1].SourceRef.TextRange!.Start, Is.EqualTo(0));
        Assert.That(document.Blocks[1].SourceRef.TextRange!.Length, Is.EqualTo(9));
        Assert.That(document.Blocks[2].Runs.Count, Is.EqualTo(3));
        Assert.That(document.Blocks[2].Runs[0].Text, Is.EqualTo("活荷载标准值为2.0kN/m²。"));
        Assert.That(document.Blocks[2].Runs[1].Text, Is.EqualTo("\n"));
        Assert.That(document.Blocks[2].Runs[2].Semantic, Is.EqualTo(RunSemantic.Superscript));
        Assert.That(document.Blocks[3].Type, Is.EqualTo(BlockType.Spacer));
        Assert.That(document.Blocks[3].SlotCount, Is.EqualTo(1));
        Assert.That(document.Blocks[3].Runs, Is.Empty);
    }

    [Test]
    public void DocumentRoundTripPreservesSemanticsAndUnknownExtensions()
    {
        var original = Fixtures.Json("document-valid.json");
        var loaded = Protocol.LoadDocument(original.ToString());
        Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Diagnostics));
        var saved = Protocol.SaveDocument(loaded.Value!);
        Assert.That(JToken.DeepEquals(original, JToken.Parse(saved)), Is.True, saved);

        var extensions = loaded.Value!.Extensions;
        Assert.That(extensions.ContainsKey("vendor:review"), Is.True);
        Assert.That(extensions.ContainsKey("acme:flag"), Is.True);
        var review = JToken.FromObject(extensions["vendor:review"]!);
        Assert.That(JToken.DeepEquals(review, JObject.Parse("{\"reviewer\":\"kim\",\"round\":2}")), Is.True);
        Assert.That(JToken.DeepEquals(JToken.FromObject(extensions["acme:flag"]!), new JValue(true)), Is.True);
    }

    [Test]
    public void RoundTripIsDeterministic()
    {
        var first = Protocol.SaveDocument(Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!);
        var second = Protocol.SaveDocument(Protocol.LoadDocument(first).Value!);
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void SerializedJsonKeepsUnicodeLiteralsAndCanonicalEnumNames()
    {
        var saved = Protocol.SaveDocument(Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!);
        Assert.That(saved, Does.Contain("²"));
        Assert.That(saved, Does.Contain("混凝土采用C30。"));
        Assert.That(saved, Does.Contain("heading1"));
        Assert.That(saved, Does.Contain("superscript"));
        Assert.That(saved, Does.Contain("automatic"));
        Assert.That(saved, Does.Contain("docx"));
    }

    [Test]
    public void DocumentHashIsStableAndContentSensitive()
    {
        var document = Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!;
        var hash = Protocol.ComputeDocumentHash(document);
        Assert.That(hash.Length, Is.EqualTo(64));
        Assert.That(hash, Is.EqualTo(Protocol.ComputeDocumentHash(Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!)));
        document.DocumentId = "sample-doc-002";
        Assert.That(Protocol.ComputeDocumentHash(document), Is.Not.EqualTo(hash));
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void NonFiniteNumbersAreRejected(string literal)
    {
        var json = Protocol.SaveDocument(Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!);
        var poisoned = json.Replace("\"round\":2", "\"round\":" + literal);
        Assert.That(poisoned, Is.Not.EqualTo(json), "fixture 中未找到注入点");
        var result = Protocol.LoadDocument(poisoned);
        Assert.That(result.Success, Is.False);
        var diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.ESchemaInvalid));
        Assert.That(diagnostic.Severity, Is.EqualTo(Severity.Error));
        Assert.That(diagnostic.Stage, Is.EqualTo(DiagnosticStage.Protocol));
        Assert.That(diagnostic.Details!["jsonPath"], Is.EqualTo("$.extensions.vendor:review.round"));
    }

    [Test]
    public void SerializationRejectsNonFiniteModelValues()
    {
        var document = Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!;
        document.Extensions["vendor:nan"] = double.NaN;
        Assert.Throws<InvalidOperationException>(() => Protocol.SaveDocument(document));
    }

    [Test]
    public void SerializationRejectsModelThatViolatesSchema()
    {
        var document = Protocol.LoadDocument(Fixtures.Text("document-valid.json")).Value!;
        document.DocumentId = string.Empty;
        Assert.Throws<InvalidOperationException>(() => Protocol.SaveDocument(document));
    }

    [Test]
    public void EmptyExtensionsObjectRoundTripsAsEmptyObject()
    {
        var token = JObject.Parse(Fixtures.Text("document-valid.json"));
        token["extensions"] = new JObject();
        var loaded = Protocol.LoadDocument(token.ToString());
        Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Diagnostics));
        Assert.That(loaded.Value!.Extensions, Is.Empty);
        Assert.That(JToken.Parse(Protocol.SaveDocument(loaded.Value))["extensions"], Is.EqualTo(new JObject()));
    }
}
