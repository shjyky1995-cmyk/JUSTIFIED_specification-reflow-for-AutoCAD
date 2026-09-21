namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

public sealed class StyleDefinition
{
    public string FontRef { get; set; } = string.Empty;

    public double Indent { get; set; }

    public double HangingIndent { get; set; }

    public int BeforeSlots { get; set; }

    public int AfterSlots { get; set; }
}
