using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

public sealed class FontProfile
{
    public List<FontEntry> Fonts { get; set; } = new List<FontEntry>();
}

public sealed class FontEntry
{
    public string Family { get; set; } = string.Empty;

    public string? BigFont { get; set; }

    public string? FileIdentity { get; set; }
}
