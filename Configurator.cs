using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class ConfiguratorForm : Form
{
    readonly TextBox _configGroup = new TextBox();
    readonly ListView _list = new ListView();
    readonly Label _status = new Label();
    Button _edit, _delete, _up, _down;
    readonly AppCatalog _catalog = new AppCatalog();
    readonly string _configPath;

    public ConfiguratorForm(string configPath, AppCatalog catalog)
    {
        _configPath = configPath;
        if (catalog != null)
        {
            _catalog.ConfiguratorGroup = catalog.ConfiguratorGroup;
            _catalog.Settings = LauncherSettings.Normalize(catalog.Settings);
            if (catalog.Items != null)
                _catalog.Items.AddRange(catalog.Items);
        }

        Text = "TALK — Настройка каталога";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(940, 500); MinimumSize = new Size(940, 460);
        Theme.Apply(this);

        var top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 94; top.BackColor = Color.White;
        var label = new Label();
        label.Text = "Кто может изменять каталог";
        label.SetBounds(24, 15, 300, 22);
        top.Controls.Add(label);
        _configGroup.ReadOnly = true;
        _configGroup.SetBounds(24, 48, 420, 26);
        _configGroup.Text = _catalog.ConfiguratorGroup ?? "Только администраторы";
        top.Controls.Add(_configGroup);
        var pick = new SoftButton();
        pick.Text = "Выбрать";
        pick.SetBounds(460, 43, 160, 36);
        pick.Click += delegate { PickConfiguratorGroup(); };
        top.Controls.Add(pick);
        var clear = new SoftButton();
        clear.Text = "Администраторы";
        clear.SetBounds(632, 43, 210, 36);
        clear.Click += delegate { ClearConfiguratorGroup(); };
        top.Controls.Add(clear);

        _list.View = View.Details; _list.FullRowSelect = true; _list.MultiSelect = false; _list.HideSelection = false;
        _list.BorderStyle = BorderStyle.None; _list.BackColor = Color.White;
        _list.Columns.Add("Приложение", 220); _list.Columns.Add("Кому доступно", 230); _list.Columns.Add("Путь к программе", 395);
        _list.SelectedIndexChanged += delegate { UpdateSelection(); };
        _list.DoubleClick += delegate { EditSelected(); };
        _list.Dock = DockStyle.Fill;

        var buttons = new FlowLayoutPanel();
        buttons.Dock = DockStyle.Bottom;
        buttons.Height = 72;
        buttons.Padding = new Padding(16, 14, 8, 8);
        buttons.WrapContents = false;
        buttons.Controls.Add(MakeButton("Добавить", delegate { EditItem(null); }));
        buttons.Controls.Add(MakeButton("Разделитель", delegate { EditSeparator(null); }));
        _edit = MakeButton("Изменить", delegate { EditSelected(); }); _edit.Enabled=false; buttons.Controls.Add(_edit);
        _delete = MakeButton("Удалить", delegate { DeleteSelected(); }); _delete.Enabled=false; buttons.Controls.Add(_delete);
        _up = MakeButton("Выше", delegate { MoveSelected(-1); }); _up.Enabled=false; buttons.Controls.Add(_up);
        _down = MakeButton("Ниже", delegate { MoveSelected(1); }); _down.Enabled=false; buttons.Controls.Add(_down);
        buttons.Controls.Add(MakeButton("Настройка", delegate { EditSettings(); }));

        Controls.Add(_list);
        Controls.Add(top);
        Controls.Add(buttons);
        _status.Dock=DockStyle.Bottom; _status.Height=32; _status.Padding=new Padding(24,4,0,0); _status.ForeColor=Theme.Muted;
        _status.Text="Изменения сохраняются автоматически"; Controls.Add(_status);
        Fill();
    }

    static Button MakeButton(string text, EventHandler click)
    {
        var button = new SoftButton();
        button.Text = text;
        button.Width = text == "Разделитель" ? 128 : 110;
        button.Height = 36;
        button.Margin = new Padding(0, 0, 8, 0);
        ((SoftButton)button).Primary = text == "Добавить";
        button.Click += click;
        return button;
    }

    void Fill()
    {
        _list.Items.Clear();
        foreach (var item in _catalog.Items)
        {
            if (item.Separator)
            {
                var section = new ListViewItem("▸ " + item.DisplayName);
                section.SubItems.Add("раздел");
                section.SubItems.Add("");
                section.ForeColor = Theme.Brand;
                _list.Items.Add(section);
                continue;
            }
            var name = item.DisplayName;
            var who = string.IsNullOrWhiteSpace(item.Group) ? "всем" : item.Group;
            var row = new ListViewItem(name);
            row.SubItems.Add(who == "всем" ? "Все пользователи" : who);
            row.SubItems.Add(item.Path ?? "");
            _list.Items.Add(row);
        }
    }

    bool SaveCatalog()
    {
        try { CatalogStore.Save(_configPath, _catalog); _status.Text="Изменения сохранены · " + DateTime.Now.ToString("HH:mm"); return true; }
        catch(Exception ex) { _status.Text="Не удалось сохранить изменения"; MessageBox.Show(this,"Не удалось сохранить каталог. Проверьте доступ к файлу и повторите действие.\n"+ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning); return false; }
    }
    void RememberConfiguratorGroup()
    {
        _catalog.ConfiguratorGroup = string.IsNullOrWhiteSpace(_configGroup.Text) || _configGroup.Text == "Только администраторы" ? null : _configGroup.Text.Trim();
    }

    void EditSettings()
    {
        using (var dialog = new SettingsDialog(_catalog.Settings))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            var previous = _catalog.Settings;
            _catalog.Settings = dialog.Result;
            if (!SaveCatalog())
                _catalog.Settings = previous;
        }
    }

    void PickConfiguratorGroup()
    {
        string account;
        string error;
        if (!DomainGroupPicker.Pick(Handle, out account, out error))
        {
            if (error != null)
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string previous = _catalog.ConfiguratorGroup;
        _configGroup.Text = account;
        RememberConfiguratorGroup();
        if (!SaveCatalog()) { _catalog.ConfiguratorGroup=previous; _configGroup.Text=previous??"Только администраторы"; }
    }

    void ClearConfiguratorGroup()
    {
        if (MessageBox.Show(this,"Управлять каталогом смогут только администраторы. Продолжить?",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
        string previous = _catalog.ConfiguratorGroup;
        _configGroup.Text = "Только администраторы";
        RememberConfiguratorGroup();
        if (!SaveCatalog()) { _catalog.ConfiguratorGroup=previous; _configGroup.Text=previous??"Только администраторы"; }
    }

    void EditSelected()
    {
        if (_list.SelectedIndices.Count == 0)
            return;
        var item = _catalog.Items[_list.SelectedIndices[0]];
        if (item.Separator)
            EditSeparator(item);
        else
            EditItem(item);
    }

    void EditItem(AppItem existing)
    {
        using (var dialog = new ItemDialog(existing))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            var backup = existing == null ? null : CloneItem(existing);
            if (existing == null)
                _catalog.Items.Add(dialog.Item);
            else
                CopyItem(dialog.Item, existing);
            RememberConfiguratorGroup();
            if (!SaveCatalog()) {
                if(existing==null) _catalog.Items.Remove(dialog.Item);
                else CopyItem(backup, existing);
                return;
            }
            Fill(); SelectRow(existing == null ? _catalog.Items.Count - 1 : _catalog.Items.IndexOf(existing));
        }
    }

    void EditSeparator(AppItem existing)
    {
        using (var dialog = new SeparatorDialog(existing))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            var backup = existing == null ? null : CloneItem(existing);
            if (existing == null)
            {
                int insert = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] + 1 : _catalog.Items.Count;
                _catalog.Items.Insert(insert, dialog.Item);
                existing = dialog.Item;
            }
            else
            {
                existing.Name = dialog.Item.Name;
                existing.CaptionSize = dialog.Item.CaptionSize;
                existing.FontColor = dialog.Item.FontColor;
                existing.Separator = true;
            }
            RememberConfiguratorGroup();
            if (!SaveCatalog())
            {
                if (backup == null)
                    _catalog.Items.Remove(dialog.Item);
                else
                    CopyItem(backup, existing);
                return;
            }
            Fill();
            SelectRow(_catalog.Items.IndexOf(existing));
        }
    }

    static AppItem CloneItem(AppItem source)
    {
        return new AppItem
        {
            Name = source.Name,
            Path = source.Path,
            Args = source.Args,
            Group = source.Group,
            Icon = source.Icon,
            Separator = source.Separator,
            CaptionSize = source.CaptionSize,
            FontColor = source.FontColor
        };
    }

    static void CopyItem(AppItem source, AppItem target)
    {
        target.Name = source.Name;
        target.Path = source.Path;
        target.Args = source.Args;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.Separator = source.Separator;
        target.CaptionSize = source.CaptionSize;
        target.FontColor = source.FontColor;
    }

    void DeleteSelected()
    {
        if (_list.SelectedIndices.Count == 0)
            return;
        var current = _catalog.Items[_list.SelectedIndices[0]];
        string prompt = current.Separator
            ? "Удалить раздел «" + current.DisplayName + "»? Программы в списке останутся."
            : "Удалить «" + current.DisplayName + "» из каталога? Сам файл останется на месте.";
        if (MessageBox.Show(this, prompt, current.Separator ? "Удаление раздела" : "Удаление программы", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        int index=_list.SelectedIndices[0]; var removed=_catalog.Items[index];
        _catalog.Items.RemoveAt(index);
        RememberConfiguratorGroup();
        if (!SaveCatalog()) _catalog.Items.Insert(index, removed);
        Fill();
        if (index > _catalog.Items.Count - 1) index = _catalog.Items.Count - 1;
        SelectRow(index);
    }

    void MoveSelected(int delta)
    {
        if (_list.SelectedIndices.Count == 0)
            return;
        int index = _list.SelectedIndices[0];
        int target = index + delta;
        if (target < 0 || target >= _catalog.Items.Count)
            return;
        var item = _catalog.Items[index];
        _catalog.Items.RemoveAt(index);
        _catalog.Items.Insert(target, item);
        RememberConfiguratorGroup();
        if (!SaveCatalog())
        {
            _catalog.Items.RemoveAt(target);
            _catalog.Items.Insert(index, item);
            return;
        }
        Fill();
        SelectRow(target);
    }

    void UpdateSelection()
    {
        bool has = _list.SelectedIndices.Count > 0;
        int index = has ? _list.SelectedIndices[0] : -1;
        if (_edit != null) _edit.Enabled = has;
        if (_delete != null) _delete.Enabled = has;
        if (_up != null) _up.Enabled = has && index > 0;
        if (_down != null) _down.Enabled = has && index < _catalog.Items.Count - 1;
    }

    void SelectRow(int index)
    {
        if (index < 0 || index >= _list.Items.Count)
            return;
        _list.Items[index].Selected = true;
        _list.Items[index].Focused = true;
        _list.Items[index].EnsureVisible();
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { CatalogStore.EnsureLocation(); }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть папку config.\n" + ex.Message, "Настройка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var path = CatalogStore.DefaultPath();
        AppCatalog catalog;
        try
        {
            catalog = CatalogStore.Load(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не прочитан список:\n" + ex.Message, "Настройка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Access.CanConfigure(catalog.ConfiguratorGroup))
        {
            var who = string.IsNullOrWhiteSpace(catalog.ConfiguratorGroup)
                ? "администраторы"
                : catalog.ConfiguratorGroup.Trim();
            MessageBox.Show("Нет права на настройку. Нужна группа: " + who, "Настройка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Application.Run(new ConfiguratorForm(path, catalog));
    }
}

public class SeparatorDialog : Form
{
    readonly TextBox _name = new TextBox();
    readonly NumericUpDown _size = new NumericUpDown();
    readonly Panel _color = new Panel();
    public AppItem Item { get; private set; }

    public SeparatorDialog(AppItem existing)
    {
        Text = existing == null ? "Добавить разделитель" : "Изменить разделитель";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 220);
        Theme.Apply(this);

        var title = new Label();
        title.Text = "Название раздела";
        title.SetBounds(20, 18, 200, 22);
        Controls.Add(title);
        _name.SetBounds(20, 42, 420, 26);
        Controls.Add(_name);

        var sizeLabel = new Label();
        sizeLabel.Text = "Размер шрифта";
        sizeLabel.SetBounds(20, 84, 160, 22);
        Controls.Add(sizeLabel);
        _size.SetBounds(200, 80, 80, 26);
        _size.Minimum = 8;
        _size.Maximum = 72;
        _size.Value = existing != null && existing.CaptionSize >= 8 && existing.CaptionSize <= 72
            ? existing.CaptionSize
            : 12;
        Controls.Add(_size);

        var colorLabel = new Label();
        colorLabel.Text = "Цвет шрифта";
        colorLabel.SetBounds(20, 124, 160, 22);
        Controls.Add(colorLabel);
        _color.SetBounds(200, 120, 36, 26);
        _color.BorderStyle = BorderStyle.FixedSingle;
        _color.BackColor = existing != null && !string.IsNullOrWhiteSpace(existing.FontColor)
            ? LauncherSettings.ParseColor(existing.FontColor, Theme.Ink)
            : Theme.Ink;
        Controls.Add(_color);
        var pick = new SoftButton();
        pick.Text = "Цвет…";
        pick.SetBounds(246, 118, 100, 28);
        pick.Click += delegate
        {
            using (var dialog = new ColorDialog())
            {
                dialog.Color = _color.BackColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _color.BackColor = dialog.Color;
            }
        };
        Controls.Add(pick);

        var ok = new SoftButton();
        ok.Text = "Сохранить";
        ok.SetBounds(220, 172, 120, 28);
        ok.Click += delegate { Accept(); };
        Controls.Add(ok);
        var cancel = new SoftButton();
        cancel.Text = "Отмена";
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(348, 172, 92, 28);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        if (existing != null)
            _name.Text = existing.Name ?? "";
    }

    void Accept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Укажите название раздела.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Item = new AppItem();
        Item.Separator = true;
        Item.Name = _name.Text.Trim();
        Item.CaptionSize = (int)_size.Value;
        Item.FontColor = LauncherSettings.FormatColor(_color.BackColor);
        DialogResult = DialogResult.OK;
    }
}

public class ItemDialog : Form
{
    readonly TextBox _name = new TextBox();
    readonly TextBox _path = new TextBox();
    readonly TextBox _args = new TextBox();
    readonly TextBox _group = new TextBox();
    readonly TextBox _icon = new TextBox();
    readonly PictureBox _preview = new PictureBox();
    public AppItem Item { get; private set; }

    public ItemDialog(AppItem existing)
    {
        Text = existing == null ? "Добавить программу" : "Изменить программу";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 268);
        Theme.Apply(this);

        Controls.Add(LabelAt("Название", 12));
        _name.SetBounds(120, 12, 390, 23);
        Controls.Add(_name);

        Controls.Add(LabelAt("Программа", 44));
        _path.SetBounds(120, 44, 390, 23);
        Controls.Add(_path);
        var browse = new SoftButton();
        browse.Text = "Обзор…";
        browse.SetBounds(518, 43, 108, 25);
        browse.Click += delegate { BrowseFile(); };
        Controls.Add(browse);

        Controls.Add(LabelAt("Аргументы", 76));
        _args.SetBounds(120, 76, 506, 23);
        Controls.Add(_args);

        Controls.Add(LabelAt("Доступ", 108));
        _group.ReadOnly = true;
        _group.SetBounds(120, 108, 280, 23);
        Controls.Add(_group);
        var pickGroup = new SoftButton();
        pickGroup.Text = "Выбрать";
        pickGroup.SetBounds(408, 107, 100, 25);
        pickGroup.Click += delegate { PickGroup(); };
        Controls.Add(pickGroup);
        var clearGroup = new SoftButton();
        clearGroup.Text = "Всем";
        clearGroup.SetBounds(514, 107, 112, 25);
        clearGroup.Click += delegate { _group.Text = ""; };
        Controls.Add(clearGroup);

        Controls.Add(LabelAt("Иконка", 140));
        _icon.SetBounds(120, 140, 280, 23);
        Controls.Add(_icon);
        var pickIcon = new SoftButton();
        pickIcon.Text = "Выбрать";
        pickIcon.SetBounds(408, 139, 100, 25);
        pickIcon.Click += delegate { BrowseIcon(); };
        Controls.Add(pickIcon);
        var clearIcon = new SoftButton();
        clearIcon.Text = "Из файла";
        clearIcon.SetBounds(514, 139, 112, 25);
        clearIcon.Click += delegate
        {
            _icon.Text = "";
            RefreshPreview();
        };
        Controls.Add(clearIcon);

        _preview.SetBounds(120, 172, 48, 48);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_preview);

        var ok = new SoftButton();
        ok.Text = "Сохранить";
        ok.SetBounds(386, 224, 136, 28);
        ok.Click += delegate { Accept(); };
        Controls.Add(ok);
        var cancel = new SoftButton();
        cancel.Text = "Отмена";
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(530, 224, 96, 28);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        if (existing != null)
        {
            _name.Text = existing.Name ?? "";
            _path.Text = existing.Path ?? "";
            _args.Text = existing.Args ?? "";
            _group.Text = existing.Group ?? "";
            _icon.Text = existing.Icon ?? "";
        }
        _path.TextChanged += delegate { RefreshPreview(); };
        _icon.TextChanged += delegate { RefreshPreview(); };
        RefreshPreview();
    }

    static Label LabelAt(string text, int top)
    {
        var label = new Label();
        label.Text = text;
        label.SetBounds(12, top + 3, 100, 20);
        return label;
    }

    void BrowseFile()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Программы (*.exe;*.lnk;*.cmd;*.bat)|*.exe;*.lnk;*.cmd;*.bat|Все файлы|*.*";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            _path.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(_name.Text))
                _name.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            RefreshPreview();
        }
    }

    void BrowseIcon()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Значки (*.exe;*.dll;*.lnk;*.ico;*.png;*.bmp;*.jpg)|*.exe;*.dll;*.lnk;*.ico;*.png;*.bmp;*.jpg;*.jpeg|Все файлы|*.*";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            _icon.Text = dialog.FileName;
            RefreshPreview();
        }
    }

    void PickGroup()
    {
        string account;
        string error;
        if (!DomainGroupPicker.Pick(Handle, out account, out error))
        {
            if (error != null)
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _group.Text = account;
    }

    void RefreshPreview()
    {
        var previous = _preview.Image;
        _preview.Image = AppIcons.Load(_icon.Text, _path.Text);
        if (previous != null)
            previous.Dispose();
    }

    void Accept()
    {
        if (string.IsNullOrWhiteSpace(_path.Text))
        {
            MessageBox.Show(this, "Укажите файл.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Item = new AppItem();
        Item.Name = string.IsNullOrWhiteSpace(_name.Text)
            ? Path.GetFileNameWithoutExtension(_path.Text.Trim())
            : _name.Text.Trim();
        Item.Path = _path.Text.Trim();
        Item.Args = _args.Text.Trim();
        Item.Group = string.IsNullOrWhiteSpace(_group.Text) ? null : _group.Text.Trim();
        Item.Icon = string.IsNullOrWhiteSpace(_icon.Text) ? null : _icon.Text.Trim();
        Item.Separator = false;
        DialogResult = DialogResult.OK;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _preview.Image != null)
            _preview.Image.Dispose();
        base.Dispose(disposing);
    }
}

public class SettingsDialog : Form
{
    readonly TextBox _title = new TextBox();
    readonly Panel _color = new Panel();
    readonly Panel _font = new Panel();
    readonly TextBox _image = new TextBox();
    readonly CheckBox _search = new CheckBox();
    readonly CheckBox _plate = new CheckBox();
    readonly NumericUpDown _icon = new NumericUpDown();
    readonly NumericUpDown _tileWidth = new NumericUpDown();
    readonly NumericUpDown _tileHeight = new NumericUpDown();
    readonly NumericUpDown _caption = new NumericUpDown();
    readonly NumericUpDown _windowWidth = new NumericUpDown();
    readonly NumericUpDown _windowHeight = new NumericUpDown();
    public LauncherSettings Result { get; private set; }

    public SettingsDialog(LauncherSettings current)
    {
        var settings = LauncherSettings.Normalize(current);
        Text = "Настройка лаунчера";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(720, 580);
        Theme.Apply(this);

        const int labelX = 20;
        const int fieldX = 230;
        const int fieldW = 460;

        Controls.Add(LabelAt("Название", labelX, 22));
        _title.SetBounds(fieldX, 18, fieldW, 26);
        _title.Text = settings.LauncherTitle;
        Controls.Add(_title);

        Controls.Add(LabelAt("Фон", labelX, 66));
        _color.SetBounds(fieldX, 62, 36, 26);
        _color.BorderStyle = BorderStyle.FixedSingle;
        _color.BackColor = LauncherSettings.ParseColor(settings.Background, Theme.Background);
        Controls.Add(_color);
        AddButton("Цвет…", 276, 60, 100, delegate { ChooseColor(_color); });
        AddButton("Рисунок…", 384, 60, 120, delegate { ChooseImage(); });
        AddButton("Без рисунка", 512, 60, 150, delegate { _image.Text = ""; });
        _image.ReadOnly = true;
        _image.SetBounds(fieldX, 98, fieldW, 26);
        _image.Text = settings.BackgroundImage ?? "";
        Controls.Add(_image);

        Controls.Add(LabelAt("Шрифт", labelX, 142));
        _font.SetBounds(fieldX, 138, 36, 26);
        _font.BorderStyle = BorderStyle.FixedSingle;
        _font.BackColor = LauncherSettings.ParseColor(settings.FontColor, Theme.Ink);
        Controls.Add(_font);
        AddButton("Цвет…", 276, 136, 100, delegate { ChooseColor(_font); });

        _search.Text = "Показывать поиск";
        _search.SetBounds(fieldX, 178, 240, 26);
        _search.Checked = settings.SearchVisible;
        Controls.Add(_search);

        Controls.Add(LabelAt("Размер иконки", labelX, 218));
        SetNumber(_icon, fieldX, 214, 24, 128, settings.IconSize);
        Controls.Add(LabelAt("Ширина плитки", labelX, 258));
        SetNumber(_tileWidth, fieldX, 254, 80, 480, settings.TileWidth);
        Controls.Add(LabelAt("Высота плитки", labelX, 298));
        SetNumber(_tileHeight, fieldX, 294, 80, 480, settings.TileHeight);
        Controls.Add(LabelAt("Символов подписи", labelX, 338));
        SetNumber(_caption, fieldX, 334, 4, 80, settings.CaptionLength);
        Controls.Add(LabelAt("Ширина окна", labelX, 378));
        SetNumber(_windowWidth, fieldX, 374, 640, 3840, settings.WindowWidth);
        Controls.Add(LabelAt("Высота окна", labelX, 418));
        SetNumber(_windowHeight, fieldX, 414, 420, 2160, settings.WindowHeight);

        _plate.Text = "Убрать белый квадрат под иконкой";
        _plate.SetBounds(fieldX, 454, 460, 26);
        _plate.Checked = settings.TilePlateHidden;
        Controls.Add(_plate);

        var ok = AddButton("Сохранить", 430, 510, 140, delegate { AcceptSettings(); });
        ok.Primary = true;
        var cancel = AddButton("Отмена", 580, 510, 120, null);
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = ok;
        CancelButton = cancel;
    }

    static Label LabelAt(string text, int left, int top)
    {
        var label = new Label();
        label.Text = text;
        label.AutoSize = false;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.SetBounds(left, top, 200, 26);
        return label;
    }

    void SetNumber(NumericUpDown box, int left, int top, int minimum, int maximum, int value)
    {
        box.SetBounds(left, top, 120, 26);
        box.Minimum = minimum;
        box.Maximum = maximum;
        box.Value = Math.Min(maximum, Math.Max(minimum, value));
        Controls.Add(box);
    }

    SoftButton AddButton(string text, int left, int top, int width, EventHandler click)
    {
        var button = new SoftButton();
        button.Text = text;
        button.SetBounds(left, top, width, 34);
        if (click != null)
            button.Click += click;
        Controls.Add(button);
        return button;
    }

    void ChooseColor(Panel swatch)
    {
        using (var dialog = new ColorDialog())
        {
            dialog.Color = swatch.BackColor;
            dialog.FullOpen = true;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                swatch.BackColor = dialog.Color;
        }
    }

    void ChooseImage()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Рисунки (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _image.Text = dialog.FileName;
        }
    }

    void AcceptSettings()
    {
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            MessageBox.Show(this, "Укажите название лаунчера.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Result = new LauncherSettings();
        Result.LauncherTitle = _title.Text.Trim();
        Result.Background = LauncherSettings.FormatColor(_color.BackColor);
        Result.FontColor = LauncherSettings.FormatColor(_font.BackColor);
        Result.BackgroundImage = string.IsNullOrWhiteSpace(_image.Text) ? null : _image.Text.Trim();
        Result.ShowSearch = _search.Checked ? "true" : "false";
        Result.HideTilePlate = _plate.Checked ? "true" : "false";
        Result.IconSize = (int)_icon.Value;
        Result.TileWidth = (int)_tileWidth.Value;
        Result.TileHeight = (int)_tileHeight.Value;
        Result.CaptionLength = (int)_caption.Value;
        Result.WindowWidth = (int)_windowWidth.Value;
        Result.WindowHeight = (int)_windowHeight.Value;
        DialogResult = DialogResult.OK;
    }
}

