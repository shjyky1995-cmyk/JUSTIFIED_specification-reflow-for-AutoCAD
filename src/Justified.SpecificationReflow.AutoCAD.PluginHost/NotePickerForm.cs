using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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
    private static readonly Color CardSelected = Color.FromArgb(242, 248, 255);
    // 从插件包加载字体，不依赖使用者电脑是否安装 Noto Sans SC。
    private static readonly PrivateFontCollection BundledFonts = LoadBundledFonts();
    private static readonly FontFamily UiFontFamily = BundledFonts.Families.First(family =>
        string.Equals(family.Name, "Noto Sans SC", StringComparison.Ordinal));
    private readonly string _root;
    private readonly TextBox _docx = new TextBox();
    private readonly TextBox _scale = new TextBox();
    private readonly Label _status = new Label();
    private readonly Button _continue = new Button();
    private readonly List<PaperCard> _cards = new List<PaperCard>();
    private List<TemplateSummary> _available = new List<TemplateSummary>();
    private readonly NoteSettings? _previous;
    private string? _selectedPaper;
    private Point _dragCursor;
    private Point _dragWindow;

    public NotePickerForm(string defaultRoot, NoteSettings? previous)
    {
        _previous = previous;
        _root = defaultRoot;
        Text = "导入说明";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(700, 400);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font(UiFontFamily, 14F, FontStyle.Regular, GraphicsUnit.Pixel);
        BackColor = Color.White;
        ForeColor = Ink;
        DoubleBuffered = true;
        ApplyRoundedRegion();
        EnableDragging(this);

        var logo = LoadLogo();
        if (logo != null)
        {
            var logoBox = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Left = 22, Top = 10, Width = 40, Height = 42 };
            Controls.Add(logoBox);
            EnableDragging(logoBox);
        }
        EnableDragging(Label("导入说明", 78, 8, 300, 26, 20F, true));
        var subtitle = Label("选择文件与图幅", 78, 32, 300, 18, 12F, false);
        subtitle.ForeColor = Muted;
        EnableDragging(subtitle);
        var close = AddButton("×", 640, 10, 30, 30, () => { DialogResult = DialogResult.Cancel; Close(); });
        close.Font = new Font(Font.FontFamily, 18F, FontStyle.Regular, GraphicsUnit.Pixel);
        close.ForeColor = Muted;
        close.BackColor = Color.White;
        Divider(60);

        Section("Word 文件", 72);
        var wordBox = Box(130, 64, 540, 46);
        var wordIcon = Label("W", 14, 7, 26, 32, 14F, true, wordBox);
        wordIcon.ForeColor = Color.White;
        wordIcon.BackColor = Color.FromArgb(40, 103, 191);
        wordIcon.TextAlign = ContentAlignment.MiddleCenter;
        _docx.SetBounds(48, 10, 420, 24);
        _docx.Text = previous?.DocumentPath ?? string.Empty;
        _docx.BorderStyle = BorderStyle.None;
        _docx.Font = new Font(Font.FontFamily, 14F, FontStyle.Regular, GraphicsUnit.Pixel);
        wordBox.Controls.Add(_docx);
        AddButton("更换", 552, 6, 100, 34, BrowseDocx, wordBox);

        Section("图幅", 130);
        var papers = new[] { ("A1", "3 列"), ("A2", "3 列"), ("A3", "2 列"), ("更多", "···") };
        for (var i = 0; i < papers.Length; i++)
        {
            var card = new PaperCard(papers[i].Item1, papers[i].Item2)
                { Left = 130 + i * 138, Top = 120, Width = 126, Height = 94 };
            card.Click += (_, _) => SelectPaper(card.Paper);
            _cards.Add(card);
            Controls.Add(card);
        }
        Divider(230, 22, 648);

        Section("单位比例", 240);
        var scaleBox = Box(130, 232, 260, 40);
        _scale.SetBounds(14, 8, 230, 24);
        _scale.BorderStyle = BorderStyle.None;
        _scale.Font = new Font(Font.FontFamily, 14F, FontStyle.Regular, GraphicsUnit.Pixel);
        _scale.Text = previous == null ? string.Empty : previous.UnitScale.ToString("G17", CultureInfo.InvariantCulture);
        scaleBox.Controls.Add(_scale);
        var presets = new[] { "1", "10", "100", "1000" };
        for (var i = 0; i < presets.Length; i++)
        {
            var preset = presets[i];
            var chip = AddButton(preset + "×", 402 + i * 69, 239, 59, 26, () => _scale.Text = preset);
            chip.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        var hint = Label("按图纸单位核对：1:1 填 1，1:100 填 100", 130, 278, 400, 18, 11F, false);
        hint.ForeColor = Muted;

        _status.SetBounds(130, 298, 540, 18);
        _status.ForeColor = Muted;
        _status.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Pixel);
        Controls.Add(_status);
        Divider(328);
        Label("① 选择", 22, 338, 70, 24, 13F, true).ForeColor = Blue;
        Label("② 点位置", 104, 338, 90, 24, 13F, false).ForeColor = Muted;
        _continue.Text = "在图纸中点位置";
        _continue.SetBounds(380, 332, 200, 40);
        _continue.BackColor = Blue;
        _continue.ForeColor = Color.White;
        _continue.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold, GraphicsUnit.Pixel);
        _continue.FlatStyle = FlatStyle.Flat;
        _continue.FlatAppearance.BorderSize = 0;
        _continue.Click += (_, _) => Confirm();
        Controls.Add(_continue);
        var cancel = AddButton("取消", 592, 332, 78, 40, () => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.BackColor = Color.White;
        cancel.ForeColor = Muted;
        CancelButton = cancel;
        AcceptButton = _continue;
        RefreshTemplates();
    }

    private void ApplyRoundedRegion()
    {
        var radius = 10;
        var bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    public string StandardRoot => _root;
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
            _status.Text = "内置模板缺失，请联系管理员安装模板。";
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
                _status.Text = "内置模板不可用，请联系管理员检查。";
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
        _status.Text = count == 1 ? string.Empty : "该图幅对应多份模板，请管理员保留唯一有效版本。";
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

    private static Image? LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Justified.SpecificationReflow.AutoCAD.PluginHost.Assets.note-logo.png");
        if (stream == null) return null;
        using var original = Image.FromStream(stream);
        return new Bitmap(original);
    }

    private static PrivateFontCollection LoadBundledFonts()
    {
        var directory = Path.GetDirectoryName(typeof(NotePickerForm).Assembly.Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(directory, "fonts", "NotoSansSC-VF.ttf");
        if (!File.Exists(path)) throw new FileNotFoundException("缺少程序界面字体 Noto Sans SC。", path);
        var fonts = new PrivateFontCollection();
        fonts.AddFontFile(path);
        if (!fonts.Families.Any(family => string.Equals(family.Name, "Noto Sans SC", StringComparison.Ordinal)))
            throw new InvalidDataException("程序界面字体文件不是预期的 Noto Sans SC。");
        return fonts;
    }

    private void EnableDragging(Control target)
    {
        target.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragCursor = Cursor.Position;
            _dragWindow = Location;
        };
        target.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            var cursor = Cursor.Position;
            Location = new Point(_dragWindow.X + cursor.X - _dragCursor.X,
                _dragWindow.Y + cursor.Y - _dragCursor.Y);
        };
    }

    private void Divider(int top, int left = 0, int width = 700) =>
        Controls.Add(new Panel { Left = left, Top = top, Width = width, Height = 1, BackColor = Line });
    private Panel Box(int left, int top, int width, int height)
    {
        var panel = new RoundedPanel
        {
            Left = left, Top = top, Width = width, Height = height,
            BackColor = Color.White, Radius = 8
        };
        Controls.Add(panel);
        return panel;
    }
    private void Section(string text, int top) => Label(text, 22, top, 125, 24, 14F, true);
    private Label Label(string text, int left, int top, int width, int height, float size, bool bold, Control? parent = null)
    {
        var label = new Label { Text = text, Left = left, Top = top, Width = width, Height = height,
            Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel) };
        (parent ?? this).Controls.Add(label);
        return label;
    }
    private Button AddButton(string text, int left, int top, int width, int height, Action click, Control? parent = null)
    {
        var button = new Button
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = Color.FromArgb(245, 248, 253), ForeColor = Blue, FlatStyle = FlatStyle.Flat,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold, GraphicsUnit.Pixel)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => click();
        (parent ?? this).Controls.Add(button);
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
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不铺直角底色，圆角由 OnPaint 的圆角路径填充。
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fillPath = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8);
            using (var fill = new SolidBrush(Color.White))
                g.FillPath(fill, fillPath);
            using (var border = new Pen(Selected ? Blue : Line, Selected ? 2F : 1F))
                g.DrawPath(border, fillPath);
            var color = Available ? Ink : Muted;
            if (Paper == "更多")
            {
                using var pen = new Pen(color, 2F);
                for (var row = 0; row < 2; row++)
                    for (var column = 0; column < 2; column++)
                        g.DrawRectangle(pen, 46 + column * 18, 10 + row * 18, 14, 14);
            }
            else
            {
                using var pen = new Pen(color, 1.5F);
                g.DrawRectangle(pen, 42, 12, 42, 36);
                var columns = Paper == "A3" ? 2 : 3;
                for (var i = 1; i < columns; i++)
                    g.DrawLine(pen, 42 + i * 42 / columns, 16, 42 + i * 42 / columns, 44);
                if (Selected)
                {
                    using var dot = new SolidBrush(Blue);
                    g.FillEllipse(dot, 100, 6, 18, 18);
                    using var tick = new Pen(Color.White, 2F);
                    g.DrawLines(tick, new[] { new Point(104, 13), new Point(109, 18), new Point(117, 8) });
                }
            }
            using var mainFont = new Font(UiFontFamily, 16F, FontStyle.Bold, GraphicsUnit.Pixel);
            using var detailFont = new Font(UiFontFamily, 11F, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, Paper, mainFont,
                new Rectangle(4, 54, Width - 8, 22), color, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Detail, detailFont,
                new Rectangle(4, 80, Width - 8, 16), Muted, TextFormatFlags.HorizontalCenter);
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

    private sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 8;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不铺直角底色，圆角外的四角透出窗体白色。
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            using (var fill = new SolidBrush(BackColor))
                e.Graphics.FillPath(fill, path);
            using var pen = new Pen(Line, 1f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        if (diameter > bounds.Width) diameter = bounds.Width;
        if (diameter > bounds.Height) diameter = bounds.Height;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
