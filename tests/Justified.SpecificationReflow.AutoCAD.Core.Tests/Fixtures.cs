using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

internal static class Fixtures
{
    public static string Text(string name)
    {
        var assembly = typeof(Fixtures).GetTypeInfo().Assembly;
        var resource = typeof(Fixtures).Namespace + ".Fixtures." + name;
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("缺少 fixture 资源：" + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static JToken Json(string name) => JToken.Parse(Text(name));
}
