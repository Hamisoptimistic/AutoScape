using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MetroHub.Core.Models;
using MetroHub.Core.Services;

namespace MetroHub.Presentation.Controls;

public partial class CategoryControl : UserControl
{
    public static readonly RoutedEvent TileActivatedEvent = EventManager.RegisterRoutedEvent(
        nameof(TileActivated), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CategoryControl));

    public static readonly RoutedEvent LayoutChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(LayoutChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CategoryControl));

    public event RoutedEventHandler TileActivated
    {
        add => AddHandler(TileActivatedEvent, value);
        remove => RemoveHandler(TileActivatedEvent, value);
    }

    public event RoutedEventHandler LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    public CategoryControl()
    {
        InitializeComponent();
    }

    private void OnHeaderKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
            RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
        }
    }

    private void OnHeaderLostFocus(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
    }

    private void OnAddTileClick(object sender, RoutedEventArgs e)
    {
        PromptAddTile();
    }

    private void OnGhostTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            PromptAddTile();
        }
    }

    private void OnGhostTileMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            b.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        }
    }

    private void OnGhostTileMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
            b.Background = new SolidColorBrush(Color.FromArgb(13, 255, 255, 255));
        }
    }

    private void PromptAddTile()
    {
        if (MainWindow.Current != null) MainWindow.Current.IsDialogOpen = true;
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Application or Shortcut to Pin",
                Filter = "Executables & Shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (string file in dialog.FileNames)
                {
                    AddFileAsTile(file);
                }
            }
        }
        finally
        {
            if (MainWindow.Current != null) MainWindow.Current.IsDialogOpen = false;
        }
    }

    public void AddFileAsTile(string filePath)
    {
        if (DataContext is TileGroup group && (File.Exists(filePath) || Directory.Exists(filePath)))
        {
            string title = Path.GetFileNameWithoutExtension(filePath);
            string? iconPath = IconExtractorService.ExtractAndCacheIcon(filePath);

            var tile = new TileModel
            {
                Title = title,
                TargetPath = filePath,
                IconPath = iconPath,
                TileType = TileType.App,
                SpanX = 2,
                SpanY = 2,
                Group = group.Name
            };

            group.Tiles.Add(tile);
            RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDropFile(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                foreach (string file in files)
                {
                    AddFileAsTile(file);
                }
                e.Handled = true;
            }
        }
    }

    private void OnTileActivated(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(TileActivatedEvent, e.OriginalSource));
    }

    private void OnTileUnpinned(object sender, RoutedEventArgs e)
    {
        if (DataContext is TileGroup group && e.OriginalSource is TileModel tile)
        {
            group.Tiles.Remove(tile);
            RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
        }
    }
}
