using System.Collections.Generic;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

// 只生成十进制可见编号。省略的 lvlRestart 在遇到更外层编号时重置内层；
// lvlRestart=0 表示该级不重置。起始值省略时按 OOXML 取 0，Word 通常会写出 1。
internal sealed class NumberingSession
{
    private readonly Dictionary<int, NumDef> _definitions;
    private readonly Dictionary<int, Counter> _counters = new Dictionary<int, Counter>();

    private NumberingSession(Dictionary<int, NumDef> definitions)
    {
        _definitions = definitions;
    }

    public static NumberingSession Load(MainDocumentPart? main)
    {
        var definitions = new Dictionary<int, NumDef>();
        var numbering = main?.NumberingDefinitionsPart?.Numbering;
        if (numbering == null) return new NumberingSession(definitions);

        var abstracts = new Dictionary<int, AbstractDef>();
        foreach (var abs in numbering.Elements<AbstractNum>())
        {
            var id = IntOf(abs.AbstractNumberId);
            if (id == null) continue;
            var levels = new Dictionary<int, LevelDef>();
            foreach (var level in abs.Elements<Level>())
            {
                var ilvl = IntOf(level.LevelIndex);
                if (ilvl == null) continue;
                levels[ilvl.Value] = ReadLevel(level, null);
            }

            abstracts[id.Value] = new AbstractDef(levels, abs.StyleLink != null || abs.NumberingStyleLink != null);
        }

        foreach (var instance in numbering.Elements<NumberingInstance>())
        {
            var numId = IntOf(instance.NumberID);
            var abstractId = IntOf(instance.AbstractNumId?.Val);
            if (numId == null || abstractId == null || !abstracts.TryGetValue(abstractId.Value, out var abs)) continue;
            var levels = new Dictionary<int, LevelDef>(abs.Levels);
            foreach (var ov in instance.Elements<LevelOverride>())
            {
                var ilvl = IntOf(ov.LevelIndex);
                if (ilvl == null) continue;
                var startOverride = IntOf(ov.StartOverrideNumberingValue?.Val);
                if (ov.Level != null)
                    levels[ilvl.Value] = ReadLevel(ov.Level, startOverride);
                else if (startOverride != null && levels.TryGetValue(ilvl.Value, out var existing))
                    levels[ilvl.Value] = existing.WithStart(startOverride.Value);
            }

            definitions[numId.Value] = new NumDef(numId.Value, levels, abs.Linked && abs.Levels.Count == 0);
        }

        return new NumberingSession(definitions);
    }

    public bool TryNext(int numId, int ilvl, out string label, out string failure)
    {
        label = string.Empty;
        if (ilvl < 0 || ilvl > 8)
        {
            failure = "编号级别超出 0–8。";
            return false;
        }

        if (!_definitions.TryGetValue(numId, out var def))
        {
            failure = "找不到编号定义 numId=" + numId + "。请改为手工编号。";
            return false;
        }

        if (def.UnresolvedLink)
        {
            failure = "编号定义链接到其他样式，无法确定可见编号。请改为手工编号。";
            return false;
        }

        if (!def.Levels.TryGetValue(ilvl, out var level))
        {
            failure = "缺少编号级别 " + ilvl + " 的定义。";
            return false;
        }

        if (level.PictureBullet)
        {
            failure = "图片项目符号不受支持。请改为十进制编号或手工编号。";
            return false;
        }

        if (level.Format == null)
        {
            failure = "无法确定编号格式。请改为十进制编号或手工编号。";
            return false;
        }

        if (level.Format.Value != NumberFormatValues.Decimal)
        {
            failure = "编号格式 " + level.Format.Value + " 不受支持。请改为十进制编号或手工编号。";
            return false;
        }

        var levelText = level.Text;
        if (levelText == null || levelText.Length == 0 || levelText.IndexOf('\t') >= 0)
        {
            failure = "编号文字为空或含有制表符。请改为手工编号。";
            return false;
        }

        if (!ReferencedFormatsAreDecimal(def, levelText, out failure)) return false;

        var counter = CounterFor(def.Id);
        RestartDeeper(def, counter, ilvl);
        if (!counter.Started[ilvl])
        {
            counter.Values[ilvl] = level.Start;
            counter.Started[ilvl] = true;
        }
        else
        {
            counter.Values[ilvl]++;
        }

        return TryRender(def, counter, ilvl, out label, out failure);
    }

    private static bool ReferencedFormatsAreDecimal(NumDef def, string text, out string failure)
    {
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '%' || text[i + 1] < '1' || text[i + 1] > '9') continue;
            var refLevel = text[i + 1] - '1';
            if (!def.Levels.TryGetValue(refLevel, out var referenced))
            {
                failure = "编号文字引用了不存在的级别 %" + (refLevel + 1) + "。";
                return false;
            }

            if (referenced.Format != NumberFormatValues.Decimal)
            {
                failure = "编号级别 " + refLevel + " 的格式不是十进制。";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private static void RestartDeeper(NumDef def, Counter counter, int ilvl)
    {
        for (var i = ilvl + 1; i < 9; i++)
        {
            if (!def.Levels.TryGetValue(i, out var deeper))
            {
                counter.Started[i] = false;
                continue;
            }

            if (deeper.Restart == 0) continue;
            if (deeper.Restart is int trigger && trigger != ilvl + 1) continue;
            counter.Started[i] = false;
        }
    }

    private bool TryRender(NumDef def, Counter counter, int ilvl, out string label, out string failure)
    {
        var text = def.Levels[ilvl].Text ?? string.Empty;
        var builder = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 1 < text.Length && text[i + 1] >= '1' && text[i + 1] <= '9')
            {
                var refLevel = text[i + 1] - '1';
                int value;
                if (refLevel == ilvl || counter.Started[refLevel])
                {
                    value = counter.Values[refLevel];
                }
                else
                {
                    value = def.Levels[refLevel].Start;
                    if (refLevel < ilvl)
                    {
                        counter.Values[refLevel] = value;
                        counter.Started[refLevel] = true;
                    }
                }

                builder.Append(value);
                i++;
                continue;
            }

            builder.Append(text[i]);
        }

        label = builder.ToString();
        failure = string.Empty;
        return true;
    }

    private Counter CounterFor(int numId)
    {
        if (!_counters.TryGetValue(numId, out var counter))
        {
            counter = new Counter();
            _counters[numId] = counter;
        }

        return counter;
    }

    private static LevelDef ReadLevel(Level level, int? startOverride)
    {
        NumberFormatValues? format = null;
        if (level.NumberingFormat?.Val != null && level.NumberingFormat.Val.HasValue)
            format = level.NumberingFormat.Val.Value;
        var start = startOverride ?? IntOf(level.StartNumberingValue?.Val) ?? 0;
        return new LevelDef(format, level.LevelText?.Val?.Value, start, IntOf(level.LevelRestart?.Val), level.LevelPictureBulletId != null);
    }

    private static int? IntOf(DocumentFormat.OpenXml.Int32Value? value)
    {
        if (value == null || !value.HasValue) return null;
        return value.Value;
    }

    private sealed class AbstractDef
    {
        public AbstractDef(Dictionary<int, LevelDef> levels, bool linked)
        {
            Levels = levels;
            Linked = linked;
        }

        public Dictionary<int, LevelDef> Levels { get; }

        public bool Linked { get; }
    }

    private sealed class NumDef
    {
        public NumDef(int id, Dictionary<int, LevelDef> levels, bool unresolvedLink)
        {
            Id = id;
            Levels = levels;
            UnresolvedLink = unresolvedLink;
        }

        public int Id { get; }

        public Dictionary<int, LevelDef> Levels { get; }

        public bool UnresolvedLink { get; }
    }

    private sealed class LevelDef
    {
        public LevelDef(NumberFormatValues? format, string? text, int start, int? restart, bool pictureBullet)
        {
            Format = format;
            Text = text;
            Start = start;
            Restart = restart;
            PictureBullet = pictureBullet;
        }

        public NumberFormatValues? Format { get; }

        public string? Text { get; }

        public int Start { get; }

        public int? Restart { get; }

        public bool PictureBullet { get; }

        public LevelDef WithStart(int start)
        {
            return new LevelDef(Format, Text, start, Restart, PictureBullet);
        }
    }

    private sealed class Counter
    {
        public int[] Values { get; } = new int[9];

        public bool[] Started { get; } = new bool[9];
    }
}
