using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// Native Unicode Shell links. No Windows Script Host, automation reflection,
// PowerShell, or command-shell dependency is involved in creating a shortcut.
internal static class ShellShortcut
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    internal static void Create(string file, string target, string arguments)
    {
        object instance = new ShellLinkObject();
        try
        {
            var link = (IShellLinkW)instance;
            link.SetPath(Path.GetFullPath(target));
            link.SetArguments(arguments ?? "");
            link.SetWorkingDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
            link.SetDescription("QuestBell"); link.SetShowCmd(1);
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) link.SetIconLocation(target, 0);
            ((IPersistFile)instance).Save(Path.GetFullPath(file), true);
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    internal static string ReadTarget(string file)
    {
        object instance = new ShellLinkObject();
        try
        {
            ((IPersistFile)instance).Load(Path.GetFullPath(file), 0);
            var buffer = new StringBuilder(32768);
            ((IShellLinkW)instance).GetPath(buffer, buffer.Capacity, IntPtr.Zero, 4);
            return buffer.ToString();
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
}
