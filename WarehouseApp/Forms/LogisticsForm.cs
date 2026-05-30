using WarehouseApp.Models;
using WarehouseApp.Services;

namespace WarehouseApp.Forms;

public class LogisticsForm : Form
{
    private readonly AppServices _svc;
    private readonly decimal _totalCost;
    private readonly List<string> _productNames;

    public ShipmentLogistics Result { get; private set; }

    private static readonly Color CardBg = Color.White;
    private static readonly Color Green = Color.FromArgb(20, 140, 20);
    private static readonly Color Red = Color.FromArgb(200, 30, 30);
    private static readonly Color Orange = Color.FromArgb(210, 120, 0);
    private static readonly Color Neutral = Color.FromArgb(120, 120, 120);
    private static readonly Color PanelBlue = Color.FromArgb(36, 57, 117);

    private TextBox _txtCompany = null!;
    private TextBox _txtInn = null!;
    private Button _btnCheck = null!;
    private Label _lblTax = null!, _lblBank = null!, _lblDir = null!;
    private Label _lblTaxNote = null!, _lblBankNote = null!, _lblDirNote = null!;
    private RoundedPanel _statusPanel = null!;
    private Label _lblStatus = null!, _lblStatusDate = null!;

    private AddressSuggestBox _addrBox = null!;
    private Label _lblRouteFrom = null!, _lblRouteTo = null!, _lblDistance = null!;
    private Label _lblWeather = null!, _lblWeatherDetails = null!, _lblWeatherRisk = null!;
    private Label _lblInsurance = null!;
    private Label _lblWeatherWarn = null!;
    private Button _btnApply = null!, _btnRefresh = null!;

    public LogisticsForm(AppServices svc, string companyName, string address,
        decimal totalCost, List<string> productNames, ShipmentLogistics? existing)
    {
        _svc = svc;
        _totalCost = totalCost;
        _productNames = productNames ?? new List<string>();
        Result = existing ?? new ShipmentLogistics();
        Result.CounterpartyType = "Покупатель";
        Result.CompanyName = string.IsNullOrWhiteSpace(Result.CompanyName) ? (companyName ?? "") : Result.CompanyName;
        if (string.IsNullOrWhiteSpace(Result.RouteTo)) Result.RouteTo = address ?? "";

        Build();
        LoadFromResult();
        Shown += (_, _) => RefreshWeatherAndRoute();
    }

    private void Build()
    {
        Text = "Проверка контрагента и логистика";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(960, 820);

        var scroll = new Panel { AutoScroll = true, BackColor = UI.BgLight };
        scroll.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height - 72);
        scroll.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(scroll);

        int y = 16;
        y = BuildCounterpartyCard(scroll, y);
        y = BuildLogisticsCard(scroll, y);
        scroll.AutoScrollMinSize = new Size(0, y);

        var bottom = UI.CreatePanel(UI.BgLight);
        bottom.Bounds = new Rectangle(0, ClientSize.Height - 72, ClientSize.Width, 72);
        bottom.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(bottom);

        var btnSave = UI.CreatePillButton("Сохранить", UI.BtnGreen, new Size(200, 46), UI.FontMedBold);
        btnSave.Location = new Point(ClientSize.Width - 430, 14);
        btnSave.Click += (_, _) => { Result.CompanyName = _txtCompany.Text.Trim(); Result.RouteTo = _addrBox.AddressText.Trim(); DialogResult = DialogResult.OK; Close(); };
        bottom.Controls.Add(btnSave);

        var btnCancel = UI.CreatePillButton("Отмена", UI.ValueBar, new Size(190, 46), UI.FontMed);
        btnCancel.Location = new Point(ClientSize.Width - 210, 14);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bottom.Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private int BuildCounterpartyCard(Control host, int y)
    {
        var card = UI.CreateRoundedPanel(CardBg, 16);
        card.Bounds = new Rectangle(16, y, ClientSize.Width - 52, 480);
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        host.Controls.Add(card);

        card.Controls.Add(Title("Проверка получателя", 18, 14));

        int lx = 24, fx = 300, fw = 360, ry = 60, rh = 42, gap = 54;

        card.Controls.Add(Caption("Название компании:", lx, ry + 8));
        _txtCompany = FieldBox(card, fx, ry, fw);

        ry += gap;
        card.Controls.Add(Caption("ИНН:", lx, ry + 8));
        _txtInn = FieldBox(card, fx, ry, 200);
        _txtInn.TextChanged += (_, _) => _btnCheck.Enabled = !string.IsNullOrWhiteSpace(_txtInn.Text);

        _btnCheck = UI.CreatePillButton("Проверить", PanelBlue, new Size(180, rh), UI.FontMed);
        _btnCheck.ForeColor = Color.White;
        _btnCheck.Location = new Point(fx + 220, ry);
        _btnCheck.Click += (_, _) => DoCounterpartyCheck();
        card.Controls.Add(_btnCheck);

        ry += gap + 6;
        card.Controls.Add(Caption("Результаты проверки", lx, ry, bold: true));
        ry += 36;

        (_lblTax, _lblTaxNote) = ResultRow(card, "Налоговые задолженности:", lx, ry); ry += 54;
        (_lblBank, _lblBankNote) = ResultRow(card, "Банкротство:", lx, ry); ry += 54;
        (_lblDir, _lblDirNote) = ResultRow(card, "Дисквалифицированные лица:", lx, ry); ry += 58;

        _statusPanel = UI.CreateRoundedPanel(Color.FromArgb(232, 245, 233), 12);
        _statusPanel.Bounds = new Rectangle(lx, ry, card.Width - 48, 68);
        _statusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(_statusPanel);
        _lblStatus = new Label { Font = UI.FontMedBold, ForeColor = Green, Bounds = new Rectangle(16, 10, card.Width - 80, 30), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _statusPanel.Controls.Add(_lblStatus);
        _lblStatusDate = new Label { Font = UI.FontTiny, ForeColor = Neutral, Bounds = new Rectangle(16, 40, card.Width - 80, 22), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _statusPanel.Controls.Add(_lblStatusDate);

        return y + card.Height + 16;
    }

    private int BuildLogisticsCard(Control host, int y)
    {
        var card = UI.CreateRoundedPanel(CardBg, 16);
        card.Bounds = new Rectangle(16, y, ClientSize.Width - 52, 580);
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        host.Controls.Add(card);

        card.Controls.Add(Title("Логистика и погода", 18, 14));

        int lx = 24, ry = 54;

        card.Controls.Add(Caption("Город доставки (начните вводить название города):", lx, ry, bold: true)); ry += 32;
        _addrBox = new AddressSuggestBox(Color.FromArgb(245, 247, 250)) { Bounds = new Rectangle(lx, ry, card.Width - 48, 42), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _addrBox.SetSuggester(q => _svc.GeoService.Suggest(q));
        _addrBox.AddressText = Result.RouteTo;
        _addrBox.SelectionChanged += (_, _) => RefreshWeatherAndRoute();
        card.Controls.Add(_addrBox);
        ry += 52;

        card.Controls.Add(Caption("Маршрут", lx, ry, bold: true)); ry += 32;
        _lblRouteFrom = ValueLabel(card, "Откуда: —", lx, ry, 420);
        _lblDistance = ValueLabel(card, "Расстояние: —", lx + 460, ry, 360); ry += 28;
        _lblRouteTo = ValueLabel(card, "Куда: —", lx, ry, card.Width - 48); ry += 36;

        card.Controls.Add(Caption("Погода в регионе доставки (через 2 дня)", lx, ry, bold: true)); ry += 32;
        _lblWeather = new Label { Font = UI.Px(28, FontStyle.Bold), ForeColor = UI.TextDark, Bounds = new Rectangle(lx, ry, 320, 46), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Text = "—" };
        card.Controls.Add(_lblWeather);
        _lblWeatherRisk = new Label { Font = UI.FontMedBold, ForeColor = Neutral, Bounds = new Rectangle(lx + 330, ry + 8, card.Width - lx - 350, 30), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Text = "" };
        card.Controls.Add(_lblWeatherRisk);
        _lblWeatherDetails = new Label { Font = UI.FontSmall, ForeColor = UI.TextGray, Bounds = new Rectangle(lx, ry + 50, card.Width - 48, 26), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Text = "" };
        card.Controls.Add(_lblWeatherDetails);
        ry += 84;

        _lblWeatherWarn = new Label { Font = UI.FontSmall, ForeColor = Red, Bounds = new Rectangle(lx, ry, card.Width - 48, 26), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Text = "", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        card.Controls.Add(_lblWeatherWarn);
        ry += 30;

        card.Controls.Add(Caption("Рекомендации по подготовке груза", lx, ry, bold: true)); ry += 32;
        _lblInsurance = RecRow(card, "Страховка груза (жара/холод):", lx, ry); ry += 38;

        _btnRefresh = UI.CreatePillButton("Обновить прогноз", UI.ValueBar, new Size(230, 44), UI.FontMed);
        _btnRefresh.Location = new Point(lx, ry);
        _btnRefresh.Click += (_, _) => RefreshWeatherAndRoute();
        card.Controls.Add(_btnRefresh);

        _btnApply = UI.CreatePillButton("Применить рекомендации", PanelBlue, new Size(300, 44), UI.FontMed);
        _btnApply.ForeColor = Color.White;
        _btnApply.Location = new Point(lx + 250, ry);
        _btnApply.Click += (_, _) =>
        {
            Result.RecommendationsApplied = true;
            MessageBox.Show("Рекомендации применены к отгрузке и будут сохранены в её карточке.",
                "Рекомендации", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRecommendationUi();
        };
        card.Controls.Add(_btnApply);

        return y + card.Height + 16;
    }

    private void DoCounterpartyCheck()
    {
        Result.CompanyName = _txtCompany.Text.Trim();
        Result.Inn = _txtInn.Text.Trim();

        if (!_svc.CounterpartyService.ValidateInn(Result.Inn, out var error))
        {
            MessageBox.Show(error, "Проверка ИНН", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var res = _svc.CounterpartyService.Check(Result);
        if (!res.Success && !Result.CheckPerformed)
        {
            MessageBox.Show(res.Message, "Проверка контрагента", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        UpdateCheckUi();
    }

    private void RefreshWeatherAndRoute()
    {
        var s = _svc.CurrencyService.Settings;
        var addr = _addrBox.AddressText.Trim();
        Result.RouteFrom = s.WarehouseAddress;
        Result.RouteTo = addr;

        _btnRefresh.Enabled = false;
        _lblWeather.Text = "загрузка…";
        _lblWeatherRisk.Text = ""; _lblWeatherDetails.Text = ""; _lblWeatherWarn.Text = "";
        _lblDistance.Text = "Расстояние: …";

        var selected = _addrBox.Selected;
        var date = DateTime.Today.AddDays(2);

        System.Threading.Tasks.Task.Run(() =>
        {
            double? dlat = selected?.Lat, dlon = selected?.Lon;
            if (!dlat.HasValue && !string.IsNullOrWhiteSpace(addr))
            {
                var g = _svc.GeoService.Geocode(addr);
                if (g != null) { dlat = g.Lat; dlon = g.Lon; }
            }

            if (dlat.HasValue)
            {
                Result.DestLat = dlat.Value; Result.DestLon = dlon!.Value; Result.HasCoords = true;
                var d = _svc.GeoService.RoadDistanceKm(s.WarehouseLat, s.WarehouseLon, dlat.Value, dlon.Value);
                Result.DistanceKm = d ?? 0;
                Result.RouteError = d.HasValue ? "" : "Не удалось построить маршрут.";
            }
            else
            {
                Result.HasCoords = false; Result.DistanceKm = 0;
                Result.RouteError = "Не удалось определить координаты адреса — уточните адрес.";
            }

            _svc.WeatherService.GetForecast(Result,
                Result.HasCoords ? Result.DestLat : (double?)null,
                Result.HasCoords ? Result.DestLon : (double?)null,
                addr, date);

            _svc.LogisticsService.BuildRecommendations(Result, _productNames, _totalCost);

            if (IsDisposed) return;
            try
            {
                BeginInvoke(() =>
                {
                    UpdateRouteUi();
                    UpdateWeatherUi();
                    UpdateRecommendationUi();
                    _btnRefresh.Enabled = true;
                });
            }
            catch { }
        });
    }

    private void LoadFromResult()
    {
        _txtCompany.Text = Result.CompanyName;
        _txtInn.Text = Result.Inn;
        _btnCheck.Enabled = !string.IsNullOrWhiteSpace(_txtInn.Text);
        if (Result.CheckPerformed) UpdateCheckUi();
        else
        {
            SetOutcome(_lblTax, _lblTaxNote, CheckOutcome.Clean, "—");
            SetOutcome(_lblBank, _lblBankNote, CheckOutcome.Clean, "—");
            SetOutcome(_lblDir, _lblDirNote, CheckOutcome.Clean, "—");
            _lblTax.Text = "—"; _lblBank.Text = "—"; _lblDir.Text = "—";
            _lblTax.ForeColor = _lblBank.ForeColor = _lblDir.ForeColor = Neutral;
            _lblStatus.Text = "Статус: проверка не выполнялась";
            _lblStatus.ForeColor = Neutral;
            _statusPanel.BackColor = Color.FromArgb(238, 238, 238);
            _lblStatusDate.Text = "";
        }
    }

    private void UpdateCheckUi()
    {
        SetOutcome(_lblTax, _lblTaxNote, Result.TaxOutcome, Result.TaxNote);
        SetOutcome(_lblBank, _lblBankNote, Result.BankruptcyOutcome, Result.BankruptcyNote);
        SetOutcome(_lblDir, _lblDirNote, Result.DirectorOutcome, Result.DirectorNote);

        switch (Result.Decision)
        {
            case DealDecision.Allowed:
                _lblStatus.Text = "Статус: можно продолжить сделку";
                _lblStatus.ForeColor = Green;
                _statusPanel.BackColor = Color.FromArgb(232, 245, 233);
                break;
            case DealDecision.NeedsReview:
                _lblStatus.Text = "Статус: требуется решение администратора";
                _lblStatus.ForeColor = Red;
                _statusPanel.BackColor = Color.FromArgb(252, 232, 232);
                break;
            default:
                _lblStatus.Text = "Статус: проверка не выполнялась";
                _lblStatus.ForeColor = Neutral;
                _statusPanel.BackColor = Color.FromArgb(238, 238, 238);
                break;
        }
        _lblStatusDate.Text = Result.CheckedAt.HasValue
            ? $"По данным на {Result.CheckedAt.Value:dd.MM.yyyy HH:mm}"
            : "";
    }

    private void SetOutcome(Label status, Label note, CheckOutcome outcome, string noteText)
    {
        status.Text = outcome.DisplayText();
        status.ForeColor = outcome switch
        {
            CheckOutcome.Clean => Green,
            CheckOutcome.Risk => Red,
            _ => Orange
        };
        note.Text = noteText;
    }

    private void UpdateRouteUi()
    {
        _lblRouteFrom.Text = $"Откуда: {_svc.CurrencyService.Settings.WarehouseCity}";
        _lblRouteTo.Text = string.IsNullOrEmpty(Result.RouteError)
            ? $"Куда: {Result.RouteTo}"
            : $"Куда: {Result.RouteTo}  ({Result.RouteError})";
        _lblRouteTo.ForeColor = string.IsNullOrEmpty(Result.RouteError) ? UI.TextDark : Red;
        _lblDistance.Text = Result.DistanceKm > 0 ? $"Расстояние: {Result.DistanceKm} км" : "Расстояние: —";
    }

    private void UpdateWeatherUi()
    {
        if (!Result.WeatherLoaded)
        {
            _lblWeather.Text = "—";
            _lblWeatherRisk.Text = "";
            _lblWeatherDetails.Text = "";
            _lblWeatherWarn.Text = Result.WeatherError;
            return;
        }

        _lblWeather.Text = $"{Result.Temperature:+0;-0;0}°C  {Result.Condition}";
        _lblWeatherDetails.Text =
            $"Ощущается как {Result.FeelsLike:+0;-0;0}°C,   влажность {Result.Humidity}%,   ветер {Result.WindSpeed:0.#} м/с {Result.WindDirection},   прогноз на {Result.ForecastDate:dd.MM.yyyy}";

        (string text, Color color) = Result.Risk switch
        {
            WeatherRisk.High => ("Погодный риск: ВЫСОКИЙ", Red),
            WeatherRisk.Medium => ("Погодный риск: средний", Orange),
            _ => ("Погодный риск: низкий", Green)
        };
        _lblWeatherRisk.Text = text;
        _lblWeatherRisk.ForeColor = color;

        _lblWeatherWarn.Text = Result.Risk == WeatherRisk.High
            ? "Внимание: аномальная температура в регионе. Высокий риск повреждения хрупких/чувствительных товаров."
            : "";
    }

    private void UpdateRecommendationUi()
    {
        SetRec(_lblInsurance, Result.InsuranceRecommendation);
        _btnApply.Text = Result.RecommendationsApplied ? "Рекомендации применены" : "Применить рекомендации";
    }

    private void SetRec(Label l, string text)
    {
        l.Text = string.IsNullOrWhiteSpace(text) ? "—" : text;
        l.ForeColor = text.StartsWith("Обязател", StringComparison.OrdinalIgnoreCase) ? Red
            : text.StartsWith("Рекоменд", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Желат", StringComparison.OrdinalIgnoreCase) ? Orange
            : UI.TextGray;
    }

    private static Label Title(string text, float size, int y) =>
        new() { Text = text, Font = UI.Px(size, FontStyle.Bold), ForeColor = UI.TextDark, Location = new Point(24, y), AutoSize = true, BackColor = Color.Transparent };

    private static Label Caption(string text, int x, int y, bool bold = false) =>
        new() { Text = text, Font = bold ? UI.FontMedBold : UI.FontMed, ForeColor = UI.TextDark, Location = new Point(x, y), AutoSize = true, BackColor = Color.Transparent };

    private static Label ValueLabel(Control host, string text, int x, int y, int w)
    {
        var l = new Label { Text = text, Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = new Rectangle(x, y, w, 28), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true };
        host.Controls.Add(l);
        return l;
    }

    private TextBox FieldBox(Control card, int x, int y, int w)
    {
        var bg = Color.FromArgb(245, 247, 250);
        var hostPanel = UI.CreatePanel(bg);
        hostPanel.Bounds = new Rectangle(x, y, w, 42);
        hostPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(200, 205, 215), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, hostPanel.Width - 1, hostPanel.Height - 1);
        };
        card.Controls.Add(hostPanel);
        var box = new TextBox { BorderStyle = BorderStyle.None, Font = UI.FontMed, BackColor = bg, ForeColor = UI.TextDark };
        hostPanel.Controls.Add(box);
        UI.BindControlToHost(hostPanel, box, new Padding(12, 6, 12, 6), verticalOffset: -1);
        return box;
    }

    private (Label status, Label note) ResultRow(Control card, string caption, int x, int y)
    {
        card.Controls.Add(new Label { Text = caption, Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = new Rectangle(x, y, 430, 30), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true });
        var status = new Label { Text = "—", Font = UI.FontMedBold, ForeColor = Neutral, Bounds = new Rectangle(x + 440, y, card.Width - x - 460, 30), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        card.Controls.Add(status);
        var note = new Label { Text = "", Font = UI.FontTiny, ForeColor = Neutral, Bounds = new Rectangle(x, y + 28, card.Width - x - 24, 22), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        card.Controls.Add(note);
        return (status, note);
    }

    private Label RecRow(Control card, string caption, int x, int y)
    {
        card.Controls.Add(new Label { Text = caption, Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = new Rectangle(x, y, 290, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
        var val = new Label { Text = "—", Font = UI.FontMed, ForeColor = UI.TextGray, Bounds = new Rectangle(x + 300, y, card.Width - x - 320, 32), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        card.Controls.Add(val);
        return val;
    }
}
