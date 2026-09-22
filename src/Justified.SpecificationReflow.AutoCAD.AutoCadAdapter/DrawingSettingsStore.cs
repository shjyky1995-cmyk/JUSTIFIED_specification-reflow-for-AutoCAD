using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

namespace Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;

// 工程设置存在当前图的 JSR_NOTE_SETTINGS 命名字典里，跟随图纸走。只存字符串块，
// 编解码由 Standards.NoteSettingsCodec 负责。读失败即当作没有设置，不用半份设置生成。
public sealed class DrawingSettingsStore
{
    public const string DictionaryName = "JSR_NOTE_SETTINGS";

    public List<string>? Load(Database database)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            try
            {
                if (!TryOpenRecord(database, transaction, out var record)) { transaction.Commit(); return null; }
                var chunks = new List<string>();
                foreach (TypedValue value in record.Data)
                {
                    if (value.TypeCode != (int)DxfCode.Text) return null;
                    chunks.Add(value.Value?.ToString() ?? string.Empty);
                }

                transaction.Commit();
                return chunks.Count == 0 ? null : chunks;
            }
            catch (System.Exception)
            {
                transaction.Abort();
                return null;
            }
        }
    }

    public Diagnostic? Save(Database database, List<string> chunks)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));
        if (chunks == null) throw new ArgumentNullException(nameof(chunks));
        if (chunks.Count == 0) throw new ArgumentException("设置不能是空的。", nameof(chunks));
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            try
            {
                var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
                Xrecord record;
                if (dictionary.Contains(DictionaryName))
                {
                    record = (Xrecord)transaction.GetObject(dictionary.GetAt(DictionaryName), OpenMode.ForWrite);
                }
                else
                {
                    record = new Xrecord();
                    dictionary.UpgradeOpen();
                    dictionary.SetAt(DictionaryName, record);
                    transaction.AddNewlyCreatedDBObject(record, true);
                }

                var data = new ResultBuffer();
                foreach (var chunk in chunks) data.Add(new TypedValue((int)DxfCode.Text, chunk));
                record.Data = data;
                data.Dispose();
                transaction.Commit();
                return null;
            }
            catch (System.Exception error)
            {
                return Problem("写入工程设置失败：" + (error is Autodesk.AutoCAD.Runtime.Exception acad ? acad.ErrorStatus + " " + acad.Message : error.GetType().Name + " " + error.Message) + "。本次没有写入。");
            }
        }
    }

    public Diagnostic? Clear(Database database)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            try
            {
                var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (dictionary.Contains(DictionaryName))
                {
                    var id = dictionary.GetAt(DictionaryName);
                    dictionary.UpgradeOpen();
                    dictionary.Remove(DictionaryName);
                    var record = transaction.GetObject(id, OpenMode.ForWrite);
                    record.Erase();
                }

                transaction.Commit();
                return null;
            }
            catch (System.Exception error)
            {
                return Problem("清除工程设置失败：" + (error is Autodesk.AutoCAD.Runtime.Exception acad ? acad.ErrorStatus + " " + acad.Message : error.GetType().Name + " " + error.Message) + "。");
            }
        }
    }

    private static bool TryOpenRecord(Database database, Transaction transaction, out Xrecord record)
    {
        record = new Xrecord();
        var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!dictionary.Contains(DictionaryName)) return false;
        var id = dictionary.GetAt(DictionaryName);
        if (id.IsNull) return false;
        if (transaction.GetObject(id, OpenMode.ForRead) is not Xrecord found) return false;
        record = found;
        return true;
    }

    private static Diagnostic Problem(string message)
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.ECadEnv,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Render,
            Message = message
        };
    }
}
