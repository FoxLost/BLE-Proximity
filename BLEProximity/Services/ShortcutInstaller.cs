using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace BLEProximity.Services;

/// <summary>
/// Creates a Start Menu .lnk shortcut with AppUserModelID using IShellLink COM interop.
/// Required for toast notification display from non-packaged desktop apps.
/// </summary>
public class ShortcutInstaller : IShortcutInstaller
{
    /// <summary>
    /// The AppUserModelID used to identify this application for toast notifications.
    /// </summary>
    public const string AppUserModelId = "BLEProximity";

    private static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Windows\Start Menu\Programs\BLE Proximity.lnk");

    /// <summary>
    /// Verifies that the Start Menu shortcut exists with the correct AppUserModelID,
    /// and creates it if missing. Errors are logged and do not prevent application startup.
    /// </summary>
    public void EnsureShortcutExists()
    {
        try
        {
            if (File.Exists(ShortcutPath))
            {
                // Shortcut already exists; verify it has the correct AppUserModelID
                if (HasCorrectAppUserModelId(ShortcutPath))
                {
                    return;
                }

                // Remove the old shortcut and recreate with correct properties
                File.Delete(ShortcutPath);
            }

            CreateShortcut();
        }
        catch (Exception ex)
        {
            // Log and continue if shortcut creation fails - toast notifications
            // may not work but the app should still function
            Debug.WriteLine($"ShortcutInstaller: Failed to ensure shortcut exists: {ex.Message}");
        }
    }

    private static bool HasCorrectAppUserModelId(string shortcutPath)
    {
        try
        {
            var shellLink = (IShellLinkW)new CShellLink();
            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(shortcutPath, 0); // STGM_READ = 0

            var propertyStore = (IPropertyStore)shellLink;
            var appIdKey = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5); // System.AppUserModel.ID

            propertyStore.GetValue(ref appIdKey, out var propVariant);
            try
            {
                if (propVariant.VarType == VarEnum.VT_LPWSTR)
                {
                    var value = Marshal.PtrToStringUni(propVariant.Data);
                    return string.Equals(value, AppUserModelId, StringComparison.Ordinal);
                }
                return false;
            }
            finally
            {
                propVariant.Clear();
            }
        }
        catch
        {
            // If we can't read the shortcut properties, recreate it
            return false;
        }
    }

    private static void CreateShortcut()
    {
        var shellLink = (IShellLinkW)new CShellLink();

        // Set the target path to the current application executable
        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine application executable path.");

        shellLink.SetPath(exePath);
        shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);
        shellLink.SetDescription("BLE Proximity");

        // Set the AppUserModelID property
        var propertyStore = (IPropertyStore)shellLink;
        var appIdKey = new PropertyKey(
            new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5); // System.AppUserModel.ID

        var propVariant = PropVariant.FromString(AppUserModelId);
        try
        {
            propertyStore.SetValue(ref appIdKey, ref propVariant);
            propertyStore.Commit();
        }
        finally
        {
            propVariant.Clear();
        }

        // Ensure the directory exists
        var directory = Path.GetDirectoryName(ShortcutPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save the shortcut
        var persistFile = (IPersistFile)shellLink;
        persistFile.Save(ShortcutPath, true);
    }

    #region COM Interop Definitions

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile,
            int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath,
            int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariant pv);
        void SetValue(ref PropertyKey key, ref PropVariant propvar);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public VarEnum VarType;
        private ushort _wReserved1;
        private ushort _wReserved2;
        private ushort _wReserved3;
        public IntPtr Data;
        private IntPtr _dataExt;

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                VarType = VarEnum.VT_LPWSTR,
                Data = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public void Clear()
        {
            PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }

    #endregion
}
