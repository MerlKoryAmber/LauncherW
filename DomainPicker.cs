using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;

public static class DomainGroupPicker
{
    const int ScopeUplevelJoined = 0x2;
    const int ScopeDownlevelJoined = 0x4;
    const int ScopeEnterprise = 0x8;
    const int ScopeCatalog = 0x10;
    const int ScopeExternalUplevel = 0x20;
    const int ScopeExternalDownlevel = 0x40;
    const int ScopeWorkgroup = 0x80;
    const int ScopeUserUplevel = 0x100;
    const int ScopeUserDownlevel = 0x200;
    const int ScopeStarting = 0x1;
    const int ScopeWinNt = 0x2;
    const int ScopeDefaultGroups = 0x80;
    const int FilterBuiltin = 0x4;
    const int FilterWellKnown = 0x8;
    const int FilterUniversalDl = 0x10;
    const int FilterUniversalSe = 0x20;
    const int FilterGlobalDl = 0x40;
    const int FilterGlobalSe = 0x80;
    const int FilterLocalDl = 0x100;
    const int FilterLocalSe = 0x200;
    const int DownlevelLocal = unchecked((int)0x80000002);
    const int DownlevelGlobal = unchecked((int)0x80000004);
    const int DownlevelWellKnown = unchecked((int)0x80020000);
    const int Cancelled = unchecked((int)0x800704C7);
    const int InvalidArgument = unchecked((int)0x80070057);

    public static int CheckLayout()
    {
        IDsObjectPicker picker;
        IntPtr scopes;
        IntPtr attributes;
        IntPtr name;
        string error;
        int hr = Prepare(out picker, out scopes, out attributes, out name, out error);
        Release(picker, scopes, attributes, name);
        return hr;
    }

    public static bool Pick(IntPtr parent, out string account, out string error)
    {
        account = null;
        error = null;
        IDsObjectPicker picker;
        IntPtr scopes;
        IntPtr attributes;
        IntPtr name;
        int hr = Prepare(out picker, out scopes, out attributes, out name, out error);
        Release(null, scopes, attributes, name);
        scopes = IntPtr.Zero;
        attributes = IntPtr.Zero;
        name = IntPtr.Zero;
        if (hr != 0)
        {
            Release(picker, scopes, attributes, name);
            if (hr == InvalidArgument)
                error = "Окно выбора группы не открылось.";
            return false;
        }
        IDataObject data = null;
        try
        {
            hr = picker.InvokeDialog(parent, out data);
            if (hr == Cancelled || hr == 1 || data == null)
                return false;
            if (hr != 0)
            {
                error = Marshal.GetExceptionForHR(hr).Message;
                return false;
            }
            account = ReadAccount(data);
            if (string.IsNullOrWhiteSpace(account))
            {
                error = "Группа не выбрана.";
                return false;
            }
            return true;
        }
        finally
        {
            if (data != null)
                Marshal.ReleaseComObject(data);
            Release(picker, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }
    }

    static int Prepare(out IDsObjectPicker picker, out IntPtr scopes, out IntPtr attributes, out IntPtr attributeName, out string error)
    {
        error = null;
        picker = null;
        scopes = IntPtr.Zero;
        attributes = IntPtr.Zero;
        attributeName = IntPtr.Zero;
        try
        {
            picker = (IDsObjectPicker)new DsObjectPicker();
            var scope = new DsopScopeInitInfo();
            scope.cbSize = Marshal.SizeOf(typeof(DsopScopeInitInfo));
            scope.flType = ScopeUplevelJoined | ScopeDownlevelJoined | ScopeEnterprise | ScopeCatalog
                | ScopeExternalUplevel | ScopeExternalDownlevel | ScopeWorkgroup | ScopeUserUplevel | ScopeUserDownlevel;
            scope.flScope = ScopeStarting | ScopeWinNt | ScopeDefaultGroups;
            scope.FilterFlags.Uplevel.flBothModes = FilterBuiltin | FilterWellKnown | FilterUniversalDl | FilterUniversalSe
                | FilterGlobalDl | FilterGlobalSe | FilterLocalDl | FilterLocalSe;
            scope.FilterFlags.flDownlevel = DownlevelLocal | DownlevelGlobal | DownlevelWellKnown;
            scopes = Marshal.AllocHGlobal(scope.cbSize);
            Marshal.StructureToPtr(scope, scopes, false);
            attributeName = Marshal.StringToHGlobalUni("objectSid");
            attributes = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(attributes, attributeName);
            var info = new DsopInitInfo();
            info.cbSize = Marshal.SizeOf(typeof(DsopInitInfo));
            info.cDsScopeInfos = 1;
            info.aDsScopeInfos = scopes;
            info.cAttributesToFetch = 1;
            info.apwzAttributeNames = attributes;
            int hr = picker.Initialize(ref info);
            if (hr != 0)
                error = Marshal.GetExceptionForHR(hr).Message;
            return hr;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return ex is COMException ? ((COMException)ex).ErrorCode : InvalidArgument;
        }
    }

    static void Release(IDsObjectPicker picker, IntPtr scopes, IntPtr attributes, IntPtr attributeName)
    {
        if (scopes != IntPtr.Zero)
            Marshal.FreeHGlobal(scopes);
        if (attributeName != IntPtr.Zero)
            Marshal.FreeHGlobal(attributeName);
        if (attributes != IntPtr.Zero)
            Marshal.FreeHGlobal(attributes);
        if (picker != null)
            Marshal.ReleaseComObject(picker);
    }

    static string ReadAccount(IDataObject data)
    {
        var format = new FORMATETC();
        format.cfFormat = (short)RegisterClipboardFormat("CFSTR_DSOP_DS_SELECTION_LIST");
        format.dwAspect = DVASPECT.DVASPECT_CONTENT;
        format.lindex = -1;
        format.tymed = TYMED.TYMED_HGLOBAL;
        STGMEDIUM medium;
        data.GetData(ref format, out medium);
        try
        {
            IntPtr list = GlobalLock(medium.unionmember);
            if (list == IntPtr.Zero)
                return null;
            try
            {
                int count = Marshal.ReadInt32(list);
                if (count < 1)
                    return null;
                var selection = (DsSelection)Marshal.PtrToStructure(new IntPtr(list.ToInt64() + 8), typeof(DsSelection));
                var sid = SidFromVariant(selection.pvarFetchedAttributes);
                if (sid != null)
                {
                    try { return sid.Translate(typeof(NTAccount)).ToString(); }
                    catch (IdentityNotMappedException) { }
                }
                var path = Marshal.PtrToStringUni(selection.pwzADsPath);
                var account = FromWinNt(path);
                if (!string.IsNullOrEmpty(account))
                    return account;
                return Marshal.PtrToStringUni(selection.pwzName);
            }
            finally { GlobalUnlock(medium.unionmember); }
        }
        finally
        {
            if (medium.pUnkForRelease == null && medium.unionmember != IntPtr.Zero)
                GlobalFree(medium.unionmember);
        }
    }

    static SecurityIdentifier SidFromVariant(IntPtr variant)
    {
        if (variant == IntPtr.Zero)
            return null;
        short kind = Marshal.ReadInt16(variant);
        if (kind != (short)(0x2000 | 17))
            return null;
        IntPtr array = Marshal.ReadIntPtr(variant, 8);
        if (array == IntPtr.Zero)
            return null;
        IntPtr data;
        if (SafeArrayAccessData(array, out data) != 0 || data == IntPtr.Zero)
            return null;
        try
        {
            int upper;
            if (SafeArrayGetUBound(array, 1, out upper) != 0 || upper < 0)
                return null;
            var bytes = new byte[upper + 1];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            return new SecurityIdentifier(bytes, 0);
        }
        finally { SafeArrayUnaccessData(array); }
    }

    static string FromWinNt(string path)
    {
        if (string.IsNullOrEmpty(path) || path.IndexOf("WinNT://", StringComparison.OrdinalIgnoreCase) != 0)
            return null;
        var rest = path.Substring(8).TrimEnd('/');
        int slash = rest.IndexOf('/');
        if (slash <= 0 || slash >= rest.Length - 1)
            return null;
        var domain = rest.Substring(0, slash);
        var name = rest.Substring(slash + 1);
        int comma = name.IndexOf(',');
        if (comma > 0)
            name = name.Substring(0, comma);
        return domain + "\\" + name;
    }

    [ComImport, Guid("17D6CCD8-3B7B-11D2-B9E0-00C04FD8DBF7")]
    class DsObjectPicker { }

    [ComImport, Guid("0C87E64E-3B7A-11D2-B9E0-00C04FD8DBF7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDsObjectPicker
    {
        [PreserveSig]
        int Initialize(ref DsopInitInfo init);
        [PreserveSig]
        int InvokeDialog(IntPtr parent, out IDataObject selections);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DsopInitInfo
    {
        public int cbSize;
        public string pwzTargetComputer;
        public int cDsScopeInfos;
        public IntPtr aDsScopeInfos;
        public int flOptions;
        public int cAttributesToFetch;
        public IntPtr apwzAttributeNames;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DsopScopeInitInfo
    {
        public int cbSize;
        public int flType;
        public int flScope;
        public DsopFilterFlags FilterFlags;
        public string pwzDcName;
        public string pwzADsPath;
        public int hr;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DsopFilterFlags
    {
        public DsopUpLevelFilterFlags Uplevel;
        public int flDownlevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DsopUpLevelFilterFlags
    {
        public int flBothModes;
        public int flMixedModeOnly;
        public int flNativeModeOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DsSelection
    {
        public IntPtr pwzName;
        public IntPtr pwzADsPath;
        public IntPtr pwzClass;
        public IntPtr pwzUPN;
        public IntPtr pvarFetchedAttributes;
        public int flScopeType;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalFree(IntPtr handle);

    [DllImport("oleaut32.dll")]
    static extern int SafeArrayAccessData(IntPtr array, out IntPtr data);

    [DllImport("oleaut32.dll")]
    static extern int SafeArrayUnaccessData(IntPtr array);

    [DllImport("oleaut32.dll")]
    static extern int SafeArrayGetUBound(IntPtr array, int dimension, out int bound);
}
