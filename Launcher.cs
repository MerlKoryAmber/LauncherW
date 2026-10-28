using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class LauncherForm : Form
{
    readonly FlowLayoutPanel grid = new FlowLayoutPanel();
    readonly TextBox search = new TextBox();
    readonly Label status = new Label();
    readonly Label empty = new Label();
    readonly Label searchLabel;
    readonly Label brand;
    readonly Panel top;
    readonly List<AppItem> visible = new List<AppItem>();
    readonly ToolTip tips = new ToolTip();
    LauncherSettings view = LauncherSettings.Normalize(null);
    Image background;
    string loadError;

    public LauncherForm()
    {
        Text = view.LauncherTitle;
        Theme.Apply(this);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(840, 500);
        MinimumSize = new Size(450, 360);
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White };
        brand = Theme.Label(view.LauncherTitle, 18, true);
        brand.AutoSize = false;
        brand.Dock = DockStyle.Fill;
        brand.TextAlign = ContentAlignment.MiddleCenter;
        brand.AutoEllipsis = true;
        brand.ForeColor = Theme.Brand;
        header.Controls.Add(brand);
        top = new Panel { Dock = DockStyle.Top, Height = 78 };
        searchLabel = Theme.Label("Поиск приложений", 9, false);
        searchLabel.Location = new Point(29, 8);
        searchLabel.ForeColor = Theme.Muted;
        top.Controls.Add(searchLabel);
        search.SetBounds(29, 32, 200, 29);
        search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        search.TextChanged += delegate { Fill(); };
        top.Controls.Add(search);
        top.Resize += delegate { FitSearch(); };
        grid.Dock = DockStyle.Fill;
        grid.Padding = new Padding(22, 6, 16, 12);
        grid.AutoScroll = true;
        grid.SizeChanged += delegate { ResizeTiles(); };
        empty.TextAlign = ContentAlignment.MiddleCenter;
        empty.ForeColor = Theme.Muted;
        empty.Height = 110;
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White };
        status.Dock = DockStyle.Fill;
        status.BackColor = Color.White;
        status.Padding = new Padding(28, 18, 0, 0);
        status.ForeColor = Theme.Muted;
        var refreshHost = new Panel { Dock = DockStyle.Right, Width = 156, BackColor = Color.White };
        var refresh = Theme.Button("Обновить", delegate { Reload(); }, false);
        refresh.SetBounds(12, 10, 132, 36);
        refreshHost.Controls.Add(refresh);
        footer.Controls.Add(status);
        footer.Controls.Add(refreshHost);
        Controls.Add(grid);
        Controls.Add(top);
        Controls.Add(header);
        Controls.Add(footer);
        Reload();
        Shown += delegate { FitSearch(); };
    }

    void FitSearch()
    {
        search.Width = Math.Max(80, top.ClientSize.Width - search.Left * 2);
    }

    void Reload()
    {
        visible.Clear();
        loadError = null;
        try
        {
            var catalog = CatalogStore.Load(CatalogStore.DefaultPath());
            view = LauncherSettings.Normalize(catalog.Settings);
            visible.AddRange(Access.Visible(catalog.Items));
            if (catalog.Items.Count == 0)
                loadError = "Список ещё не настроен. Обратитесь к администратору.";
            else if (visible.Count == 0)
                loadError = "Нет доступных вам программ. Обратитесь к администратору.";
        }
        catch (Exception ex)
        {
            loadError = "Не удалось загрузить каталог. Нажмите «Обновить».";
            tips.SetToolTip(status, ex.Message);
        }
        ApplyView();
        Fill();
    }

    void ApplyView()
    {
        Text = view.LauncherTitle;
        brand.Text = view.LauncherTitle;
        ClientSize = new Size(view.WindowWidth, view.WindowHeight);
        bool show = view.SearchVisible;
        search.Visible = show;
        searchLabel.Visible = show;
        if (!show)
            search.Text = "";
        top.Height = show ? search.Bottom + 12 : 0;
        FitSearch();
        var color = LauncherSettings.ParseColor(view.Background, Theme.Background);
        var font = LauncherSettings.ParseColor(view.FontColor, Theme.Ink);
        BackColor = color;
        top.BackColor = Color.White;
        grid.BackColor = color;
        empty.BackColor = color;
        empty.ForeColor = font;
        searchLabel.ForeColor = Theme.Muted;
        if (background != null)
        {
            grid.BackgroundImage = null;
            background.Dispose();
            background = null;
        }
        if (!string.IsNullOrWhiteSpace(view.BackgroundImage) && File.Exists(view.BackgroundImage))
        {
            try
            {
                using (var image = Image.FromFile(view.BackgroundImage))
                    background = new Bitmap(image);
                grid.BackgroundImage = background;
                grid.BackgroundImageLayout = ImageLayout.Zoom;
            }
            catch (Exception)
            {
                background = null;
            }
        }
    }

    void Fill()
    {
        grid.SuspendLayout();
        var old = new List<Control>();
        foreach (Control control in grid.Controls)
            old.Add(control);
        grid.Controls.Clear();
        foreach (var control in old)
            if (control != empty)
                control.Dispose();
        int count = 0;
        string query = search.Text ?? "";
        for (int i = 0; i < visible.Count; i++)
        {
            var item = visible[i];
            if (item.Separator)
            {
                bool any = false;
                for (int j = i + 1; j < visible.Count; j++)
                {
                    if (visible[j].Separator)
                        break;
                    var name = visible[j].DisplayName;
                    if (name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        any = true;
                        break;
                    }
                }
                if (any)
                {
                    var sectionColor = string.IsNullOrWhiteSpace(item.FontColor)
                        ? LauncherSettings.ParseColor(view.FontColor, Theme.Ink)
                        : LauncherSettings.ParseColor(item.FontColor, Theme.Ink);
                    grid.Controls.Add(new SectionHeader(item.DisplayName, sectionColor, item.ResolvedCaptionSize()));
                }
                continue;
            }
            var caption = item.DisplayName;
            if (caption.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0)
                continue;
            var tile = new AppTile(item, view);
            tile.Click += delegate { Launch(item); };
            tips.SetToolTip(tile, caption + "\n" + (item.Path ?? ""));
            grid.Controls.Add(tile);
            count++;
        }
        if (count == 0)
        {
            empty.Text = loadError ?? "Ничего не найдено. Попробуйте другое название.";
            grid.Controls.Add(empty);
        }
        status.Text = loadError == null ? "Каталог загружен  ·  Приложений: " + count : "Требуется внимание";
        ResizeTiles();
        grid.ResumeLayout();
    }

    void ResizeTiles()
    {
        int available = Math.Max(260, grid.ClientSize.Width - 60);
        foreach (Control control in grid.Controls)
            if (control == empty || control is SectionHeader)
                control.Width = available;
    }

    void Launch(AppItem item)
    {
        if (!Access.CanLaunch(item.Group))
        {
            MessageBox.Show(this, "Доступ изменился. Каталог будет обновлён.", Text);
            Reload();
            return;
        }
        if (!File.Exists(item.Path))
        {
            MessageBox.Show(this, "Не найден файл программы:\n" + item.Path + "\nОбратитесь к администратору каталога.", "Программа недоступна", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(item.Path, item.Args ?? "") { WorkingDirectory = Path.GetDirectoryName(item.Path), UseShellExecute = true });
            status.Text = "Запуск: " + item.Name;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось запустить программу.\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tips.Dispose();
            if (background != null)
                background.Dispose();
        }
        base.Dispose(disposing);
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { CatalogStore.EnsureLocation(); }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть папку config.\n" + ex.Message, "TALK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Application.Run(new LauncherForm());
    }
}

public class SectionHeader : Label
{
    public SectionHeader(string title, Color color, int fontSize)
    {
        Text = title ?? "";
        AutoSize = false;
        int size = fontSize < 8 ? 12 : fontSize;
        Height = Math.Max(28, size + 18);
        Margin = new Padding(8, 16, 8, 2);
        Padding = new Padding(4, 0, 0, 0);
        Font = new Font("Segoe UI", size, FontStyle.Bold);
        ForeColor = color;
        BackColor = Color.Transparent;
        TextAlign = ContentAlignment.MiddleCenter;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = title;
    }
}

public class AppTile : SoftButton
{
    readonly Bitmap image;
    readonly int iconSize;
    readonly Color captionColor;
    readonly bool plateHidden;

    public AppTile(AppItem item, LauncherSettings settings)
    {
        var full = string.IsNullOrEmpty(item.Name) ? item.Path : item.Name;
        AccessibleName = full;
        AccessibleRole = AccessibleRole.PushButton;
        int limit = settings.CaptionLength;
        Text = full.Length <= limit ? full : full.Substring(0, limit).TrimEnd() + "…";
        iconSize = settings.IconSize;
        captionColor = LauncherSettings.ParseColor(settings.FontColor, Theme.Ink);
        plateHidden = settings.TilePlateHidden;
        image = AppIcons.Load(item.Icon, item.Path);
        Width = Math.Max(settings.TileWidth, iconSize + 24);
        Height = Math.Max(settings.TileHeight, iconSize + 40);
        Margin = new Padding(8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!plateHidden)
        {
            base.OnPaint(e);
            return;
        }
        PaintParentBackground(e.Graphics);
        PaintCaption(e.Graphics);
    }

    protected override void PaintCaption(Graphics g)
    {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(image, (Width - iconSize) / 2, 12, iconSize, iconSize);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(8, iconSize + 18, Width - 16, Height - iconSize - 24), captionColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            image.Dispose();
        base.Dispose(disposing);
    }
}
