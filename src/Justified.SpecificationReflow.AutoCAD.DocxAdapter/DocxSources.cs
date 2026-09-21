using System;
using System.IO;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

public sealed class DocxFileSource : IDocumentSource
{
    private readonly string _path;

    public DocxFileSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("DOCX 路径不能为空。", nameof(path));
        _path = path;
        Info = new SourceInfo
        {
            Kind = DocumentSourceKind.Docx,
            Name = Path.GetFileName(path),
            LocalPath = path,
            ContentHash = string.Empty
        };
    }

    public SourceInfo Info { get; }

    public Stream OpenRead()
    {
        return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}

public sealed class DocxBytesSource : IDocumentSource
{
    private readonly byte[] _bytes;

    public DocxBytesSource(string name, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("DOCX 名称不能为空。", nameof(name));
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        _bytes = bytes;
        Info = new SourceInfo
        {
            Kind = DocumentSourceKind.Docx,
            Name = name,
            ContentHash = string.Empty
        };
    }

    public SourceInfo Info { get; }

    public Stream OpenRead()
    {
        return new MemoryStream(_bytes, writable: false);
    }
}
