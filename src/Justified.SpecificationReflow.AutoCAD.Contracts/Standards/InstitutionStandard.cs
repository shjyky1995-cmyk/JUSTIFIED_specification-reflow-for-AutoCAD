using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

public sealed class InstitutionStandard
{
    public string StandardId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public FontProfile FontProfile { get; set; } = new FontProfile();

    public double TextHeight { get; set; }

    public double WidthFactor { get; set; }

    public double ObliqueAngle { get; set; }

    public double RowPitch { get; set; }

    // 上下标标定（相对字高的比例）。缺值即未标定，排版与落图都必须阻断，不扁平化。
    public double? SuperscriptScale { get; set; }

    public double? SuperscriptRise { get; set; }

    public double? SubscriptScale { get; set; }

    public double? SubscriptDrop { get; set; }

    public Dictionary<string, StyleDefinition> Styles { get; set; } = new Dictionary<string, StyleDefinition>();

    public LayerPolicy LayerPolicy { get; set; } = new LayerPolicy();

    public string SymbolMapVersion { get; set; } = string.Empty;

    public string LineBreakRuleVersion { get; set; } = string.Empty;

    public double MeasurementTolerance { get; set; }
}
