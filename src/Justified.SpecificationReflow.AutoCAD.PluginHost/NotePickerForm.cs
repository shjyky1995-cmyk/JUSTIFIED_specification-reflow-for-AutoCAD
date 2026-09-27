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
    private static readonly Color Ink = Color.FromArgb(17, 24, 39);
    private static readonly Color Muted = Color.FromArgb(107, 114, 128);
    private static readonly Color Blue = Color.FromArgb(49, 88, 255);
    private static readonly Color BlueTint = Color.FromArgb(239, 246, 255);
    private static readonly Color Line = Color.FromArgb(225, 229, 235);
    private static readonly Color Footer = Color.FromArgb(249, 250, 251);
    private static readonly Color Bad = Color.FromArgb(190, 48, 43);
    private static readonly PrivateFontCollection BundledFonts = LoadBundledFonts();
    private static readonly FontFamily UiFontFamily = BundledFonts.Families.First(family =>
        string.Equals(family.Name, "Noto Sans SC", StringComparison.Ordinal));

    private readonly string _root;
    private readonly NoteSettings? _previous;
    private readonly Label _docxDisplay = new Label();
    private readonly ToolTip _docxTip = new ToolTip();
    private readonly Label _status = new Label();
    private readonly Button _continue;
    private readonly Button _otherScale;
    private readonly List<PaperCard> _cards = new List<PaperCard>();
    private readonly List<ChoiceButton> _scales = new List<ChoiceButton>();
    private readonly Dictionary<Control, (float Size, bool Bold)> _baseText = new Dictionary<Control, (float, bool)>();
    private List<TemplateSummary> _available = new List<TemplateSummary>();
    private string _documentPath;
    private string? _selectedPaper;
    private double? _selectedScale;
    private Point _dragCursor;
    private Point _dragWindow;

    public NotePickerForm(string defaultRoot, NoteSettings? previous)
    {
        _root = defaultRoot;
        _previous = previous;
        _documentPath = previous?.DocumentPath ?? string.Empty;
        Text = "导入说明";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 364);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = UiFont(13F);
        BackColor = Color.White;
        ForeColor = Ink;
        DoubleBuffered = true;
        ApplyRoundedRegion();
        SizeChanged += (_, _) => { ApplyRoundedRegion(); ScaleText(); };
        EnableDragging(this);

        var logoBadge = AddSurface(this, 20, 16, 32, 32, BlueTint, BlueTint, 9);
        EnableDragging(logoBadge);
        var logo = LoadLogo();
        if (logo != null)
        {
            logoBadge.Icon = logo;
            logoBadge.Disposed += (_, _) => logo.Dispose();
        }
        EnableDragging(AddLabel(this, "导入说明", 64, 12, 300, 23, 15F, true, Ink));
        EnableDragging(AddLabel(this, "选择文件与图幅", 64, 35, 300, 18, 11F, false, Muted));
        var close = AddTextButton(this, "×", 490, 16, 30, 30, 20F, Muted, () => { DialogResult = DialogResult.Cancel; Close(); });
        close.AccessibleName = "关闭导入窗口";
        Divider(this, 0, 64, 540);

        AddLabel(this, "Word 文件", 20, 89, 80, 32, 13F, false, Ink);
        var wordBox = AddSurface(this, 100, 84, 420, 42, Color.White, Line, 8);
        wordBox.Cursor = Cursors.Hand;
        wordBox.Click += (_, _) => BrowseDocx();
        var wordIcon = AddSurface(wordBox, 12, 9, 24, 24, Blue, Blue, 4);
        var letter = AddLabel(wordIcon, "W", 0, 0, 24, 24, 13F, true, Color.White);
        letter.TextAlign = ContentAlignment.MiddleCenter;
        letter.BackColor = Blue;
        _docxDisplay.SetBounds(47, 8, 300, 26);
        _docxDisplay.TextAlign = ContentAlignment.MiddleLeft;
        _docxDisplay.AutoEllipsis = true;
        _docxDisplay.Font = UiFont(13F);
        _docxDisplay.Cursor = Cursors.Hand;
        _docxDisplay.Click += (_, _) => BrowseDocx();
        wordBox.Controls.Add(_docxDisplay);
        var browse = AddTextButton(wordBox, "更换", 359, 5, 55, 32, 12F, Blue, BrowseDocx);
        browse.AccessibleName = "选择 Word DOCX 文件";
        UpdateDocxDisplay();

        AddLabel(this, "图幅", 20, 151, 80, 30, 13F, false, Ink);
        var papers = new[] { ("A1", "3列"), ("A2", "3列"), ("A3", "2列"), ("更多", "···") };
        for (var i = 0; i < papers.Length; i++)
        {
            var card = new PaperCard(papers[i].Item1, papers[i].Item2)
            {
                Left = 100 + i * 108,
                Top = 146,
                Width = 96,
                Height = 88,
            };
            card.Click += (_, _) => SelectPaper(card.Paper);
            _cards.Add(card);
            Controls.Add(card);
        }

        AddLabel(this, "单位比例", 20, 252, 80, 32, 13F, false, Ink);
        var presets = new[] { ("1x", 1D), ("100x", 100D), ("1000x", 1000D) };
        for (var i = 0; i < presets.Length; i++)
        {
            var scale = new ChoiceButton(presets[i].Item1)
            {
                Left = 100 + i * 142,
                Top = 254,
                Width = i == 2 ? 136 : 134,
                Height = 34,
            };
            var value = presets[i].Item2;
            scale.Click += (_, _) => SelectScale(value);
            _scales.Add(scale);
            Controls.Add(scale);
        }
        _otherScale = AddTextButton(this, "其他比例", 19, 281, 80, 21, 10F, Blue, ChooseOtherScale);
        _otherScale.TextAlign = ContentAlignment.MiddleLeft;
        _otherScale.AccessibleName = "输入其他单位比例";
        if (previous != null && previous.UnitScale > 0 && !double.IsInfinity(previous.UnitScale) && !double.IsNaN(previous.UnitScale))
            SelectScale(previous.UnitScale);
        else
            UpdateScaleButtons();

        _status.SetBounds(100, 290, 420, 16);
        _status.ForeColor = Bad;
        _status.Font = UiFont(10F);
        _status.AutoEllipsis = true;
        _status.Visible = false;
        Controls.Add(_status);
        var footer = AddSurface(this, 0, 308, 540, 56, Footer, Footer, 0);
        Divider(footer, 0, 0, 540);
        AddStep(footer, 20, "1", "选择", true);
        AddStep(footer, 88, "2", "点位置", false);
        var cancel = AddTextButton(footer, "取消", 319, 11, 51, 34, 12F, Muted,
            () => { DialogResult = DialogResult.Cancel; Close(); });
        _continue = new ChoiceButton("⊙  在图纸中点位置")
        {
            Left = 377,
            Top = 12,
            Width = 145,
            Height = 32,
            IsPrimary = true,
        };
        _continue.Click += (_, _) => Confirm();
        footer.Controls.Add(_continue);
        CancelButton = cancel;
        AcceptButton = _continue;
        RefreshTemplates();
        CaptureTextStyles(this);
    }

    private static Font UiFont(float pixels, bool bold = false) =>
        new Font(UiFontFamily, pixels, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

    private float CurrentScale => ClientSize.Width / 540F;

    private void CaptureTextStyles(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label || child is Button)
                _baseText[child] = (child.Font.Size, child.Font.Bold);
            CaptureTextStyles(child);
        }
    }

    private void ScaleText()
    {
        if (_baseText.Count == 0) return;
        var scale = CurrentScale;
        foreach (var entry in _baseText)
        {
            var target = entry.Value.Size * scale;
            if (Math.Abs(entry.Key.Font.Size - target) < 0.1F) continue;
            entry.Key.Font = UiFont(target, entry.Value.Bold);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _docxTip.Dispose();
        base.Dispose(disposing);
    }

    private void ApplyRoundedRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var path = Rounded(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            Math.Max(1, (int)Math.Round(12 * CurrentScale)));
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    public string StandardRoot => _root;
    public string DocumentPath => _documentPath.Trim().Trim('"');
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
            SetStatus("内置模板缺失，请联系管理员安装模板。");
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
                SetStatus("内置模板不可用，请联系管理员检查。");
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
            SetStatus("读取模板失败：" + error.Message);
            UpdateCards();
        }
    }

    private void SelectPaper(string paper)
    {
        if (paper == "更多")
        {
            var extra = _available.Select(item => item.PaperCode).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(item => item != "A1" && item != "A2" && item != "A3").ToList();
            if (extra.Count == 0) { SetStatus("当前没有其他已发布图幅。"); return; }
            var menu = new ContextMenuStrip();
            foreach (var item in extra)
            {
                var code = item;
                menu.Items.Add(item, null, (_, _) => SelectPaper(code));
            }
            menu.Closed += (_, _) => menu.Dispose();
            menu.Show(_cards[3], new Point(0, _cards[3].Height));
            return;
        }
        if (!_available.Any(item => string.Equals(item.PaperCode, paper, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus(paper + " 尚无可用模板。");
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
        SetStatus(count == 1 ? string.Empty : "该图幅对应多份模板，请管理员保留唯一有效版本。");
    }

    private void SelectScale(double value)
    {
        _selectedScale = value;
        UpdateScaleButtons();
        if (SelectedTemplate != null) SetStatus(string.Empty);
    }

    private void UpdateScaleButtons()
    {
        var values = new[] { 1D, 100D, 1000D };
        for (var i = 0; i < _scales.Count; i++)
        {
            _scales[i].Selected = _selectedScale == values[i];
            _scales[i].Invalidate();
        }
        _otherScale.Text = _selectedScale.HasValue && !values.Contains(_selectedScale.Value)
            ? _selectedScale.Value.ToString("G17", CultureInfo.InvariantCulture) + "x · 修改"
            : "其他比例";
    }

    private void ChooseOtherScale()
    {
        using var dialog = new Form
        {
            Text = "其他单位比例",
            ClientSize = new Size(310, 126),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            Font = UiFont(13F),
        };
        var caption = new Label { Text = "请输入 1 图形单位对应的毫米倍数", Left = 16, Top = 13, Width = 275, Height = 23 };
        var input = new TextBox { Left = 16, Top = 42, Width = 275, Height = 26, Text = _selectedScale?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty };
        var okay = new Button { Text = "使用", Left = 216, Top = 81, Width = 75, Height = 30, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange(new Control[] { caption, input, okay });
        dialog.AcceptButton = okay;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!double.TryParse(input.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            SetStatus("其他比例须为大于 0 的数字，例如 10。");
            return;
        }
        SelectScale(value);
    }

    private void Confirm()
    {
        if (SelectedTemplate == null) { SetStatus("模板配置不唯一或不可用，请管理员检查。"); return; }
        if (!_selectedScale.HasValue)
        {
            SetStatus("请选择单位比例，或点“其他比例”填写。");
            _scales[0].Focus();
            return;
        }
        if (!File.Exists(DocumentPath) || !string.Equals(Path.GetExtension(DocumentPath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("请选择本机存在的 .docx 文件。");
            return;
        }
        UnitScale = _selectedScale.Value;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseDocx()
    {
        using var picker = new OpenFileDialog { Filter = "Word 文档 (*.docx)|*.docx", CheckFileExists = true, Title = "选择本次说明 Word 文档" };
        if (File.Exists(DocumentPath)) picker.FileName = DocumentPath;
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        _documentPath = picker.FileName;
        UpdateDocxDisplay();
        if (SelectedTemplate != null) SetStatus(string.Empty);
    }

    private void UpdateDocxDisplay()
    {
        _docxDisplay.Text = string.IsNullOrWhiteSpace(_documentPath) ? "未选择文件" : Path.GetFileName(_documentPath);
        _docxDisplay.ForeColor = string.IsNullOrWhiteSpace(_documentPath) ? Muted : Ink;
        _docxTip.SetToolTip(_docxDisplay, _documentPath);
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
        _status.Visible = message.Length > 0;
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

    private static Label AddLabel(Control parent, string text, int left, int top, int width, int height, float size, bool bold, Color color)
    {
        var label = new Label
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            Font = UiFont(size, bold), ForeColor = color, TextAlign = ContentAlignment.MiddleLeft,
        };
        parent.Controls.Add(label);
        return label;
    }

    private static Button AddTextButton(Control parent, string text, int left, int top, int width, int height,
        float size, Color color, Action click)
    {
        var button = new Button
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = parent.BackColor, ForeColor = color, Font = UiFont(size),
            FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => click();
        parent.Controls.Add(button);
        return button;
    }

    private static RoundedPanel AddSurface(Control parent, int left, int top, int width, int height,
        Color fill, Color border, int radius)
    {
        var panel = new RoundedPanel { Left = left, Top = top, Width = width, Height = height,
            BackColor = fill, BorderColor = border, Radius = radius };
        parent.Controls.Add(panel);
        return panel;
    }

    private static void Divider(Control parent, int left, int top, int width) =>
        parent.Controls.Add(new Panel { Left = left, Top = top, Width = width, Height = 1,
            BackColor = Color.FromArgb(242, 243, 245) });

    private static void AddStep(Control footer, int left, string number, string label, bool active)
    {
        var badge = AddSurface(footer, left, 22, 16, 16,
            active ? Color.FromArgb(225, 235, 255) : Color.White,
            active ? Color.FromArgb(225, 235, 255) : Line, 8);
        var text = AddLabel(badge, number, 0, 0, 16, 16, 10F, false, active ? Blue : Color.FromArgb(156, 163, 175));
        text.TextAlign = ContentAlignment.MiddleCenter;
        AddLabel(footer, label, left + 22, 18, 60, 24, 12F, false, active ? Blue : Color.FromArgb(156, 163, 175));
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

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var scale = Width / 96F;
            g.ScaleTransform(scale, scale);
            using var path = Rounded(new Rectangle(1, 1, 93, 85), 8);
            using (var fill = new SolidBrush(Selected ? BlueTint : Color.White)) g.FillPath(fill, path);
            using (var border = new Pen(Selected ? Blue : Line, Selected ? 2F : 1F)) g.DrawPath(border, path);
            var color = Available ? Ink : Muted;
            if (Paper == "更多")
            {
                using var outline = new Pen(Color.FromArgb(176, 183, 195), 1F) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(outline, 34, 12, 28, 28);
                using var mini = new Pen(Color.FromArgb(151, 160, 174), 1F);
                for (var row = 0; row < 2; row++)
                    for (var column = 0; column < 2; column++)
                        g.DrawRectangle(mini, 42 + column * 6, 21 + row * 6, 4, 4);
            }
            else
            {
                using var pen = new Pen(color, 1F);
                g.DrawRectangle(pen, 35, 12, 26, 27);
                var columns = Paper == "A3" ? 2 : 3;
                for (var i = 1; i < columns; i++)
                    g.DrawLine(pen, 35 + i * 26 / columns, 13, 35 + i * 26 / columns, 38);
                if (Selected)
                {
                    using var dot = new SolidBrush(Color.White);
                    g.FillEllipse(dot, 74, 7, 14, 14);
                }
            }
            g.ResetTransform();
            using var titleFont = UiFont(13F * scale);
            using var detailFont = UiFont(10F * scale);
            TextRenderer.DrawText(g, Paper, titleFont, ScaleRect(new Rectangle(4, 48, 88, 21), scale),
                Selected ? Color.FromArgb(20, 57, 150) : color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Detail, detailFont, ScaleRect(new Rectangle(4, 67, 88, 15), scale),
                Selected ? Blue : Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

    private sealed class ChoiceButton : Button
    {
        public ChoiceButton(string text)
        {
            Text = text;
            Font = UiFont(13F);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        public bool Selected { get; set; }
        public bool IsPrimary { get; set; }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var selected = Selected || IsPrimary;
            var fill = !Enabled ? Color.FromArgb(163, 174, 195) : selected ? Blue : Footer;
            var border = selected ? fill : Line;
            var scale = Height / (IsPrimary ? 32F : 34F);
            using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1),
                Math.Max(1, (int)Math.Round(6 * scale)));
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            using (var pen = new Pen(border, scale)) g.DrawPath(pen, path);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(3, 0, Width - 6, Height),
                selected ? Color.White : Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    private sealed class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; } = Line;
        public int Radius { get; set; } = 8;
        public Image? Icon { get; set; }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            e.Graphics.Clear(Parent?.BackColor ?? Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Radius <= 0)
            {
                using var fill = new SolidBrush(BackColor);
                e.Graphics.FillRectangle(fill, ClientRectangle);
                return;
            }
            var scale = FindForm() is NotePickerForm form ? form.CurrentScale : 1F;
            using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1),
                Math.Max(1, (int)Math.Round(Radius * scale)));
            using (var fill = new SolidBrush(BackColor)) e.Graphics.FillPath(fill, path);
            using var pen = new Pen(BorderColor, scale);
            e.Graphics.DrawPath(pen, path);
            if (Icon != null)
            {
                var side = Math.Max(1, (int)Math.Round(18 * scale));
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(Icon, new Rectangle((Width - side) / 2, (Height - side) / 2, side, side));
            }
        }
    }

    private static Rectangle ScaleRect(Rectangle bounds, float scale) => new Rectangle(
        (int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale),
        (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
