using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Xcmd.Infrastructure.ShellMenuNative;

namespace Xcmd.Infrastructure;

public sealed class WindowsShellInteraction : IShellInteraction
{
    private IContextMenu? activeMenu;
    private const uint AddToZipCommand = 0x8000;

    public ShellMenuAction ShowContextMenu(string[] paths, nint owner, int x, int y, bool extended)
    {
        RequireOwner(owner);
        using var context = new ShellContext(paths, owner, extended);
        activeMenu = context.Menu;
        try
        {
            var command = TrackPopupMenuEx(context.Handle, 0x0102, x, y, owner, 0);
            if (command == AddToZipCommand) return ShellMenuAction.AddToZip;
            if (command != 0) context.Invoke(command - 1, owner);
            return ShellMenuAction.None;
        }
        catch (COMException error) { throw new IOException("Windows could not execute the menu command.", error); }
        finally { activeMenu = null; }
    }

    public void ShowProperties(string path, nint owner)
    {
        RequireOwner(owner);
        var info = new ExecuteInfo { Size = Marshal.SizeOf<ExecuteInfo>(), Mask = 0x10C, Owner = owner, Verb = "properties", File = path, Show = 1 };
        if (!ShellExecuteExW(ref info)) throw new IOException("Windows could not open properties: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    public bool HandleMessage(uint message, nint wParam, nint lParam, out nint result)
    {
        result = 0;
        if (message is not (0x117 or 0x2B or 0x2C or 0x120)) return false;
        if (activeMenu is IContextMenu3 third) return third.HandleMenuMsg2(message, wParam, lParam, out result) == 0;
        return message != 0x120 && activeMenu is IContextMenu2 second && second.HandleMenuMsg(message, wParam, lParam) == 0;
    }

    public static ShellMenuInspection InspectMenu(string path, bool asynchronousState = true)
        => InspectMenu([path], asynchronousState);

    public static ShellMenuInspection InspectMenu(string[] paths, bool asynchronousState = true)
    {
        using var context = new ShellContext(paths, 0, false, asynchronousState);
        var verbs = new List<string>();
        for (uint index = 0; index < context.Count; index++)
        {
            var text = new StringBuilder(256);
            if (context.Menu.GetCommandString(index, 4, 0, text, 256) == 0 && text.Length > 0) verbs.Add(text.ToString());
        }
        var applicationCommand = new StringBuilder(256);
        GetMenuStringW(context.Handle, AddToZipCommand, applicationCommand, applicationCommand.Capacity, 0);
        return new(verbs, context.ParseMilliseconds, context.BindMilliseconds, context.ObjectMilliseconds, context.PopulateMilliseconds)
        { ApplicationCommands = [applicationCommand.ToString()] };
    }

    private static void RequireOwner(nint owner)
    {
        if (owner == 0) throw new IOException("A native Windows window is required for this action.");
    }

    private sealed class ShellContext : IDisposable
    {
        public IContextMenu Menu { get; private set; } = null!;
        public nint Handle { get; private set; }
        public uint Count { get; private set; }
        public double ParseMilliseconds { get; }
        public double BindMilliseconds { get; }
        public double ObjectMilliseconds { get; }
        public double PopulateMilliseconds { get; }

        public ShellContext(string[] paths, nint owner, bool extended, bool asynchronousState = true)
        {
            if (paths.Length == 0 || paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                throw new IOException("Windows group menus require items from the same folder.");
            var pidls = new List<nint>();
            IShellFolder? parent = null;
            try
            {
                var watch = Stopwatch.StartNew();
                foreach (var path in paths)
                {
                    Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var pidl, 0, out _));
                    pidls.Add(pidl);
                }
                ParseMilliseconds = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                var folderId = typeof(IShellFolder).GUID;
                var children = new List<nint>();
                foreach (var pidl in pidls)
                {
                    Marshal.ThrowExceptionForHR(SHBindToParent(pidl, in folderId, out var folder, out var child));
                    if (parent == null) parent = folder;
                    else if (OperatingSystem.IsWindows()) Marshal.ReleaseComObject(folder);
                    children.Add(child);
                }
                BindMilliseconds = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                var menuId = typeof(IContextMenu).GUID;
                Marshal.ThrowExceptionForHR(parent!.GetUIObjectOf(owner, (uint)children.Count, children.ToArray(), in menuId, 0, out var menu));
                Menu = menu;
                ObjectMilliseconds = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                Handle = CreatePopupMenu();
                if (Handle == 0) throw new IOException("Windows could not create the context menu.");
                // CMF_ASYNCVERBSTATE lets Explorer handlers defer expensive command-state
                // checks instead of blocking the UI before the menu can be displayed.
                // CMF_ITEMMENU identifies the clicked item rather than a folder background.
                var flags = (extended ? 0x100u : 0) | (asynchronousState ? 0x480u : 0);
                var count = Menu.QueryContextMenu(Handle, 0, 1, 0x7FFF, flags);
                Marshal.ThrowExceptionForHR(count);
                PopulateMilliseconds = watch.Elapsed.TotalMilliseconds;
                Count = (uint)(count & 0xFFFF);
                if (!AppendMenuW(Handle, 0x800, 0, null) || !AppendMenuW(Handle, 0, AddToZipCommand, "Add to &ZIP…"))
                    throw new IOException("Windows could not add the ZIP command to the context menu.");
            }
            catch (Exception error) when (error is COMException or IOException)
            { Dispose(); throw new IOException("Windows could not load the context menu for '" + paths[0] + "'.", error); }
            finally
            {
                if (parent != null && OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(parent);
                foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl);
            }
        }

        public void Invoke(uint command, nint owner)
        {
            var info = new InvokeInfo { Size = Marshal.SizeOf<InvokeInfo>(), Owner = owner, Verb = (nint)command, Show = 1 };
            Marshal.ThrowExceptionForHR(Menu.InvokeCommand(in info));
        }

        public void Dispose()
        {
            if (Handle != 0) { DestroyMenu(Handle); Handle = 0; }
            if (Menu != null && OperatingSystem.IsWindows()) { Marshal.FinalReleaseComObject(Menu); Menu = null!; }
        }
    }
}

