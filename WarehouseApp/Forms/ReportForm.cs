using WarehouseApp.Models;

namespace WarehouseApp.Forms;

public class ReportForm : Form
{
    private readonly AppServices _svc;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;

    private Panel _shipScrollArea = null!;
    private Label _lblShipSummary = null!;
    private List<Shipment> _shipData = new();

    private Panel _writeOffScrollArea = null!;
    private Label _lblWriteOffSummary = null!;
    private List<WriteOff> _writeOffData = new();

    public ReportForm(AppServices svc)
    {
        _svc = svc;
        Build();
    }

    private void Build()
    {
        Text = "Отчёт по отгрузкам и списаниям";
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;

        var topBar = UI.CreatePanel(UI.TopBar);
        topBar.Height = 72;
        topBar.Dock = DockStyle.Top;
        Controls.Add(topBar);

        topBar.Controls.Add(new Label
        {
            Text = "Отчёт за период", Font = UI.Px(24), ForeColor = Color.White,
            AutoSize = true, Location = new Point(20, 18), BackColor = Color.Transparent
        });
        topBar.Controls.Add(new Label { Text = "С:", Font = UI.FontMed, ForeColor = Color.White, AutoSize = true, Location = new Point(320, 24), BackColor = Color.Transparent });
        _dtpFrom = new DateTimePicker { Font = UI.FontMed, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1), Location = new Point(360, 20), Width = 160 };
        topBar.Controls.Add(_dtpFrom);
        topBar.Controls.Add(new Label { Text = "По:", Font = UI.FontMed, ForeColor = Color.White, AutoSize = true, Location = new Point(530, 24), BackColor = Color.Transparent });
        _dtpTo = new DateTimePicker { Font = UI.FontMed, Format = DateTimePickerFormat.Short, Value = DateTime.Today, Location = new Point(582, 20), Width = 160 };
        topBar.Controls.Add(_dtpTo);

        var btnApply = UI.CreatePillButton("Показать", UI.BtnBlue, new Size(140, 48), UI.FontMed);
        btnApply.Location = new Point(756, 14);
        btnApply.Click += (_, _) => RunReport();
        topBar.Controls.Add(btnApply);

        var btnExport = UI.CreatePillButton("Экспорт PDF", UI.BtnGreen, new Size(170, 48), UI.FontMed);
        btnExport.Location = new Point(910, 14);
        btnExport.Click += (_, _) => ExportReport();
        topBar.Controls.Add(btnExport);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = UI.FontMed
        };
        Controls.Add(tabs);
        tabs.BringToFront();

        var pageShip = new TabPage("Отгрузки") { BackColor = UI.BgLight };
        tabs.TabPages.Add(pageShip);

        var shipHeaderPanel = UI.CreatePanel(UI.HeaderRow);
        shipHeaderPanel.Height = 44;
        shipHeaderPanel.Dock = DockStyle.Top;
        shipHeaderPanel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(UI.TextDark);
            using var f = UI.FontMed;
            var sfL = new StringFormat { LineAlignment = StringAlignment.Center };
            var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            int w = shipHeaderPanel.Width;
            string cur = CurSymbol();
            e.Graphics.DrawString("Дата",            f, brush, new RectangleF(16,      0, 110, 44), sfL);
            e.Graphics.DrawString("Покупатель",       f, brush, new RectangleF(130,     0, 270, 44), sfL);
            e.Graphics.DrawString("Адрес",            f, brush, new RectangleF(410,     0, 230, 44), sfL);
            e.Graphics.DrawString($"Сумма ({cur})",   f, brush, new RectangleF(w - 440, 0, 130, 44), sfR);
            e.Graphics.DrawString($"Себест. ({cur})", f, brush, new RectangleF(w - 300, 0, 130, 44), sfR);
            e.Graphics.DrawString($"Прибыль ({cur})", f, brush, new RectangleF(w - 160, 0, 150, 44), sfR);
        };
        pageShip.Controls.Add(shipHeaderPanel);

        _lblShipSummary = new Label
        {
            Font = UI.FontMedBold, ForeColor = UI.TextDark, BackColor = UI.BgCard,
            Dock = DockStyle.Bottom, Height = 46, TextAlign = ContentAlignment.MiddleCenter
        };
        pageShip.Controls.Add(_lblShipSummary);

        _shipScrollArea = UI.CreatePanel(Color.White);
        _shipScrollArea.AutoScroll = true;
        _shipScrollArea.Dock = DockStyle.Fill;
        pageShip.Controls.Add(_shipScrollArea);
        _shipScrollArea.BringToFront();

        var pageWO = new TabPage("Списания") { BackColor = UI.BgLight };
        tabs.TabPages.Add(pageWO);

        var woHeaderPanel = UI.CreatePanel(UI.HeaderRow);
        woHeaderPanel.Height = 44;
        woHeaderPanel.Dock = DockStyle.Top;
        woHeaderPanel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(UI.TextDark);
            using var f = UI.FontMed;
            var sfL = new StringFormat { LineAlignment = StringAlignment.Center };
            var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            int w = woHeaderPanel.Width;
            e.Graphics.DrawString("Дата",        f, brush, new RectangleF(16,      0, 130, 44), sfL);
            e.Graphics.DrawString("Товар",       f, brush, new RectangleF(150,     0, 350, 44), sfL);
            e.Graphics.DrawString("Кол-во",      f, brush, new RectangleF(w - 430, 0, 120, 44), sfR);
            e.Graphics.DrawString("Цена ед.",    f, brush, new RectangleF(w - 300, 0, 130, 44), sfR);
            e.Graphics.DrawString("Убыток (₽)",  f, brush, new RectangleF(w - 160, 0, 140, 44), sfR);
        };
        pageWO.Controls.Add(woHeaderPanel);

        _lblWriteOffSummary = new Label
        {
            Font = UI.FontMedBold, ForeColor = UI.BtnRed, BackColor = Color.FromArgb(255, 245, 245),
            Dock = DockStyle.Bottom, Height = 46, TextAlign = ContentAlignment.MiddleCenter
        };
        pageWO.Controls.Add(_lblWriteOffSummary);

        _writeOffScrollArea = UI.CreatePanel(Color.White);
        _writeOffScrollArea.AutoScroll = true;
        _writeOffScrollArea.Dock = DockStyle.Fill;
        pageWO.Controls.Add(_writeOffScrollArea);
        _writeOffScrollArea.BringToFront();

        Shown += (_, _) => RunReport();
    }

    private void RunReport()
    {
        try
        {
            _shipData = _svc.ReportService.GetShipmentsByPeriod(_dtpFrom.Value, _dtpTo.Value);
        }
        catch { _shipData = new(); }

        try
        {
            _writeOffData = _svc.ReportService.GetWriteOffsByPeriod(_dtpFrom.Value, _dtpTo.Value);
        }
        catch { _writeOffData = new(); }

        RenderShipments();
        RenderWriteOffs();
    }

    private void RenderShipments()
    {
        _shipScrollArea.SuspendLayout();
        _shipScrollArea.Controls.Clear();

        int y = 0;
        int w = Math.Max(_shipScrollArea.ClientSize.Width - 2, 600);

        foreach (var s in _shipData)
        {
            var row = MakeRow(w, y, Color.White);
            row.Paint += (_, e) => DrawBorder(e, row);
            row.Controls.Add(Lbl(s.DisplayDate,  new Rectangle(16,  0, 110, 52)));
            row.Controls.Add(Lbl(s.Recipient,    new Rectangle(130, 0, 270, 52)));
            var a = Lbl(s.Address, new Rectangle(410, 0, 230, 52));
            a.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(a);
            AddRight(row, FmtAt(s.TotalCost, s),         w - 440, UI.TextDark);
            AddRight(row, FmtAt(s.TotalPurchaseCost, s), w - 300, UI.TextDark);
            AddRight(row, FmtAt(s.Profit, s),            w - 160,
                s.Profit >= 0 ? Color.FromArgb(20, 140, 20) : UI.BtnRed, bold: true);
            _shipScrollArea.Controls.Add(row);
            y += 52;
        }

        _shipScrollArea.ResumeLayout();

        decimal sum    = _shipData.Sum(s => Convert(s.TotalCost, s));
        decimal cost   = _shipData.Sum(s => Convert(s.TotalPurchaseCost, s));
        decimal profit = _shipData.Sum(s => Convert(s.Profit, s));
        _lblShipSummary.Text = _shipData.Count == 0
            ? "Отгрузок за период нет"
            : $"Итого: {_shipData.Count} отгрузок  |  Сумма: {FmtAgg(sum)}  |  Себестоимость: {FmtAgg(cost)}  |  Прибыль: {FmtAgg(profit)}";
    }

    private void RenderWriteOffs()
    {
        _writeOffScrollArea.SuspendLayout();
        _writeOffScrollArea.Controls.Clear();

        int y = 0;
        int w = Math.Max(_writeOffScrollArea.ClientSize.Width - 2, 600);

        foreach (var wo in _writeOffData)
        {
            var row = MakeRow(w, y, Color.FromArgb(255, 250, 250));
            row.Paint += (_, e) => DrawBorder(e, row);
            row.Controls.Add(Lbl(wo.WrittenOffAt.ToString("dd.MM.yyyy"), new Rectangle(16,  0, 130, 52)));
            var n = Lbl(wo.Product?.Name ?? "—", new Rectangle(150, 0, 350, 52));
            n.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(n);
            AddRight(row, wo.Quantity.ToString(),      w - 430, UI.TextDark);
            AddRight(row, $"{wo.PurchasePrice:N2} ₽",  w - 300, UI.TextDark);
            AddRight(row, $"{wo.TotalLoss:N2} ₽",      w - 160, UI.BtnRed, bold: true);
            _writeOffScrollArea.Controls.Add(row);
            y += 52;
        }

        _writeOffScrollArea.ResumeLayout();

        decimal totalLoss = _writeOffData.Sum(w => w.TotalLoss);
        _lblWriteOffSummary.Text = _writeOffData.Count == 0
            ? "Списаний за период нет"
            : $"Итого: {_writeOffData.Count} списаний  |  Общий убыток: {totalLoss:N2} ₽";
    }

    private static Panel MakeRow(int w, int y, Color bg) =>
        new Panel { Bounds = new Rectangle(0, y, w, 52), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = bg };

    private static void DrawBorder(PaintEventArgs e, Control row)
    {
        using var pen = new Pen(UI.RowBorder, 1);
        e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
    }

    private static Label Lbl(string text, Rectangle bounds, ContentAlignment align = ContentAlignment.MiddleLeft) =>
        new Label { Text = text, Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = bounds, TextAlign = align, AutoEllipsis = true, BackColor = Color.Transparent };

    private void AddRight(Panel row, string text, int x, Color color, bool bold = false) =>
        row.Controls.Add(new Label
        {
            Text = text, Font = bold ? UI.FontMedBold : UI.FontMed, ForeColor = color,
            Bounds = new Rectangle(x, 0, 130, 52), TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent
        });

    private string CurSymbol() =>
        _svc.CurrencyService.Settings.Currency == "RUB" ? "₽" : _svc.CurrencyService.Settings.Currency;

    private string FmtAt(decimal rub, Shipment s) =>
        _svc.CurrencyService.FormatPriceAt(rub, s.GetStoredRate(_svc.CurrencyService.Settings.Currency));

    private decimal Convert(decimal rub, Shipment s)
    {
        var st = _svc.CurrencyService.Settings;
        if (st.Currency == "RUB") return rub;
        decimal rate = s.GetStoredRate(st.Currency) ?? st.GetRate(st.Currency);
        return rate <= 0 ? rub : Math.Round(rub / rate, 2);
    }

    private string FmtAgg(decimal amount)
    {
        var s = _svc.CurrencyService.Settings;
        return s.Currency == "RUB" ? $"{amount:N0} р." : $"{amount:N2} {s.CurrencySymbol}";
    }

    private void ExportReport()
    {
        if (_shipData.Count == 0 && _writeOffData.Count == 0)
        {
            MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "PDF документ|*.pdf",
            FileName = $"Отчёт_{_dtpFrom.Value:dd.MM.yyyy}_{_dtpTo.Value:dd.MM.yyyy}.pdf"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            Services.ReportPdfExporter.Export(dlg.FileName, _dtpFrom.Value, _dtpTo.Value,
                _shipData, _writeOffData, _svc.CurrencyService.Settings);

            if (MessageBox.Show("Отчёт успешно экспортирован в PDF.\nОткрыть документ?", "Экспорт",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
