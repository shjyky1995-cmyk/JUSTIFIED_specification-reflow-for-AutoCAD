using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

namespace Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;

// 用当前图的 DBText 字形边界测量。事务不提交，临时文字和临时文字样式不会留下。
public sealed class HostTextMeasureService : ITextMeasureService, IDisposable
{
    private readonly Database _database;
    private readonly Dictionary<string, ObjectId> _styles = new Dictionary<string, ObjectId>(StringComparer.Ordinal);
    private Transaction? _transaction;
    private BlockTableRecord? _space;
    private bool _disposed;

    public int MeasureCalls { get; private set; }
    public double MeasureMilliseconds { get; private set; }
    public double CleanupMilliseconds { get; private set; }

    public HostTextMeasureService(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public TextMeasurement Measure(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return MeasureCore(runs, style, cancellationToken);
        }
        finally
        {
            timer.Stop();
            MeasureCalls++;
            MeasureMilliseconds += timer.Elapsed.TotalMilliseconds;
        }
    }

    private TextMeasurement MeasureCore(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HostTextMeasureService));
        if (runs == null) throw new ArgumentNullException(nameof(runs));
        if (style == null) throw new ArgumentNullException(nameof(style));
        if (cancellationToken.IsCancellationRequested)
            return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.Cancelled, "测量已取消。") });

        if (!Positive(style.TextHeight) || !Positive(style.WidthFactor) || !Finite(style.ObliqueAngle))
            return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.ETemplateInvalid, "测量样式缺少已标定的字高或宽度系数。") });

        foreach (var run in runs)
        {
            if (run != null && run.Semantic != RunSemantic.Normal)
                return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.ETemplateInvalid, "上下标缩放尚未标定，不能测量，也不会把上下标改成普通文字。") });
            if (run != null && run.Text.IndexOf('\n') >= 0 && run.Text != "\n")
                return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.ECadEnv, "内嵌换行不能放进一个 DBText。硬换行应是单独的 run。") });
        }

        var fontName = style.Font == null ? null : style.Font.FileIdentity;
        if (string.IsNullOrWhiteSpace(fontName) || !TryResolve(fontName!, out var fontPath))
            return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.EFontMissing, "找不到字体 " + (fontName ?? string.Empty) + "。不会用其他字体代替。") });

        string? bigPath = null;
        var bigName = style.Font == null ? null : style.Font.BigFont;
        if (!string.IsNullOrWhiteSpace(bigName))
        {
            if (!TryResolve(bigName!, out bigPath))
                return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.EFontMissing, "找不到大字体 " + bigName + "。不会用其他字体代替。") });
        }

        var measurements = new List<RunMeasurement>();
        var diagnostics = new List<Diagnostic>();
        try
        {
            EnsureSession();
            var key = StyleKey(fontPath, bigPath, style);
            if (!_styles.TryGetValue(key, out var styleId))
            {
                styleId = CreateStyle(_transaction!, fontPath, bigPath, style);
                _styles.Add(key, styleId);
            }
            foreach (var run in runs)
            {
                if (run == null || run.Text.Length == 0 || run.Text == "\n")
                {
                    measurements.Add(new RunMeasurement(0, new Bounds2()));
                    continue;
                }

                var text = new DBText();
                text.SetDatabaseDefaults(_database);
                text.TextStyleId = styleId;
                text.Height = style.TextHeight;
                text.WidthFactor = style.WidthFactor;
                text.Oblique = style.ObliqueAngle * Math.PI / 180.0;
                text.Position = Point3d.Origin;
                text.HorizontalMode = TextHorizontalMode.TextLeft;
                text.VerticalMode = TextVerticalMode.TextBase;
                text.TextString = CadTextCodes.Encode(run.Text, fontName);
                _space!.AppendEntity(text);
                _transaction!.AddNewlyCreatedDBObject(text, true);
                var extents = text.GeometricExtents;
                measurements.Add(new RunMeasurement(
                    extents.MaxPoint.X - text.Position.X,
                    new Bounds2
                    {
                        MinX = extents.MinPoint.X - text.Position.X,
                        MinY = extents.MinPoint.Y - text.Position.Y,
                        MaxX = extents.MaxPoint.X - text.Position.X,
                        MaxY = extents.MaxPoint.Y - text.Position.Y
                    }));
            }
        }
        catch (System.Exception error)
        {
            measurements.Clear();
            diagnostics.Add(Problem(DiagnosticCodes.ECadEnv, "宿主测量失败：" + error.GetType().Name + ": " + error.Message));
            Dispose();
        }

        return new TextMeasurement(measurements, diagnostics);
    }

    private void EnsureSession()
    {
        if (_transaction != null) return;
        _transaction = _database.TransactionManager.StartTransaction();
        _space = (BlockTableRecord)_transaction.GetObject(_database.CurrentSpaceId, OpenMode.ForWrite);
    }

    private static string StyleKey(string fontPath, string? bigPath, ResolvedStyle style)
    {
        return string.Join("|", new[]
        {
            fontPath,
            bigPath ?? string.Empty,
            style.TextHeight.ToString("R", CultureInfo.InvariantCulture),
            style.WidthFactor.ToString("R", CultureInfo.InvariantCulture),
            style.ObliqueAngle.ToString("R", CultureInfo.InvariantCulture)
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var timer = Stopwatch.StartNew();
        _transaction?.Dispose(); // 未 Commit；全部临时 DBText 和样式回滚。
        timer.Stop();
        CleanupMilliseconds += timer.Elapsed.TotalMilliseconds;
        _transaction = null;
        _space = null;
        _styles.Clear();
    }

    private ObjectId CreateStyle(Transaction transaction, string fontPath, string? bigPath, ResolvedStyle style)
    {
        var table = (TextStyleTable)transaction.GetObject(_database.TextStyleTableId, OpenMode.ForWrite);
        var record = new TextStyleTableRecord
        {
            Name = "DN_MEASURE_" + Guid.NewGuid().ToString("N"),
            FileName = fontPath,
            BigFontFileName = bigPath ?? string.Empty,
            TextSize = style.TextHeight,
            XScale = style.WidthFactor,
            ObliquingAngle = style.ObliqueAngle * Math.PI / 180.0
        };
        var id = table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        return id;
    }

    private bool TryResolve(string name, out string path)
    {
        path = string.Empty;
        try
        {
            var found = HostApplicationServices.Current.FindFile(name, _database, FindFileHint.FontFile);
            if (string.IsNullOrWhiteSpace(found)) return false;
            path = found;
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static bool Positive(double value) => Finite(value) && value > 0;

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Measure,
            Message = message
        };
    }
}
