using WarehouseApp.Services;

namespace WarehouseApp.Forms;

public class AddressSuggestBox : Panel
{
    private readonly TextBox _tb;
    private readonly ListBox _list;
    private readonly Form _popup;
    private readonly System.Windows.Forms.Timer _debounce;
    private Func<string, List<GeoSuggestion>>? _suggester;
    private List<GeoSuggestion> _current = new();
    private bool _suppress;
    private bool _picking;
    private Form? _hookedForm;

    public event EventHandler? SelectionChanged;
    public GeoSuggestion? Selected { get; private set; }

    public string AddressText
    {
        get => _tb.Text;
        set { _suppress = true; _tb.Text = value ?? string.Empty; _suppress = false; }
    }

    public AddressSuggestBox(Color back)
    {
        BackColor = back;
        Padding = new Padding(12, 6, 12, 6);

        _tb = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = UI.FontMed,
            BackColor = back,
            ForeColor = UI.TextDark,
            Dock = DockStyle.Fill
        };
        Controls.Add(_tb);

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = UI.FontSmall,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        _list.MouseDown += (_, e) =>
        {
            int idx = _list.IndexFromPoint(e.Location);
            if (idx >= 0) { _list.SelectedIndex = idx; Pick(); }
        };
        _list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Pick(); };

        _popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            TopMost = true,
            MinimumSize = new Size(10, 10)
        };
        _popup.Controls.Add(_list);

        _debounce = new System.Windows.Forms.Timer { Interval = 350 };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSuggest(); };

        _tb.TextChanged += (_, _) =>
        {
            if (_suppress) return;
            Selected = null;
            _debounce.Stop();
            if (_tb.Text.Trim().Length >= 3) _debounce.Start();
            else HidePopup();
        };
        _tb.LostFocus += (_, _) =>
        {
            if (_picking) return;
            try { BeginInvoke(() => { if (!_picking && !_popup.ContainsFocus && !_list.Focused) HidePopup(); }); }
            catch { HidePopup(); }
        };
        _tb.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && _popup.Visible && _list.Items.Count > 0)
            { _list.Focus(); _list.SelectedIndex = 0; e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) HidePopup();
        };

        Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(200, 205, 215), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    public void SetSuggester(Func<string, List<GeoSuggestion>> suggester) => _suggester = suggester;

    private void RunSuggest()
    {
        var query = _tb.Text.Trim();
        if (_suggester == null || query.Length < 3) return;
        var fn = _suggester;
        System.Threading.Tasks.Task.Run(() =>
        {
            List<GeoSuggestion> res;
            try { res = fn(query); } catch { res = new List<GeoSuggestion>(); }
            if (IsDisposed || !_tb.IsHandleCreated) return;
            try
            {
                BeginInvoke(() =>
                {
                    if (_tb.Text.Trim() != query || !_tb.Focused) return;
                    ShowSuggestions(res);
                });
            }
            catch { }
        });
    }

    private void ShowSuggestions(List<GeoSuggestion> res)
    {
        _current = res;
        _list.Items.Clear();
        if (res.Count == 0) { HidePopup(); return; }
        foreach (var s in res) _list.Items.Add(s.Display);

        var form = FindForm();
        if (form == null) return;
        if (_hookedForm != form)
        {
            if (_hookedForm != null)
            {
                _hookedForm.Move -= OnFormChanged;
                _hookedForm.Deactivate -= OnFormChanged;
            }
            _hookedForm = form;
            _hookedForm.Move += OnFormChanged;
            _hookedForm.Deactivate += OnFormChanged;
        }
        var screenPt = PointToScreen(new Point(0, Height));
        int h = Math.Min(res.Count, 7) * (_list.Font.Height + 8) + 4;
        _popup.Owner = form;
        _popup.Bounds = new Rectangle(screenPt.X, screenPt.Y + 1, Math.Max(Width, 320), Math.Max(40, h));
        if (!_popup.Visible) _popup.Show(form);
        _popup.BringToFront();
    }

    private void Pick()
    {
        int i = _list.SelectedIndex;
        if (i < 0 || i >= _current.Count) return;
        _picking = true;
        Selected = _current[i];
        AddressText = _current[i].Display;
        HidePopup();
        _tb.Focus();
        _tb.SelectionStart = _tb.Text.Length;
        _picking = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFormChanged(object? sender, EventArgs e) => HidePopup();

    private void HidePopup()
    {
        if (_popup.Visible) _popup.Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _debounce.Dispose(); _popup.Dispose(); }
        base.Dispose(disposing);
    }
}
