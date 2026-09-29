using System;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

public static class TableStyleRules
{
    public static bool IsValid(TableStyle s) => s != null
        && Finite(s.HorizontalPaddingEm) && s.HorizontalPaddingEm >= 0 && s.HorizontalPaddingEm <= 10
        && Finite(s.VerticalPaddingEm) && s.VerticalPaddingEm >= 0 && s.VerticalPaddingEm <= 10
        && s.BeforeSlots >= 0 && s.BeforeSlots <= 100 && s.AfterSlots >= 0 && s.AfterSlots <= 100
        && !string.IsNullOrWhiteSpace(s.Layer) && s.Linetype == "Continuous"
        && Array.IndexOf(new[] { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 }, s.Lineweight) >= 0;
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
}
