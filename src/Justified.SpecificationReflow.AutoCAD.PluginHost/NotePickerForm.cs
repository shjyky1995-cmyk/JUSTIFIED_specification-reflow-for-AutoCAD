using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// 本次生成的选择只由用户确认。目录和文档沿用上次值，但不按文件名推断图幅或单位。
internal sealed class NotePickerForm : Form
{
    private readonly TextBox _root = new TextBox();
    private readonly ComboBox _paper = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _template = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _scale = new TextBox();
    private readonly TextBox _docx = new TextBox();
    private readonly Label _status = new Label();
    private readonly Button _generate = new Button();
    private List<TemplateSummary> _available = new List<TemplateSummary>();
    private readonly NoteSettings? _previous;

    public NotePickerForm(string defaultRoot, NoteSettings? previous)
    {
        _previous = previous;
        Text = "导入 Word 说明";
        Width = 620;
        Height = 335;
        MinimumSize = new System.Drawing.Size(620, 335);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        AddLabel("标准包", 18);
        Place(_root, 96, 15, 410);
        _root.Text = previous?.StandardRoot ?? defaultRoot;
        AddButton("浏览…", 514, 14, () => BrowseFolder(_root));

        AddLabel("图幅", 55);
        Place(_paper, 96, 52, 180);
        _paper.SelectedIndexChanged += (_, _) => FillTemplates();

        AddLabel("模板版本", 92);
        Place(_template, 96, 89, 480);

        AddLabel("单位比例", 129);
        Place(_scale, 96, 126, 180);
        _scale.Text = previous == null ? string.Empty : previous.UnitScale.ToString("G17", CultureInfo.InvariantCulture);
        var hint = new Label { Text = "1 个标准毫米对应的图形单位数；不确定时先核对图纸", Left = 286, Top = 132, Width = 300 };
        Controls.Add(hint);

        AddLabel("Word 文件", 166);
        Place(_docx, 96, 163, 410);
        _docx.Text = previous?.DocumentPath ?? string.Empty;
        AddButton("浏览…", 514, 162, BrowseDocx);

        _status.Left = 18;
        _status.Top = 205;
        _status.Width = 560;
        _status.Height = 45;
        Controls.Add(_status);

        _generate.Text = "确认并点选位置";
        _generate.Left = 386;
        _generate.Top = 258;
        _generate.Width = 126;
        _generate.Click += (_, _) => Confirm();
        Controls.Add(_generate);
        var cancel = new Button { Text = "取消", Left = 520, Top = 258, Width = 60, DialogResult = DialogResult.Cancel };
        Controls.Add(cancel);
        CancelButton = cancel;
        AcceptButton = _generate;

        _root.Leave += (_, _) => RefreshTemplates();
        RefreshTemplates();
    }

    public string StandardRoot => _root.Text.Trim().Trim('"');
    public string DocumentPath => _docx.Text.Trim().Trim('"');
    public string? ReportDirectory => _previous?.ReportDirectory;
    public double UnitScale { get; private set; }
    public TemplateSummary? SelectedTemplate => (_template.SelectedItem as TemplateChoice)?.Template;

    private void RefreshTemplates()
    {
        _paper.Items.Clear();
        _template.Items.Clear();
        _available.Clear();
        _generate.Enabled = false;
        var root = StandardRoot;
        if (!Directory.Exists(root))
        {
            _status.Text = "找不到标准包。请使用已发布标准包所在目录。";
            return;
        }

        try
        {
            var result = new DirectoryPackageCatalog(root, allowTestFixtures: false)
                .ListTemplates(System.Threading.CancellationToken.None);
            _available = result.Templates.Where(item =>
                string.Equals(item.Classification, "production", StringComparison.OrdinalIgnoreCase)
                && item.Calibrated && !string.IsNullOrWhiteSpace(item.PaperCode)
                && !string.IsNullOrWhiteSpace(item.TemplateId) && !string.IsNullOrWhiteSpace(item.Version)).ToList();
            foreach (var paper in _available.Select(item => item.PaperCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
                _paper.Items.Add(paper);
            if (_paper.Items.Count == 0)
            {
                _status.Text = "该目录没有可用的已发布模板。";
                return;
            }
            var savedPaper = _previous?.PaperCode;
            var savedIndex = savedPaper == null ? -1 : _paper.FindStringExact(savedPaper);
            _paper.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;
            _status.Text = "每次生成均可重新选择图幅、模板版本和 Word 文件。";
            _generate.Enabled = true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _status.Text = "读取标准包失败：" + error.Message;
        }
    }

    private void FillTemplates()
    {
        _template.Items.Clear();
        var paper = _paper.SelectedItem as string;
        if (paper == null) return;
        foreach (var item in _available.Where(item => string.Equals(item.PaperCode, paper, StringComparison.OrdinalIgnoreCase)))
            _template.Items.Add(new TemplateChoice(item));
        var prior = _previous == null ? -1 : Enumerable.Range(0, _template.Items.Count).Where(index =>
            _template.Items[index] is TemplateChoice choice
            && choice.Template.TemplateId == _previous.TemplateId
            && choice.Template.Version == _previous.TemplateVersion).DefaultIfEmpty(-1).First();
        if (_template.Items.Count > 0) _template.SelectedIndex = prior >= 0 ? prior : 0;
    }

    private void Confirm()
    {
        if (SelectedTemplate == null)
        {
            _status.Text = "请先选择模板版本。";
            return;
        }
        if (!double.TryParse(_scale.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            _status.Text = "请填写已确认的单位比例，例如 1。";
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

    private void BrowseFolder(TextBox target)
    {
        using var picker = new FolderBrowserDialog { Description = "选择已发布标准包的根目录" };
        if (Directory.Exists(target.Text)) picker.SelectedPath = target.Text;
        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = picker.SelectedPath;
            RefreshTemplates();
        }
    }

    private void AddLabel(string text, int top)
    {
        Controls.Add(new Label { Text = text, Left = 18, Top = top + 5, Width = 76 });
    }

    private void Place(Control control, int left, int top, int width)
    {
        control.Left = left;
        control.Top = top;
        control.Width = width;
        Controls.Add(control);
    }

    private void AddButton(string text, int left, int top, Action click)
    {
        var button = new Button { Text = text, Left = left, Top = top, Width = 65 };
        button.Click += (_, _) => click();
        Controls.Add(button);
    }

    private sealed class TemplateChoice
    {
        public TemplateChoice(TemplateSummary template) => Template = template;
        public TemplateSummary Template { get; }
        public override string ToString() => Template.TemplateId + "  v" + Template.Version
            + (string.IsNullOrWhiteSpace(Template.DisciplineCode) ? string.Empty : "  " + Template.DisciplineCode);
    }
}
