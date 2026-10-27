using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Serialization;

public class AppItem
{
    [XmlElement("name")]
    public string Name { get; set; }

    [XmlElement("path")]
    public string Path { get; set; }

    [XmlElement("args")]
    public string Args { get; set; }

    [XmlElement("group")]
    public string Group { get; set; }

    [XmlElement("icon")]
    public string Icon { get; set; }

    [XmlElement("separator")]
    public bool Separator { get; set; }

    [XmlElement("captionSize")]
    public int CaptionSize { get; set; }

    [XmlElement("fontColor")]
    public string FontColor { get; set; }

    public bool ShouldSerializeSeparator()
    {
        return Separator;
    }

    public bool ShouldSerializeCaptionSize()
    {
        return Separator && CaptionSize > 0;
    }

    public bool ShouldSerializeFontColor()
    {
        return Separator && !string.IsNullOrWhiteSpace(FontColor);
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name.Trim();
            if (Separator)
                return "Раздел";
            return Path ?? "";
        }
    }

    public int ResolvedCaptionSize()
    {
        if (CaptionSize < 8 || CaptionSize > 72)
            return 12;
        return CaptionSize;
    }
}

public static class TileLayout
{
    public const int Columns = 5;
    public const int TileWidth = 104;
    public const int TileHeight = 112;
    public const int Gap = 12;

    public static Point Place(int index)
    {
        int column = index % Columns;
        int row = index / Columns;
        return new Point(Gap + column * (TileWidth + Gap), Gap + row * (TileHeight + Gap));
    }

    public static Size ContentSize(int count)
    {
        int rows = count <= 0 ? 0 : (count + Columns - 1) / Columns;
        return new Size(Gap + Columns * (TileWidth + Gap), Gap + rows * (TileHeight + Gap));
    }
}

public static class AppIcons
{
    public static Bitmap Load(string iconPath, string filePath)
    {
        var chosen = TryLoad(iconPath);
        if (chosen != null)
            return chosen;
        var associated = TryAssociated(filePath);
        if (associated != null)
            return associated;
        return SystemIcons.Application.ToBitmap();
    }

    static Bitmap TryLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var extension = Path.GetExtension(path);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            return LoadShortcut(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            return LoadModuleIcon(path, 0);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            return TryAssociated(path);
        if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return LoadIco(path);
        try
        {
            using (var image = Image.FromFile(path))
                return new Bitmap(image);
        }
        catch (Exception)
        {
            return null;
        }
    }

    static Bitmap LoadIco(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 22 || BitConverter.ToUInt16(bytes, 2) != 1)
                return null;
            int count = BitConverter.ToUInt16(bytes, 4);
            int best = -1;
            int bestDistance = int.MaxValue;
            int bestSize = -1;
            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                if (entry + 16 > bytes.Length)
                    break;
                int size = IconEdge(bytes[entry]);
                int offset = BitConverter.ToInt32(bytes, entry + 12);
                int length = BitConverter.ToInt32(bytes, entry + 8);
                if (length <= 8 || offset < 0 || offset + length > bytes.Length)
                    continue;
                int distance = Math.Abs(size - 48);
                if (distance < bestDistance || (distance == bestDistance && size > bestSize))
                {
                    bestDistance = distance;
                    bestSize = size;
                    best = i;
                }
            }
            if (best < 0)
                return null;
            int chosen = 6 + best * 16;
            int dataOffset = BitConverter.ToInt32(bytes, chosen + 12);
            int dataLength = BitConverter.ToInt32(bytes, chosen + 8);
            if (IsPng(bytes, dataOffset))
                return CopyPng(bytes, dataOffset, dataLength);
            return CopyBmpIcon(bytes, chosen, dataOffset, dataLength);
        }
        catch (Exception)
        {
            return null;
        }
    }

    static int IconEdge(byte value)
    {
        return value == 0 ? 256 : value;
    }

    static bool IsPng(byte[] bytes, int offset)
    {
        return offset + 8 <= bytes.Length
            && bytes[offset] == 137 && bytes[offset + 1] == 80
            && bytes[offset + 2] == 78 && bytes[offset + 3] == 71;
    }

    static Bitmap CopyPng(byte[] bytes, int offset, int length)
    {
        using (var stream = new MemoryStream(bytes, offset, length, false))
        using (var image = Image.FromStream(stream))
            return new Bitmap(image);
    }

    static Bitmap CopyBmpIcon(byte[] bytes, int entry, int offset, int length)
    {
        var packed = new byte[22 + length];
        packed[2] = 1;
        packed[4] = 1;
        Buffer.BlockCopy(bytes, entry, packed, 6, 16);
        Buffer.BlockCopy(BitConverter.GetBytes(22), 0, packed, 18, 4);
        Buffer.BlockCopy(bytes, offset, packed, 22, length);
        using (var stream = new MemoryStream(packed))
        using (var icon = new Icon(stream))
            return icon.ToBitmap();
    }

    static Bitmap TryAssociated(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var extension = Path.GetExtension(path);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            return LoadShortcut(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            return LoadModuleIcon(path, 0);
        try
        {
            using (var icon = Icon.ExtractAssociatedIcon(path))
            {
                if (icon == null)
                    return null;
                return icon.ToBitmap();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    static Bitmap LoadShortcut(string path)
    {
        string target;
        string iconFile;
        int iconIndex;
        if (!ReadShortcut(path, out target, out iconFile, out iconIndex))
            return null;
        if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
        {
            var fromIcon = LoadModuleIcon(iconFile, iconIndex);
            if (fromIcon != null)
                return fromIcon;
            var loaded = TryLoad(iconFile);
            if (loaded != null)
                return loaded;
        }
        if (!string.IsNullOrWhiteSpace(target))
            return LoadModuleIcon(target, 0);
        return null;
    }

    static bool ReadShortcut(string path, out string target, out string iconFile, out int iconIndex)
    {
        target = null;
        iconFile = null;
        iconIndex = 0;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return false;
            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            var shortcutType = shortcut.GetType();
            target = shortcutType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
            var location = shortcutType.InvokeMember("IconLocation", BindingFlags.GetProperty, null, shortcut, null) as string;
            SplitIconLocation(location, out iconFile, out iconIndex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void SplitIconLocation(string location, out string file, out int index)
    {
        file = null;
        index = 0;
        if (string.IsNullOrWhiteSpace(location))
            return;
        int comma = location.LastIndexOf(',');
        if (comma > 0)
        {
            int parsed;
            if (int.TryParse(location.Substring(comma + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                file = location.Substring(0, comma).Trim();
                index = parsed;
                return;
            }
        }
        file = location.Trim();
    }

    static Bitmap LoadModuleIcon(string path, int index)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return LoadIco(path);
        IntPtr large;
        IntPtr small;
        SHDefExtractIcon(path, index, 0, out large, out small, (uint)(48 | (32 << 16)));
        var bitmap = CopyIcon(large);
        if (bitmap == null)
            bitmap = CopyIcon(small);
        else if (small != IntPtr.Zero)
            DestroyIcon(small);
        if (bitmap != null)
            return bitmap;
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return null;
        return TryAssociatedLocal(path);
    }

    static Bitmap CopyIcon(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            using (var temp = Icon.FromHandle(handle))
            using (var clone = (Icon)temp.Clone())
                return clone.ToBitmap();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    static Bitmap TryAssociatedLocal(string path)
    {
        try
        {
            using (var icon = Icon.ExtractAssociatedIcon(path))
            {
                if (icon == null)
                    return null;
                return icon.ToBitmap();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr hIcon);
}

public class LauncherSettings
{
    [XmlElement("launcherTitle")]
    public string LauncherTitle { get; set; }

    [XmlElement("background")]
    public string Background { get; set; }

    [XmlElement("fontColor")]
    public string FontColor { get; set; }

    [XmlElement("backgroundImage")]
    public string BackgroundImage { get; set; }

    [XmlElement("showSearch")]
    public string ShowSearch { get; set; }

    [XmlElement("hideTilePlate")]
    public string HideTilePlate { get; set; }

    [XmlElement("iconSize")]
    public int IconSize { get; set; }

    [XmlElement("tileWidth")]
    public int TileWidth { get; set; }

    [XmlElement("tileHeight")]
    public int TileHeight { get; set; }

    [XmlElement("captionLength")]
    public int CaptionLength { get; set; }

    [XmlElement("windowWidth")]
    public int WindowWidth { get; set; }

    [XmlElement("windowHeight")]
    public int WindowHeight { get; set; }

    public bool SearchVisible
    {
        get { return !string.Equals(ShowSearch, "false", StringComparison.OrdinalIgnoreCase); }
    }

    public bool TilePlateHidden
    {
        get { return string.Equals(HideTilePlate, "true", StringComparison.OrdinalIgnoreCase); }
    }

    public static LauncherSettings Normalize(LauncherSettings source)
    {
        var settings = source ?? new LauncherSettings();
        if (string.IsNullOrWhiteSpace(settings.LauncherTitle))
            settings.LauncherTitle = "TALK — Корпоративные приложения";
        if (string.IsNullOrWhiteSpace(settings.Background))
            settings.Background = "#F5F5F7";
        if (string.IsNullOrWhiteSpace(settings.FontColor))
            settings.FontColor = "#1D2225";
        if (settings.IconSize < 24 || settings.IconSize > 128)
            settings.IconSize = 48;
        if (settings.TileWidth < 80 || settings.TileWidth > 480)
            settings.TileWidth = 112;
        if (settings.TileHeight < 80 || settings.TileHeight > 480)
            settings.TileHeight = 98;
        if (settings.CaptionLength < 4 || settings.CaptionLength > 80)
            settings.CaptionLength = 24;
        if (settings.WindowWidth < 640 || settings.WindowWidth > 3840)
            settings.WindowWidth = 840;
        if (settings.WindowHeight < 420 || settings.WindowHeight > 2160)
            settings.WindowHeight = 500;
        if (string.IsNullOrWhiteSpace(settings.ShowSearch))
            settings.ShowSearch = "true";
        return settings;
    }

    public static Color ParseColor(string text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        var hex = text.Trim();
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        int rgb;
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
            return fallback;
        return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }

    public static string FormatColor(Color color)
    {
        return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
    }
}

[XmlRoot("apps")]
public class AppCatalog
{
    [XmlElement("configuratorGroup")]
    public string ConfiguratorGroup { get; set; }

    [XmlElement("settings")]
    public LauncherSettings Settings { get; set; }

    [XmlElement("app")]
    public List<AppItem> Items { get; set; }

    public AppCatalog()
    {
        Items = new List<AppItem>();
    }
}

public static class CatalogStore
{
    public static string ConfigDirectory(string baseDirectory)
    {
        var full = Path.GetFullPath(baseDirectory ?? ".");
        var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name.Equals("config", StringComparison.OrdinalIgnoreCase))
            return full;
        return Path.Combine(full, "config");
    }

    public static string DefaultPath()
    {
        return Path.Combine(ConfigDirectory(AppDomain.CurrentDomain.BaseDirectory), "apps.config");
    }

    public static void EnsureLocation()
    {
        var directory = ConfigDirectory(AppDomain.CurrentDomain.BaseDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "apps.config");
        var parent = Directory.GetParent(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parent == null || File.Exists(path))
            return;
        var legacy = Path.Combine(parent.FullName, "apps.config");
        if (!File.Exists(legacy) || string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            return;
        File.Move(legacy, path);
    }

    public static AppCatalog Load(string path)
    {
        var catalog = new AppCatalog();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return catalog;
        var serializer = new XmlSerializer(typeof(AppCatalog));
        using (var stream = File.OpenRead(path))
        {
            var loaded = (AppCatalog)serializer.Deserialize(stream);
            if (loaded == null)
                return catalog;
            catalog.ConfiguratorGroup = loaded.ConfiguratorGroup;
            catalog.Settings = loaded.Settings;
            if (loaded.Items != null)
                catalog.Items.AddRange(loaded.Items);
        }
        return catalog;
    }

    public static void Save(string path, AppCatalog catalog)
    {
        var serializer = new XmlSerializer(typeof(AppCatalog));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary)) serializer.Serialize(stream, catalog);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class Access
{
    const int TokenElevationType = 18;
    const int TokenLinkedToken = 19;
    const int TokenElevationTypeLimited = 3;

    public static bool IsMember(string group)
    {
        using (var id = OpenFullIdentity())
            return InRole(id, group);
    }

    public static bool CanLaunch(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return true;
        return IsMember(group);
    }

    public static bool CanConfigure(string group)
    {
        using (var id = OpenFullIdentity())
        {
            if (string.IsNullOrWhiteSpace(group))
                return IsAdmin(id);
            return InRole(id, group);
        }
    }

    public static List<AppItem> Visible(IEnumerable<AppItem> items)
    {
        var raw = new List<AppItem>();
        if (items == null)
            return raw;
        using (var id = OpenFullIdentity())
        {
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                if (item.Separator)
                {
                    raw.Add(item);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Group) || InRole(id, item.Group))
                    raw.Add(item);
            }
        }
        var list = new List<AppItem>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (!raw[i].Separator)
            {
                list.Add(raw[i]);
                continue;
            }
            bool hasApps = false;
            for (int j = i + 1; j < raw.Count; j++)
            {
                if (raw[j].Separator)
                    break;
                hasApps = true;
                break;
            }
            if (hasApps)
                list.Add(raw[i]);
        }
        return list;
    }

    static bool InRole(WindowsIdentity id, string group)
    {
        if (id == null || string.IsNullOrWhiteSpace(group))
            return false;
        group = group.Trim();
        try
        {
            var principal = new WindowsPrincipal(id);
            if (group.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
                return principal.IsInRole(new SecurityIdentifier(group));
            return principal.IsInRole(group);
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    static bool IsAdmin(WindowsIdentity id)
    {
        if (id == null)
            return false;
        var principal = new WindowsPrincipal(id);
        if (principal.IsInRole(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)))
            return true;
        if (id.User == null)
            return false;
        var domain = id.User.AccountDomainSid;
        if (domain == null)
            return false;
        return principal.IsInRole(new SecurityIdentifier(WellKnownSidType.AccountDomainAdminsSid, domain));
    }

    static WindowsIdentity OpenFullIdentity()
    {
        var current = WindowsIdentity.GetCurrent();
        int elev;
        int needed;
        if (!GetTokenInformation(current.Token, TokenElevationType, out elev, 4, out needed)
            || elev != TokenElevationTypeLimited)
            return current;

        IntPtr linked;
        if (!GetTokenInformation(current.Token, TokenLinkedToken, out linked, IntPtr.Size, out needed)
            || linked == IntPtr.Zero)
            return current;

        try
        {
            return new WindowsIdentity(linked);
        }
        finally
        {
            CloseHandle(linked);
            current.Dispose();
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(
        IntPtr token,
        int informationClass,
        out int information,
        int length,
        out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(
        IntPtr token,
        int informationClass,
        out IntPtr information,
        int length,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);
}
