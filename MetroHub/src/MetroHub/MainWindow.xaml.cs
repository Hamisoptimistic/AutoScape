using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MetroHub.Core.Models;
using MetroHub.Core.Services;

namespace MetroHub;

public partial class MainWindow : Window
{
    public ObservableCollection<TileModel> Tiles { get; set; } = new();
    public AppSettings Settings { get; set; } = new();

    public static MainWindow? Current { get; private set; }
    public bool IsDialogOpen { get; set; } = false;

    private readonly HotkeyService _hotkeyService = new();
    private DispatcherTimer? _hudTimer;
    private bool _isClosingToExit = false;

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        DataContext = this;

        LoadData();
        SetupHudTimer();

        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        SizeChanged += (s, e) => { UpdateLayoutMetrics(); UpdateCanvasHeight(); };
    }

    private DateTime _lastShownTime = DateTime.MinValue;
    private bool _isFullyActivated = false;

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _isFullyActivated = true;
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Must be fully activated first, and ignore premature deactivation within 1.5s of opening
        if (!_isFullyActivated)
        {
            return;
        }

        if ((DateTime.UtcNow - _lastShownTime).TotalMilliseconds < 1500)
        {
            return;
        }

        if (!IsDialogOpen && IsVisible)
        {
            HideScreen();
        }
    }

    private void LoadData()
    {
        Settings = StorageService.LoadSettings();
        Tiles = StorageService.LoadLayout();

        UpdateLayoutMetrics();

        GridPlacementService.SanitizeAndSnapAll(Tiles, GridPlacementService.MaxCols);
        StorageService.SaveLayout(Tiles);
        TilesListBox.ItemsSource = Tiles;
        UpdateCanvasHeight();
    }

    private void UpdateLayoutMetrics()
    {
        double viewportWidth = ContentScrollViewer?.ActualWidth > 0 
            ? ContentScrollViewer.ActualWidth 
            : (Width > 0 ? Width : 1920);

        GridPlacementService.UpdateMetrics(viewportWidth);
        double margin = GridPlacementService.OriginX;

        if (HeaderGrid != null)
        {
            HeaderGrid.Margin = new Thickness(margin, 0, margin, 0);
        }
        if (FooterGrid != null)
        {
            FooterGrid.Margin = new Thickness(margin, 0, margin, 0);
        }
    }

    private void UpdateCanvasHeight()
    {
        if (MainCanvasGrid == null) return;
        double maxBottom = (Tiles != null && Tiles.Any()) ? Tiles.Max(t => t.Y + t.HeightPixels) + 120 : 600;
        double viewportHeight = ContentScrollViewer?.ActualHeight > 0 ? ContentScrollViewer.ActualHeight : 600;
        MainCanvasGrid.MinHeight = Math.Max(viewportHeight, maxBottom);
    }

    private void SetupHudTimer()
    {
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _hudTimer.Tick += (s, e) =>
        {
            if (IsVisible)
            {
                long bytes = Process.GetCurrentProcess().WorkingSet64;
                double mb = Math.Round(bytes / (1024.0 * 1024.0), 1);
                RamHudTextBlock.Text = $"RAM: {mb} MB";
            }
        };
        _hudTimer.Start();
    }

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        // Apply native Windows 11 hardware-accelerated Acrylic/Mica backdrop
        NativeMethods.ApplyMica(hwnd, dark: true, NativeMethods.DWMSBT_MAINWINDOW);

        // Crucial for Mica in WPF: set CompositionTarget background to Transparent
        var hwndSource = HwndSource.FromHwnd(hwnd);
        if (hwndSource?.CompositionTarget != null)
        {
            hwndSource.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }

        // Hook Win32 window messages for instant broadcast IPC wake
        hwndSource?.AddHook(WndProc);

        // Register the global hotkey
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.Register(hwnd, Settings);

        // Snap to active monitor work area (never cover the Windows Taskbar)
        SnapToWorkArea();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == NativeMethods.WM_SHOW_METROHUB)
        {
            Dispatcher.Invoke(ShowScreen);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void SnapToWorkArea()
    {
        Rect workArea = NativeMethods.GetActiveMonitorWorkArea();
        Left = workArea.Left;
        Top = workArea.Top;
        Width = workArea.Width;
        Height = workArea.Height;
        UpdateLayoutMetrics();
        UpdateCanvasHeight();
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(ToggleVisibility);
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            HideScreen();
        }
        else
        {
            ShowScreen();
        }
    }

    public void ShowScreen()
    {
        _lastShownTime = DateTime.UtcNow;
        _isFullyActivated = false;
        SnapToWorkArea();
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.ApplyMica(hwnd, dark: true, NativeMethods.DWMSBT_MAINWINDOW);
            NativeMethods.ForceForeground(hwnd);
        }

        Activate();
        Focus();
        UpdateLayoutMetrics();
    }

    public void HideScreen()
    {
        _isFullyActivated = false;
        Hide();
        // Immediately trim physical memory down to ~10 MB on hide
        NativeMethods.FlushMemory();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
        {
            HideScreen();
            e.Handled = true;
        }
    }

    private TileModel? _draggedTile;
    private Presentation.Controls.TileControl? _draggedControl;
    private ContentPresenter? _draggedContainer;
    private Point _dragStartPoint;
    private double _dragOffsetX;  // offset from mouse to tile's left edge
    private double _dragOffsetY;  // offset from mouse to tile's top edge
    private int _dragOriginalCol;
    private int _dragOriginalRow;
    private bool _isPotentialDrag;
    private bool _isDragging;

    private void OnCanvasPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        DependencyObject? dep = e.OriginalSource as DependencyObject;
        var tileControl = FindParent<Presentation.Controls.TileControl>(dep);

        if (tileControl != null && tileControl.DataContext is TileModel tile)
        {
            _draggedTile = tile;
            _draggedControl = tileControl;
            _draggedContainer = FindParent<ContentPresenter>(tileControl)
                ?? TilesListBox.ItemContainerGenerator.ContainerFromItem(tile) as ContentPresenter;
            _dragStartPoint = e.GetPosition(this);

            // Record exact pre-drag grid coordinates for collision/swap resolution
            _dragOriginalCol = GridPlacementService.ColFromPixel(tile.X);
            _dragOriginalRow = GridPlacementService.RowFromPixel(tile.Y);

            // Calculate offset from mouse position to the tile's current top-left corner
            // so the tile doesn't "jump" when dragging starts
            Point mouseOnCanvas = e.GetPosition(TilesListBox);
            _dragOffsetX = mouseOnCanvas.X - tile.X;
            _dragOffsetY = mouseOnCanvas.Y - tile.Y;

            _isPotentialDrag = true;
            _isDragging = false;
        }
        else
        {
            _isPotentialDrag = false;
            _isDragging = false;
        }
    }

    private void OnCanvasPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPotentialDrag && !_isDragging && e.LeftButton == MouseButtonState.Pressed && _draggedTile != null)
        {
            Point current = e.GetPosition(this);
            Vector diff = current - _dragStartPoint;
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                _isDragging = true;
                _draggedTile.IsBeingDragged = true;

                if (_draggedContainer != null)
                {
                    Panel.SetZIndex(_draggedContainer, 9999);
                }

                // Show DropSlotIndicator with matching tile dimensions
                DropSlotIndicator.Width = _draggedTile.WidthPixels;
                DropSlotIndicator.Height = _draggedTile.HeightPixels;
                DropSlotIndicator.Visibility = Visibility.Visible;

                RootGrid.CaptureMouse();
            }
        }

        if (_isDragging && _draggedTile != null)
        {
            Point mouseOnCanvas = e.GetPosition(TilesListBox);

            UpdateLayoutMetrics();
            int maxCols = GridPlacementService.MaxCols;
            int maxAllowedCol = Math.Max(0, maxCols - _draggedTile.SpanX);
            double maxAllowedX = GridPlacementService.PixelXFromCol(maxAllowedCol);

            // Calculate new tile position using the original click offset
            double newX = mouseOnCanvas.X - _dragOffsetX;
            double newY = mouseOnCanvas.Y - _dragOffsetY;

            // Strict boundary clamping: tiles NEVER go outside the window on the right or top/bottom
            newX = Math.Max(GridPlacementService.OriginX, Math.Min(newX, maxAllowedX));
            newY = Math.Max(GridPlacementService.OriginY, Math.Min(newY, 3000));

            // Fluid real-time drag position for the tile itself
            _draggedTile.X = newX;
            _draggedTile.Y = newY;

            if (_draggedContainer != null)
            {
                Canvas.SetLeft(_draggedContainer, newX);
                Canvas.SetTop(_draggedContainer, newY);
            }

            // Snapped target grid cell clamped within maxCols
            int targetCol = Math.Min(GridPlacementService.ColFromPixel(newX), maxAllowedCol);
            int targetRow = GridPlacementService.RowFromPixel(newY);

            double snappedX = GridPlacementService.PixelXFromCol(targetCol);
            double snappedY = GridPlacementService.PixelYFromRow(targetRow);

            Canvas.SetLeft(DropSlotIndicator, snappedX);
            Canvas.SetTop(DropSlotIndicator, snappedY);
        }
    }

    private void OnCanvasPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _isPotentialDrag = false;
            DropSlotIndicator.Visibility = Visibility.Collapsed;
            RootGrid.ReleaseMouseCapture();

            if (_draggedTile != null)
            {
                _draggedTile.IsBeingDragged = false;

                UpdateLayoutMetrics();
                int maxCols = GridPlacementService.MaxCols;
                int maxAllowedCol = Math.Max(0, maxCols - _draggedTile.SpanX);

                int targetCol = Math.Min(GridPlacementService.ColFromPixel(_draggedTile.X), maxAllowedCol);
                int targetRow = GridPlacementService.RowFromPixel(_draggedTile.Y);

                // Place tile and resolve swaps/displacements so tiles NEVER overlap
                var modifiedTiles = GridPlacementService.PlaceAndResolveCollisions(
                    _draggedTile, 
                    targetCol, 
                    targetRow, 
                    _dragOriginalCol, 
                    _dragOriginalRow, 
                    maxCols, 
                    Tiles);

                // Synchronize visual containers for all affected tiles
                foreach (var tile in modifiedTiles)
                {
                    var container = TilesListBox.ItemContainerGenerator.ContainerFromItem(tile) as ContentPresenter;
                    if (container != null)
                    {
                        Canvas.SetLeft(container, tile.X);
                        Canvas.SetTop(container, tile.Y);
                    }
                }

                UpdateCanvasHeight();
            }

            if (_draggedContainer != null)
            {
                Panel.SetZIndex(_draggedContainer, 0);
                _draggedContainer = null;
            }

            _draggedTile = null;
            _draggedControl = null;

            // Immediately persist the clean layout to layout.json
            StorageService.SaveLayout(Tiles);
            e.Handled = true;
            return;
        }

        if (_isPotentialDrag)
        {
            _isPotentialDrag = false;
            var controlToLaunch = _draggedControl;
            _draggedTile = null;
            _draggedControl = null;
            _draggedContainer = null;

            if (controlToLaunch != null)
            {
                controlToLaunch.LaunchTile();
                if (Settings.CloseOnLaunch)
                {
                    HideScreen();
                }
            }
            return;
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private bool IsInteractive(DependencyObject? obj)
    {
        while (obj != null && obj != this)
        {
            if (obj is Presentation.Controls.TileControl ||
                obj is Button ||
                obj is TextBox ||
                obj is ContextMenu ||
                obj is MenuItem ||
                (obj is Border b && b.ToolTip != null))
            {
                return true;
            }
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }

    private void OnTileActivated(object sender, RoutedEventArgs e)
    {
        if (Settings.CloseOnLaunch)
        {
            HideScreen();
        }
    }

    private void OnTileUnpinned(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TileModel tile)
        {
            Tiles.Remove(tile);
            StorageService.SaveLayout(Tiles);
        }
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
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            b.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        }
    }

    private void OnGhostTileMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(68, 255, 255, 255));
            b.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
        }
    }

    public void PromptAddTile()
    {
        IsDialogOpen = true;
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
            IsDialogOpen = false;
        }
    }

    public void AddFileAsTile(string filePath, double x = 0, double y = 0)
    {
        if (File.Exists(filePath) || Directory.Exists(filePath))
        {
            string title = Path.GetFileNameWithoutExtension(filePath);
            string? iconPath = IconExtractorService.ExtractAndCacheIcon(filePath);

            double viewportWidth = ContentScrollViewer?.ActualWidth > 0 
                ? ContentScrollViewer.ActualWidth 
                : (Width > 0 ? Width : 1920);
            int maxCols = GridPlacementService.GetMaxCols(viewportWidth);

            int col = x > 0 ? GridPlacementService.ColFromPixel(x) : 0;
            int row = y > 0 ? GridPlacementService.RowFromPixel(y) : 0;

            var (freeCol, freeRow) = GridPlacementService.FindNearestAvailableSlot(col, row, 2, 2, Tiles, null, maxCols);

            var tile = new TileModel
            {
                Title = title,
                TargetPath = filePath,
                IconPath = iconPath,
                TileType = TileType.App,
                SpanX = 2,
                SpanY = 2,
                X = GridPlacementService.PixelXFromCol(freeCol),
                Y = GridPlacementService.PixelYFromRow(freeRow)
            };

            Tiles.Add(tile);
            StorageService.SaveLayout(Tiles);
            UpdateCanvasHeight();
        }
    }

    private void OnExportLayoutClick(object sender, RoutedEventArgs e)
    {
        IsDialogOpen = true;
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Export MetroHub Layout",
                Filter = "JSON Files (*.json)|*.json",
                FileName = "MetroHub_Layout.json"
            };

            if (sfd.ShowDialog() == true)
            {
                if (StorageService.ExportLayout(Tiles, sfd.FileName))
                {
                    MessageBox.Show("Layout exported successfully!", "MetroHub", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    private void OnCloseToTrayClick(object sender, RoutedEventArgs e)
    {
        HideScreen();
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                Point pos = e.GetPosition(TilesListBox);
                double currentX = Math.Max(0, pos.X);
                double currentY = Math.Max(0, pos.Y);
                foreach (string file in files)
                {
                    AddFileAsTile(file, currentX, currentY);
                    currentX += 70; // offset slightly for multiple files
                    currentY += 70;
                }
                e.Handled = true;
            }
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingToExit)
        {
            e.Cancel = true;
            HideScreen();
        }
        else
        {
            _hotkeyService.Dispose();
            base.OnClosing(e);
        }
    }

    public void ExitApplication()
    {
        _isClosingToExit = true;
        _hotkeyService.Dispose();
        Close();
        Application.Current.Shutdown();
    }
}