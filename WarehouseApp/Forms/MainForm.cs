using WarehouseApp.Models;

namespace WarehouseApp.Forms;

public class MainForm : Form
{
    private readonly AppServices _svc;
    private User Me => _svc.AuthService.CurrentUser!;

    private Panel _topBar = null!;
    private Panel _headerRow = null!;
    private Panel _searchHost = null!;
    private TextBox _txtSearch = null!;
    private SmoothScrollPanel _scrollArea = null!;
    private Panel? _footer;
    private Panel? _detailPanel;
    private Panel? _detailContent;
    private Button _fab = null!;
    private Panel _loginHost = null!;
    private Label _lblLogin = null!;
    private Button _btnExit = null!;
    private Button _btnSettings = null!;
    private readonly List<Button> _tabs = new();
    private readonly List<string> _tabKeys = new();
    private Panel _tabsViewport = null!;
    private Button _tabScrollLeft = null!;
    private Button _tabScrollRight = null!;
    private int _tabScrollOffset = 0;
    private int _tabContentWidth = 0;
    private readonly Dictionary<Button, int> _tabOriginalX = new();
    private readonly Dictionary<Button, Label> _tabCloseLabels = new();

    private string _view = "catalog";
    private readonly HashSet<int> _expandedCats = new() { -1 };
    private readonly HashSet<int> _expandedShipments = new();
    private readonly HashSet<int> _expandedSupplies = new();
    private readonly Dictionary<int, ShipmentDraft> _drafts = new();
    private readonly List<int> _draftOrder = new();
    private int _nextDraftId = 1;

    private TextBox? _txtRecipient;
    private TextBox? _txtAddress;
    private Label? _lblTotalVal;
    private Panel? _recipientHost;
    private Panel? _addressHost;
    private Panel? _totalHost;
    private Button? _btnShip;
    private Button? _btnLogistics;

    public MainForm(AppServices svc)
    {
        _svc = svc;
        Build();
        ShowCatalog();
    }

    private string FmtPrice(decimal rub) => _svc.CurrencyService.FormatPrice(rub);

    private string FmtPriceAt(decimal rub, Shipment s) =>
        _svc.CurrencyService.FormatPriceAt(rub,
            s.GetStoredRate(_svc.CurrencyService.Settings.Currency));

    private string FmtPriceAt(decimal rub, Supply s) =>
        _svc.CurrencyService.FormatPriceAt(rub,
            s.GetStoredRate(_svc.CurrencyService.Settings.Currency));
    private string CurSymbol => _svc.CurrencyService.Settings.CurrencySymbol;

    private void Build()
    {
        Text = $"Складской учёт — {Me.RoleDisplayName}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1360, 860);
        WindowState = FormWindowState.Maximized;
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;

        BuildTopBar();
        BuildHeaderRow();
        BuildScrollArea();
        BuildFab();

        Resize += (_, _) => LayoutShell();
        Shown += (_, _) =>
        {
            LayoutShell();
            AutoWriteOffOverdue();
            RefreshContent();
        };
    }

    private void BuildTopBar()
    {
        _topBar = UI.CreatePanel(UI.TopBar);
        _topBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_topBar);

        _tabsViewport = UI.CreatePanel(Color.Transparent);
        _tabsViewport.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _tabsViewport.AutoScroll = false;
        _topBar.Controls.Add(_tabsViewport);

        _tabScrollLeft = new Button
        {
            Text = "<", FlatStyle = FlatStyle.Flat, Font = UI.Px(14, FontStyle.Bold),
            BackColor = UI.TopBar, ForeColor = Color.White,
            Size = new Size(28, UI.TabHeight), Cursor = Cursors.Hand, Visible = false
        };
        _tabScrollLeft.FlatAppearance.BorderSize = 0;
        _tabScrollLeft.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 80, 140);
        _tabScrollLeft.Click += (_, _) => { _tabScrollOffset = Math.Max(0, _tabScrollOffset - 200); ApplyTabScroll(); };
        _topBar.Controls.Add(_tabScrollLeft);

        _tabScrollRight = new Button
        {
            Text = ">", FlatStyle = FlatStyle.Flat, Font = UI.Px(14, FontStyle.Bold),
            BackColor = UI.TopBar, ForeColor = Color.White,
            Size = new Size(28, UI.TabHeight), Cursor = Cursors.Hand, Visible = false
        };
        _tabScrollRight.FlatAppearance.BorderSize = 0;
        _tabScrollRight.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 80, 140);
        _tabScrollRight.Click += (_, _) => { _tabScrollOffset = Math.Min(_tabContentWidth - _tabsViewport.Width, _tabScrollOffset + 200); ApplyTabScroll(); };
        _topBar.Controls.Add(_tabScrollRight);

        _loginHost = UI.CreateRoundedPanel(UI.TabActive, 14);
        _loginHost.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _topBar.Controls.Add(_loginHost);

        _lblLogin = new Label
        {
            Text = Me.Login,
            Font = UI.Px(18),
            ForeColor = UI.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        _loginHost.Controls.Add(_lblLogin);

        _btnSettings = UI.CreatePillButton("Настройки", Color.FromArgb(60, 80, 140), new Size(150, UI.TabHeight), UI.Px(17));
        _btnSettings.ForeColor = Color.White;
        _btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnSettings.Click += (_, _) =>
        {
            using var form = new SettingsForm(_svc);
            if (form.ShowDialog(this) == DialogResult.OK)
                RefreshContent();
        };
        _topBar.Controls.Add(_btnSettings);

        _btnExit = UI.CreatePillButton("Выйти", UI.BtnLogout, new Size(130, UI.TabHeight), UI.Px(18));
        _btnExit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnExit.Click += (_, _) => Close();
        _topBar.Controls.Add(_btnExit);

        RebuildTabs();
    }

    private void BuildHeaderRow()
    {
        _headerRow = UI.CreatePanel(UI.HeaderRow);
        _headerRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _headerRow.Paint += HeaderRow_Paint;
        Controls.Add(_headerRow);

        _searchHost = UI.CreateSearchHost();
        _headerRow.Controls.Add(_searchHost);

        _txtSearch = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.Px(20),
            BackColor = Color.White,
            ForeColor = UI.TextDark,
            PlaceholderText = "Поиск",
            TextAlign = HorizontalAlignment.Center
        };
        _txtSearch.TextChanged += (_, _) => RefreshContent();
        _searchHost.Controls.Add(_txtSearch);
        UI.BindControlToHost(_searchHost, _txtSearch, new Padding(16, 6, 16, 6), verticalOffset: -1);
    }

    private void BuildScrollArea()
    {
        _scrollArea = UI.CreateScrollPanel(Color.White);
        _scrollArea.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_scrollArea);
    }

    private void BuildFab()
    {
        _fab = UI.CreateCircleButton("🛒", UI.FabOrange, 96, UI.Px(34, FontStyle.Regular, "Segoe UI Emoji"));
        _fab.Click += (_, _) => OnFabClick();
        Controls.Add(_fab);
    }

    private void RebuildTabs()
    {
        foreach (Control c in _tabsViewport.Controls) c.Dispose();
        _tabsViewport.Controls.Clear();
        _tabs.Clear();
        _tabKeys.Clear();
        _tabOriginalX.Clear();
        _tabCloseLabels.Clear();
        _tabScrollOffset = 0;

        AddTabButton("Каталог товаров", "catalog", () => ShowCatalog());

        AddTabButton("Поставки 📦", "supplies", () => ShowSupplies());

        if (Me.IsAdmin)
        {
            AddTabButton("История отгрузок", "history", () => ShowHistory());
            AddTabButton("Отчётность", "report", () => ShowReportForm());
        }
        else
        {
            foreach (var id in _draftOrder)
            {
                if (!_drafts.TryGetValue(id, out var draft)) continue;
                int capturedId = id;
                AddClosableTabButton(draft.Name, $"shipment_{capturedId}", () => ShowShipmentDraft(capturedId), () => CloseDraftTab(capturedId));
            }
        }

        AddTabButton("Тепловая карта", "heatmap", () => ShowHeatmap());

        LayoutTopBar();
        ApplyActiveTabStyle();
        ScrollActiveTabIntoView();
    }

    private void ScrollActiveTabIntoView()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].BackColor == UI.TabActive)
            {
                if (!_tabOriginalX.TryGetValue(_tabs[i], out int ox)) return;
                int tabRight = ox + _tabs[i].Width;
                int vpW = _tabsViewport.Width;
                if (ox < _tabScrollOffset)
                    _tabScrollOffset = Math.Max(0, ox - 8);
                else if (tabRight > _tabScrollOffset + vpW)
                    _tabScrollOffset = tabRight - vpW + 8;
                ApplyTabScroll();
                return;
            }
        }
    }

    private void AddTabButton(string text, string key, Action click)
    {
        var button = UI.CreatePillButton(text, UI.TabInactive, new Size(200, UI.TabHeight), UI.Px(17));
        button.Click += (_, _) => click();
        _tabs.Add(button);
        _tabKeys.Add(key);
        _tabsViewport.Controls.Add(button);
    }

    private void AddClosableTabButton(string text, string key, Action click, Action close)
    {
        var button = UI.CreatePillButton(text, UI.TabInactive, new Size(200, UI.TabHeight), UI.Px(17));

        button.Padding = new Padding(0, 0, 26, 0);
        button.Click += (_, _) => click();
        _tabs.Add(button);
        _tabKeys.Add(key);
        _tabsViewport.Controls.Add(button);

        var closeLabel = new Label
        {
            Text = "X",
            Font = UI.Px(11),
            ForeColor = UI.TextDark,
            Size = new Size(20, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = button.BackColor
        };
        closeLabel.Click += (_, _) => close();
        button.LocationChanged += (_, _) => closeLabel.Location = new Point(button.Right - 24, button.Top + (UI.TabHeight - 20) / 2);
        button.SizeChanged += (_, _) => closeLabel.Location = new Point(button.Right - 24, button.Top + (UI.TabHeight - 20) / 2);
        _tabsViewport.Controls.Add(closeLabel);
        closeLabel.BringToFront();
        _tabCloseLabels[button] = closeLabel;
    }

    private void CloseDraftTab(int id)
    {
        if (_drafts.TryGetValue(id, out var draft) && draft.Items.Count > 0)
        {
            if (MessageBox.Show($"Закрыть отгрузку «{draft.Name}»? Несохранённые позиции будут удалены.",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }
        _drafts.Remove(id);
        _draftOrder.Remove(id);
        if (_view == $"shipment_{id}")
        {
            if (_draftOrder.Count > 0) ShowShipmentDraft(_draftOrder[0]);
            else ShowCatalog();
        }
        else
        {
            RebuildTabs();
        }
    }

    private void LayoutTopBar()
    {
        if (_topBar == null) return;

        int exitW = 130;
        _btnExit.SetBounds(_topBar.Width - exitW - 16, 12, exitW, UI.TabHeight);
        int setW = 150;
        _btnSettings.SetBounds(_btnExit.Left - setW - 12, 12, setW, UI.TabHeight);

        int loginW = Math.Max(220, Math.Min(400, _topBar.Width / 4));
        _loginHost.SetBounds(_btnSettings.Left - loginW - 12, 12, loginW, UI.TabHeight);

        int arrowW = 28;
        int viewportRight = Math.Max(160, _loginHost.Left - 8);

        _tabOriginalX.Clear();
        int x = 8;
        foreach (var tab in _tabs)
        {
            int w = Math.Max(160, TextRenderer.MeasureText(tab.Text, tab.Font).Width + 60);
            _tabOriginalX[tab] = x;
            tab.SetBounds(x, 12, w, UI.TabHeight);
            x += w + 8;
        }
        _tabContentWidth = x;

        bool needScroll = _tabContentWidth > viewportRight;

        _tabScrollLeft.SetBounds(0, 12, arrowW, UI.TabHeight);
        _tabScrollRight.SetBounds(viewportRight - arrowW, 12, arrowW, UI.TabHeight);
        _tabScrollLeft.Visible = needScroll;
        _tabScrollRight.Visible = needScroll;
        _tabScrollLeft.BringToFront();
        _tabScrollRight.BringToFront();

        int vpLeft = needScroll ? arrowW + 2 : 0;
        int vpWidth = (needScroll ? viewportRight - arrowW - 2 : viewportRight) - vpLeft;
        _tabsViewport.SetBounds(vpLeft, 0, vpWidth, UI.TopBarHeight);
        _tabsViewport.Region = new Region(new Rectangle(0, 0, vpWidth, UI.TopBarHeight));

        int maxOffset = Math.Max(0, _tabContentWidth - vpWidth);
        _tabScrollOffset = Math.Clamp(_tabScrollOffset, 0, maxOffset);
        ApplyTabScroll();
    }

    private void ApplyTabScroll()
    {
        int vpWidth = _tabsViewport.Width;
        int maxOffset = Math.Max(0, _tabContentWidth - vpWidth);
        _tabScrollOffset = Math.Clamp(_tabScrollOffset, 0, maxOffset);

        foreach (var tab in _tabs)
        {
            if (!_tabOriginalX.TryGetValue(tab, out int ox)) continue;
            tab.Left = ox - _tabScrollOffset;

            if (_tabCloseLabels.TryGetValue(tab, out var xLabel))
                xLabel.Location = new Point(tab.Right - 24, tab.Top + (UI.TabHeight - 20) / 2);
        }

        _tabScrollLeft.Enabled = _tabScrollOffset > 0;
        _tabScrollRight.Enabled = _tabScrollOffset < maxOffset;
    }

    private void ApplyActiveTabStyle()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            string key = i < _tabKeys.Count ? _tabKeys[i] : "";
            bool active = key == _view;
            bool history = key == "history";

            Color bg = history ? UI.TabHistoryBg : (active ? UI.TabActive : UI.TabInactive);
            tab.BackColor = bg;
            tab.FlatAppearance.MouseDownBackColor = bg;
            tab.FlatAppearance.MouseOverBackColor = bg;

            if (_tabCloseLabels.TryGetValue(tab, out var xLabel))
                xLabel.BackColor = bg;
        }
    }

    private void LayoutShell()
    {
        _topBar.Bounds = new Rectangle(0, 0, ClientSize.Width, UI.TopBarHeight);
        _headerRow.Bounds = new Rectangle(0, _topBar.Bottom, ClientSize.Width, UI.HeaderRowHeight);

        int footerH = _footer?.Visible == true ? _footer.Height : 0;
        int bodyTop = _headerRow.Bottom;
        int bodyH = Math.Max(200, ClientSize.Height - bodyTop - footerH);

        if (_detailPanel != null)
        {
            int detailW = Math.Max(480, Math.Min(780, (int)(ClientSize.Width * 0.46)));
            _scrollArea.Bounds = new Rectangle(0, bodyTop, ClientSize.Width - detailW, bodyH);
            _detailPanel.Bounds = new Rectangle(_scrollArea.Right, bodyTop, ClientSize.Width - _scrollArea.Right, bodyH);
        }
        else
            _scrollArea.Bounds = new Rectangle(0, bodyTop, ClientSize.Width, bodyH);

        if (_footer != null)
        {
            _footer.Bounds = new Rectangle(0, ClientSize.Height - _footer.Height, ClientSize.Width, _footer.Height);
            LayoutFooter();
        }

        int listW = GetListWidth();
        int searchW = Math.Max(320, Math.Min(600, listW / 3));
        _searchHost.SetBounds((listW - searchW) / 2, 16, searchW, 44);
        UI.LayoutControlInHost(_searchHost, _txtSearch, new Padding(16, 6, 16, 6), verticalOffset: -1);

        LayoutTopBar();
        PositionFab();
        ApplyActiveTabStyle();
        _headerRow.Invalidate();
        _topBar.Invalidate();
        _fab.BringToFront();
    }

    private int GetListWidth() => _detailPanel == null ? ClientSize.Width : _scrollArea.Width;

    private void PositionFab()
    {
        int footerOff = _footer?.Visible == true ? _footer.Height : 0;
        _fab.Location = new Point(ClientSize.Width - _fab.Width - 24, ClientSize.Height - _fab.Height - footerOff - 22);
    }

    private void HeaderRow_Paint(object? sender, PaintEventArgs e)
    {
        int listW = GetListWidth();
        using var brush = new SolidBrush(UI.TextDark);
        using var hf = UI.Px(18);
        using var rf = UI.Px(16);
        var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        string left = _view == "history" ? "Наименование отгрузки"
            : _view == "supplies" ? "Наименование поставки"
            : "Наименование товара";

        e.Graphics.DrawString(left, hf, brush, 20, 20);

        if (_view.StartsWith("shipment_"))
        {
            e.Graphics.DrawString("Цена", rf, brush, new RectangleF(listW - 362, 16, 140, 44), sf);
            e.Graphics.DrawString("Количество", rf, brush, new RectangleF(listW - 214, 16, 140, 44), sf);
        }
        else
        {
            string second = _view == "history" || _view == "supplies" ? "Сумма" : "Цена";
            string third = _view == "history" || _view == "supplies" ? "Дата" : "Остаток";
            e.Graphics.DrawString(second, rf, brush, new RectangleF(listW - 320, 16, 140, 44), sf);
            e.Graphics.DrawString(third, rf, brush, new RectangleF(listW - 160, 16, 130, 44), sf);
        }
    }

    private void ShowCatalog()
    {
        _view = "catalog";
        HideFooter(); HideDetail();
        ConfigureFabForCatalog();
        RebuildTabs();
        RefreshContent();
    }

    private void ShowHistory()
    {
        _view = "history";
        HideFooter(); HideDetail();
        _fab.Visible = false;
        RebuildTabs();
        RefreshContent();
    }

    private void ShowSupplies()
    {
        _view = "supplies";
        HideFooter(); HideDetail();
        ConfigureFabForSupply();
        RebuildTabs();
        RefreshContent();
    }

    private void ShowReportForm()
    {
        using var form = new ReportForm(_svc);
        form.ShowDialog(this);
    }

    private void ShowHeatmap()
    {
        using var form = new HeatmapForm(_svc);
        form.ShowDialog(this);
    }

    private void ConfigureFabForSupply()
    {
        _fab.Visible = true;
        _fab.Text = "+";
        _fab.Font = UI.Px(36, FontStyle.Bold);
        _fab.BackColor = UI.BtnGreen;
        _fab.ForeColor = UI.TextDark;
    }

    private void ShowShipmentDraft(int id)
    {
        if (!_drafts.ContainsKey(id)) return;
        _view = $"shipment_{id}";
        HideDetail();
        ConfigureFabForShipment();
        ShowShipmentFooter(id);
        RebuildTabs();
        RefreshContent();
    }

    private void CreateNewShipmentDraft()
    {
        int id = _nextDraftId++;
        _drafts[id] = new ShipmentDraft { Id = id, Name = GenerateUniqueDraftName() };
        _draftOrder.Add(id);
        RebuildTabs();
        ShowShipmentDraft(id);
    }

    private void ConfigureFabForCatalog()
    {
        _fab.Visible = true;
        if (Me.IsAdmin)
        {
            _fab.Text = "+";
            _fab.Font = UI.Px(36, FontStyle.Bold);
            _fab.BackColor = UI.BtnGreen;
            _fab.ForeColor = UI.TextDark;
        }
        else
        {
            _fab.Text = "🛒";
            _fab.Font = UI.Px(34, FontStyle.Regular, "Segoe UI Emoji");
            _fab.BackColor = UI.FabOrange;
            _fab.ForeColor = UI.TextDark;
        }
    }

    private void ConfigureFabForShipment()
    {
        _fab.Visible = true;
        _fab.Text = "+";
        _fab.Font = UI.Px(36, FontStyle.Bold);
        _fab.BackColor = UI.BtnGreen;
        _fab.ForeColor = UI.TextDark;
    }

    private void RefreshContent()
    {
        _scrollArea.SuspendLayout();
        _scrollArea.Controls.Clear();
        _scrollArea.AutoScrollPosition = Point.Empty;

        if (_view == "catalog") RenderCatalog();
        else if (_view == "history") RenderHistory();
        else if (_view == "supplies") RenderSupplies();
        else if (_view.StartsWith("shipment_")) RenderShipmentCatalog();

        _scrollArea.ResumeLayout();
        _scrollArea.UpdateScrollbar();
        LayoutShell();
    }

    private void RenderCatalog()
    {
        var categories = _svc.CategoryService.GetAll();
        var products = GetFilteredProducts();
        int y = 0, w = Math.Max(_scrollArea.ClientSize.Width - 2, 500);

        y = AddCategoryRow(y, w, "Все", -1, null);
        if (_expandedCats.Contains(-1))
            foreach (var p in products)
                y = AddCatalogProductRow(y, w, p);

        foreach (var cat in categories)
        {
            y = AddCategoryRow(y, w, cat.Name, cat.Id, cat);
            if (_expandedCats.Contains(cat.Id))
                foreach (var p in products.Where(p => p.CategoryId == cat.Id))
                    y = AddCatalogProductRow(y, w, p);
        }
    }

    private void RenderShipmentCatalog()
    {
        int id = int.Parse(_view.Split('_')[1]);
        if (!_drafts.TryGetValue(id, out var draft)) return;

        var categories = _svc.CategoryService.GetAll();
        var products = GetFilteredProducts();
        int y = 0, w = Math.Max(_scrollArea.ClientSize.Width - 2, 500);

        y = AddCategoryRow(y, w, "Все", -1, null);
        if (_expandedCats.Contains(-1))
            foreach (var p in products)
                y = AddShipmentProductRow(y, w, p, draft);

        foreach (var cat in categories)
        {
            y = AddCategoryRow(y, w, cat.Name, cat.Id, cat);
            if (_expandedCats.Contains(cat.Id))
                foreach (var p in products.Where(p => p.CategoryId == cat.Id))
                    y = AddShipmentProductRow(y, w, p, draft);
        }
    }

    private List<Product> GetFilteredProducts()
    {
        var s = _txtSearch.Text.Trim();
        return string.IsNullOrEmpty(s) ? _svc.ProductService.GetAll() : _svc.ProductService.Search(s);
    }

    private int AddCategoryRow(int y, int w, string name, int catId, Category? cat)
    {
        bool expanded = _expandedCats.Contains(catId);
        var row = UI.CreatePanel(UI.CategoryRow);
        row.Bounds = new Rectangle(0, y, w, UI.CategoryRowHeight);
        row.Cursor = Cursors.Hand;
        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var arrow = new Label { Text = expanded ? "-" : "+", ForeColor = UI.CategoryText, Font = UI.Px(20),
            TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(10, 0, 38, UI.CategoryRowHeight) };
        row.Controls.Add(arrow);

        var title = new Label { Text = name, ForeColor = UI.CategoryText, Font = UI.Px(20),
            TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(52, 0, w - 140, UI.CategoryRowHeight),
            AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        row.Controls.Add(title);

        void Toggle() { if (_expandedCats.Contains(catId)) _expandedCats.Remove(catId); else _expandedCats.Add(catId); RefreshContent(); }
        row.Click += (_, _) => Toggle(); arrow.Click += (_, _) => Toggle(); title.Click += (_, _) => Toggle();

        if (Me.IsAdmin)
        {
            if (catId == -1)
            {
                var plus = new Label { Text = "+", Font = UI.Px(28, FontStyle.Bold), ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(w - 52, 0, 36, UI.CategoryRowHeight),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.Hand };
                plus.Click += (_, _) => CreateCategory();
                row.Controls.Add(plus);
            }
            else if (cat != null)
            {
                var close = new Label { Text = "X", Font = UI.Px(18), ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(w - 48, 0, 32, UI.CategoryRowHeight),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.Hand };
                close.Click += (_, _) => DeleteCategory(cat);
                row.Controls.Add(close);
                title.DoubleClick += (_, _) => RenameCategory(cat);
            }
        }

        _scrollArea.Controls.Add(row);
        return y + UI.CategoryRowHeight;
    }

    private int AddCatalogProductRow(int y, int w, Product product)
    {
        int rowH = UI.ProductRowHeight;
        int stockW = 140;
        int priceW = 140;
        int expiryW = product.DeadlineDisplayShort.Length > 0 ? 120 : 0;
        int nameW = Math.Min(340, Math.Max(240, (int)(w * 0.28)));
        int descX = 24 + nameW;
        int descW = Math.Max(120, w - descX - priceW - stockW - expiryW - 60);

        var row = CreateProductRowBase(y, w);
        row.Cursor = Cursors.Hand;
        row.Controls.Add(CRL(product.TruncatedName, new Rectangle(20, 0, nameW, rowH), UI.FontLarge, UI.TextDark));

        var desc = CRL(product.Description ?? "", new Rectangle(descX, 0, descW, rowH), UI.FontLarge, UI.TextDark);
        desc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Controls.Add(desc);

        if (expiryW > 0)
        {
            Color ec = product.HasOverdueBatches ? UI.BtnRed : product.HasDiscountedBatches ? UI.BtnOrange : UI.TextGray;
            var exp = CRL(product.DeadlineDisplayShort, new Rectangle(w - 420, 0, expiryW, rowH), UI.FontSmall, ec, ContentAlignment.MiddleRight);
            exp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(exp);
        }

        string priceText;
        if (product.DiscountPercent > 0)
            priceText = $"{FmtPrice(product.DiscountedPrice)} (-{product.DiscountPercent}%)";
        else
            priceText = FmtPrice(product.PurchasePrice);

        var price = CRL(priceText, new Rectangle(w - 296, 0, priceW, rowH), UI.FontLarge,
            product.DiscountPercent > 0 ? Color.FromArgb(200, 80, 0) : UI.TextDark, ContentAlignment.MiddleRight);
        price.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        row.Controls.Add(price);

        var stock = CRL(product.DisplayStock, new Rectangle(w - 152, 0, stockW, rowH), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
        stock.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        row.Controls.Add(stock);

        if (product.HasOverdueBatches)
            row.BackColor = Color.FromArgb(255, 235, 235);
        else if (product.HasDiscountedBatches)
            row.BackColor = Color.FromArgb(255, 248, 230);

        void Open() => OpenProductDetail(product);
        row.Click += (_, _) => Open();
        foreach (Control c in row.Controls) c.Click += (_, _) => Open();

        _scrollArea.Controls.Add(row);
        return y + rowH;
    }

    private int AddShipmentProductRow(int y, int w, Product product, ShipmentDraft draft)
    {
        int rowH = UI.ProductRowHeight;

        int priceLeft = w - 362, priceW = 140;
        int qtyLeft = w - 214, qtyW = 120;

        int nameW = Math.Min(340, Math.Max(240, (int)(w * 0.28)));
        int descX = 24 + nameW;
        int descW = Math.Max(160, priceLeft - descX - 12);

        var current = draft.Items.FirstOrDefault(i => i.ProductId == product.Id);
        int pid = product.Id;
        var row = CreateProductRowBase(y, w);
        row.Cursor = Cursors.Hand;

        row.Controls.Add(CRL(product.TruncatedName, new Rectangle(20, 0, nameW, rowH), UI.FontLarge, UI.TextDark));
        var desc = CRL(product.Description ?? "", new Rectangle(descX, 0, descW, rowH), UI.FontLarge, UI.TextDark);
        desc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Controls.Add(desc);

        var shownPrice = current != null ? FmtPrice(current.Price) : FmtPrice(product.DiscountedPrice);
        var price = CRL(shownPrice, new Rectangle(priceLeft, 0, priceW, rowH), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
        price.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        row.Controls.Add(price);

        var qty = CRL(current != null ? $"{current.Quantity} шт." : "0 шт.", new Rectangle(qtyLeft, 0, qtyW, rowH), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
        qty.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        row.Controls.Add(qty);

        var btnMinus = UI.CreateCircleButton("-", UI.BtnRed, 36, UI.Px(22, FontStyle.Bold));
        btnMinus.Location = new Point(w - 46, (rowH - 36) / 2);
        btnMinus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnMinus.Visible = current != null;
        btnMinus.Click += (_, _) =>
        {
            var cur = draft.Items.FirstOrDefault(i => i.ProductId == pid);
            if (cur == null) return;
            if (cur.Quantity > 1) cur.Quantity--; else draft.Items.Remove(cur);
            RefreshContent(); UpdateFooterTotal(draft.Id);
        };
        row.Controls.Add(btnMinus);

        var btnPlus = UI.CreateCircleButton("+", UI.BtnGreen, 36, UI.Px(18, FontStyle.Bold));
        btnPlus.Location = new Point(w - 86, (rowH - 36) / 2);
        btnPlus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnPlus.Enabled = product.StockQuantity > 0 || current != null;
        if (!btnPlus.Enabled) btnPlus.BackColor = Color.Silver;
        btnPlus.Click += (_, _) => EditSelection();
        row.Controls.Add(btnPlus);

        void EditSelection()
        {
            var freshProduct = _svc.ProductService.GetById(pid) ?? product;
            var cur = draft.Items.FirstOrDefault(i => i.ProductId == pid);
            if (freshProduct.StockQuantity <= 0 && cur == null) { MessageBox.Show("Этого товара нет в наличии на складе.", "Нет остатка", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            ShowShipmentItemDialog(freshProduct, draft);
        }

        row.Click += (_, _) => EditSelection();
        foreach (Control c in row.Controls) { if (c != btnPlus && c != btnMinus) c.Click += (_, _) => EditSelection(); }

        _scrollArea.Controls.Add(row);
        return y + rowH;
    }

    private Panel CreateProductRowBase(int y, int w)
    {
        var row = UI.CreatePanel(Color.White);
        row.Bounds = new Rectangle(0, y, w, UI.ProductRowHeight);
        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Margin = Padding.Empty;
        row.Paint += (_, e) => { using var pen = new Pen(UI.RowBorder, 1); e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1); };
        return row;
    }

    private static Label CRL(string text, Rectangle bounds, Font font, Color color, ContentAlignment alignment = ContentAlignment.MiddleLeft) =>
        new() { Text = text, Font = font, ForeColor = color, Bounds = bounds, TextAlign = alignment, AutoEllipsis = true };

    private void RenderHistory()
    {
        var search = _txtSearch.Text.Trim();
        var shipments = string.IsNullOrEmpty(search) ? _svc.ShipmentService.GetAll() : _svc.ShipmentService.Search(search);
        int y = 0, w = Math.Max(_scrollArea.ClientSize.Width - 2, 500);

        foreach (var shipment in shipments)
        {
            bool expanded = _expandedShipments.Contains(shipment.Id);
            var row = UI.CreatePanel(Color.White);
            row.Bounds = new Rectangle(0, y, w, UI.ProductRowHeight);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Cursor = Cursors.Hand;
            row.Paint += (_, e) => { using var pen = new Pen(UI.RowBorder, 1); e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1); };

            row.Controls.Add(CRL(expanded ? "-" : "+", new Rectangle(6, 0, 32, UI.ProductRowHeight), UI.FontMed, UI.TextGray, ContentAlignment.MiddleCenter));
            row.Controls.Add(CRL(shipment.Name, new Rectangle(40, 0, Math.Max(220, w - 400), UI.ProductRowHeight), UI.FontLarge, UI.TextDark));

            var cost = CRL(FmtPriceAt(shipment.TotalCost, shipment), new Rectangle(w - 320, 0, 150, UI.ProductRowHeight), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
            cost.Anchor = AnchorStyles.Top | AnchorStyles.Right; row.Controls.Add(cost);
            var profit = CRL($"(+{FmtPriceAt(shipment.Profit, shipment)})", new Rectangle(w - 166, 0, 80, UI.ProductRowHeight), UI.FontSmall, Color.FromArgb(20, 140, 20), ContentAlignment.MiddleCenter);
            profit.Anchor = AnchorStyles.Top | AnchorStyles.Right; row.Controls.Add(profit);
            var date = CRL(shipment.DisplayDate, new Rectangle(w - 152, 0, 130, UI.ProductRowHeight), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
            date.Anchor = AnchorStyles.Top | AnchorStyles.Right; row.Controls.Add(date);

            var sid = shipment.Id;
            void Toggle() { if (_expandedShipments.Contains(sid)) _expandedShipments.Remove(sid); else _expandedShipments.Add(sid); RefreshContent(); }
            row.Click += (_, _) => Toggle(); foreach (Control c in row.Controls) c.Click += (_, _) => Toggle();

            _scrollArea.Controls.Add(row);
            y += UI.ProductRowHeight;

            if (expanded)
            {
                if (!string.IsNullOrWhiteSpace(shipment.Recipient) || !string.IsNullOrWhiteSpace(shipment.Address))
                {
                    var infoRow = UI.CreatePanel(Color.FromArgb(240, 245, 250));
                    infoRow.Bounds = new Rectangle(0, y, w, 38); infoRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    string info = ""; if (!string.IsNullOrWhiteSpace(shipment.Recipient)) info += $"Получатель: {shipment.Recipient}";
                    if (!string.IsNullOrWhiteSpace(shipment.Address)) info += $"    Адрес: {shipment.Address}";
                    infoRow.Controls.Add(CRL(info.Trim(), new Rectangle(46, 0, w - 64, 38), UI.FontSmall, UI.TextGray));
                    _scrollArea.Controls.Add(infoRow); y += 38;
                }

                var log = shipment.Logistics;
                if (log != null)
                {
                    var parts = new List<string>();
                    if (log.CheckPerformed)
                    {
                        string mark = log.Decision == DealDecision.Allowed ? "[OK]" : "[!]";
                        parts.Add($"Контрагент: {mark} {log.DecisionText}");
                        if (!string.IsNullOrWhiteSpace(log.Inn)) parts.Add($"ИНН {log.Inn}");
                    }
                    if (log.WeatherLoaded)
                    {
                        string risk = log.Risk == WeatherRisk.High ? "высокий риск" : log.Risk == WeatherRisk.Medium ? "средний риск" : "низкий риск";
                        parts.Add($"Погода {log.WeatherCity}: {log.Temperature:+0;-0;0}°C ({risk})");
                    }
                    if (log.DistanceKm > 0) parts.Add($"Маршрут ~{log.DistanceKm} км");

                    if (parts.Count > 0)
                    {
                        bool hasRisk = log.Decision == DealDecision.NeedsReview || log.Risk == WeatherRisk.High;
                        var logRow = UI.CreatePanel(hasRisk ? Color.FromArgb(252, 240, 240) : Color.FromArgb(240, 248, 240));
                        logRow.Bounds = new Rectangle(0, y, w, 36); logRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        logRow.Controls.Add(CRL(string.Join("     ", parts), new Rectangle(46, 0, w - 64, 36), UI.FontSmall, hasRisk ? Color.FromArgb(180, 40, 40) : UI.TextGray));
                        _scrollArea.Controls.Add(logRow); y += 36;
                    }
                }

                foreach (var item in shipment.Items?.ToList() ?? new())
                {
                    var ir = UI.CreatePanel(Color.FromArgb(248, 248, 252));
                    ir.Bounds = new Rectangle(0, y, w, 44); ir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    ir.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(220, 220, 225), 1); e.Graphics.DrawLine(pen, 46, ir.Height - 1, ir.Width, ir.Height - 1); };
                    ir.Controls.Add(CRL(item.Product?.Name ?? $"Товар #{item.ProductId}", new Rectangle(46, 0, Math.Max(200, w - 460), 44), UI.FontMed, UI.TextDark));
                    var ip = CRL(FmtPriceAt(item.Price, shipment), new Rectangle(w - 380, 0, 130, 44), UI.FontTab, UI.TextGray, ContentAlignment.MiddleRight); ip.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(ip);
                    var iq = CRL($"{item.Quantity} шт.", new Rectangle(w - 240, 0, 90, 44), UI.FontTab, UI.TextGray, ContentAlignment.MiddleRight); iq.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(iq);
                    var ist = CRL(FmtPriceAt(item.Subtotal, shipment), new Rectangle(w - 140, 0, 120, 44), UI.FontTab, UI.TextDark, ContentAlignment.MiddleRight); ist.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(ist);
                    _scrollArea.Controls.Add(ir); y += 44;
                }

                var spacer = UI.CreatePanel(Color.FromArgb(230, 230, 235));
                spacer.Bounds = new Rectangle(0, y, w, 4); spacer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _scrollArea.Controls.Add(spacer); y += 4;
            }
        }
    }

    private void RenderSupplies()
    {
        var search = _txtSearch.Text.Trim();
        var supplies = string.IsNullOrEmpty(search) ? _svc.SupplyService.GetAll() : _svc.SupplyService.Search(search);
        int y = 0, w = Math.Max(_scrollArea.ClientSize.Width - 2, 500);

        foreach (var supply in supplies)
        {
            bool expanded = _expandedSupplies.Contains(supply.Id);
            var row = UI.CreatePanel(Color.White);
            row.Bounds = new Rectangle(0, y, w, UI.ProductRowHeight);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Cursor = Cursors.Hand;
            row.Paint += (_, e) => { using var pen = new Pen(UI.RowBorder, 1); e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1); };

            row.Controls.Add(CRL(expanded ? "-" : "+", new Rectangle(6, 0, 32, UI.ProductRowHeight), UI.FontMed, UI.TextGray, ContentAlignment.MiddleCenter));
            row.Controls.Add(CRL(supply.Name, new Rectangle(40, 0, Math.Max(220, w - 400), UI.ProductRowHeight), UI.FontLarge, UI.TextDark));
            var cost = CRL(FmtPriceAt(supply.TotalCost, supply), new Rectangle(w - 320, 0, 150, UI.ProductRowHeight), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
            cost.Anchor = AnchorStyles.Top | AnchorStyles.Right; row.Controls.Add(cost);
            var date = CRL(supply.DisplayDate, new Rectangle(w - 152, 0, 130, UI.ProductRowHeight), UI.FontLarge, UI.TextDark, ContentAlignment.MiddleRight);
            date.Anchor = AnchorStyles.Top | AnchorStyles.Right; row.Controls.Add(date);

            var sid = supply.Id;
            void Toggle() { if (_expandedSupplies.Contains(sid)) _expandedSupplies.Remove(sid); else _expandedSupplies.Add(sid); RefreshContent(); }
            row.Click += (_, _) => Toggle(); foreach (Control c in row.Controls) c.Click += (_, _) => Toggle();
            _scrollArea.Controls.Add(row); y += UI.ProductRowHeight;

            if (expanded)
            {
                if (!string.IsNullOrWhiteSpace(supply.Supplier))
                {
                    var ir = UI.CreatePanel(Color.FromArgb(240, 248, 240)); ir.Bounds = new Rectangle(0, y, w, 38);
                    ir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    ir.Controls.Add(CRL($"Поставщик: {supply.Supplier}", new Rectangle(46, 0, w - 64, 38), UI.FontSmall, UI.TextGray));
                    _scrollArea.Controls.Add(ir); y += 38;
                }

                var chk = supply.Check;
                if (chk != null && chk.CheckPerformed)
                {
                    bool risk = chk.Decision == DealDecision.NeedsReview;
                    var cr = UI.CreatePanel(risk ? Color.FromArgb(252, 240, 240) : Color.FromArgb(240, 248, 240));
                    cr.Bounds = new Rectangle(0, y, w, 36); cr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    string mark = risk ? "[!]" : "[OK]";
                    string txt = $"Проверка поставщика: {mark} {chk.DecisionText}";
                    if (!string.IsNullOrWhiteSpace(chk.Inn)) txt += $"    ИНН {chk.Inn}";
                    cr.Controls.Add(CRL(txt, new Rectangle(46, 0, w - 64, 36), UI.FontSmall, risk ? Color.FromArgb(180, 40, 40) : UI.TextGray));
                    _scrollArea.Controls.Add(cr); y += 36;
                }
                foreach (var item in supply.Items?.ToList() ?? new())
                {
                    var ir = UI.CreatePanel(Color.FromArgb(248, 252, 248)); ir.Bounds = new Rectangle(0, y, w, 44);
                    ir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    ir.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(220, 225, 220), 1); e.Graphics.DrawLine(pen, 46, ir.Height - 1, ir.Width, ir.Height - 1); };
                    ir.Controls.Add(CRL(item.Product?.Name ?? $"Товар #{item.ProductId}", new Rectangle(46, 0, Math.Max(200, w - 560), 44), UI.FontMed, UI.TextDark));
                    var ip = CRL(FmtPriceAt(item.PurchasePrice, supply), new Rectangle(w - 480, 0, 120, 44), UI.FontTab, UI.TextGray, ContentAlignment.MiddleRight); ip.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(ip);
                    var iq = CRL($"{item.Quantity} шт.", new Rectangle(w - 350, 0, 90, 44), UI.FontTab, UI.TextGray, ContentAlignment.MiddleRight); iq.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(iq);
                    string expText = item.SaleDeadline.HasValue ? item.SaleDeadline.Value.ToString("dd.MM.yyyy") : "—";
                    var ie = CRL(expText, new Rectangle(w - 250, 0, 110, 44), UI.FontTab, UI.TextGray, ContentAlignment.MiddleRight); ie.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(ie);
                    var ist = CRL(FmtPriceAt(item.Subtotal, supply), new Rectangle(w - 130, 0, 110, 44), UI.FontTab, UI.TextDark, ContentAlignment.MiddleRight); ist.Anchor = AnchorStyles.Top | AnchorStyles.Right; ir.Controls.Add(ist);
                    _scrollArea.Controls.Add(ir); y += 44;
                }
                var spacer = UI.CreatePanel(Color.FromArgb(200, 220, 200)); spacer.Bounds = new Rectangle(0, y, w, 4);
                spacer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _scrollArea.Controls.Add(spacer); y += 4;
            }
        }
    }

    private void AutoWriteOffOverdue()
    {
        try
        {
            var products = _svc.ProductService.GetAll().Where(p => p.HasOverdueBatches).ToList();
            if (products.Count == 0) return;

            int totalQty = products.Sum(p => p.OverdueQuantity);
            decimal totalLoss = products.Sum(p => p.OverdueLoss);

            foreach (var product in products)
            {
                var p = _svc.ProductService.GetById(product.Id);
                if (p == null) continue;
                var overdue = p.Batches.Where(b => b.IsOverdue && b.Quantity > 0).ToList();
                foreach (var batch in overdue)
                {
                    _svc.DbContext.WriteOffs.Add(new WriteOff
                    {
                        ProductId = p.Id,
                        BatchId = batch.Id,
                        Quantity = batch.Quantity,
                        PurchasePrice = batch.PurchasePrice,
                        Reason = "Истёк срок реализации"
                    });
                    p.StockQuantity -= batch.Quantity;
                    batch.Quantity = 0;
                }
                _svc.ProductService.Update(p);
            }
            _svc.DbContext.SaveChanges();

            MessageBox.Show(
                $"Автоматически списано {totalQty} ед. товара с истекшим сроком реализации.\nУбыток: {FmtPrice(totalLoss)}\nТоваров затронуто: {products.Count}",
                "Автосписание", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при автосписании: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowShipmentItemDialog(Product product, ShipmentDraft draft)
    {
        var existing = draft.Items.FirstOrDefault(i => i.ProductId == product.Id);
        using var dialog = new Form
        {
            Text = product.Name, ClientSize = new Size(480, 290), StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            BackColor = UI.BgLight, Font = UI.DefaultFont, AutoScaleMode = AutoScaleMode.None
        };

        var card = UI.CreateRoundedPanel(UI.BgCard, 20); card.Bounds = new Rectangle(18, 18, 444, 254); dialog.Controls.Add(card);

        card.Controls.Add(new Label { Text = product.Name, Font = UI.Px(20), ForeColor = UI.TextDark, Bounds = new Rectangle(20, 16, 400, 30), AutoEllipsis = true });
        card.Controls.Add(new Label { Text = $"На складе: {product.StockQuantity} {product.Unit}", Font = UI.FontSmall, ForeColor = UI.TextGray, Bounds = new Rectangle(20, 50, 220, 24) });

        card.Controls.Add(new Label { Text = "Количество", Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = new Rectangle(20, 94, 130, 28) });
        var qtyHost = UI.CreateRoundedPanel(Color.White, 12); qtyHost.Bounds = new Rectangle(160, 88, 190, 44); card.Controls.Add(qtyHost);
        var numQty = new NumericUpDown { Font = UI.FontMed, Minimum = 1, Maximum = Math.Max(1, product.StockQuantity), Value = Math.Max(1, existing?.Quantity ?? 1), BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = UI.TextDark };
        qtyHost.Controls.Add(numQty); UI.BindControlToHost(qtyHost, numQty, new Padding(16, 5, 16, 5));

        card.Controls.Add(new Label { Text = $"Цена ({CurSymbol})", Font = UI.FontMed, ForeColor = UI.TextDark, Bounds = new Rectangle(20, 148, 130, 28) });
        var priceHost = UI.CreateRoundedPanel(Color.White, 12); priceHost.Bounds = new Rectangle(160, 142, 190, 44); card.Controls.Add(priceHost);

        var settings = _svc.CurrencyService.Settings;
        var initialPriceRub = existing?.Price ?? product.DiscountedPrice;
        var initialPriceDisplay = settings.ConvertFromRub(initialPriceRub);
        var numPrice = new NumericUpDown { Font = UI.FontMed, Minimum = 0, Maximum = 999999999, DecimalPlaces = 2, Value = initialPriceDisplay, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = UI.TextDark, ThousandsSeparator = true };
        priceHost.Controls.Add(numPrice); UI.BindControlToHost(priceHost, numPrice, new Padding(16, 5, 16, 5));

        var btnOk = UI.CreatePillButton("Применить", UI.BtnGreen, new Size(160, 44), UI.FontMedBold); btnOk.Location = new Point(20, 200);
        btnOk.Click += (_, _) => { dialog.Validate(); dialog.DialogResult = DialogResult.OK; dialog.Close(); };
        card.Controls.Add(btnOk);

        if (existing != null)
        {
            var btnDel = UI.CreatePillButton("Убрать", UI.BtnRed, new Size(130, 44), UI.FontMedBold); btnDel.Location = new Point(190, 200);
            btnDel.Click += (_, _) => { draft.Items.Remove(existing); dialog.DialogResult = DialogResult.Abort; dialog.Close(); };
            card.Controls.Add(btnDel);
        }

        dialog.AcceptButton = btnOk;
        var result = dialog.ShowDialog(this);
        if (result == DialogResult.Abort) { RefreshContent(); UpdateFooterTotal(draft.Id); return; }
        if (result != DialogResult.OK) return;

        decimal priceRub = settings.Currency == "RUB"
            ? numPrice.Value
            : Math.Round(numPrice.Value * settings.GetRate(settings.Currency), 2);

        if (existing == null)
            draft.Items.Add(new ShipmentDraftItem { ProductId = product.Id, ProductName = product.Name, ProductDescription = product.Description, Quantity = (int)numQty.Value, Price = priceRub });
        else { existing.Quantity = (int)numQty.Value; existing.Price = priceRub; existing.ProductName = product.Name; existing.ProductDescription = product.Description; }

        RefreshContent(); UpdateFooterTotal(draft.Id);
    }

    private void ShowShipmentFooter(int id)
    {
        HideFooter();
        var draft = _drafts[id]; draft.Id = id;

        _footer = UI.CreatePanel(UI.TopBar); _footer.Height = UI.FooterHeight;
        _footer.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(_footer);

        var lblT = FooterLabel("Итого"); _footer.Controls.Add(lblT);
        _totalHost = FooterInputHost(); _footer.Controls.Add(_totalHost);
        _lblTotalVal = new Label { Text = FmtPrice(draft.Items.Sum(i => i.Price * i.Quantity)), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UI.Px(18), ForeColor = Color.White, BackColor = Color.Transparent };
        _totalHost.Controls.Add(_lblTotalVal);

        _btnLogistics = UI.CreatePillButton("Проверка и логистика", Color.FromArgb(60, 80, 140), new Size(260, 44), UI.Px(16));
        _btnLogistics.ForeColor = Color.White;
        _btnLogistics.Click += (_, _) => OpenLogistics(id);
        _footer.Controls.Add(_btnLogistics);

        _btnShip = UI.CreatePillButton("отгрузить", UI.BtnOrange, new Size(190, 44), UI.Px(20));
        _btnShip.Click += (_, _) => ConfirmShipment(id);
        _btnShip.Enabled = draft.Items.Count > 0;
        _footer.Controls.Add(_btnShip);

        LayoutShell(); _fab.BringToFront();
    }

    private Label FooterLabel(string text) => new() { Text = text, Font = UI.Px(15), ForeColor = Color.White, AutoSize = true, BackColor = Color.Transparent };
    private Panel FooterInputHost() => UI.CreateRoundedPanel(UI.InputBg, 12);
    private TextBox FooterTextBox(string text)
    {
        var box = new TextBox
        {
            Text = text,
            BorderStyle = BorderStyle.None,
            Font = UI.Px(16),
            BackColor = UI.InputBg,
            ForeColor = UI.TextDark
        };

        bool layoutBound = false;
        box.ParentChanged += (_, _) =>
        {
            if (layoutBound || box.Parent is not Panel h) return;
            layoutBound = true;
            UI.BindControlToHost(h, box, new Padding(14, 5, 14, 5), verticalOffset: -1);
        };

        return box;
    }

    private void LayoutFooter()
    {
        if (_footer == null || _totalHost == null || _btnShip == null) return;
        int y = 12, lY = 18, m = 14, shipW = 190;
        int totalW = Math.Max(220, Math.Min(320, ClientSize.Width / 6));
        var lbl = _footer.Controls.OfType<Label>().FirstOrDefault(l => l != _lblTotalVal);
        if (lbl == null) return;
        lbl.Location = new Point(m, lY);
        _totalHost.Bounds = new Rectangle(lbl.Right + 12, y, totalW, 44);
        _btnShip.SetBounds(_footer.Width - shipW - m, y, shipW, 44);
        if (_btnLogistics != null) _btnLogistics.SetBounds(_btnShip.Left - 290, y, 280, 44);
    }

    private void UpdateFooterTotal(int draftId)
    {
        if (_lblTotalVal == null || !_drafts.TryGetValue(draftId, out var draft)) return;
        _lblTotalVal.Text = FmtPrice(draft.Items.Sum(i => i.Price * i.Quantity));
        if (_btnShip != null) _btnShip.Enabled = draft.Items.Count > 0;
    }

    private void HideFooter()
    {
        if (_footer == null) return;
        Controls.Remove(_footer); _footer.Dispose(); _footer = null;
        _recipientHost = null; _addressHost = null; _totalHost = null;
        _txtRecipient = null; _txtAddress = null; _lblTotalVal = null; _btnShip = null; _btnLogistics = null;
    }

    private void OpenLogistics(int id)
    {
        if (!_drafts.TryGetValue(id, out var draft)) return;
        var names = draft.Items.Select(i => i.ProductName ?? "").ToList();
        decimal total = draft.Items.Sum(i => i.Price * i.Quantity);
        using var form = new LogisticsForm(_svc, draft.Recipient, draft.Address, total, names, draft.Logistics);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            draft.Logistics = form.Result;
            if (!string.IsNullOrWhiteSpace(form.Result.CompanyName)) draft.Recipient = form.Result.CompanyName;
            if (!string.IsNullOrWhiteSpace(form.Result.RouteTo)) draft.Address = form.Result.RouteTo;
            UpdateFooterTotal(id);
        }
    }

    private void OpenProductDetail(Product product)
    {
        HideDetail(false);
        _detailPanel = UI.CreatePanel(Color.FromArgb(206, 206, 206));
        _detailPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        _detailPanel.Paint += (_, e) => { using var pen = new Pen(UI.RowBorder, 1); e.Graphics.DrawLine(pen, 0, 0, 0, _detailPanel.Height); };
        Controls.Add(_detailPanel);

        int panelW = Math.Max(480, Math.Min(780, (int)(ClientSize.Width * 0.46)));

        var header = UI.CreatePanel(Color.FromArgb(206, 206, 206)); header.Dock = DockStyle.Top; header.Height = 64; _detailPanel.Controls.Add(header);
        var btnClose = UI.CreateCircleButton("X", UI.BtnRed, 48, UI.Px(18)); btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right; btnClose.Click += (_, _) => HideDetail(); header.Controls.Add(btnClose);

        Button actionButton;
        if (Me.IsStorekeeper)
        {
            actionButton = UI.CreateCircleButton("🛒", UI.FabOrange, 48, UI.Px(18, FontStyle.Regular, "Segoe UI Emoji"));
            actionButton.Click += (_, _) => AddProductToCurrentShipment(product);
        }
        else
        {
            actionButton = UI.CreateCircleButton("Изм.", Color.FromArgb(42, 58, 114), 48, UI.Px(13, FontStyle.Bold)); actionButton.ForeColor = Color.White;
            actionButton.Click += (_, _) => EditProduct(product.Id);
        }
        actionButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; header.Controls.Add(actionButton);
        header.Resize += (_, _) => { btnClose.Location = new Point(header.Width - 60, 8); actionButton.Location = new Point(header.Width - 114, 8); };
        btnClose.Location = new Point(header.Width - 60, 8); actionButton.Location = new Point(header.Width - 114, 8);

        var content = UI.CreatePanel(Color.FromArgb(206, 206, 206)); content.Dock = DockStyle.Fill; content.AutoScroll = true;
        _detailPanel.Controls.Add(content); content.BringToFront(); _detailContent = content;

        content.Controls.Add(new Label { Text = product.Name, Font = UI.Px(22), ForeColor = UI.TextDark, Bounds = new Rectangle(28, 10, panelW - 68, 34), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.Transparent });

        var photoHost = UI.CreateRoundedPanel(Color.White, 18); photoHost.Bounds = new Rectangle(28, 52, panelW - 56, 420); photoHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; content.Controls.Add(photoHost);
        var imageBox = new CoverPictureBox { Dock = DockStyle.Fill, BackColor = Color.White }; photoHost.Controls.Add(imageBox);
        var placeholder = new Label { Text = "фото товара", Dock = DockStyle.Fill, Font = UI.Px(32), ForeColor = UI.TextDark, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White };
        photoHost.Controls.Add(placeholder); placeholder.BringToFront();

        if (!string.IsNullOrWhiteSpace(product.ImagePath) && File.Exists(product.ImagePath))
        {
            try { using var src = Image.FromFile(product.ImagePath); imageBox.Image = new Bitmap(src); placeholder.Visible = false; } catch { }
        }

        int y = photoHost.Bottom + 24;

        if (product.DiscountPercent > 0)
        {
            y = AddDetailField("Цена со скидкой", $"{FmtPrice(product.DiscountedPrice)} (-{product.DiscountPercent}%)", y, panelW);
            y = AddDetailField("Цена без скидки", FmtPrice(product.PurchasePrice), y, panelW);
        }
        else
            y = AddDetailField("Цена", FmtPrice(product.PurchasePrice), y, panelW);

        y = AddDetailField("Остаток", product.StockQuantity.ToString(), y, panelW);
        y = AddDetailField("Артикул", product.Article, y, panelW);
        y = AddDetailField("Описание", product.Description ?? "", y, panelW, 40);

        {
            var extras = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(product.ExtraField1))
            {
                var parts = product.ExtraField1.Contains("||")
                    ? product.ExtraField1.Split("||")
                    : new[] { product.ExtraField1 };
                extras.AddRange(parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            if (!string.IsNullOrWhiteSpace(product.ExtraField2) && !product.ExtraField2.Contains("||"))
                extras.Add(product.ExtraField2);
            for (int ei = 0; ei < extras.Count; ei++)
                y = AddDetailField($"Характеристика {ei + 1}", extras[ei].Trim(), y, panelW);
        }

        var batches = product.Batches?.Where(b => b.Quantity > 0).OrderBy(b => b.SaleDeadline).ToList();
        if (batches != null && batches.Count > 0)
        {
            y += 10;
            content.Controls.Add(new Label { Text = "Партии (сроки реализации)", Font = UI.FontMedBold, ForeColor = UI.TextDark, Bounds = new Rectangle(28, y, panelW - 56, 28), BackColor = Color.Transparent });
            y += 32;

            foreach (var batch in batches)
            {
                Color bc = batch.IsOverdue ? Color.FromArgb(255, 230, 230) : batch.IsDiscounted ? Color.FromArgb(255, 245, 220) : Color.FromArgb(235, 245, 235);
                var br = UI.CreateRoundedPanel(bc, 8); br.Bounds = new Rectangle(28, y, panelW - 56, 36); br.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; content.Controls.Add(br);
                Color sc = batch.IsOverdue ? UI.BtnRed : batch.IsDiscounted ? UI.BtnOrange : UI.TextDark;
                br.Controls.Add(new Label { Text = $"{batch.Quantity} шт.", Font = UI.FontSmall, ForeColor = UI.TextDark, Bounds = new Rectangle(12, 0, 90, 36), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
                br.Controls.Add(new Label { Text = FmtPrice(batch.PurchasePrice), Font = UI.FontSmall, ForeColor = UI.TextGray, Bounds = new Rectangle(104, 0, 100, 36), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
                br.Controls.Add(new Label { Text = batch.StatusDisplay, Font = UI.FontSmall, ForeColor = sc, Bounds = new Rectangle(206, 0, panelW - 270, 36), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
                y += 40;
            }
        }

        LayoutShell(); _detailPanel.BringToFront(); _fab.BringToFront();
    }

    private int AddDetailField(string caption, string value, int y, int panelW, int height = 30)
    {
        var container = _detailContent ?? _detailPanel!;
        container.Controls.Add(new Label { Text = caption, Font = UI.FontTiny, ForeColor = UI.TextDark, Bounds = new Rectangle(28, y + 4, 120, 24), TextAlign = ContentAlignment.MiddleLeft });
        var host = UI.CreateRoundedPanel(UI.ValueBar, 10); host.Bounds = new Rectangle(160, y, panelW - 188, height); host.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; container.Controls.Add(host);
        host.Controls.Add(new Label { Text = value, Font = UI.Px(14), ForeColor = UI.TextDark, BackColor = Color.Transparent, Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true });
        return y + height + 14;
    }

    private void EditProduct(int productId)
    {
        var current = _svc.ProductService.GetById(productId);
        if (current == null) { HideDetail(); RefreshContent(); return; }
        using var form = new ProductEditForm(_svc, current);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        RefreshContent();
        var updated = _svc.ProductService.GetById(productId);
        if (updated == null) HideDetail(); else OpenProductDetail(updated);
    }

    private void HideDetail(bool relayout = true)
    {
        if (_detailPanel == null) return;
        Controls.Remove(_detailPanel); _detailPanel.Dispose(); _detailPanel = null; _detailContent = null;
        if (relayout) LayoutShell();
    }

    private void OnFabClick()
    {
        if (Me.IsAdmin && _view == "catalog") { using var form = new ProductEditForm(_svc, null); if (form.ShowDialog(this) == DialogResult.OK) RefreshContent(); return; }
        if (_view == "supplies") { using var form = new SupplyForm(_svc, Me.Id); if (form.ShowDialog(this) == DialogResult.OK) RefreshContent(); return; }
        if (!Me.IsStorekeeper) return;
        CreateNewShipmentDraft();
    }

    private void AddProductToCurrentShipment(Product product)
    {
        if (product.StockQuantity <= 0) { MessageBox.Show("Этого товара нет в наличии.", "Нет остатка"); return; }
        int? currentDraftId = _view.StartsWith("shipment_") ? int.Parse(_view.Split('_')[1]) : null;
        if (currentDraftId == null || !_drafts.ContainsKey(currentDraftId.Value)) { CreateNewShipmentDraft(); currentDraftId = _draftOrder.LastOrDefault(); }
        if (!_drafts.TryGetValue(currentDraftId!.Value, out var draft)) return;
        ShowShipmentItemDialog(product, draft);
        ShowShipmentDraft(currentDraftId.Value);
    }

    private void ConfirmShipment(int id)
    {
        var draft = _drafts[id];
        if (draft.Items.Count == 0) { MessageBox.Show("Добавьте товар в отгрузку.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (string.IsNullOrWhiteSpace(draft.Recipient) || string.IsNullOrWhiteSpace(draft.Address))
        {
            MessageBox.Show("Откройте «Проверка и логистика» и укажите получателя (название компании) и адрес доставки.",
                "Заполните данные получателя", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenLogistics(id);
            return;
        }

        var logistics = draft.Logistics;
        if (logistics != null && logistics.Decision == DealDecision.NeedsReview)
        {
            var prompt = Me.IsAdmin
                ? "У контрагента обнаружен риск (требуется решение администратора).\nВсё равно оформить отгрузку?"
                : "У контрагента обнаружен риск. Оформление возможно только после решения администратора.\nОформить отгрузку под вашу ответственность?";
            if (MessageBox.Show(prompt, "Требуется решение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }

        var items = draft.Items.Select(i => (i.ProductId, i.Price, i.Quantity)).ToList();
        var result = _svc.ShipmentService.CreateShipment(draft.Name, draft.Recipient, draft.Address, items, Me.Id, logistics?.ToJson());
        if (!result.Success) { MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        MessageBox.Show(result.Message, "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _drafts.Remove(id); _draftOrder.Remove(id);
        if (_draftOrder.Count == 0) ShowCatalog(); else ShowShipmentDraft(_draftOrder[0]);
    }

    private string GenerateUniqueDraftName()
    {
        int max = 0;
        foreach (var s in _svc.ShipmentService.GetAll()) if (TryParseShipmentNumber(s.Name, out var n) && n > max) max = n;
        foreach (var d in _drafts.Values) if (TryParseShipmentNumber(d.Name, out var n) && n > max) max = n;
        return $"Отгрузка {max + 1}";
    }

    private static bool TryParseShipmentNumber(string? name, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("Отгрузка ", StringComparison.OrdinalIgnoreCase)) return false;
        return int.TryParse(name[8..].Trim(), out number);
    }

    private void CreateCategory()
    {
        string? name = PromptInput("Новая категория", "Название:"); if (name == null) return;
        var result = _svc.CategoryService.Create(name);
        if (!result.Success) MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshContent();
    }

    private void RenameCategory(Category category)
    {
        string? name = PromptInput("Изменить категорию", "Новое название:", category.Name); if (name == null) return;
        var result = _svc.CategoryService.Update(category.Id, name);
        if (!result.Success) MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshContent();
    }

    private void DeleteCategory(Category category)
    {
        if (MessageBox.Show($"Удалить категорию «{category.Name}»?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var result = _svc.CategoryService.Delete(category.Id);
        if (!result.Success) MessageBox.Show(result.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshContent();
    }

    private static string? PromptInput(string title, string label, string initialValue = "")
    {
        using var dialog = new Form
        {
            Text = title, ClientSize = new Size(460, 200), StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            BackColor = UI.BgLight, AutoScaleMode = AutoScaleMode.None
        };
        var panel = UI.CreateRoundedPanel(UI.BgCard, 18); panel.Bounds = new Rectangle(18, 18, 424, 164); dialog.Controls.Add(panel);
        panel.Controls.Add(new Label { Text = label, Font = UI.FontMed, ForeColor = UI.TextDark, AutoSize = true, Location = new Point(20, 20) });
        var host = UI.CreateRoundedPanel(Color.White, 12); host.Bounds = new Rectangle(20, 54, 384, 44); panel.Controls.Add(host);
        var txt = new TextBox { Text = initialValue, BorderStyle = BorderStyle.None, Font = UI.FontMed, BackColor = Color.White, ForeColor = UI.TextDark };
        host.Controls.Add(txt);
        UI.BindControlToHost(host, txt, new Padding(14, 5, 14, 5), verticalOffset: -1);
        var btnOk = UI.CreatePillButton("OK", UI.TabActive, new Size(100, 40), UI.FontMedBold); btnOk.Location = new Point(panel.Width - 120, 112); btnOk.DialogResult = DialogResult.OK; panel.Controls.Add(btnOk);
        dialog.AcceptButton = btnOk;
        return dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text) ? txt.Text.Trim() : null;
    }
}

public class ShipmentDraft
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<ShipmentDraftItem> Items { get; set; } = new();

    public ShipmentLogistics? Logistics { get; set; }
}

public class ShipmentDraftItem
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
