using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface IStandardProvider
{
    StandardLoadResult Load(StandardRef reference, CancellationToken cancellationToken);
}

public sealed class StandardLoadResult
{
    public StandardLoadResult(InstitutionStandard? standard, IReadOnlyList<Diagnostic> diagnostics)
    {
        Standard = standard;
        Diagnostics = diagnostics;
    }

    public InstitutionStandard? Standard { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Standard != null && !Diagnostics.Any(diagnostic => diagnostic.Severity == Severity.Error);
}
