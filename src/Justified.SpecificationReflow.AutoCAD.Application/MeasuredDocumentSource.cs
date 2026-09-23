using System;
using System.Diagnostics;
using System.IO;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

namespace Justified.SpecificationReflow.AutoCAD.Application;

// 只计输入打开/读取的耗时，不重复读取源文件，也不改变解析器的资源检查。
internal sealed class MeasuredDocumentSource : IDocumentSource
{
    private readonly IDocumentSource _source;
    private readonly Stopwatch _read = new Stopwatch();
    public MeasuredDocumentSource(IDocumentSource source) => _source = source;
    public SourceInfo Info => _source.Info;
    public double ReadMilliseconds => _read.Elapsed.TotalMilliseconds;
    public Stream OpenRead()
    {
        _read.Start();
        try { return new ReadStream(_source.OpenRead(), _read); }
        finally { _read.Stop(); }
    }

    private sealed class ReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly Stopwatch _read;
        public ReadStream(Stream inner, Stopwatch read) { _inner = inner; _read = read; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            _read.Start();
            try { return _inner.Read(buffer, offset, count); }
            finally { _read.Stop(); }
        }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => _inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
