using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// 本次选择只由用户确认；上次选择仅预填，不推断图幅、标准或单位。
internal sealed class NotePickerForm : Form
{
    private static readonly Color Ink = Color.FromArgb(26, 34, 51);
    private static readonly Color Muted = Color.FromArgb(99, 111, 133);
    private static readonly Color Blue = Color.FromArgb(19, 101, 230);
    private static readonly Color Line = Color.FromArgb(210, 220, 235);
    private readonly TextBox _root = new TextBox();
    private readonly TextBox _docx = new TextBox();
    private readonly TextBox _scale = new TextBox();
    private readonly ComboBox _template = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _status = new Label();
    private readonly Button _continue = new Button();
    private readonly List<PaperCard> _cards = new List<PaperCard>();
    private List<TemplateSummary> _available = new List<TemplateSummary>();
    private readonly NoteSettings? _previous;
    private string? _selectedPaper;

    public NotePickerForm(string defaultRoot, NoteSettings? previous)
    {
        _previous = previous;
        Text = "导入说明";
        ClientSize = new Size(830, 634);
        MinimumSize = new Size(846, 673);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Microsoft YaHei UI", 10F);
        BackColor = Color.White;
        ForeColor = Ink;

        Label("导入说明", 30, 21, 220, 38, 20F, true);
        Label("选择文件与图幅", 30, 59, 250, 28, 11F, false).ForeColor = Muted;
        Label("◇", 753, 29, 40, 40, 24F, true).ForeColor = Blue; // 临时符号；正式标志待原始文件。
        Divider(99);

        Section("Word 文件", 130);
        _docx.SetBounds(185, 119, 475, 38);
        _docx.Text = previous?.DocumentPath ?? string.Empty;
        _docx.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_docx);
        AddButton("更换", 674, 119, 118, 38, BrowseDocx);

        Section("图幅", 190);
        var papers = new[] { ("A1", "3 列"), ("A2", "3 列"), ("A3", "2 列"), ("更多", "···") };
        for (var i = 0; i < papers.Length; i++)
        {
            var card = new PaperCard(papers[i].Item1, papers[i].Item2)
                { Left = 185 + i * 153, Top = 181, Width = 137, Height = 136 };
            card.Click += (_, _) => SelectPaper(card.Paper);
            _cards.Add(card);
            Controls.Add(card);
        }
        Divider(339);

        Section("模板版本", 368);
        _template.SetBounds(185, 357, 607, 40);
        _template.FlatStyle = FlatStyle.Flat;
        Controls.Add(_template);

        Section("单位比例", 431);
        _scale.SetBounds(185, 420, 438, 39);
        _scale.BorderStyle = BorderStyle.FixedSingle;
        _scale.Text = previous == null ? string.Empty : previous.UnitScale.ToString("G17", CultureInfo.InvariantCulture);
        Controls.Add(_scale);
        Label("按图纸单位核对", 646, 426, 146, 28, 10F, false).ForeColor = Muted;

        _status.SetBounds(185, 472, 607, 47);
        _status.ForeColor = Muted;
        Controls.Add(_status);
        _root.Text = previous?.StandardRoot ?? defaultRoot;
        var rootButton = AddButton("选择模板目录", 185, 501, 160, 28, BrowseFolder);
        rootButton.FlatAppearance.BorderSize = 0;
        rootButton.TextAlign = ContentAlignment.MiddleLeft;

        Divider(546);
        Label("① 选择", 32, 562, 120, 29, 10F, true).ForeColor = Blue;
        Label("② 点位置", 155, 562, 120, 29, 10F, false).ForeColor = Muted;
        _continue.Text = "在图纸中点位置";
        _continue.SetBounds(480, 559, 221, 51);
        _continue.BackColor = Blue;
        _continue.ForeColor = Color.White;
        _continue.FlatStyle = FlatStyle.Flat;
        _continue.FlatAppearance.BorderSize = 0;
        _continue.Click += (_, _) => Confirm();
        Controls.Add(_continue);
        var cancel = AddButton("取消", 710, 559, 82, 51, () => { DialogResult = DialogResult.Cancel; Close(); });
        CancelButton = cancel;
        AcceptButton = _continue;
        RefreshTemplates();
    }

    public string StandardRoot => _root.Text.Trim().Trim('"');
    public string DocumentPath => _docx.Text.Trim().Trim('"');
    public string? ReportDirectory => _previous?.ReportDirectory;
    public double UnitScale { get; private set; }
    public TemplateSummary? SelectedTemplate => (_template.SelectedItem as TemplateChoice)?.Template;

    private void RefreshTemplates()
    {
        _available.Clear();
        _template.Items.Clear();
        _continue.Enabled = false;
        if (!Directory.Exists(StandardRoot))
        {
            _status.Text = "尚未找到模板目录。请选择本机测试包中的 local-test-package。";
            UpdateCards();
            return;
        }
        try
        {
            var result = new DirectoryPackageCatalog(StandardRoot, allowTestFixtures: false)
                .ListTemplates(System.Threading.CancellationToken.None);
            _available = result.Templates.Where(item =>
                string.Equals(item.Classification, "production", StringComparison.OrdinalIgnoreCase)
                && item.Calibrated && !string.IsNullOrWhiteSpace(item.PaperCode)
                && (item.PaperCode == "A1" || item.PaperCode == "A2" || item.PaperCode == "A3")
                && !string.IsNullOrWhiteSpace(item.TemplateId) && !string.IsNullOrWhiteSpace(item.Version)).ToList();
            if (_available.Count == 0)
            {
                _status.Text = "该目录没有可用的已发布模板；请选择正确的模板目录。";
                UpdateCards();
                return;
            }
            var savedPaper = _previous?.PaperCode;
            _selectedPaper = _available.Any(item => string.Equals(item.PaperCode, savedPaper, StringComparison.OrdinalIgnoreCase))
                ? savedPaper : _available[0].PaperCode;
            UpdateCards();
            FillTemplates();
            _status.Text = "核对图纸单位后，选择在图纸中的放置位置。";
            _continue.Enabled = true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _status.Text = "读取模板失败：" + error.Message;
            UpdateCards();
        }
    }

    private void SelectPaper(string paper)
    {
        if (paper == "更多")
        {
            var extra = _available.Select(item => item.PaperCode).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(item => item != "A1" && item != "A2" && item != "A3").ToList();
            if (extra.Count == 0) { _status.Text = "当前没有其他已发布图幅。"; return; }
            var menu = new ContextMenuStrip();
            foreach (var item in extra)
            {
                var code = item;
                menu.Items.Add(item, null, (_, _) => SelectPaper(code));
            }
            menu.Show(_cards[3], new Point(0, _cards[3].Height));
            return;
        }
        if (!_available.Any(item => string.Equals(item.PaperCode, paper, StringComparison.OrdinalIgnoreCase)))
        {
            _status.Text = paper + " 尚无可用模板。";
            return;
        }
        _selectedPaper = paper;
        UpdateCards();
        FillTemplates();
    }

    private void UpdateCards()
    {
        foreach (var card in _cards)
        {
            card.Selected = string.Equals(card.Paper, _selectedPaper, StringComparison.OrdinalIgnoreCase);
            card.Available = card.Paper == "更多" || _available.Any(item =>
                string.Equals(item.PaperCode, card.Paper, StringComparison.OrdinalIgnoreCase));
            card.Invalidate();
        }
    }

    private void FillTemplates()
    {
        _template.Items.Clear();
        foreach (var item in _available.Where(item => string.Equals(item.PaperCode, _selectedPaper, StringComparison.OrdinalIgnoreCase)))
            _template.Items.Add(new TemplateChoice(item));
        var prior = _previous == null ? -1 : Enumerable.Range(0, _template.Items.Count).Where(index =>
            _template.Items[index] is TemplateChoice choice
            && choice.Template.TemplateId == _previous.TemplateId
            && choice.Template.Version == _previous.TemplateVersion).DefaultIfEmpty(-1).First();
        if (_template.Items.Count > 0) _template.SelectedIndex = prior >= 0 ? prior : 0;
    }

    private void Confirm()
    {
        if (SelectedTemplate == null) { _status.Text = "请先选择模板版本。"; return; }
        if (!double.TryParse(_scale.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            _status.Text = "请填写已核对的单位比例，例如 1 图形单位 = 1 mm 时填 1。";
            _scale.Focus();
            return;
        }
        if (!File.Exists(DocumentPath) || !string.Equals(Path.GetExtension(DocumentPath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            _status.Text = "请选择本机存在的 .docx 文件。";
            _docx.Focus();
            return;
        }
        UnitScale = value;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseDocx()
    {
        using var picker = new OpenFileDialog { Filter = "Word 文档 (*.docx)|*.docx", CheckFileExists = true, Title = "选择本次说明 Word 文档" };
        if (File.Exists(DocumentPath)) picker.FileName = DocumentPath;
        if (picker.ShowDialog(this) == DialogResult.OK) _docx.Text = picker.FileName;
    }

    private void BrowseFolder()
    {
        using var picker = new FolderBrowserDialog { Description = "选择包含 standards 与 templates 的模板目录" };
        if (Directory.Exists(StandardRoot)) picker.SelectedPath = StandardRoot;
        if (picker.ShowDialog(this) == DialogResult.OK) { _root.Text = picker.SelectedPath; RefreshTemplates(); }
    }

    private void Divider(int top) => Controls.Add(new Panel { Left = 0, Top = top, Width = 830, Height = 1, BackColor = Line });
    private void Section(string text, int top) => Label(text, 30, top, 145, 30, 11F, true);
    private Label Label(string text, int left, int top, int width, int height, float size, bool bold)
    {
        var label = new Label { Text = text, Left = left, Top = top, Width = width, Height = height,
            Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular) };
        Controls.Add(label);
        return label;
    }
    private Button AddButton(string text, int left, int top, int width, int height, Action click)
    {
        var button = new Button { Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = Color.FromArgb(245, 248, 253), ForeColor = Blue, FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => click();
        Controls.Add(button);
        return button;
    }

    private sealed class TemplateChoice
    {
        private readonly string _source;
        public TemplateChoice(TemplateSummary template)
        {
            Template = template;
            try
            {
                var standard = JObject.Parse(File.ReadAllText(template.FilePath))["standardRef"];
                var id = standard?["id"]?.ToString() ?? string.Empty;
                _source = string.IsNullOrWhiteSpace(id) ? string.Empty : id + " v" + (standard?["version"]?.ToString() ?? string.Empty);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is Newtonsoft.Json.JsonException)
            { _source = string.Empty; }
        }
        public TemplateSummary Template { get; }
        public override string ToString()
        {
            return Template.TemplateId + " · v" + Template.Version
                + (string.IsNullOrWhiteSpace(_source) ? string.Empty : " · " + _source);
        }
    }

    private sealed class PaperCard : Panel
    {
        public PaperCard(string paper, string detail) { Paper = paper; Detail = detail; Cursor = Cursors.Hand; DoubleBuffered = true; }
        public string Paper { get; }
        public string Detail { get; }
        public bool Selected { get; set; }
        public bool Available { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Selected ? Color.FromArgb(242, 248, 255) : Color.White);
            using (var border = new Pen(Selected ? Blue : Line, Selected ? 2F : 1F))
                g.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
            var color = Available ? Ink : Muted;
            if (Paper == "更多")
            {
                using var pen = new Pen(color, 2F);
                for (var row = 0; row < 2; row++)
                    for (var column = 0; column < 2; column++)
                        g.DrawRectangle(pen, 49 + column * 21, 24 + row * 21, 16, 16);
            }
            else
            {
                using var pen = new Pen(color, 1.5F);
                g.DrawRectangle(pen, 49, 20, 43, 48);
                var columns = Paper == "A3" ? 2 : 3;
                for (var i = 1; i < columns; i++)
                    g.DrawLine(pen, 49 + i * 43 / columns, 25, 49 + i * 43 / columns, 63);
                if (Selected)
                {
                    using var dot = new SolidBrush(Blue);
                    g.FillEllipse(dot, 110, 8, 18, 18);
                    using var tick = new Pen(Color.White, 2F);
                    g.DrawLines(tick, new[] { new Point(114, 17), new Point(118, 21), new Point(124, 13) });
                }
            }
            TextRenderer.DrawText(g, Paper, new Font(Font.FontFamily, 13F, FontStyle.Bold),
                new Rectangle(4, 77, Width - 8, 26), color, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Detail, new Font(Font.FontFamily, 10F),
                new Rectangle(4, 107, Width - 8, 22), Muted, TextFormatFlags.HorizontalCenter);
        }
    }
}
