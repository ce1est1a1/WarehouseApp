using WarehouseApp.Models;

namespace WarehouseApp.Forms;

public class ProductEditForm : Form
{
    private readonly AppServices _svc;
    private readonly Product? _existing;

    private TextBox _txtName = null!;
    private TextBox _txtArticle = null!;
    private TextBox _txtUnit = null!;
    private TextBox _txtDescription = null!;
    private NumericUpDown _numPrice = null!;
    private NumericUpDown? _numStock;
    private ComboBox _cmbCategory = null!;
    private CoverPictureBox _picPreview = null!;
    private Label _lblPhotoInfo = null!;
    private Label _lblPreviewPlaceholder = null!;
    private string? _imagePath;
    private string _generatedArticle = string.Empty;

    private readonly List<TextBox> _extraRows = new();
    private Panel _extrasContent = null!;

    private const int FieldHostHeight = 54;
    private const int FieldGap = 10;
    private const int LabelYOffset = 10;
    private const int InputPadLeft = 18;
    private const int InputPadRight = 18;
    private const int TextVerticalOffset = -2;
    private const int DescriptionHostHeight = 112;
    private const int ExtraRowHeight = 54;
    private const int ExtraRowGap = 8;

    public ProductEditForm(AppServices svc, Product? existing)
    {
        _svc = svc;
        _existing = existing;
        _imagePath = existing?.ImagePath;
        _generatedArticle = existing?.Article ?? GenerateArticle();
        Build();
        if (existing != null) Fill(existing);
        else _txtArticle.Text = _generatedArticle;
        UpdatePreview();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000;
            return cp;
        }
    }

    private void Build()
    {
        Text = _existing != null ? "Редактировать товар" : "Новый товар";
        ClientSize = new Size(1080, 960);
        MinimumSize = new Size(1080, 920);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = UI.BgLight; Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;

        var root = UI.CreatePanel(UI.BgLight); root.Dock = DockStyle.Fill; root.Padding = new Padding(28); Controls.Add(root);
        var card = UI.CreateRoundedPanel(UI.BgCard, 28); card.Dock = DockStyle.Fill; root.Controls.Add(card);
        var content = UI.CreateScrollPanel(Color.Transparent); content.Dock = DockStyle.Fill; card.Controls.Add(content);
        var footer = UI.CreatePanel(Color.Transparent); footer.Dock = DockStyle.Bottom; footer.Height = 90; card.Controls.Add(footer); footer.BringToFront();

        content.Controls.Add(new Label { Text = _existing != null ? "Редактирование товара" : "Создание товара", Font = UI.Px(26), ForeColor = UI.TextDark, Bounds = new Rectangle(38, 28, 460, 40), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });

        int labelX = 48, inputX = 290, inputW = 600, y = 94;

        y = AddField(content, "Название", out _txtName, labelX, inputX, inputW, y);
        y = AddField(content, "Артикул", out _txtArticle, labelX, inputX, inputW, y, true);
        y = AddCategoryField(content, labelX, inputX, inputW, y);
        y = AddField(content, "Ед. измерения", out _txtUnit, labelX, inputX, inputW, y); _txtUnit.Text = "шт.";
        y = AddPriceField(content, labelX, inputX, y);
        if (_existing == null) y = AddStockField(content, labelX, inputX, y);
        y = AddDescriptionField(content, labelX, inputX, inputW, y);

        content.Controls.Add(CreateLabel("Фото", labelX, y + 10));
        var btnPhoto = UI.CreatePillButton("Выбрать фото", UI.BtnBlue, new Size(240, 48), UI.FontMed);
        btnPhoto.Location = new Point(inputX, y); btnPhoto.Click += (_, _) => ChoosePhoto(); content.Controls.Add(btnPhoto);

        _lblPhotoInfo = new Label { Text = "Фото не выбрано", Font = UI.FontTiny, ForeColor = UI.TextGray, Bounds = new Rectangle(inputX, y + 54, 240, 22), AutoEllipsis = true, BackColor = Color.Transparent };
        content.Controls.Add(_lblPhotoInfo);

        var previewHost = UI.CreateRoundedPanel(Color.White, 18); previewHost.Bounds = new Rectangle(inputX + 260, y, 330, 140); content.Controls.Add(previewHost);
        _lblPreviewPlaceholder = new Label { Text = "фото товара", Bounds = new Rectangle(0, 0, 330, 140), Font = UI.Px(18), ForeColor = UI.TextGray, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
        previewHost.Controls.Add(_lblPreviewPlaceholder);
        _picPreview = new CoverPictureBox { Bounds = new Rectangle(0, 0, 330, 140), BackColor = Color.White }; previewHost.Controls.Add(_picPreview);
        previewHost.Resize += (_, _) => { _picPreview.Bounds = new Rectangle(0, 0, previewHost.Width, previewHost.Height); _lblPreviewPlaceholder.Bounds = new Rectangle(0, 0, previewHost.Width, previewHost.Height); };

        y += 156;

        y = AddCharacteristicsSection(content, labelX, inputX, inputW, y);

        content.Controls.Add(new Panel { Bounds = new Rectangle(0, y + 20, 10, 10), BackColor = Color.Transparent });

        AddButtons(footer);
    }

    private int AddCharacteristicsSection(Panel parent, int labelX, int inputX, int inputW, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = "Характеристики",
            Font = UI.Px(18),
            ForeColor = UI.TextDark,
            Bounds = new Rectangle(labelX, y + 6, 220, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        });

        var btnAdd = UI.CreatePillButton("+ Добавить", UI.BtnBlue, new Size(160, 40), UI.FontMed);
        btnAdd.Location = new Point(inputX + inputW - btnAdd.Width, y);
        btnAdd.Click += (_, _) => AddExtraRow(parent, labelX, inputX, inputW);
        parent.Controls.Add(btnAdd);

        y += btnAdd.Height + 8;

        _extrasContent = UI.CreatePanel(Color.Transparent);
        _extrasContent.Bounds = new Rectangle(inputX, y, inputW, 0);
        parent.Controls.Add(_extrasContent);

        return y;
    }

    private void AddExtraRow(Panel parent, int labelX, int inputX, int inputW, string? value = null, bool deferRefresh = false)
    {
        int currentY = _extraRows.Count * (ExtraRowHeight + ExtraRowGap);

        var rowPanel = UI.CreatePanel(Color.Transparent);
        rowPanel.SuspendLayout();
        rowPanel.Bounds = new Rectangle(0, currentY, inputW, ExtraRowHeight);

        var host = UI.CreateRoundedPanel(UI.InputWhite, 14);
        host.Bounds = new Rectangle(0, 0, inputW - 54, ExtraRowHeight);
        rowPanel.Controls.Add(host);

        var lblNum = new Label
        {
            Text = $"{_extraRows.Count + 1}.",
            Font = UI.FontMedBold,
            ForeColor = UI.TextGray,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        host.Controls.Add(lblNum);

        var tb = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.FontMed,
            BackColor = UI.InputWhite,
            ForeColor = UI.TextDark,
            PlaceholderText = $"Характеристика {_extraRows.Count + 1}",
            Text = value ?? ""
        };
        host.Controls.Add(tb);

        void LayoutHost()
        {
            lblNum.Bounds = new Rectangle(8, 0, 30, host.Height);
            UI.LayoutControlInHost(host, tb, new Padding(42, 5, 10, 5), TextVerticalOffset);
        }

        host.Resize += (_, _) => LayoutHost();
        tb.FontChanged += (_, _) => LayoutHost();
        LayoutHost();

        var btnRemove = UI.CreateCircleButton("X", UI.BtnRed, 36, UI.Px(14));
        btnRemove.Location = new Point(inputW - btnRemove.Width - 10, Math.Max(0, (ExtraRowHeight - btnRemove.Height) / 2));
        btnRemove.Click += (_, _) => RemoveExtraRow(parent, labelX, inputX, inputW, rowPanel, tb);
        rowPanel.Controls.Add(btnRemove);

        rowPanel.Tag = lblNum;

        _extraRows.Add(tb);
        _extrasContent.Controls.Add(rowPanel);
        rowPanel.ResumeLayout(false);

        if (!deferRefresh)
            RefreshExtrasHeight(parent, inputX, inputW);
    }

    private void RemoveExtraRow(Panel parent, int labelX, int inputX, int inputW, Panel rowToRemove, TextBox tb)
    {
        _extrasContent.SuspendLayout();
        try
        {
            _extraRows.Remove(tb);
            _extrasContent.Controls.Remove(rowToRemove);
            rowToRemove.Dispose();

            int idx = 0;
            foreach (Control ctrl in _extrasContent.Controls)
            {
                if (ctrl is Panel rp)
                {
                    rp.Location = new Point(0, idx * (ExtraRowHeight + ExtraRowGap));
                    if (rp.Tag is Label lbl) lbl.Text = $"{idx + 1}.";
                    idx++;
                }
            }
        }
        finally
        {
            _extrasContent.ResumeLayout(false);
        }

        RefreshExtrasHeight(parent, inputX, inputW);
    }

    private void RefreshExtrasHeight(Panel parent, int inputX, int inputW)
    {
        int newH = _extraRows.Count > 0
            ? _extraRows.Count * (ExtraRowHeight + ExtraRowGap) - ExtraRowGap
            : 0;

        _extrasContent.Height = newH;

        if (parent is SmoothScrollPanel sp)
            sp.UpdateScrollbar();
    }

    private static RoundedPanel CreateInputHost(Control parent, int x, int y, int width, int height = FieldHostHeight)
    {
        var host = UI.CreateRoundedPanel(UI.InputWhite, 16);
        host.Bounds = new Rectangle(x, y, width, height);
        parent.Controls.Add(host);
        return host;
    }

    private static void BindInputControl(Control host, Control child, Padding? padding = null, int verticalOffset = TextVerticalOffset)
    {
        UI.BindControlToHost(
            host,
            child,
            padding ?? new Padding(InputPadLeft, 5, InputPadRight, 5),
            verticalOffset);
    }

    private static Label CreateFormLabel(string text, int x, int y)
        => new()
        {
            Text = text,
            Font = UI.FontSmall,
            ForeColor = UI.TextDark,
            Bounds = new Rectangle(x, y, 220, 32),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

    private int AddField(Panel parent, string label, out TextBox txt, int labelX, int inputX, int inputW, int y, bool readOnly = false)
    {
        parent.Controls.Add(CreateFormLabel(label, labelX, y + LabelYOffset));

        var host = CreateInputHost(parent, inputX, y, inputW);

        txt = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.FontMed,
            BackColor = UI.InputWhite,
            ForeColor = readOnly ? UI.TextGray : UI.TextDark,
            ReadOnly = readOnly,
            TabStop = !readOnly
        };

        host.Controls.Add(txt);
        BindInputControl(host, txt);

        return y + FieldHostHeight + FieldGap;
    }

    private int AddCategoryField(Panel parent, int labelX, int inputX, int inputW, int y)
    {
        parent.Controls.Add(CreateFormLabel("Категория", labelX, y + LabelYOffset));

        var host = CreateInputHost(parent, inputX, y, inputW);

        _cmbCategory = new ComboBox
        {
            Font = UI.FontMed,
            FlatStyle = FlatStyle.Standard,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = UI.InputWhite,
            ForeColor = UI.TextDark,
            IntegralHeight = false,
            MaxDropDownItems = 12,
            ItemHeight = Math.Max(28, UI.FontMed.Height + 4),
            DisplayMember = "Name"
        };

        _cmbCategory.Items.Add("(без категории)");
        _cmbCategory.SelectedIndex = 0;
        foreach (var cat in _svc.CategoryService.GetAll())
        {
            _cmbCategory.Items.Add(cat);
            if (_existing?.CategoryId == cat.Id)
                _cmbCategory.SelectedItem = cat;
        }

        host.Controls.Add(_cmbCategory);
        BindInputControl(host, _cmbCategory, new Padding(14, 5, 14, 5), verticalOffset: 0);

        return y + FieldHostHeight + FieldGap;
    }

    private int AddPriceField(Panel parent, int labelX, int inputX, int y)
    {
        string currencySymbol = _svc.CurrencyService.Settings.CurrencySymbol;
        parent.Controls.Add(CreateFormLabel($"Цена ({currencySymbol})", labelX, y + LabelYOffset));

        var host = CreateInputHost(parent, inputX, y, 400);
        _numPrice = CreateNumeric(host, 999999999, 2);

        return y + FieldHostHeight + FieldGap;
    }

    private int AddStockField(Panel parent, int labelX, int inputX, int y)
    {
        parent.Controls.Add(CreateFormLabel("Начальный остаток", labelX, y + LabelYOffset));

        var host = CreateInputHost(parent, inputX, y, 400);
        _numStock = CreateNumeric(host, 999999999, 0);

        return y + FieldHostHeight + FieldGap;
    }

    private int AddDescriptionField(Panel parent, int labelX, int inputX, int inputW, int y)
    {
        parent.Controls.Add(CreateFormLabel("Описание", labelX, y + LabelYOffset));

        var host = CreateInputHost(parent, inputX, y, inputW, DescriptionHostHeight);

        _txtDescription = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.FontMed,
            BackColor = UI.InputWhite,
            ForeColor = UI.TextDark,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        host.Controls.Add(_txtDescription);

        void LayoutDescription()
        {
            _txtDescription.Bounds = new Rectangle(
                InputPadLeft,
                12,
                Math.Max(0, host.Width - InputPadLeft - InputPadRight),
                Math.Max(0, host.Height - 24));
        }

        host.Resize += (_, _) => LayoutDescription();
        _txtDescription.FontChanged += (_, _) => LayoutDescription();
        LayoutDescription();

        return y + DescriptionHostHeight + FieldGap;
    }

    private NumericUpDown CreateNumeric(Control parent, decimal max, int decimals)
    {
        var n = new NumericUpDown
        {
            Font = UI.FontMed,
            Minimum = 0,
            Maximum = max,
            DecimalPlaces = decimals,
            BorderStyle = BorderStyle.None,
            BackColor = UI.InputWhite,
            ForeColor = UI.TextDark,
            ThousandsSeparator = true
        };

        parent.Controls.Add(n);
        BindInputControl(parent, n);

        return n;
    }

    private Label CreateLabel(string text, int x, int y) => CreateFormLabel(text, x, y);

    private void AddButtons(Panel footer)
    {
        var btnSave = UI.CreatePillButton("Сохранить", UI.BtnGreen, new Size(200, 52), UI.FontMedBold); btnSave.Click += BtnSave_Click; footer.Controls.Add(btnSave);
        var btnCancel = UI.CreatePillButton("Отмена", UI.TabInactive, new Size(180, 52), UI.FontMed); btnCancel.Click += (_, _) => Close(); footer.Controls.Add(btnCancel);
        Button? btnDelete = null;
        if (_existing != null) { btnDelete = UI.CreatePillButton("Удалить", UI.BtnRed, new Size(180, 52), UI.FontMedBold); btnDelete.Click += BtnDel_Click; footer.Controls.Add(btnDelete); }

        void Layout()
        {
            int gap = 18;
            if (btnDelete != null)
            {
                int total = btnSave.Width + gap + btnDelete.Width + gap + btnCancel.Width;
                int sx = Math.Max(18, (footer.ClientSize.Width - total) / 2);
                int by = Math.Max(12, (footer.Height - btnSave.Height) / 2);
                btnSave.Location = new Point(sx, by); btnDelete.Location = new Point(btnSave.Right + gap, by); btnCancel.Location = new Point(btnDelete.Right + gap, by);
            }
            else
            {
                int total = btnSave.Width + gap + btnCancel.Width;
                int sx = Math.Max(18, (footer.ClientSize.Width - total) / 2);
                int by = Math.Max(12, (footer.Height - btnSave.Height) / 2);
                btnSave.Location = new Point(sx, by); btnCancel.Location = new Point(btnSave.Right + gap, by);
            }
        }
        footer.Resize += (_, _) => Layout(); footer.HandleCreated += (_, _) => Layout(); Layout();
    }

    private string GenerateArticle()
    {
        int max = 0;
        foreach (var p in _svc.ProductService.GetAll()) { var d = new string((p.Article ?? "").Where(char.IsDigit).ToArray()); if (int.TryParse(d, out var v) && v > max) max = v; }
        return (max + 1).ToString();
    }

    private void ChoosePhoto()
    {
        using var dlg = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images"); Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, $"{Guid.NewGuid()}{Path.GetExtension(dlg.FileName)}"); File.Copy(dlg.FileName, dest, true);
        _imagePath = dest; UpdatePreview();
    }

    private void UpdatePreview()
    {
        _picPreview.Image?.Dispose(); _picPreview.Image = null;
        if (!string.IsNullOrWhiteSpace(_imagePath) && File.Exists(_imagePath))
        {
            try
            {
                using var src = Image.FromFile(_imagePath!);
                _picPreview.Image = new Bitmap(src);
                _picPreview.BringToFront();
                _lblPhotoInfo.Text = Path.GetFileName(_imagePath);
                _lblPreviewPlaceholder.Visible = false;
                return;
            }
            catch { }
        }
        _lblPhotoInfo.Text = "Фото не выбрано";
        _lblPreviewPlaceholder.Visible = true;
        _lblPreviewPlaceholder.BringToFront();
    }

    private static string SerializeExtras(IEnumerable<string> values)
        => string.Join("||", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));

    private static string[] ParseExtras(string? field1, string? field2)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(field1))
        {
            var parts = field1.Contains("||") ? field1.Split("||") : new[] { field1 };
            list.AddRange(parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        if (!string.IsNullOrWhiteSpace(field2) && !field2.Contains("||"))
            list.Add(field2);
        return list.ToArray();
    }

    private void Fill(Product p)
    {
        _txtName.Text = p.Name;
        _txtArticle.Text = p.Article; _txtArticle.ReadOnly = true;
        _txtUnit.Text = p.Unit;
        _txtDescription.Text = p.Description ?? "";

        var settings = _svc.CurrencyService.Settings;
        decimal displayPrice = settings.ConvertFromRub(p.PurchasePrice);
        _numPrice.Value = Math.Min(displayPrice, _numPrice.Maximum);

        if (_numStock != null) _numStock.Value = Math.Min(p.StockQuantity, (int)_numStock.Maximum);

        var extras = ParseExtras(p.ExtraField1, p.ExtraField2);

        var parent = _extrasContent.Parent as Panel;
        if (parent != null && extras.Length > 0)
        {
            int inputX = _extrasContent.Left;
            int inputW = _extrasContent.Width;

            _extrasContent.SuspendLayout();
            try
            {
                foreach (var val in extras)
                    AddExtraRow(parent, 48, inputX, inputW, val, deferRefresh: true);
            }
            finally
            {
                _extrasContent.ResumeLayout(false);
            }
            RefreshExtrasHeight(parent, inputX, inputW);
        }
    }

    private Product ToProduct()
    {
        var settings = _svc.CurrencyService.Settings;

        decimal priceInRub = settings.ConvertToRub(_numPrice.Value);

        var extras = _extraRows.Select(tb => tb.Text.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        var p = new Product
        {
            Name = _txtName.Text.Trim(),
            Article = _generatedArticle,
            Unit = _txtUnit.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(_txtDescription.Text) ? null : _txtDescription.Text.Trim(),
            PurchasePrice = priceInRub,
            StockQuantity = _existing?.StockQuantity ?? (int)(_numStock?.Value ?? 0),
            ImagePath = _imagePath,
            ExtraField1 = extras.Count > 0 ? SerializeExtras(extras) : null,
            ExtraField2 = null
        };
        if (_existing != null) { p.Id = _existing.Id; p.Article = _existing.Article; }
        if (_cmbCategory.SelectedItem is Category cat) p.CategoryId = cat.Id;
        return p;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show("Поле «Название» обязательно.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtName.Focus(); return; }
        if (string.IsNullOrWhiteSpace(_txtUnit.Text)) { MessageBox.Show("Поле «Ед. измерения» обязательно.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtUnit.Focus(); return; }
        var product = ToProduct();
        var result = _existing != null ? _svc.ProductService.Update(product) : _svc.ProductService.Create(product);
        if (!result.Success) { MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        DialogResult = DialogResult.OK; Close();
    }

    private void BtnDel_Click(object? sender, EventArgs e)
    {
        if (_existing == null) return;
        if (MessageBox.Show($"Удалить «{_existing.Name}»?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var result = _svc.ProductService.Delete(_existing.Id);
        if (!result.Success) { MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        DialogResult = DialogResult.OK; Close();
    }
}
