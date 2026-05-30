namespace WarehouseApp.Forms;

public class LoginForm : Form
{
    private readonly AppServices _svc;
    private Panel _card = null!;

    private Panel _loginView = null!;
    private TextBox _txtLogin = null!;
    private TextBox _txtPassword = null!;
    private Label _lblLoginError = null!;
    private Button _btnLoginSubmit = null!;

    private Panel _registerView = null!;
    private TextBox _txtRegLogin = null!;
    private TextBox _txtRegPassword = null!;
    private TextBox _txtRegConfirm = null!;
    private Label _lblRegError = null!;
    private Button _btnRegSubmit = null!;

    public LoginForm(AppServices svc)
    {
        _svc = svc;
        Build();
    }

    private void Build()
    {
        Text = "Складской учёт";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 700);
        ClientSize = new Size(1400, 860);
        BackColor = UI.BgLight;
        Font = UI.DefaultFont;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;

        _card = UI.CreateRoundedPanel(UI.BgCard, 24);
        Controls.Add(_card);

        BuildLoginView();
        BuildRegisterView();

        _loginView.Visible = true;
        _registerView.Visible = false;
        AcceptButton = _btnLoginSubmit;

        void LayoutCard(object? _, EventArgs __)
        {
            int cardW = Math.Max(540, Math.Min(740, ClientSize.Width / 2));

            bool regVisible = _registerView.Visible;
            int cardH = regVisible
                ? Math.Max(520, Math.Min(640, ClientSize.Height - 160))
                : Math.Max(460, Math.Min(580, ClientSize.Height - 180));

            _card.Size = new Size(cardW, cardH);
            _card.Location = new Point((ClientSize.Width - cardW) / 2, (ClientSize.Height - cardH) / 2);

            _loginView.Size = _card.ClientSize;
            _registerView.Size = _card.ClientSize;

            LayoutLoginControls();
            LayoutRegisterControls();
        }

        Resize += LayoutCard;
        Shown += LayoutCard;
        LayoutCard(this, EventArgs.Empty);
    }

    private void BuildLoginView()
    {
        _loginView = UI.CreatePanel(Color.Transparent);
        _loginView.Dock = DockStyle.Fill;
        _card.Controls.Add(_loginView);

        _loginView.Controls.Add(new Label
        {
            Text = "Складской учёт",
            Font = UI.Px(36),
            ForeColor = UI.TextDark,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Name = "lblTitle"
        });

        _txtLogin = CreateInputInView(_loginView, "Введите логин");
        _txtPassword = CreateInputInView(_loginView, "Введите пароль");
        _txtPassword.UseSystemPasswordChar = true;

        _lblLoginError = new Label
        {
            Font = UI.FontSmall,
            ForeColor = UI.BtnRed,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            BackColor = Color.Transparent
        };
        _loginView.Controls.Add(_lblLoginError);

        _btnLoginSubmit = UI.CreatePillButton("ВОЙТИ", UI.BtnBlue, new Size(0, 90), UI.FontHugeButton);
        _btnLoginSubmit.Click += BtnLogin_Click;
        _loginView.Controls.Add(_btnLoginSubmit);

        var btnToReg = new Button
        {
            Text = "РЕГИСТРАЦИЯ",
            Font = UI.Px(22),
            ForeColor = UI.TextDark,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnToReg.FlatAppearance.BorderSize = 0;
        btnToReg.FlatAppearance.MouseDownBackColor = UI.BgCard;
        btnToReg.FlatAppearance.MouseOverBackColor = UI.BgCard;
        btnToReg.Click += (_, _) => ShowRegisterView();
        _loginView.Controls.Add(btnToReg);
        btnToReg.Name = "btnToReg";
    }

    private void LayoutLoginControls()
    {
        if (_loginView.Width == 0) return;
        int w = _loginView.Width;

        var lblTitle = _loginView.Controls["lblTitle"];
        if (lblTitle != null) lblTitle.Bounds = new Rectangle(24, 28, w - 48, 72);

        LayoutInputInView(_loginView, _txtLogin, 128);
        LayoutInputInView(_loginView, _txtPassword, 192);
        _lblLoginError.Bounds = new Rectangle(36, 260, w - 72, 28);

        int loginW = (int)(w * 0.68);
        _btnLoginSubmit.Size = new Size(loginW, 90);
        _btnLoginSubmit.Location = new Point((w - loginW) / 2, 302);

        var btnToReg = _loginView.Controls["btnToReg"];
        if (btnToReg != null)
            btnToReg.Bounds = new Rectangle((w - 320) / 2, _btnLoginSubmit.Bottom + 24, 320, 48);
    }

    private void BuildRegisterView()
    {
        _registerView = UI.CreatePanel(Color.Transparent);
        _registerView.Dock = DockStyle.Fill;
        _card.Controls.Add(_registerView);

        _registerView.Controls.Add(new Label
        {
            Text = "Регистрация",
            Font = UI.Px(36),
            ForeColor = UI.TextDark,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Name = "lblRegTitle"
        });

        _txtRegLogin = CreateInputInView(_registerView, "Введите логин");
        _txtRegPassword = CreateInputInView(_registerView, "Введите пароль");
        _txtRegPassword.UseSystemPasswordChar = true;
        _txtRegConfirm = CreateInputInView(_registerView, "Подтвердите пароль");
        _txtRegConfirm.UseSystemPasswordChar = true;

        _lblRegError = new Label
        {
            Font = UI.FontSmall,
            ForeColor = UI.BtnRed,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            BackColor = Color.Transparent
        };
        _registerView.Controls.Add(_lblRegError);

        _btnRegSubmit = UI.CreatePillButton("ЗАРЕГИСТРИРОВАТЬСЯ", UI.BtnBlue, new Size(0, 90), UI.Px(24));
        _btnRegSubmit.Click += BtnReg_Click;
        _registerView.Controls.Add(_btnRegSubmit);

        var btnBack = new Button
        {
            Text = "Назад",
            Font = UI.Px(20),
            ForeColor = UI.TextDark,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Name = "btnBack"
        };
        btnBack.FlatAppearance.BorderSize = 0;
        btnBack.FlatAppearance.MouseDownBackColor = UI.BgCard;
        btnBack.FlatAppearance.MouseOverBackColor = UI.BgCard;
        btnBack.Click += (_, _) => ShowLoginView();
        _registerView.Controls.Add(btnBack);
    }

    private void LayoutRegisterControls()
    {
        if (_registerView.Width == 0) return;
        int w = _registerView.Width;

        var lblRegTitle = _registerView.Controls["lblRegTitle"];
        if (lblRegTitle != null) lblRegTitle.Bounds = new Rectangle(24, 28, w - 48, 72);

        LayoutInputInView(_registerView, _txtRegLogin, 128);
        LayoutInputInView(_registerView, _txtRegPassword, 192);
        LayoutInputInView(_registerView, _txtRegConfirm, 256);
        _lblRegError.Bounds = new Rectangle(36, 318, w - 72, 28);

        int btnW = (int)(w * 0.74);
        _btnRegSubmit.Size = new Size(btnW, 90);
        _btnRegSubmit.Location = new Point((w - btnW) / 2, 360);

        var btnBack = _registerView.Controls["btnBack"];
        if (btnBack != null)
            btnBack.Bounds = new Rectangle((w - 240) / 2, _btnRegSubmit.Bottom + 18, 240, 44);
    }

    private void ShowRegisterView()
    {
        _lblLoginError.Visible = false;
        _loginView.Visible = false;
        _registerView.Visible = true;
        AcceptButton = _btnRegSubmit;

        int cardW = _card.Width;
        int cardH = Math.Max(520, Math.Min(640, ClientSize.Height - 160));
        _card.Size = new Size(cardW, cardH);
        _card.Location = new Point((ClientSize.Width - cardW) / 2, (ClientSize.Height - cardH) / 2);
        _registerView.Size = _card.ClientSize;
        LayoutRegisterControls();

        _txtRegLogin.Text = string.Empty;
        _txtRegPassword.Text = string.Empty;
        _txtRegConfirm.Text = string.Empty;
        _lblRegError.Visible = false;
        _txtRegLogin.Focus();
    }

    private void ShowLoginView()
    {
        _registerView.Visible = false;
        _loginView.Visible = true;
        AcceptButton = _btnLoginSubmit;

        int cardW = _card.Width;
        int cardH = Math.Max(460, Math.Min(580, ClientSize.Height - 180));
        _card.Size = new Size(cardW, cardH);
        _card.Location = new Point((ClientSize.Width - cardW) / 2, (ClientSize.Height - cardH) / 2);
        _loginView.Size = _card.ClientSize;
        LayoutLoginControls();

        _txtPassword.Text = string.Empty;
        _lblLoginError.Visible = false;
        _txtLogin.Focus();
    }

    private static void LayoutTextBoxInHost(Control host, TextBox txt)
        => UI.LayoutControlInHost(host, txt, new Padding(16, 6, 16, 6), verticalOffset: -3);

    private TextBox CreateInputInView(Control parent, string placeholder)
    {
        var host = UI.CreateRoundedPanel(UI.InputBgLogin, 14);
        host.Tag = "host";
        parent.Controls.Add(host);

        var txt = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.Px(22),
            BackColor = UI.InputBgLogin,
            ForeColor = UI.TextDark,
            PlaceholderText = placeholder
        };
        host.Controls.Add(txt);
        UI.BindControlToHost(host, txt, new Padding(16, 6, 16, 6), verticalOffset: -3);
        return txt;
    }

    private void LayoutInputInView(Control parent, TextBox txt, int y)
    {
        if (txt.Parent is not Panel host) return;
        host.Bounds = new Rectangle(40, y, parent.Width - 80, 48);
        LayoutTextBoxInHost(host, txt);
    }

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        _lblLoginError.Text = string.Empty;
        _lblLoginError.Visible = false;
        var result = _svc.AuthService.Login(_txtLogin.Text, _txtPassword.Text);
        if (!result.Success)
        {
            _lblLoginError.Text = result.Message;
            _lblLoginError.Visible = true;
            return;
        }

        var main = new MainForm(_svc);
        main.FormClosed += (_, _) =>
        {
            _svc.AuthService.Logout();
            _txtPassword.Text = string.Empty;
            _lblLoginError.Text = string.Empty;
            Show();
        };

        Hide();
        main.Show();
    }

    private void BtnReg_Click(object? sender, EventArgs e)
    {
        _lblRegError.Text = string.Empty;
        _lblRegError.Visible = false;
        var result = _svc.AuthService.Register(_txtRegLogin.Text, _txtRegPassword.Text, _txtRegConfirm.Text);
        if (!result.Success)
        {
            _lblRegError.Text = result.Message;
            _lblRegError.Visible = true;
            return;
        }

        MessageBox.Show(result.Message, "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ShowLoginView();
    }
}
