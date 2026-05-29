using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace HyperPet.Windows.Notifications;

/// <summary>
/// Registers a Start Menu shortcut with a custom AppUserModelId so toast
/// notifications can appear under a chosen display name. Used by the debug
/// notification simulator to emit toasts as if from "HyperPet Debug Messenger".
/// </summary>
public static class AumiShortcutRegistrar
{
    /// <summary>
    /// Creates (if missing) a .lnk in the user's Start Menu with the given
    /// AppUserModelId and display name. Idempotent: if a shortcut with the same
    /// path already exists with the same AUMI, no work is done.
    /// </summary>
    /// <param name="aumi">The AppUserModelId, e.g. "HyperPet.Debug.Messenger".</param>
    /// <param name="displayName">User-visible app name in toasts, e.g. "HyperPet Debug Messenger".</param>
    /// <param name="targetPath">Optional path the shortcut launches. Defaults to the current process path.</param>
    public static void EnsureRegistered(string aumi, string displayName, string? targetPath = null, string? iconPath = null)
    {
        if (string.IsNullOrWhiteSpace(aumi) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("AUMI and display name must be provided.");
        }

        targetPath ??= Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve target path.");
        // The shortcut icon defaults to the app exe, which embeds HyperPet.ico
        // (set via <ApplicationIcon>). This is what shows next to the app in
        // Windows notification settings and the Start menu.
        iconPath ??= targetPath;

        string startMenuPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs");
        Directory.CreateDirectory(startMenuPrograms);

        string shortcutPath = Path.Combine(startMenuPrograms, displayName + ".lnk");

        // Re-create when the shortcut is missing, its AUMI differs, or its icon
        // is not already pointing at our icon (older shortcuts had no icon set).
        if (File.Exists(shortcutPath)
            && GetAumi(shortcutPath) == aumi
            && string.Equals(GetIconLocation(shortcutPath), iconPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CreateShortcut(shortcutPath, aumi, targetPath, iconPath);
    }

    private static void CreateShortcut(string path, string aumi, string targetPath, string iconPath)
    {
        var shellLinkClass = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))
            ?? throw new InvalidOperationException("ShellLink COM class missing.");
        object instance = Activator.CreateInstance(shellLinkClass)
            ?? throw new InvalidOperationException("Could not create ShellLink instance.");

        try
        {
            var link = (IShellLinkW)instance;
            link.SetPath(targetPath);
            link.SetArguments(string.Empty);
            link.SetIconLocation(iconPath, 0);

            var store = (IPropertyStore)instance;
            var key = PropertyKeys.AppUserModelId;
            var pv = new PROPVARIANT();
            try
            {
                pv.SetString(aumi);
                int hr = store.SetValue(ref key, ref pv);
                if (hr != 0)
                {
                    throw Marshal.GetExceptionForHR(hr) ?? new InvalidOperationException($"SetValue HRESULT {hr:X8}");
                }

                int commitHr = store.Commit();
                if (commitHr != 0)
                {
                    throw Marshal.GetExceptionForHR(commitHr) ?? new InvalidOperationException($"Commit HRESULT {commitHr:X8}");
                }
            }
            finally
            {
                pv.Clear();
            }

            var persist = (IPersistFile)instance;
            persist.Save(path, fRemember: true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }

    private static string? GetAumi(string shortcutPath)
    {
        var shellLinkClass = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (shellLinkClass is null)
        {
            return null;
        }

        object? instance = Activator.CreateInstance(shellLinkClass);
        if (instance is null)
        {
            return null;
        }

        try
        {
            var persist = (IPersistFile)instance;
            persist.Load(shortcutPath, 0);

            var store = (IPropertyStore)instance;
            var key = PropertyKeys.AppUserModelId;
            var pv = new PROPVARIANT();
            try
            {
                int hr = store.GetValue(ref key, out pv);
                if (hr != 0)
                {
                    return null;
                }

                return pv.GetString();
            }
            finally
            {
                pv.Clear();
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }

    private static string? GetIconLocation(string shortcutPath)
    {
        var shellLinkClass = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (shellLinkClass is null)
        {
            return null;
        }

        object? instance = Activator.CreateInstance(shellLinkClass);
        if (instance is null)
        {
            return null;
        }

        try
        {
            var persist = (IPersistFile)instance;
            persist.Load(shortcutPath, 0);

            var link = (IShellLinkW)instance;
            var sb = new StringBuilder(260);
            link.GetIconLocation(sb, sb.Capacity, out _);
            return sb.ToString();
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }

    private static class PropertyKeys
    {
        // PKEY_AppUserModel_ID: {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3} 5
        public static PROPERTYKEY AppUserModelId = new()
        {
            fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            pid = 5
        };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;

        private const ushort VT_EMPTY = 0;
        private const ushort VT_LPWSTR = 31;

        public void SetString(string value)
        {
            Clear();
            vt = VT_LPWSTR;
            p = Marshal.StringToCoTaskMemUni(value);
        }

        public string? GetString()
        {
            if (vt != VT_LPWSTR || p == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(p);
        }

        public void Clear()
        {
            PropVariantClear(ref this);
            vt = VT_EMPTY;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }
}
