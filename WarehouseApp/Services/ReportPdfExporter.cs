using NLog;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public static class ReportPdfExporter
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static bool _fontResolverSet;

    private const double Margin = 40;
    private const double RowH = 20;
    private const double HeaderH = 22;

    public static void Export(string path, DateTime from, DateTime to,
        List<Shipment> shipments, List<WriteOff> writeOffs, AppSettings settings)
    {
        EnsureFontResolver();

        string cur = settings.Currency == "RUB" ? "руб." : settings.Currency;
        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var subFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var sectionFont = new XFont("Arial", 13, XFontStyleEx.Bold);
        var headFont = new XFont("Arial", 9, XFontStyleEx.Bold);
        var cellFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var totalFont = new XFont("Arial", 10, XFontStyleEx.Bold);

        var doc = new PdfDocument();
        doc.Info.Title = "Отчёт по отгрузкам и списаниям";

        var ctx = new DrawContext(doc);
        ctx.NewPage();

        ctx.Gfx.DrawString("Отчёт по отгрузкам и списаниям", titleFont, XBrushes.Black,
            new XRect(Margin, ctx.Y, ctx.ContentWidth, 26), XStringFormats.TopLeft);
        ctx.Y += 30;
        ctx.Gfx.DrawString($"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}", subFont, XBrushes.Black,
            new XRect(Margin, ctx.Y, ctx.ContentWidth, 16), XStringFormats.TopLeft);
        ctx.Y += 16;
        ctx.Gfx.DrawString($"Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}     Валюта: {cur}", subFont, XBrushes.Gray,
            new XRect(Margin, ctx.Y, ctx.ContentWidth, 16), XStringFormats.TopLeft);
        ctx.Y += 28;

        double[] shipCols = { 65, 135, 145, 60, 55, 55 };
        string[] shipHeaders = { "Дата", "Покупатель", "Адрес", "Сумма", "Себест.", "Прибыль" };
        bool[] shipRight = { false, false, false, true, true, true };

        if (shipments.Count > 0)
        {
            DrawSection(ctx, "Отгрузки", sectionFont);
            DrawTableHeader(ctx, shipCols, shipHeaders, shipRight, headFont);

            decimal sumTotal = 0, costTotal = 0, profitTotal = 0;
            foreach (var s in shipments)
            {
                decimal sum = Convert(s.TotalCost, s, settings);
                decimal cost = Convert(s.TotalPurchaseCost, s, settings);
                decimal profit = Convert(s.Profit, s, settings);
                sumTotal += sum; costTotal += cost; profitTotal += profit;

                ctx.EnsureSpace(RowH, () => DrawTableHeader(ctx, shipCols, shipHeaders, shipRight, headFont));
                string[] cells =
                {
                    s.ShippedAt.ToString("dd.MM.yyyy"), s.Recipient, s.Address,
                    Money(sum), Money(cost), Money(profit)
                };
                DrawRow(ctx, shipCols, cells, shipRight, cellFont,
                    profitColorIndex: 5, profit: profit);
            }

            ctx.Y += 4;
            ctx.Gfx.DrawString(
                $"Итого: {shipments.Count} отгрузок,   сумма: {Money(sumTotal)},   себестоимость: {Money(costTotal)},   прибыль: {Money(profitTotal)}",
                totalFont, XBrushes.Black, new XRect(Margin, ctx.Y, ctx.ContentWidth, 18), XStringFormats.TopLeft);
            ctx.Y += 34;
        }

        if (writeOffs.Count > 0)
        {
            double[] woCols = { 80, 235, 60, 70, 70 };
            string[] woHeaders = { "Дата", "Товар", "Кол-во", "Цена ед.", "Убыток" };
            bool[] woRight = { false, false, true, true, true };

            ctx.EnsureSpace(HeaderH + RowH * 2, null);
            DrawSection(ctx, "Списания", sectionFont);
            DrawTableHeader(ctx, woCols, woHeaders, woRight, headFont);

            decimal lossTotal = 0;
            foreach (var w in writeOffs)
            {
                lossTotal += w.TotalLoss;
                ctx.EnsureSpace(RowH, () => DrawTableHeader(ctx, woCols, woHeaders, woRight, headFont));
                string[] cells =
                {
                    w.WrittenOffAt.ToString("dd.MM.yyyy"), w.Product?.Name ?? "—",
                    w.Quantity.ToString(), $"{w.PurchasePrice:N2}", $"{w.TotalLoss:N2}"
                };
                DrawRow(ctx, woCols, cells, woRight, cellFont, profitColorIndex: -1, profit: 0);
            }

            ctx.Y += 4;
            ctx.Gfx.DrawString($"Итого: {writeOffs.Count} списаний,   общий убыток: {lossTotal:N2} руб.",
                totalFont, XBrushes.Black, new XRect(Margin, ctx.Y, ctx.ContentWidth, 18), XStringFormats.TopLeft);
        }

        ctx.Dispose();
        doc.Save(path);
        logger.Info("Отчёт экспортирован в PDF: {Path} ({Ship} отгрузок, {Wo} списаний)",
            path, shipments.Count, writeOffs.Count);
    }

    private static void DrawSection(DrawContext ctx, string title, XFont font)
    {
        ctx.Gfx.DrawString(title, font, XBrushes.Black,
            new XRect(Margin, ctx.Y, ctx.ContentWidth, 18), XStringFormats.TopLeft);
        ctx.Y += 22;
    }

    private static void DrawTableHeader(DrawContext ctx, double[] cols, string[] headers, bool[] right, XFont font)
    {
        double x = Margin;
        var bg = new XSolidBrush(XColor.FromArgb(225, 228, 235));
        ctx.Gfx.DrawRectangle(bg, Margin, ctx.Y, ctx.ContentWidth, HeaderH);
        for (int i = 0; i < cols.Length; i++)
        {
            var fmt = right[i] ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
            var rect = new XRect(x + 4, ctx.Y, cols[i] - 8, HeaderH);
            ctx.Gfx.DrawString(Fit(ctx.Gfx, headers[i], font, cols[i] - 8), font, XBrushes.Black, rect, fmt);
            x += cols[i];
        }
        ctx.Y += HeaderH;
    }

    private static void DrawRow(DrawContext ctx, double[] cols, string[] cells, bool[] right, XFont font,
        int profitColorIndex, decimal profit)
    {
        double x = Margin;
        for (int i = 0; i < cols.Length; i++)
        {
            var brush = XBrushes.Black;
            if (i == profitColorIndex)
                brush = profit >= 0 ? new XSolidBrush(XColor.FromArgb(20, 140, 20)) : new XSolidBrush(XColor.FromArgb(200, 30, 30));
            var fmt = right[i] ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
            var rect = new XRect(x + 4, ctx.Y, cols[i] - 8, RowH);
            ctx.Gfx.DrawString(Fit(ctx.Gfx, cells[i], font, cols[i] - 8), font, brush, rect, fmt);
            x += cols[i];
        }
        ctx.Gfx.DrawLine(new XPen(XColor.FromArgb(220, 220, 225), 0.5), Margin, ctx.Y + RowH, Margin + ctx.ContentWidth, ctx.Y + RowH);
        ctx.Y += RowH;
    }

    private static string Fit(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (gfx.MeasureString(text, font).Width <= maxWidth) return text;
        const string ell = "...";
        var s = text;
        while (s.Length > 1 && gfx.MeasureString(s + ell, font).Width > maxWidth)
            s = s[..^1];
        return s + ell;
    }

    private static string Money(decimal amount) => $"{amount:N2}";

    private static decimal Convert(decimal rub, Shipment s, AppSettings settings)
    {
        if (settings.Currency == "RUB") return rub;
        decimal rate = s.GetStoredRate(settings.Currency) ?? settings.GetRate(settings.Currency);
        return rate <= 0 ? rub : Math.Round(rub / rate, 2);
    }

    private static void EnsureFontResolver()
    {
        if (_fontResolverSet) return;
        try
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new WinArialFontResolver();
        }
        catch {  }
        _fontResolverSet = true;
    }

    private sealed class DrawContext : IDisposable
    {
        private readonly PdfDocument _doc;
        public XGraphics Gfx { get; private set; } = null!;
        public double Y { get; set; }
        public double ContentWidth { get; private set; }
        private double _pageBottom;

        public DrawContext(PdfDocument doc) => _doc = doc;

        public void NewPage()
        {
            Gfx?.Dispose();
            var page = _doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            Gfx = XGraphics.FromPdfPage(page);
            ContentWidth = page.Width.Point - 2 * Margin;
            _pageBottom = page.Height.Point - Margin;
            Y = Margin;
        }

        public void EnsureSpace(double needed, Action? redrawHeader)
        {
            if (Y + needed <= _pageBottom) return;
            NewPage();
            redrawHeader?.Invoke();
        }

        public void Dispose() => Gfx?.Dispose();
    }
}

internal sealed class WinArialFontResolver : IFontResolver
{
    private static readonly string FontsDir =
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public byte[]? GetFont(string faceName)
    {
        string file = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? "arialbd.ttf" : "arial.ttf";
        var path = Path.Combine(FontsDir, file);
        if (!File.Exists(path)) path = Path.Combine(FontsDir, "arial.ttf");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new FontResolverInfo(isBold ? "Arial#Bold" : "Arial#Regular");
}
