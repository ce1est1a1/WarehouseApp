using WarehouseApp.Services;

namespace WarehouseApp.Forms;

public class HeatmapForm : Form
{
    private readonly AppServices _svc;
    private HeatmapMode _mode = HeatmapMode.ShelfLife;
    private List<CellInfo> _cells = new();
    private CellInfo? _selected;

    private Panel _gridHost = null!;
    private Panel _legendHost = null!;
    private Panel _sideHost = null!;
    private Button _tabShelf = null!, _tabMove = null!;
    private Label _lblUpdated = null!;
    private readonly Dictionary<int, RoundedButton> _cellButtons = new();

    private static readonly Color ColGreen = Color.FromArgb(76, 175, 80);
    private static readonly Color ColYellow = Color.FromArgb(252, 211, 0);
    private static readonly Color ColOrange = Color.FromArgb(245, 150, 30);
    private static readonly Color ColRed = Color.FromArgb(229, 57, 53);
    private static readonly Color ColEmpty = Color.FromArgb(232, 234, 238);
    private static readonly Color PanelBlue = Color.FromArgb(36, 57, 117);

    public HeatmapForm(AppServices svc)
    {
        _svc = svc;
        Build();
        ReloadData();
    }

    private void Build()
    {
        Text = "Тепловая карта склада";
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1180, 760);

        var header = UI.CreatePanel(UI.BgLight);
        header.Bounds = new Rectangle(0, 0, ClientSize.Width, 64);
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(header);
        header.Controls.Add(new Label { Text = "Тепловая карта склада", Font = UI.Px(22, FontStyle.Bold), ForeColor = UI.TextDark, Bounds = new Rectangle(20, 12, 500, 40), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });

        _tabShelf = UI.CreatePillButton("По сроку хранения", PanelBlue, new Size(240, 40), UI.FontMed);
        _tabShelf.ForeColor = Color.White;
        _tabShelf.Location = new Point(520, 12);
        _tabShelf.Click += (_, _) => SetMode(HeatmapMode.ShelfLife);
        header.Controls.Add(_tabShelf);

        _tabMove = UI.CreatePillButton("По скорости движения", UI.ValueBar, new Size(300, 40), UI.FontMed);
        _tabMove.Location = new Point(770, 12);
        _tabMove.Click += (_, _) => SetMode(HeatmapMode.Movement);
        header.Controls.Add(_tabMove);

        _gridHost = UI.CreatePanel(Color.White);
        _gridHost.Bounds = new Rectangle(16, 76, ClientSize.Width - 360, ClientSize.Height - 200);
        _gridHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(_gridHost);

        _sideHost = UI.CreateRoundedPanel(Color.White, 14);
        _sideHost.Bounds = new Rectangle(ClientSize.Width - 332, 76, 316, ClientSize.Height - 200);
        _sideHost.Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(_sideHost);

        _legendHost = UI.CreatePanel(UI.BgLight);
        _legendHost.Bounds = new Rectangle(16, ClientSize.Height - 116, ClientSize.Width - 32, 56);
        _legendHost.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(_legendHost);

        _lblUpdated = new Label { Font = UI.FontTiny, ForeColor = UI.TextGray, Bounds = new Rectangle(20, ClientSize.Height - 50, 500, 24), BackColor = Color.Transparent, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.Add(_lblUpdated);

        var btnRefresh = UI.CreatePillButton("Обновить карту", PanelBlue, new Size(220, 40), UI.FontMed);
        btnRefresh.ForeColor = Color.White;
        btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnRefresh.Location = new Point(ClientSize.Width - 470, ClientSize.Height - 52);
        btnRefresh.Click += (_, _) => ReloadData();
        Controls.Add(btnRefresh);

        var btnClose = UI.CreatePillButton("Закрыть", UI.ValueBar, new Size(180, 40), UI.FontMed);
        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.Location = new Point(ClientSize.Width - 230, ClientSize.Height - 52);
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        _gridHost.Resize += (_, _) => LayoutGrid();
        RenderSidePanel();
    }

    private void SetMode(HeatmapMode mode)
    {
        _mode = mode;
        _tabShelf.BackColor = mode == HeatmapMode.ShelfLife ? PanelBlue : UI.ValueBar;
        _tabShelf.ForeColor = mode == HeatmapMode.ShelfLife ? Color.White : UI.TextDark;
        _tabMove.BackColor = mode == HeatmapMode.Movement ? PanelBlue : UI.ValueBar;
        _tabMove.ForeColor = mode == HeatmapMode.Movement ? Color.White : UI.TextDark;
        RenderCellColors();
        RenderLegend();
        RenderSidePanel();
    }

    private void ReloadData()
    {
        _cells = _svc.HeatmapService.GetCells();
        if (_selected != null) _selected = _cells.FirstOrDefault(c => c.CellId == _selected.CellId);
        BuildGrid();
        SetMode(_mode);
        _lblUpdated.Text = $"Данные обновлены: {DateTime.Now:dd.MM.yyyy HH:mm}";
    }

    private void BuildGrid()
    {
        _gridHost.SuspendLayout();
        foreach (Control c in _gridHost.Controls) c.Dispose();
        _gridHost.Controls.Clear();
        _cellButtons.Clear();

        var rows = _cells.Select(c => c.RowLabel).Distinct().OrderBy(r => r).ToList();
        var colCount = _cells.Any() ? _cells.Max(c => c.ColIndex) : 0;

        for (int col = 1; col <= colCount; col++)
        {
            var lbl = new Label { Text = col.ToString("00"), Font = UI.FontMedBold, ForeColor = UI.TextGray, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Tag = $"colhdr_{col}" };
            _gridHost.Controls.Add(lbl);
        }

        foreach (var row in rows)
        {
            var rowLbl = new Label { Text = row, Font = UI.FontMedBold, ForeColor = UI.TextGray, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Tag = $"rowhdr_{row}" };
            _gridHost.Controls.Add(rowLbl);
        }
        foreach (var cell in _cells)
        {
            var btn = UI.CreatePillButton(cell.Code, ColEmpty, new Size(80, 50), UI.FontSmall);
            btn.CornerRadius = 6;
            var captured = cell;
            btn.Click += (_, _) => { _selected = captured; RenderSidePanel(); HighlightSelected(); };
            _gridHost.Controls.Add(btn);
            _cellButtons[cell.CellId] = btn;
        }
        _gridHost.ResumeLayout();
        LayoutGrid();
    }

    private void LayoutGrid()
    {
        if (_cells.Count == 0) return;
        var rows = _cells.Select(c => c.RowLabel).Distinct().OrderBy(r => r).ToList();
        int colCount = _cells.Max(c => c.ColIndex);

        int leftPad = 20, topPad = 16, rowHdrW = 36, colHdrH = 32, gap = 8;
        int availW = _gridHost.Width - leftPad - rowHdrW - 16;
        int availH = _gridHost.Height - topPad - colHdrH - 16;
        int cw = Math.Max(50, (availW - gap * (colCount - 1)) / Math.Max(1, colCount));
        int ch = Math.Max(36, (availH - gap * (rows.Count - 1)) / Math.Max(1, rows.Count));
        cw = Math.Min(cw, 120); ch = Math.Min(ch, 64);

        int gridLeft = leftPad + rowHdrW;
        int gridTop = topPad + colHdrH;

        foreach (Control c in _gridHost.Controls)
        {
            if (c.Tag is string tag)
            {
                if (tag.StartsWith("colhdr_"))
                {
                    int col = int.Parse(tag[7..]);
                    c.Bounds = new Rectangle(gridLeft + (col - 1) * (cw + gap), topPad, cw, colHdrH);
                }
                else if (tag.StartsWith("rowhdr_"))
                {
                    string row = tag[7..];
                    int ri = rows.IndexOf(row);
                    c.Bounds = new Rectangle(leftPad, gridTop + ri * (ch + gap), rowHdrW, ch);
                }
            }
        }
        foreach (var cell in _cells)
        {
            if (!_cellButtons.TryGetValue(cell.CellId, out var btn)) continue;
            int ri = rows.IndexOf(cell.RowLabel);
            btn.Bounds = new Rectangle(gridLeft + (cell.ColIndex - 1) * (cw + gap), gridTop + ri * (ch + gap), cw, ch);
        }
    }

    private void RenderCellColors()
    {
        foreach (var cell in _cells)
        {
            if (!_cellButtons.TryGetValue(cell.CellId, out var btn)) continue;
            var bg = ColorFor(cell);
            btn.BackColor = bg;
            double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B);
            btn.ForeColor = lum < 140 ? Color.White : UI.TextDark;
        }
        HighlightSelected();
    }

    private Color ColorFor(CellInfo cell)
    {
        if (cell.IsEmpty) return ColEmpty;
        if (_mode == HeatmapMode.ShelfLife)
        {
            if (!cell.ShelfLifeDays.HasValue) return ColGreen;
            int d = cell.ShelfLifeDays.Value;
            if (d < 8) return ColRed;
            if (d <= 30) return ColOrange;
            if (d <= 90) return ColYellow;
            return ColGreen;
        }
        return cell.Movement switch
        {
            MovementLevel.High => ColGreen,
            MovementLevel.Medium => ColYellow,
            MovementLevel.Low => ColRed,
            _ => ColEmpty
        };
    }

    private void HighlightSelected()
    {
        foreach (var kv in _cellButtons)
        {
            var cell = _cells.FirstOrDefault(c => c.CellId == kv.Key);
            bool sel = _selected != null && cell != null && cell.CellId == _selected.CellId;
            kv.Value.BorderWidth = sel ? 3 : 0;
            kv.Value.BorderColor = PanelBlue;
            kv.Value.Invalidate();
        }
    }

    private void RenderLegend()
    {
        foreach (Control c in _legendHost.Controls) c.Dispose();
        _legendHost.Controls.Clear();

        string title = _mode == HeatmapMode.ShelfLife ? "Срок хранения (дней):" : "Скорость движения:";
        int titleW = TextRenderer.MeasureText(title, UI.FontMedBold).Width + 12;
        _legendHost.Controls.Add(new Label
        {
            Text = title, Font = UI.FontMedBold, ForeColor = UI.TextDark,
            Bounds = new Rectangle(0, 12, titleW, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent
        });

        (Color c, string t)[] items = _mode == HeatmapMode.ShelfLife
            ? new[] { (ColGreen, "> 90"), (ColYellow, "31-90"), (ColOrange, "8-30"), (ColRed, "< 8"), (ColEmpty, "пусто") }
            : new[] { (ColGreen, "высокая"), (ColYellow, "средняя"), (ColRed, "низкая"), (ColEmpty, "пусто") };

        int x = titleW + 16;
        foreach (var (col, txt) in items)
        {
            var swatch = UI.CreateRoundedPanel(col, 5);
            swatch.Bounds = new Rectangle(x, 16, 26, 24);
            _legendHost.Controls.Add(swatch);

            int tw = TextRenderer.MeasureText(txt, UI.FontSmall).Width + 6;
            var lbl = new Label { Text = txt, Font = UI.FontSmall, ForeColor = UI.TextDark, Bounds = new Rectangle(x + 32, 12, tw, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            _legendHost.Controls.Add(lbl);

            x += 32 + tw + 24;
        }
    }

    private void RenderSidePanel()
    {
        foreach (Control c in _sideHost.Controls) c.Dispose();
        _sideHost.Controls.Clear();

        _sideHost.Controls.Add(new Label { Text = "Выбранная ячейка", Font = UI.Px(18, FontStyle.Bold), ForeColor = UI.TextDark, Bounds = new Rectangle(18, 16, 280, 34), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });

        if (_selected == null)
        {
            _sideHost.Controls.Add(new Label { Text = "Выберите ячейку на карте, чтобы увидеть данные о товаре, остатке и сроках.", Font = UI.FontSmall, ForeColor = UI.TextGray, Bounds = new Rectangle(18, 60, 284, 120), BackColor = Color.Transparent });
            return;
        }

        var cell = _selected;
        _sideHost.Controls.Add(new Label { Text = cell.Code, Font = UI.Px(30, FontStyle.Bold), ForeColor = PanelBlue, Bounds = new Rectangle(18, 52, 284, 48), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });

        int y = 112;
        if (cell.IsEmpty)
        {
            _sideHost.Controls.Add(new Label { Text = "Ячейка свободна", Font = UI.FontMedBold, ForeColor = UI.TextGray, Bounds = new Rectangle(18, y, 280, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            return;
        }

        y = SideRow("Товар:", cell.ProductName, y);
        y = SideRow("Остаток:", $"{cell.Stock} шт.", y);

        string shelf = cell.ShelfLifeDays.HasValue ? $"{cell.ShelfLifeDays.Value} дн." : "бессрочный";
        Color shelfColor = cell.ShelfLifeDays.HasValue && cell.ShelfLifeDays.Value < 8 ? ColRed
            : cell.ShelfLifeDays.HasValue && cell.ShelfLifeDays.Value <= 30 ? ColOrange : UI.TextDark;
        y = SideRow("Срок хранения:", shelf, y, shelfColor);

        string moveText = cell.Movement switch
        {
            MovementLevel.High => "высокая",
            MovementLevel.Medium => "средняя",
            MovementLevel.Low => "низкая",
            _ => "—"
        };
        y = SideRow("Скорость движения:", $"{moveText} ({cell.MovementUnits} шт./мес.)", y);

        if (cell.ShelfLifeDays.HasValue && cell.ShelfLifeDays.Value < 8)
        {
            y += 8;
            var warn = UI.CreateRoundedPanel(Color.FromArgb(252, 232, 232), 10);
            warn.Bounds = new Rectangle(16, y, 284, 56);
            _sideHost.Controls.Add(warn);
            warn.Controls.Add(new Label { Text = "Высокий риск просрочки. Рекомендуется отгрузить в первую очередь.", Font = UI.FontTiny, ForeColor = ColRed, Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4), BackColor = Color.Transparent });
        }
    }

    private int SideRow(string caption, string value, int y, Color? valueColor = null)
    {
        _sideHost.Controls.Add(new Label { Text = caption, Font = UI.FontTiny, ForeColor = UI.TextGray, Bounds = new Rectangle(18, y, 280, 24), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
        _sideHost.Controls.Add(new Label { Text = value, Font = UI.FontMed, ForeColor = valueColor ?? UI.TextDark, Bounds = new Rectangle(18, y + 24, 284, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true });
        return y + 60;
    }
}
