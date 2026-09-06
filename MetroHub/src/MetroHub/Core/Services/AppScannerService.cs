using System.Collections.ObjectModel;
using System.IO;
using MetroHub.Core.Models;

namespace MetroHub.Core.Services;

public static class AppScannerService
{
    public static ObservableCollection<TileModel> GenerateStarterTemplate()
    {
        var tiles = new ObservableCollection<TileModel>();

        // File Explorer (Medium 2x2)
        tiles.Add(CreateAppTile("File Explorer", "explorer.exe", IconExtractorService.ExtractAndCacheIcon("explorer.exe"), 2, 2, "#0078D7", 40, 40));

        // Default Browser (Edge / Chrome)
        string browserPath = FindDefaultBrowser();
        tiles.Add(CreateAppTile("Browser", browserPath, IconExtractorService.ExtractAndCacheIcon(browserPath), 2, 2, "#0080FF", 170, 40));

        // Terminal / PowerShell
        string terminalPath = FindTerminal();
        tiles.Add(CreateAppTile("Terminal", terminalPath, IconExtractorService.ExtractAndCacheIcon(terminalPath), 2, 2, "#4E5664", 300, 40));

        // Task Manager
        string taskmgr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskmgr.exe");
        tiles.Add(CreateAppTile("Task Manager", taskmgr, IconExtractorService.ExtractAndCacheIcon(taskmgr), 2, 2, "#0063B1", 430, 40));

        // Windows Settings (ms-settings:)
        tiles.Add(new TileModel
        {
            Title = "Settings",
            TargetPath = "ms-settings:",
            TileType = TileType.App,
            SpanX = 2,
            SpanY = 2,
            AccentColor = "#69797E",
            IconPath = IconExtractorService.ExtractAndCacheIcon(taskmgr),
            X = 560,
            Y = 40
        });

        // Notepad (1x1)
        string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        tiles.Add(CreateAppTile("Notepad", notepad, IconExtractorService.ExtractAndCacheIcon(notepad), 1, 1, "#107C41", 690, 40));

        // Calculator (1x1)
        string calc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe");
        tiles.Add(CreateAppTile("Calculator", calc, IconExtractorService.ExtractAndCacheIcon(calc), 1, 1, "#008272", 690, 105));

        // Command Prompt (1x1)
        string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        tiles.Add(CreateAppTile("Command Prompt", cmd, IconExtractorService.ExtractAndCacheIcon(cmd), 1, 1, "#303030", 755, 40));

        return tiles;
    }

    private static TileModel CreateAppTile(string title, string path, string? iconPath, int spanX, int spanY, string? color, double x = 0, double y = 0)
    {
        return new TileModel
        {
            Title = title,
            TargetPath = path,
            IconPath = iconPath,
            TileType = TileType.App,
            SpanX = spanX,
            SpanY = spanY,
            AccentColor = color,
            X = x,
            Y = y
        };
    }

    private static string FindDefaultBrowser()
    {
        string edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (File.Exists(edge)) return edge;

        string chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (File.Exists(chrome)) return chrome;

        return "explorer.exe";
    }

    private static string FindTerminal()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string wt = Path.Combine(localAppData, @"Microsoft\WindowsApps\wt.exe");
        if (File.Exists(wt)) return wt;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
    }
}
