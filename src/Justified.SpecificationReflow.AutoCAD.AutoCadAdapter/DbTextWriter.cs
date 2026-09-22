using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

namespace Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;

// 一次事务写入全部 DBText。失败或取消不提交，所以不会留下半套文字。再次生成只追加，不删除旧字。
public sealed class DbTextWriter
{
    public RenderReport Write(Database database, IReadOnlyList<PlacedText> texts, double elevation, System.Threading.CancellationToken cancellationToken)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));
        if (texts == null) throw new ArgumentNullException(nameof(texts));
        if (cancellationToken.IsCancellationRequested) return Report(false, 0, 0, Problem(DiagnosticCodes.Cancelled, "落图已取消。"));
        if (texts.Count == 0) return Report(true, 0, 0);

        using (var transaction = database.TransactionManager.StartTransaction())
        {
            try
            {
                if (!IsModelSpace(database, transaction, out var space, out var spaceError))
                    return Report(false, 0, 0, spaceError!);

                var styles = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                var layers = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                var pages = new HashSet<int>();
                var written = 0;
                foreach (var text in texts)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Report(false, 0, 0, Problem(DiagnosticCodes.Cancelled, "落图已取消。"));
                    if (text == null || string.IsNullOrEmpty(text.Text)) continue;
                    var layerId = EnsureLayer(database, transaction, text.Layer, layers, out var layerError);
                    if (layerError != null) return Report(false, 0, 0, layerError);
                    var styleId = EnsureStyle(database, transaction, text, styles, out var styleError);
                    if (styleError != null) return Report(false, 0, 0, styleError);

                    var entity = new DBText();
                    entity.SetDatabaseDefaults(database);
                    entity.LayerId = layerId;
                    entity.TextStyleId = styleId;
                    entity.TextString = text.Text;
                    entity.Height = text.Height;
                    entity.WidthFactor = text.WidthFactor;
                    entity.Oblique = text.ObliqueDegrees * Math.PI / 180.0;
                    entity.Rotation = 0;
                    entity.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                    var point = new Point3d(text.Position.X, text.Position.Y, elevation);
                    entity.HorizontalMode = TextHorizontalMode.TextLeft;
                    entity.VerticalMode = TextVerticalMode.TextBase;
                    entity.Position = point;
                    entity.AlignmentPoint = point;
                    space.AppendEntity(entity);
                    transaction.AddNewlyCreatedDBObject(entity, true);
                    entity.AdjustAlignment(database);
                    pages.Add(text.PageIndex);
                    written++;
                }

                if (written == 0) return Report(true, 0, 0);
                transaction.Commit();
                return Report(true, pages.Count, written);
            }
            catch (System.Exception error)
            {
                return Report(false, 0, 0, Problem(DiagnosticCodes.ERenderFailed, "写入单行文字失败：" + error.GetType().Name + "。本次没有提交。"));
            }
        }
    }

    private static bool IsModelSpace(Database database, Transaction transaction, out BlockTableRecord space, out Diagnostic? error)
    {
        var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelId = table[BlockTableRecord.ModelSpace];
        space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        if (database.CurrentSpaceId == modelId)
        {
            error = null;
            return true;
        }

        error = Problem(DiagnosticCodes.ECadEnv, "请在模型空间生成。纸空间不会写入。");
        return false;
    }

    private static ObjectId EnsureLayer(Database database, Transaction transaction, string name, Dictionary<string, ObjectId> cache, out Diagnostic? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = Problem(DiagnosticCodes.ETemplateInvalid, "图层名是空的。");
            return ObjectId.Null;
        }

        if (cache.TryGetValue(name, out var existing)) return existing;
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (table.Has(name))
        {
            var id = table[name];
            cache[name] = id;
            return id;
        }

        table.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = name,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
        };
        var created = table.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
        cache[name] = created;
        return created;
    }

    private static ObjectId EnsureStyle(Database database, Transaction transaction, PlacedText text, Dictionary<string, ObjectId> cache, out Diagnostic? error)
    {
        error = null;
        if (!TryResolveFont(database, text.FontFile, out var fontPath))
        {
            error = Problem(DiagnosticCodes.EFontMissing, "找不到字体 " + text.FontFile + "。不会用其他字体代替。");
            return ObjectId.Null;
        }

        var bigPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(text.BigFont))
        {
            if (!TryResolveFont(database, text.BigFont, out bigPath))
            {
                error = Problem(DiagnosticCodes.EFontMissing, "找不到大字体 " + text.BigFont + "。不会用其他字体代替。");
                return ObjectId.Null;
            }
        }

        if (cache.TryGetValue(text.StyleName, out var cached)) return cached;
        var table = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        if (table.Has(text.StyleName))
        {
            var id = table[text.StyleName];
            var record = (TextStyleTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            if (!SameFont(record.FileName, fontPath) || !SameFont(record.BigFontFileName, bigPath) || Math.Abs(record.TextSize) > 1e-9 || Math.Abs(record.XScale - 1) > 1e-9 || Math.Abs(record.ObliquingAngle) > 1e-9)
            {
                error = Problem(DiagnosticCodes.EStyleConflict, "图中已有样式 " + text.StyleName + "，且与本次文字不同。不会修改旧样式。");
                return ObjectId.Null;
            }

            cache[text.StyleName] = id;
            return id;
        }

        table.UpgradeOpen();
        var created = new TextStyleTableRecord
        {
            Name = text.StyleName,
            FileName = fontPath,
            BigFontFileName = bigPath,
            TextSize = 0,
            XScale = 1,
            ObliquingAngle = 0
        };
        var createdId = table.Add(created);
        transaction.AddNewlyCreatedDBObject(created, true);
        cache[text.StyleName] = createdId;
        return createdId;
    }

    private static bool TryResolveFont(Database database, string name, out string path)
    {
        path = string.Empty;
        try
        {
            var found = HostApplicationServices.Current.FindFile(name, database, FindFileHint.FontFile);
            if (string.IsNullOrWhiteSpace(found)) return false;
            path = found;
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static bool SameFont(string existing, string expected)
    {
        if (string.IsNullOrEmpty(existing) && string.IsNullOrEmpty(expected)) return true;
        return string.Equals(Path.GetFileName(existing ?? string.Empty), Path.GetFileName(expected ?? string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    private static RenderReport Report(bool success, int pages, int objects, Diagnostic? diagnostic = null)
    {
        var report = new RenderReport
        {
            Success = success,
            PageCount = pages,
            ObjectCount = objects
        };
        if (diagnostic != null) report.Diagnostics.Add(diagnostic);
        return report;
    }

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Render,
            Message = message
        };
    }
}
