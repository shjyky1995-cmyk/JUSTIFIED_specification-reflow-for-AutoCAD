using System.IO;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface IDocumentSource
{
    SourceInfo Info { get; }

    Stream OpenRead();
}
