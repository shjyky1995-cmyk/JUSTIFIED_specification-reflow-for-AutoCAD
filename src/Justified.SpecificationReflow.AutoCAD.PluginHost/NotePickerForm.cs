using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;

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
    private readonly Label _status = new Label();
    private readonly Button _continue = new Button();
    private readonly Button _rootButton = new Button();
    private readonly List<PaperCard> _cards = new List<PaperCard>();
    private List<TemplateSummary> _available = new List<TemplateSummary>();
    private readonly NoteSettings? _previous;
    private string? _selectedPaper;

    public NotePickerForm(string defaultRoot, NoteSettings? previous)
    {
        _previous = previous;
        Text = "导入说明";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1000, 690);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Regular, GraphicsUnit.Pixel);
        BackColor = Color.White;
        ForeColor = Ink;
        DoubleBuffered = true;

        var logo = LoadLogo();
        if (logo != null)
            Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Left = 38, Top = 20, Width = 70, Height = 75 });
        Label("导入说明", 145, 24, 240, 45, 30F, true);
        Label("选择文件与图幅", 145, 72, 300, 31, 21F, false).ForeColor = Muted;
        var close = AddButton("×", 922, 18, 54, 54, () => { DialogResult = DialogResult.Cancel; Close(); });
        close.Font = new Font(Font.FontFamily, 34F, FontStyle.Regular, GraphicsUnit.Pixel);
        close.ForeColor = Muted;
        close.BackColor = Color.White;
        Divider(118);

        Section("Word 文件", 165);
        Box(215, 144, 742, 80);
        var wordIcon = Label("W", 238, 160, 43, 47, 25F, true);
        wordIcon.ForeColor = Color.White;
        wordIcon.BackColor = Color.FromArgb(40, 103, 191);
        wordIcon.TextAlign = ContentAlignment.MiddleCenter;
        _docx.SetBounds(300, 168, 520, 39);
        _docx.Text = previous?.DocumentPath ?? string.Empty;
        _docx.BorderStyle = BorderStyle.None;
        _docx.Font = new Font(Font.FontFamily, 21F, FontStyle.Regular, GraphicsUnit.Pixel);
        Controls.Add(_docx);
        AddButton("更换", 841, 159, 99, 50, BrowseDocx);

        Section("图幅", 271);
        var papers = new[] { ("A1", "3 列"), ("A2", "3 列"), ("A3", "2 列"), ("更多", "···") };
        for (var i = 0; i < papers.Length; i++)
        {
            var card = new PaperCard(papers[i].Item1, papers[i].Item2)
                { Left = 215 + i * 184, Top = 247, Width = 164, Height = 180 };
            card.Click += (_, _) => SelectPaper(card.Paper);
            _cards.Add(card);
            Controls.Add(card);
        }
        Divider(447, 40, 917);

        Section("单位比例", 485);
        Box(215, 469, 534, 64);
        _scale.SetBounds(232, 486, 496, 37);
        _scale.BorderStyle = BorderStyle.None;
        _scale.Font = new Font(Font.FontFamily, 21F, FontStyle.Regular, GraphicsUnit.Pixel);
        _scale.Text = previous == null ? string.Empty : previous.UnitScale.ToString("G17", CultureInfo.InvariantCulture);
        Controls.Add(_scale);
        Label("按图纸单位核对", 772, 487, 184, 31, 19F, false).ForeColor = Muted;

        _status.SetBounds(215, 548, 535, 26);
        _status.ForeColor = Muted;
        _status.Font = new Font(Font.FontFamily, 14F, FontStyle.Regular, GraphicsUnit.Pixel);
        Controls.Add(_status);
        _root.Text = previous?.StandardRoot ?? defaultRoot;
        _rootButton.Text = "选择模板目录";
        _rootButton.SetBounds(776, 544, 181, 27);
        _rootButton.FlatStyle = FlatStyle.Flat;
        _rootButton.FlatAppearance.BorderSize = 0;
        _rootButton.ForeColor = Blue;
        _rootButton.BackColor = Color.White;
        _rootButton.TextAlign = ContentAlignment.MiddleRight;
        _rootButton.Click += (_, _) => BrowseFolder();
        Controls.Add(_rootButton);

        Divider(584);
        Label("① 选择", 46, 610, 120, 42, 20F, true).ForeColor = Blue;
        Label("② 点位置", 176, 610, 145, 42, 20F, false).ForeColor = Muted;
        _continue.Text = "在图纸中点位置";
        _continue.SetBounds(570, 605, 279, 64);
        _continue.BackColor = Blue;
        _continue.ForeColor = Color.White;
        _continue.Font = new Font(Font.FontFamily, 22F, FontStyle.Bold, GraphicsUnit.Pixel);
        _continue.FlatStyle = FlatStyle.Flat;
        _continue.FlatAppearance.BorderSize = 0;
        _continue.Click += (_, _) => Confirm();
        Controls.Add(_continue);
        var cancel = AddButton("取消", 868, 605, 100, 64, () => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.BackColor = Color.White;
        cancel.ForeColor = Muted;
        CancelButton = cancel;
        AcceptButton = _continue;
        RefreshTemplates();
    }

    public string StandardRoot => _root.Text.Trim().Trim('"');
    public string DocumentPath => _docx.Text.Trim().Trim('"');
    public string? ReportDirectory => _previous?.ReportDirectory;
    public double UnitScale { get; private set; }
    public TemplateSummary? SelectedTemplate
    {
        get
        {
            var matches = _available.Where(item => string.Equals(item.PaperCode, _selectedPaper, StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
    }

    private void RefreshTemplates()
    {
        _available.Clear();
        _continue.Enabled = false;
        if (!Directory.Exists(StandardRoot))
        {
            _status.Text = "尚未找到模板，请选择模板目录。";
            _rootButton.Visible = true;
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
                _rootButton.Visible = true;
                UpdateCards();
                return;
            }
            var savedPaper = _previous?.PaperCode;
            _selectedPaper = _available.Any(item => string.Equals(item.PaperCode, savedPaper, StringComparison.OrdinalIgnoreCase))
                ? savedPaper : _available[0].PaperCode;
            UpdateCards();
            ValidateCurrentPaper();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _status.Text = "读取模板失败：" + error.Message;
            _rootButton.Visible = true;
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
        ValidateCurrentPaper();
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

    private void ValidateCurrentPaper()
    {
        var count = _available.Count(item => string.Equals(item.PaperCode, _selectedPaper, StringComparison.OrdinalIgnoreCase));
        _continue.Enabled = count == 1;
        _rootButton.Visible = count != 1;
        _status.Text = count == 1
            ? "核对图纸单位后，选择在图纸中的放置位置。"
            : "该图幅对应多份模板，请管理员保留唯一有效版本。";
    }

    private void Confirm()
    {
        if (SelectedTemplate == null) { _status.Text = "模板配置不唯一或不可用，请管理员检查。"; return; }
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

    private static Image? LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Justified.SpecificationReflow.AutoCAD.PluginHost.Assets.note-logo.png");
        if (stream == null) return null;
        using var original = Image.FromStream(stream);
        return new Bitmap(original);
    }

    private void Divider(int top, int left = 0, int width = 1000) =>
        Controls.Add(new Panel { Left = left, Top = top, Width = width, Height = 1, BackColor = Line });
    private void Box(int left, int top, int width, int height) =>
        Controls.Add(new Panel { Left = left, Top = top, Width = width, Height = height,
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle });
    private void Section(string text, int top) => Label(text, 40, top, 160, 35, 22F, true);
    private Label Label(string text, int left, int top, int width, int height, float size, bool bold)
    {
        var label = new Label { Text = text, Left = left, Top = top, Width = width, Height = height,
            Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel) };
        Controls.Add(label);
        return label;
    }
    private Button AddButton(string text, int left, int top, int width, int height, Action click)
    {
        var button = new Button { Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = Color.FromArgb(245, 248, 253), ForeColor = Blue, FlatStyle = FlatStyle.Flat,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold, GraphicsUnit.Pixel) };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => click();
        Controls.Add(button);
        return button;
    }

    private sealed class PaperCard : Panel
    {
        public PaperCard(string paper, string detail)
        {
            Paper = paper;
            Detail = detail;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = paper + " " + detail;
        }
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
                        g.DrawRectangle(pen, 61 + column * 21, 33 + row * 21, 16, 16);
            }
            else
            {
                using var pen = new Pen(color, 1.5F);
                g.DrawRectangle(pen, 57, 28, 52, 61);
                var columns = Paper == "A3" ? 2 : 3;
                for (var i = 1; i < columns; i++)
                    g.DrawLine(pen, 57 + i * 52 / columns, 34, 57 + i * 52 / columns, 83);
                if (Selected)
                {
                    using var dot = new SolidBrush(Blue);
                    g.FillEllipse(dot, 133, 14, 22, 22);
                    using var tick = new Pen(Color.White, 2F);
                    g.DrawLines(tick, new[] { new Point(137, 24), new Point(142, 29), new Point(150, 19) });
                }
            }
            using var mainFont = new Font("Microsoft YaHei UI", 25F, FontStyle.Bold, GraphicsUnit.Pixel);
            using var detailFont = new Font("Microsoft YaHei UI", 18F, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, Paper, mainFont,
                new Rectangle(4, 111, Width - 8, 34), color, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Detail, detailFont,
                new Rectangle(4, 144, Width - 8, 28), Muted, TextFormatFlags.HorizontalCenter);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
