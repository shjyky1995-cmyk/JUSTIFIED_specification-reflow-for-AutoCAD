using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class JsonSchemaValidatorTests
{
    private static IReadOnlyList<SchemaViolation> Validate(string schema, string instance)
    {
        var validator = new JsonSchemaValidator(JObject.Parse(schema));
        return validator.Validate(JToken.Parse(instance));
    }

    [Test]
    public void ValidInstanceProducesNoViolations()
    {
        var violations = Validate(@"{""type"":""object"",""properties"":{""a"":{""type"":""integer"",""minimum"":0}},""required"":[""a""]}", @"{""a"":1}");
        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void IntegerTypeAcceptsIntegralFloatButRejectsFraction()
    {
        Assert.That(Validate(@"{""type"":""integer""}", "1.0"), Is.Empty);
        Assert.That(Validate(@"{""type"":""integer""}", "1.5"), Is.Not.Empty);
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void NumberTypeRejectsNonFinite(string literal)
    {
        Assert.That(Validate(@"{""type"":""number""}", literal), Is.Not.Empty, literal);
    }

    [Test]
    public void AdditionalPropertiesFalseRejectsUnknownMembers()
    {
        Assert.That(Validate(@"{""type"":""object"",""properties"":{""a"":{}},""additionalProperties"":false}", @"{""a"":1,""b"":2}"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""object"",""properties"":{""a"":{}},""additionalProperties"":false}", @"{""a"":1}"), Is.Empty);
    }

    [Test]
    public void PatternMinLengthAndNumericBoundsEnforce()
    {
        Assert.That(Validate(@"{""type"":""string"",""pattern"":""^1\\.[0-9]+$""}", @"""1.0"""), Is.Empty);
        Assert.That(Validate(@"{""type"":""string"",""pattern"":""^1\\.[0-9]+$""}", @"""2.0"""), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""string"",""minLength"":2}", @"""a"""), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""number"",""minimum"":0}", "-1"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""number"",""maximum"":10}", "11"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""number"",""exclusiveMinimum"":0}", "0"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""number"",""exclusiveMinimum"":0}", "0.5"), Is.Empty);
        Assert.That(Validate(@"{""type"":""number"",""exclusiveMaximum"":10}", "10"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""number"",""exclusiveMaximum"":10}", "9.5"), Is.Empty);
    }

    [Test]
    public void ItemsAndMinItemsEnforce()
    {
        Assert.That(Validate(@"{""type"":""array"",""items"":{""type"":""string""},""minItems"":1}", @"[""a""]"), Is.Empty);
        Assert.That(Validate(@"{""type"":""array"",""items"":{""type"":""string""}}", @"[""a"",1]"), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":""array"",""minItems"":1}", "[]"), Is.Not.Empty);
    }

    [Test]
    public void EnumConstAndTypeArrayEnforce()
    {
        Assert.That(Validate(@"{""enum"":[""a"",""b""]}", @"""a"""), Is.Empty);
        Assert.That(Validate(@"{""enum"":[""a"",""b""]}", @"""c"""), Is.Not.Empty);
        Assert.That(Validate(@"{""const"":""a""}", @"""a"""), Is.Empty);
        Assert.That(Validate(@"{""const"":""a""}", @"""b"""), Is.Not.Empty);
        Assert.That(Validate(@"{""type"":[""string"",""null""]}", "null"), Is.Empty);
        Assert.That(Validate(@"{""type"":[""string"",""null""]}", "1"), Is.Not.Empty);
    }

    [Test]
    public void IfThenElseAndNotEnforce()
    {
        var schema = @"{""type"":""object"",""properties"":{""kind"":{""type"":""string""},""n"":{""type"":""integer""}},""if"":{""properties"":{""kind"":{""const"":""x""}},""required"":[""kind""]},""then"":{""required"":[""n""]},""else"":{""not"":{""required"":[""n""]}}}";
        Assert.That(Validate(schema, @"{""kind"":""x"",""n"":1}"), Is.Empty);
        Assert.That(Validate(schema, @"{""kind"":""x""}"), Is.Not.Empty);
        Assert.That(Validate(schema, @"{""kind"":""y""}"), Is.Empty);
        Assert.That(Validate(schema, @"{""kind"":""y"",""n"":1}"), Is.Not.Empty);
    }

    [Test]
    public void LocalRefResolves()
    {
        var schema = @"{""type"":""object"",""properties"":{""a"":{""$ref"":""#/definitions/t""}},""definitions"":{""t"":{""type"":""string"",""minLength"":2}}}";
        Assert.That(Validate(schema, @"{""a"":""ok""}"), Is.Empty);
        Assert.That(Validate(schema, @"{""a"":""x""}"), Is.Not.Empty);
    }

    [Test]
    public void UnknownKeywordsAreIgnoredForForwardCompatibility()
    {
        Assert.That(Validate(@"{""type"":""object"",""x-vendor"":true}", "{}"), Is.Empty);
    }

    [Test]
    public void NonFiniteNumbersAreFoundAnywhereInTheTree()
    {
        var token = JObject.Parse(@"{""a"":{""b"":[1,NaN,2]},""c"":""x""}");
        var violations = JsonSchemaValidator.FindNonFiniteNumbers(token);
        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That(violations[0].Path, Is.EqualTo("$.a.b[1]"));
    }
}
