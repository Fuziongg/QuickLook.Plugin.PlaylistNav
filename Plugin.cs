// Copyright © 2026 Fuziongg
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace QuickLook.Plugin.PlaylistNav;

public sealed class Plugin : IViewer
{
    private static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aif", ".aiff",
        ".ape", ".alac", ".mka", ".mid", ".midi", ".wv", ".ac3", ".dts",
    };

    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".flv", ".m4v", ".mpg", ".mpeg",
        ".ts", ".mts", ".m2ts", ".3gp",
    };

    private IViewer _inner;
    private string _path;

    // must be higher than the built-in VideoViewer (-3)
    public int Priority => -2;

    public void Init()
    {
    }

    private IViewer Inner
    {
        get
        {
            if (_inner != null) return _inner;
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "QuickLook.Plugin.VideoViewer")
                ?.GetType("QuickLook.Plugin.VideoViewer.Plugin");
            if (type != null) _inner = (IViewer)Activator.CreateInstance(type);
            return _inner;
        }
    }

    public bool CanHandle(string path)
    {
        try { return Inner?.CanHandle(path) ?? false; }
        catch { return false; }
    }

    public void Prepare(string path, ContextObject context) => Inner.Prepare(path, context);

    public void View(string path, ContextObject context)
    {
        _path = path;
        Inner.View(path, context);

        try { AddButtons(context.ViewerContent as FrameworkElement); }
        catch { }
    }

    public void Cleanup()
    {
        _path = null;
        Inner?.Cleanup();
    }

    private void AddButtons(FrameworkElement panel)
    {
        if (panel == null) return;
        var play = panel.GetType()
            .GetField("buttonPlayPause", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(panel) as Button;
        if (play?.Parent is not DockPanel bar) return;

        var style = panel.TryFindResource("ControlButtonStyle") as Style;

        var prev = MakeButton("", "Previous", style, -1);
        var next = MakeButton("", "Next", style, +1);

        var i = bar.Children.IndexOf(play);
        bar.Children.Insert(i + 1, next);
        bar.Children.Insert(i, prev);
    }

    private Button MakeButton(string glyph, string tip, Style style, int step)
    {
        var b = new Button { Content = glyph, ToolTip = tip, Focusable = false };
        if (style != null) b.Style = style;
        DockPanel.SetDock(b, Dock.Left);
        b.Click += (_, _) => Go(step);
        return b;
    }

    private void Go(int step)
    {
        var target = Neighbor(_path, step);
        if (target == null) return;

        // QuickLook disposes this plugin when switching, so wait for the click to finish
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try { SelectInExplorer(target); } catch { }

            var wm = Assembly.GetEntryAssembly()?.GetType("QuickLook.ViewWindowManager");
            var instance = wm?.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, null);
            wm?.GetMethod("InvokePreview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(instance, new object[] { target });
        }));
    }

    private static void SelectInExplorer(string path)
    {
        const int flags = 1 | 4 | 8 | 16; // select, deselect others, ensure visible, focus
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
        foreach (dynamic w in shell.Windows())
        {
            try
            {
                dynamic doc = w.Document;
                string folder = doc?.Folder?.Self?.Path;
                if (!string.Equals(folder?.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;

                dynamic item = doc.Folder.ParseName(name);
                if (item != null) doc.SelectItem(item, flags);
            }
            catch { }
        }
    }

    private static string Neighbor(string path, int step)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var dir = Path.GetDirectoryName(path);
        if (dir == null || !Directory.Exists(dir)) return null;

        var ext = Path.GetExtension(path);
        var kind = AudioExt.Contains(ext) ? AudioExt : VideoExt.Contains(ext) ? VideoExt : null;
        if (kind == null) return null;

        var files = Directory.GetFiles(dir)
            .Where(f => kind.Contains(Path.GetExtension(f)))
            .Where(f => (File.GetAttributes(f) & FileAttributes.Hidden) == 0)
            .ToList();
        files.Sort((a, b) => StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b)));

        var i = files.FindIndex(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        if (i < 0 || files.Count < 2) return null;

        return files[((i + step) % files.Count + files.Count) % files.Count];
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);
}
