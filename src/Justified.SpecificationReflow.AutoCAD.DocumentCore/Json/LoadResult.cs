using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

namespace Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;

public sealed class LoadResult<T>
    where T : class
{
    public LoadResult(T? value, IReadOnlyList<Diagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T? Value { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Value != null && !HasErrors();

    private bool HasErrors()
    {
        foreach (var diagnostic in Diagnostics)
        {
            if (diagnostic.Severity == Severity.Error) return true;
        }
        return false;
    }
}
