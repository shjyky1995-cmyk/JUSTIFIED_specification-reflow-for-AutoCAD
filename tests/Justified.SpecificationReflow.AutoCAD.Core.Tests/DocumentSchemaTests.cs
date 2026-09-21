using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// AC-04：未知主版本/未知必需块拒绝（E_SCHEMA_VERSION）；其余协议违例拒绝（E_SCHEMA_INVALID）。
public class DocumentSchemaTests
{
    private static readonly JsonProtocol Protocol = JsonProtocol.Default;

    [Test]
    public void ValidFixturePassesValidation()
    {
        Assert.That(Protocol.ValidateDocument(Fixtures.Text("document-valid.json")), Is.Empty);
    }

    [TestCase("document-invalid-unknown-major.json", "E_SCHEMA_VERSION", "$.schemaVersion")]
    [TestCase("document-invalid-unknown-block.json", "E_SCHEMA_VERSION", "$.blocks[0].type")]
    [TestCase("document-invalid-missing-required.json", "E_SCHEMA_INVALID", "$")]
    [TestCase("document-invalid-unknown-property.json", "E_SCHEMA_INVALID", "$.vendor:extra")]
    [TestCase("document-invalid-bad-semantic.json", "E_SCHEMA_INVALID", "$.blocks[0].runs[0].semantic")]
    [TestCase("document-invalid-negative-index.json", "E_SCHEMA_INVALID", "$.blocks[0].sourceRef.paragraphIndex")]
    [TestCase("document-invalid-spacer-without-slot-count.json", "E_SCHEMA_INVALID", "$.blocks[0]")]
    public void InvalidFixturesAreRejectedWithExpectedCodeAndPath(string fixture, string code, string path)
    {
        var diagnostics = Protocol.ValidateDocument(Fixtures.Text(fixture));
        Assert.That(diagnostics, Is.Not.Empty);
        var diagnostic = diagnostics.Single();
        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Severity, Is.EqualTo(Severity.Error));
        Assert.That(diagnostic.Stage, Is.EqualTo(DiagnosticStage.Protocol));
        Assert.That(diagnostic.Details!["jsonPath"], Is.EqualTo(path));
        Assert.That(Protocol.LoadDocument(Fixtures.Text(fixture)).Success, Is.False);
    }

    [Test]
    public void MalformedJsonIsRejected()
    {
        var diagnostics = Protocol.ValidateDocument("{ not json");
        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ESchemaInvalid));
        Assert.That(Protocol.LoadDocument("{ not json").Success, Is.False);
    }

    [Test]
    public void WrongScalarTypeIsRejectedWithPath()
    {
        var token = JObject.Parse(Fixtures.Text("document-valid.json"));
        token["documentId"] = 123;
        var diagnostics = Protocol.ValidateDocument(token.ToString());
        Assert.That(diagnostics.Single().Details!["jsonPath"], Is.EqualTo("$.documentId"));
    }

    [Test]
    public void NonObjectRootIsRejected()
    {
        Assert.That(Protocol.ValidateDocument("[]"), Is.Not.Empty);
        Assert.That(Protocol.LoadDocument("[]").Success, Is.False);
    }

    [Test]
    public void UnknownMajorVersionIsRejectedBeforeDeserialization()
    {
        var token = JObject.Parse(Fixtures.Text("document-invalid-unknown-major.json"));
        var result = Protocol.LoadDocument(token.ToString());
        Assert.That(result.Success, Is.False);
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ESchemaVersion));
        Assert.That(result.Diagnostics.Single().Details!["jsonPath"], Is.EqualTo("$.schemaVersion"));
    }
}
