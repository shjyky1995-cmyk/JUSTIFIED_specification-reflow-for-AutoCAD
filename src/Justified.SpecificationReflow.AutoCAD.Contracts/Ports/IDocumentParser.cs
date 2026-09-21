using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface IDocumentParser
{
    DocumentParseResult Parse(IDocumentSource source, ParseProfile profile, CancellationToken cancellationToken);
}

public sealed class ParseProfile
{
    public StandardRef Standard { get; set; } = new StandardRef();
}

public sealed class DocumentParseResult
{
    public DocumentParseResult(Document? document, IReadOnlyList<Diagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public Document? Document { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Document != null && !Diagnostics.Any(diagnostic => diagnostic.Severity == Severity.Error);
}
