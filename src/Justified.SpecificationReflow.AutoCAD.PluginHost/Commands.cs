using System;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Justified.SpecificationReflow.AutoCAD.PluginHost.Commands))]

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// M0 technical diagnostic only. No drawing writes or production generation command.
public class Commands
{
    [CommandMethod("DN_DIAG", CommandFlags.Modal)]
    public void Diagnose()
    {
        var editor = CadApplication.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            foreach (var name in new[] {
                "Justified.SpecificationReflow.AutoCAD.Contracts", "Justified.SpecificationReflow.AutoCAD.DocumentCore", "Justified.SpecificationReflow.AutoCAD.DocxAdapter",
                "Justified.SpecificationReflow.AutoCAD.Standards", "Justified.SpecificationReflow.AutoCAD.LayoutEngine", "Justified.SpecificationReflow.AutoCAD.Application",
                "Justified.SpecificationReflow.AutoCAD.AutoCadAdapter",
                "Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed",
                "DocumentFormat.OpenXml, Version=3.0.2.0, Culture=neutral, PublicKeyToken=8fb06cb64d019a17",
                "DocumentFormat.OpenXml.Framework, Version=3.0.2.0, Culture=neutral, PublicKeyToken=8fb06cb64d019a17"
            })
            {
                var assembly = Assembly.Load(name);
                var expected = new AssemblyName(name).Version ?? new Version(0, 1, 0, 0);
                if (assembly.GetName().Version != expected)
                    throw new InvalidOperationException("Dependency version mismatch: " + assembly.FullName);
                editor.WriteMessage("\nDN_DEP " + assembly.GetName().Name + " " + assembly.GetName().Version);
            }
            editor.WriteMessage("\nDN_DIAG_OK: M0 dependency load only; production generation unavailable.\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_DIAG_FAILED: " + error.GetType().Name + ": " + error.Message + "\n");
        }
    }
}
