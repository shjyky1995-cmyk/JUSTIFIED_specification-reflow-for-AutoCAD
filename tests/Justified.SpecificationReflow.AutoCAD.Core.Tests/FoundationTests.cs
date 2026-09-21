using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class FoundationTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Test]
    public void OpenXmlCanRoundTripUnicodeDocxWithoutWord()
    {
        const string expected = "混凝土 C30，20kN/m²；Φ20@200。";
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(expected)))));
            main.Document.Save();
        }
        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        Assert.That(reopened.MainDocumentPart!.Document.InnerText, Is.EqualTo(expected));
    }

    [Test]
    public void JsonDependencyPreservesUnicodeAndUnknownFields()
    {
        const string text = "{\"text\":\"Φ ± ℃ ²\",\"vendor:extra\":{\"keep\":true}}";
        var input = JObject.Parse(text);
        var restored = JObject.Parse(input.ToString());
        Assert.That(JToken.DeepEquals(input, restored), Is.True);
    }

    [Test]
    public void ProductionProjectGraphMatchesApprovedBoundaries()
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["Contracts"] = Array.Empty<string>(),
            ["DocumentCore"] = new[] { "Contracts" },
            ["DocxAdapter"] = new[] { "Contracts" },
            ["Standards"] = new[] { "Contracts" },
            ["LayoutEngine"] = new[] { "Contracts" },
            ["Application"] = new[] { "Contracts", "DocumentCore" },
            ["AutoCadAdapter"] = new[] { "Contracts" },
            ["PluginHost"] = new[] { "Application", "Contracts", "DocumentCore", "DocxAdapter", "Standards", "LayoutEngine", "AutoCadAdapter" }
        };
        var projects = Directory.GetFiles(Path.Combine(Root(), "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.That(projects.Length, Is.EqualTo(allowed.Count), "New modules require an architecture decision.");
        foreach (var path in projects)
        {
            var name = Path.GetFileNameWithoutExtension(path).Replace("Justified.SpecificationReflow.AutoCAD.", "");
            Assert.That(allowed.ContainsKey(name), Is.True, name);
            var project = XDocument.Load(path);
            var refs = project.Descendants("ProjectReference")
                .Select(x => Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!).Replace("Justified.SpecificationReflow.AutoCAD.", "")).ToArray();
            Assert.That(refs, Is.SubsetOf(allowed[name]), name);
            if (name == "AutoCadAdapter" || name == "PluginHost") continue;
            Assert.That(project.Descendants("TargetFramework").Single().Value, Is.EqualTo("netstandard2.0"), name);
            var forbidden = project.Descendants("Reference").Concat(project.Descendants("PackageReference"))
                .Select(x => (string)x.Attribute("Include")!)
                .Where(x => x.StartsWith("AcMgd", StringComparison.OrdinalIgnoreCase)
                    || x.StartsWith("AcDbMgd", StringComparison.OrdinalIgnoreCase)
                    || x.StartsWith("AcCoreMgd", StringComparison.OrdinalIgnoreCase)
                    || x.IndexOf("Autodesk", StringComparison.OrdinalIgnoreCase) >= 0
                    || x.IndexOf("Windows.Forms", StringComparison.OrdinalIgnoreCase) >= 0
                    || x.IndexOf("Office.Interop", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(forbidden, Is.Empty, name);
            foreach (var source in Directory.GetFiles(Path.GetDirectoryName(path)!, "*.cs", SearchOption.AllDirectories)
                .Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
                Assert.That(File.ReadAllText(source), Does.Not.Contain("Autodesk.AutoCAD"), source);
        }
    }

    [Test]
    public void AgentRulesRemainSmallAndLocalLinksResolve()
    {
        var root = Root();
        Assert.That(File.ReadAllLines(Path.Combine(root, "AGENTS.md")).Length, Is.LessThanOrEqualTo(60));
        foreach (var file in new[] { "AGENTS.md", "README.md" })
        foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(Path.Combine(root, file)), @"\]\(([^)]+)\)"))
            Assert.That(File.Exists(Path.Combine(root, link.Groups[1].Value)), Is.True, link.Value);
    }
}
