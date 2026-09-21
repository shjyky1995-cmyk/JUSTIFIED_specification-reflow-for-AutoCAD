using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface ILayoutEngine
{
    LayoutResult Layout(Document document, InstitutionStandard standard, LayoutTemplate template, ITextMeasureService measure, CancellationToken cancellationToken);
}
