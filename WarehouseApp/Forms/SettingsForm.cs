namespace WarehouseApp.Forms;

public class SettingsForm : Form
{
    private readonly AppServices _svc;
    private ComboBox _cmbCurrency = null!;
    private Label _lblUsd = null!;
    private Label _lblEur = null!;
    private Label _lblUsdt = null!;
    private Label _lblUpdated = null!;

    public SettingsForm(AppServices svc)
    {
        _svc = svc;
        Build();
    }

    private void Build()
    {
        Text = "Настройки";
        ClientSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;

        var card = UI.CreateRoundedPanel(UI.BgCard, 24);
        card.Bounds = new Rectangle(20, 20, 520, 380);
        Controls.Add(card);

        card.Controls.Add(new Label
        {
            Text = "Настройки приложения",
            Font = UI.Px(24),
            ForeColor = UI.TextDark,
            Bounds = new Rectangle(24, 20, 460, 36),
            BackColor = Color.Transparent
        });

        card.Controls.Add(MakeLabel("Валюта:", 24, 80));

        var cmbHost = UI.CreateRoundedPanel(UI.InputWhite, 12);
        cmbHost.Bounds = new Rectangle(260, 74, 220, 42);
        card.Controls.Add(cmbHost);

        _cmbCurrency = new ComboBox
        {
            Font = UI.FontMed,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Standard,
            BackColor = UI.InputWhite,
            ForeColor = UI.TextDark,
            Items = { "RUB (Рубль)", "USD (Доллар)", "EUR (Евро)", "USDT (Tether)" }
        };
        cmbHost.Controls.Add(_cmbCurrency);
        UI.BindControlToHost(cmbHost, _cmbCurrency, new Padding(8, 5, 8, 5));

        var settings = _svc.CurrencyService.Settings;
        _cmbCurrency.SelectedIndex = settings.Currency switch
        {
            "USD" => 1, "EUR" => 2, "USDT" => 3, _ => 0
        };

        card.Controls.Add(MakeLabel("Текущие курсы к рублю:", 24, 140));

        _lblUsd = MakeLabel($"1 USD = {settings.UsdRate:N2} ₽", 40, 178);
        card.Controls.Add(_lblUsd);
        _lblEur = MakeLabel($"1 EUR = {settings.EurRate:N2} ₽", 40, 210);
        card.Controls.Add(_lblEur);
        _lblUsdt = MakeLabel($"1 USDT = {settings.UsdtRate:N2} ₽", 40, 242);
        card.Controls.Add(_lblUsdt);

        _lblUpdated = new Label
        {
            Font = UI.FontTiny,
            ForeColor = UI.TextGray,
            Bounds = new Rectangle(40, 274, 400, 22),
            BackColor = Color.Transparent,
            Text = settings.RatesUpdatedAt > DateTime.MinValue
                ? $"Обновлено: {settings.RatesUpdatedAt:dd.MM.yyyy HH:mm}"
                : "Курсы ещё не обновлялись (используются значения по умолчанию)"
        };
        card.Controls.Add(_lblUpdated);

        var btnUpdate = UI.CreatePillButton("Обновить курсы", UI.BtnBlue, new Size(200, 42), UI.FontMed);
        btnUpdate.Location = new Point(24, 310);
        btnUpdate.Click += async (_, _) =>
        {
            btnUpdate.Enabled = false;
            btnUpdate.Text = "Обновление...";
            await _svc.CurrencyService.UpdateRatesAsync();
            var s = _svc.CurrencyService.Settings;
            _lblUsd.Text = $"1 USD = {s.UsdRate:N2} ₽";
            _lblEur.Text = $"1 EUR = {s.EurRate:N2} ₽";
            _lblUsdt.Text = $"1 USDT = {s.UsdtRate:N2} ₽";
            _lblUpdated.Text = s.RatesUpdatedAt > DateTime.MinValue
                ? $"Обновлено: {s.RatesUpdatedAt:dd.MM.yyyy HH:mm}"
                : "Не удалось обновить (нет сети)";
            btnUpdate.Text = "Обновить курсы";
            btnUpdate.Enabled = true;
        };
        card.Controls.Add(btnUpdate);

        var btnSave = UI.CreatePillButton("Сохранить", UI.BtnGreen, new Size(160, 42), UI.FontMedBold);
        btnSave.Location = new Point(240, 310);
        btnSave.Click += (_, _) =>
        {
            string currency = _cmbCurrency.SelectedIndex switch
            {
                1 => "USD", 2 => "EUR", 3 => "USDT", _ => "RUB"
            };
            _svc.CurrencyService.SetCurrency(currency);
            DialogResult = DialogResult.OK;
            Close();
        };
        card.Controls.Add(btnSave);

        var btnCancel = UI.CreatePillButton("Отмена", UI.TabInactive, new Size(100, 42), UI.FontMed);
        btnCancel.Location = new Point(410, 310);
        btnCancel.Click += (_, _) => Close();
        card.Controls.Add(btnCancel);
    }

    private static Label MakeLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Font = UI.FontMed,
            ForeColor = UI.TextDark,
            AutoSize = true,
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };
    }
}
