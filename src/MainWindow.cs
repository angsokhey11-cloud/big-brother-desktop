using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace BBDesktop;

/// <summary>
/// BIG BROTHER Desktop workstation: locally installed accounting frontend
/// and separate persistent native browser profile. No privileged JS bridge.
/// </summary>
internal sealed class MainWindow : Window
{
    private const string DefaultWorkspaceUrl =
        "https://app.bigbrother.test/big-brother-dashboard/index.html";
    private const string TelegramUrl = "https://web.telegram.org/";

    private readonly WorkstationSettings _preferences = SettingsStore.Load();
    private readonly GlobalBrowserWorkspace _globalBrowser;
    private readonly WebView2 _workspaceView = new();
    private readonly WebView2 _browserView = new();
    private readonly Canvas _overlay = new();
    private readonly Border _browserPanel = new();
    private readonly Button _bubble = new();
    private readonly TextBox _browserAddress = new();
    private readonly StackPanel _shortcutButtons = new() { Orientation = Orientation.Horizontal };
    private readonly List<DispatcherTimer> _shortcutClickTimers = new();
    private readonly TextBlock _notice = new();
    private readonly TextBlock _workspaceStatus = new();
    private readonly Grid _body = new();
    private Window? _bubbleWindow;
    private Window? _browserWindow;
    private Window? _browserDialog;

    private bool _browserReady;
    private bool _browserStarting;
    private bool _panelOpen;
    private bool _invoiceScopeActive;

    // The embedded browser belongs to the Invoice Generator only. The browser
    // stays alive if the user switches accounting functions.
    private const int UpdateSystemMenuCommand = 0x1E00;
    private static string DesktopTitle =>
        "BIG BROTHER Desktop • v" +
        (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "development");
    private const int LocalUpdateMenuCommand = 0x1E10;
    private const int WmSysCommand = 0x0112;
    private const uint MfSeparator = 0x0800;
    private const uint MfString = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr window, bool revert);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, nuint identifier, string? label);
    private bool _dragging;
    private bool _resizing;
    private Border? _dragSurface;
    private Thumb? _resizeGrip;
    private Point _dragFrom;
    private Point _panelFrom;

    // CSS affects ONLY this Windows desktop shell, not the production website.
    private const string HideWebInvoiceBubbleScript = """
        (function(){
          function hideOldBubble(){
            if(!document.documentElement || document.getElementById('bbNativeShellHideBrowser')) return;
            var css=document.createElement('style');
            css.id='bbNativeShellHideBrowser';
            // Desktop-only brand override: restrict Dashboard button's hit
            // area to its visible text. Do not paint a background over the
            // nearby native Browser button.
            css.textContent =
              '#bbInvoiceBrowserBubble,#bbInvoiceBrowserPanel{display:none!important}' +
              '.brand{background:transparent!important;box-shadow:none!important}' +
              '.brand>.brand-home{display:inline-block!important;' +
              'width:max-content!important;max-width:calc(100% - 46px)!important;' +
              'padding:4px 0!important;margin:0!important;' +
              'border:0!important;background:transparent!important;' +
              'box-shadow:none!important}' +
              '.brand>.brand-home:hover,.brand>.brand-home:focus{' +
              'background:transparent!important;box-shadow:none!important}';
            (document.head || document.documentElement).appendChild(css);
          }
          hideOldBubble();
          document.addEventListener('DOMContentLoaded',hideOldBubble);
          setInterval(hideOldBubble,1800);
        })();
        """;

    // Runs only in the top-level Dashboard document. The dashboard already
    // identifies the active iframe with moduleFrame.dataset.module = "invoice".
    // The small observer reports iframe bounds but never touches accounting data.
    private const string InvoiceScopeScript = """
        (function () {
          if (window !== window.top || window.__bbDesktopInvoiceScopeV2) return;
          window.__bbDesktopInvoiceScopeV2 = true;
          let last = "", currentFrame = null, currentWorkspace = null;
          let observer = null;

          function report() {
            const frame = document.getElementById("moduleFrame");
            const workspace = document.getElementById("moduleWorkspace");
            const src = String(frame?.getAttribute("src") || "");
            const active = !!(frame && workspace && !workspace.hidden &&
              frame.dataset.module === "invoice" &&
              src.includes("/big-brother-invoice-generator/") &&
              !src.startsWith("about:"));
            const rect = active ? frame.getBoundingClientRect() : null;
            const visible = !!(rect && rect.width >= 180 && rect.height >= 180 &&
              rect.right > 0 && rect.bottom > 0 &&
              rect.left < innerWidth && rect.top < innerHeight);
            const payload = {
              type: "bbDesktopInvoiceScopeV2", visible,
              x: visible ? rect.left : 0,
              y: visible ? rect.top : 0,
              width: visible ? rect.width : 0,
              height: visible ? rect.height : 0,
              viewportWidth: innerWidth,
              viewportHeight: innerHeight
            };
            const value = JSON.stringify(payload);
            if (value === last) return;
            last = value;
            try { window.chrome?.webview?.postMessage(value); } catch (_) {}
          }

          function bind() {
            const frame = document.getElementById("moduleFrame");
            const workspace = document.getElementById("moduleWorkspace");
            if (frame !== currentFrame || workspace !== currentWorkspace) {
              observer?.disconnect();
              currentFrame = frame;
              currentWorkspace = workspace;
              observer = new MutationObserver(report);
              if (frame) observer.observe(frame, {
                attributes: true,
                attributeFilter: ["src", "data-module", "class", "style"]
              });
              if (workspace) observer.observe(workspace, {
                attributes: true,
                attributeFilter: ["hidden", "class", "style"]
              });
            }
            report();
          }

          window.addEventListener("resize", bind, {passive:true});
          window.addEventListener("scroll", report, true);
          document.addEventListener("DOMContentLoaded", bind);
          // Recovery for the dashboard loader's document.write and responsive
          // layout changes; this never polls APIs or the accounting database.
          setInterval(bind, 1200);
          bind();
        })();
        """;

    public MainWindow()
    {
        _globalBrowser = new GlobalBrowserWorkspace(this, _preferences);
        Title = DesktopTitle;
        Width = 1380;
        Height = 880;
        MinWidth = 840;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Paint("#edf2f9");
        FontFamily = new FontFamily("Segoe UI");
        // Explicit WPF taskbar/title-bar icon; EXE/installer have the same brand.
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/big-brother.ico", UriKind.Absolute));
        SourceInitialized += InstallNativeUpdateMenu;
        SourceInitialized += (_, _) => _globalBrowser.Initialize();
        Activated += (_, _) => Dispatcher.BeginInvoke(
            new Action(_globalBrowser.Refresh), DispatcherPriority.ContextIdle);
        Deactivated += (_, _) => Dispatcher.BeginInvoke(
            new Action(_globalBrowser.HideIfOtherAppActive), DispatcherPriority.ContextIdle);
        LocationChanged += (_, _) => _globalBrowser.Position();
        SizeChanged += (_, _) => _globalBrowser.Position();
        // Desktop must never retain a browser-panel pointer capture when the
        // user switches to Excel, Chrome, another program, or the taskbar.
        Deactivated += (_, _) => ReleaseBrowserMouseCapture();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                ReleaseBrowserMouseCapture();
                _globalBrowser.Hide();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(_globalBrowser.Refresh),
                    DispatcherPriority.ContextIdle);
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && (_dragging || _resizing))
            {
                ReleaseBrowserMouseCapture();
                e.Handled = true;
            }
        };

        BuildWindow();
        Loaded += async (_, _) =>
        {
            await InitializeWorkspaceAsync();
            // Give the installed Dashboard time to render first. The update
            // check is quiet when offline or already current, and never blocks
            // regular accounting work or downloads without a user decision.
            await Task.Delay(2200);
            await UpdateService.CheckForUpdatesAsync(this, ShowUpdateStatus,
                quietIfCurrent: true);
        };
        Closing += (_, _) =>
        {
            ReleaseBrowserMouseCapture();
            _globalBrowser.Hide();
            SettingsStore.Save(_preferences);
        };
        Closed += (_, _) =>
        {
            // The user data directories are not deleted. Only native WebView2
            // handles are disposed after the application window has closed.
            _globalBrowser.Dispose();
            _browserView.Dispose();
            _workspaceView.Dispose();
        };
    }

    private static Brush Paint(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

    private static TextBlock Text(string text, double size, string color, FontWeight? weight = null) =>
        new()
        {
            Text = text, FontSize = size, Foreground = Paint(color),
            FontWeight = weight ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static Button MakeButton(string label, string background = "#e8f1fc",
        string foreground = "#194a87", double fontSize = 12, double width = 0)
    {
        var b = new Button
        {
            Content = label, Background = Paint(background),
            Foreground = Paint(foreground), FontWeight = FontWeights.SemiBold,
            FontSize = fontSize, Height = 34,
            Padding = new Thickness(11, 4, 11, 4),
            BorderBrush = Paint("#c4d5eb"), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = Cursors.Hand
        };
        if (width > 0) b.Width = width;
        return b;
    }

    private static TextBox MakeAddressBox()
    {
        return new TextBox
        {
            Height = 34, FontSize = 12, Background = Brushes.White,
            Foreground = Paint("#193653"), BorderBrush = Paint("#b9cbe0"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 7, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void BuildWindow()
    {
        // No extra toolbar: show the existing Dashboard edge-to-edge beneath
        // the normal Windows title bar.
        Content = _body;
        _body.ClipToBounds = true;

        _workspaceView.DefaultBackgroundColor = System.Drawing.Color.White;
        _body.Children.Add(_workspaceView);

        // This overlay is physically bounded to the active Invoice Generator
        // iframe, not the global Dashboard or its sidebar.
        _overlay.Background = null;
        _overlay.IsHitTestVisible = false;
        _overlay.ClipToBounds = true;
        _overlay.HorizontalAlignment = HorizontalAlignment.Left;
        _overlay.VerticalAlignment = VerticalAlignment.Top;
        _overlay.Width = 1;
        _overlay.Height = 1;
        _overlay.Visibility = Visibility.Collapsed;
        _body.Children.Add(_overlay);
        _overlay.SizeChanged += (_, _) =>
        {
            FitOverlay();
            RefreshNativeBrowserWindows();
        };

        // The old Invoice-only native browser is superseded by the
        // dashboard-wide GlobalBrowserWorkspace. No old bubble is created.
    }

    private void InitializeNativeBrowserWindows()
    {
        // Standard WebView2 hosts a normal HWND instead of using screen
        // capture/composition. A small owned window is used only for the
        // floating browser, and only when Invoice Generator is active.
        _bubbleWindow = new Window
        {
            Owner = this,
            Content = _bubble,
            Title = "BIG BROTHER Invoice Browser",
            Width = _bubble.Width,
            Height = _bubble.Height,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = false
        };
        _browserWindow = new Window
        {
            Owner = this,
            Content = _browserPanel,
            Title = "BIG BROTHER Invoice Browser",
            Width = _preferences.PanelWidth,
            Height = _preferences.PanelHeight,
            Background = Brushes.White,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = false
        };

        _browserWindow.Deactivated += (_, _) =>
        {
            ReleaseBrowserMouseCapture();
            Dispatcher.BeginInvoke(new Action(HideNativeWindowsIfAnotherAppActive),
                DispatcherPriority.ContextIdle);
        };
        _bubbleWindow.Deactivated += (_, _) =>
            Dispatcher.BeginInvoke(new Action(HideNativeWindowsIfAnotherAppActive),
                DispatcherPriority.ContextIdle);
    }

    private void HideNativeBrowserWindows()
    {
        if (_bubbleWindow?.IsVisible == true) _bubbleWindow.Hide();
        if (_browserWindow?.IsVisible == true) _browserWindow.Hide();
    }

    private void HideNativeWindowsIfAnotherAppActive()
    {
        if (IsActive || _browserWindow?.IsActive == true ||
            _bubbleWindow?.IsActive == true || _browserDialog?.IsActive == true) return;
        // Native windows are never topmost. Hide both when another program
        // becomes active; do not leave a ghost layer over other applications.
        HideNativeBrowserWindows();
    }

    private Point ScreenPointDip(Point relativeToOverlay)
    {
        if (!IsLoaded || !_overlay.IsVisible) return new Point(0, 0);
        var screen = _overlay.PointToScreen(relativeToOverlay);
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return fromDevice.Transform(screen);
    }

    private Point MouseScreenPointDip()
    {
        if (!GetCursorPos(out var mouse)) return new Point(0, 0);
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return fromDevice.Transform(new Point(mouse.X, mouse.Y));
    }

    private void PositionNativeBrowserWindows()
    {
        if (_bubbleWindow is null || _browserWindow is null ||
            !_invoiceScopeActive || WindowState == WindowState.Minimized ||
            !_overlay.IsVisible || _overlay.ActualWidth < 180) return;
        var frameOrigin = ScreenPointDip(new Point(0, 0));
        _bubbleWindow.Left = frameOrigin.X +
            _overlay.ActualWidth - _bubble.Width - 18;
        _bubbleWindow.Top = frameOrigin.Y + 17;
        _browserWindow.Width = _browserPanel.Width;
        _browserWindow.Height = _browserPanel.Height;
        _browserWindow.Left = frameOrigin.X + Canvas.GetLeft(_browserPanel);
        _browserWindow.Top = frameOrigin.Y + Canvas.GetTop(_browserPanel);
    }

    private void RefreshNativeBrowserWindows()
    {
        if (_bubbleWindow is null || _browserWindow is null) return;
        if (!_invoiceScopeActive || WindowState == WindowState.Minimized ||
            !_overlay.IsVisible || _overlay.ActualWidth < 180 ||
            _overlay.ActualHeight < 180 ||
            (!IsActive && _browserWindow.IsActive == false &&
             _bubbleWindow.IsActive == false && _browserDialog?.IsActive != true))
        {
            HideNativeBrowserWindows();
            return;
        }

        PositionNativeBrowserWindows();
        if (_panelOpen)
        {
            if (_bubbleWindow.IsVisible) _bubbleWindow.Hide();
            if (!_browserWindow.IsVisible) _browserWindow.Show();
        }
        else
        {
            if (_browserWindow.IsVisible) _browserWindow.Hide();
            if (!_bubbleWindow.IsVisible) _bubbleWindow.Show();
        }
    }

    // Keep Check Updates accessible from the standard Windows title-bar
    // system menu (Alt+Space), without an extra toolbar across the Dashboard.
    private void InstallNativeUpdateMenu(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var menu = GetSystemMenu(hwnd, false);
        if (menu != IntPtr.Zero)
        {
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, (nuint)UpdateSystemMenuCommand,
                "Check BIG BROTHER Desktop Updates");
            AppendMenu(menu, MfString, (nuint)LocalUpdateMenuCommand,
                "Install Downloaded Desktop Update...");
        }
        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(HandleSystemMenu);
    }

    private IntPtr HandleSystemMenu(IntPtr hwnd, int msg, IntPtr wParam,
        IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand &&
            ((wParam.ToInt64() & 0xFFF0) == UpdateSystemMenuCommand))
        {
            handled = true;
            _ = CheckUpdatesAsync();
        }
        else if (msg == WmSysCommand &&
                 ((wParam.ToInt64() & 0xFFF0) == LocalUpdateMenuCommand))
        {
            handled = true;
            _ = UpdateService.PromptLocalUpdateAsync(this, ShowUpdateStatus);
        }
        return IntPtr.Zero;
    }

    private void ApplyInvoiceScope(double left, double top, double width,
        double height, double viewportWidth, double viewportHeight, bool active)
    {
        var valid = active && width >= 180 && height >= 180 &&
                    viewportWidth > 0 && viewportHeight > 0 &&
                    _body.ActualWidth > 0 && _body.ActualHeight > 0;
        if (!valid)
        {
            // The Invoice iframe can disappear while the user is dragging or
            // resizing its browser. Merely clearing _dragging leaves WPF
            // holding mouse capture and can steal clicks from other apps.
            ReleaseBrowserMouseCapture();
            if (_invoiceScopeActive && _panelOpen)
            {
                RememberPanel();
                _panelOpen = false;
            }
            _invoiceScopeActive = false;
            _overlay.Visibility = Visibility.Collapsed;
            _browserPanel.Visibility = Visibility.Collapsed;
            _bubble.Visibility = Visibility.Collapsed;
            HideNativeBrowserWindows();
            return;
        }

        // Convert WebView viewport CSS pixels to WPF device-independent pixels.
        var scaleX = _body.ActualWidth / viewportWidth;
        var scaleY = _body.ActualHeight / viewportHeight;
        var x = Bound(left * scaleX, 0, _body.ActualWidth);
        var y = Bound(top * scaleY, 0, _body.ActualHeight);
        var w = Math.Min(width * scaleX, _body.ActualWidth - x);
        var h = Math.Min(height * scaleY, _body.ActualHeight - y);
        if (w < 180 || h < 180)
        {
            ApplyInvoiceScope(0, 0, 0, 0, 0, 0, false);
            return;
        }

        _invoiceScopeActive = true;
        _overlay.Margin = new Thickness(x, y, 0, 0);
        _overlay.Width = w;
        _overlay.Height = h;
        _overlay.Visibility = Visibility.Visible;
        _browserPanel.Visibility = _panelOpen ? Visibility.Visible : Visibility.Collapsed;
        _bubble.Visibility = _panelOpen ? Visibility.Collapsed : Visibility.Visible;
        FitOverlay();
        RefreshNativeBrowserWindows();
    }

    private void OnWorkspaceMessage(object? sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) ||
                !source.Host.Equals("app.bigbrother.test", StringComparison.OrdinalIgnoreCase) ||
                !source.AbsolutePath.StartsWith("/big-brother-dashboard/",
                    StringComparison.OrdinalIgnoreCase))
                return;
            var message = args.TryGetWebMessageAsString();
            using var json = JsonDocument.Parse(message);
            var value = json.RootElement;
            if (!value.TryGetProperty("type", out var type) ||
                type.GetString() != "bbDesktopInvoiceScopeV2")
                return;

            var active = value.GetProperty("visible").GetBoolean();
            ApplyInvoiceScope(
                value.GetProperty("x").GetDouble(),
                value.GetProperty("y").GetDouble(),
                value.GetProperty("width").GetDouble(),
                value.GetProperty("height").GetDouble(),
                value.GetProperty("viewportWidth").GetDouble(),
                value.GetProperty("viewportHeight").GetDouble(),
                active);
        }
        catch
        {
            // A malformed page message must never show the native browser.
        }
    }

    private void BuildBubble()
    {
        _bubble.Content = "◎";
        _bubble.FontSize = 32;
        _bubble.FontWeight = FontWeights.Bold;
        _bubble.Foreground = Brushes.White;
        _bubble.Background = Paint("#225fc0");
        _bubble.BorderBrush = Brushes.White;
        _bubble.BorderThickness = new Thickness(2);
        _bubble.Width = 58;
        _bubble.Height = 58;
        _bubble.Cursor = Cursors.Hand;
        _bubble.ToolTip = "Open the native floating browser";
        _bubble.Click += async (_, _) => await OpenBrowserPanelAsync();
        _bubble.Visibility = Visibility.Collapsed;

        // This button is hosted in its own tiny native owned Window rather
        // than over the HwndHost WebView2; there is NO full-screen overlay
        // Window that could intercept mouse input to Excel/Chrome/desktop.
    }

    private void BuildBrowserPanel()
    {
        _browserPanel.Background = Brushes.White;
        _browserPanel.BorderBrush = Paint("#91b0d4");
        _browserPanel.BorderThickness = new Thickness(1);
        _browserPanel.CornerRadius = new CornerRadius(11);
        _browserPanel.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black, BlurRadius = 25, ShadowDepth = 5, Opacity = .22
        };
        _browserPanel.Visibility = Visibility.Collapsed;
        _browserPanel.ClipToBounds = true;

        var panelGrid = new Grid();
        panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        var browserAddressGridRow = new RowDefinition { Height = new GridLength(43) };
        panelGrid.RowDefinitions.Add(browserAddressGridRow);
        panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(49) });
        panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        _browserPanel.Child = panelGrid;

        var titleStrip = new DockPanel { Background = Paint("#174888"), LastChildFill = true };
        Grid.SetRow(titleStrip, 0);
        panelGrid.Children.Add(titleStrip);

        var minimize = MakeButton("—", "#24589a", "#ffffff", 18, 38);
        minimize.Margin = new Thickness(0, 4, 7, 4);
        minimize.ToolTip = "Hide browser into the circular button (session stays open)";
        minimize.Click += (_, _) => MinimizeBrowser();
        DockPanel.SetDock(minimize, Dock.Right);
        titleStrip.Children.Add(minimize);

        var toggleAddress = MakeButton("URL ▴", "#24589a", "#ffffff", 11, 67);
        toggleAddress.Margin = new Thickness(0, 4, 3, 4);
        toggleAddress.ToolTip = "Show or hide the browser address bar";
        DockPanel.SetDock(toggleAddress, Dock.Right);
        titleStrip.Children.Add(toggleAddress);

        var dragSurface = new Border
        {
            Background = Brushes.Transparent, Cursor = Cursors.SizeAll,
            Padding = new Thickness(13, 0, 0, 0),
            Child = Text("◉  Browser Workspace    •    drag this title bar", 13, "#ffffff", FontWeights.Bold)
        };
        titleStrip.Children.Add(dragSurface);
        WirePanelDrag(dragSurface);

        var addressRow = new Grid
        {
            Background = Paint("#f4f7fc"),
            Margin = new Thickness(9, 5, 9, 4)
        };
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(addressRow, 1);
        panelGrid.Children.Add(addressRow);
        void ApplyAddressBarVisibility(bool visible)
        {
            addressRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            browserAddressGridRow.Height = new GridLength(visible ? 43 : 0);
            toggleAddress.Content = visible ? "URL ▴" : "URL ▾";
            toggleAddress.ToolTip = visible ? "Hide URL bar" : "Show URL bar";
        }
        ApplyAddressBarVisibility(_preferences.BrowserAddressBarVisible);
        toggleAddress.Click += (_, _) =>
        {
            _preferences.BrowserAddressBarVisible = !_preferences.BrowserAddressBarVisible;
            ApplyAddressBarVisibility(_preferences.BrowserAddressBarVisible);
            SettingsStore.Save(_preferences);
        };

        var back = MakeButton("‹", "#eaf1fa", "#245281", 20, 35);
        back.ToolTip = "Go back";
        back.Click += (_, _) => { if (_browserView.CoreWebView2?.CanGoBack == true) _browserView.CoreWebView2.GoBack(); };
        Grid.SetColumn(back, 0);
        addressRow.Children.Add(back);

        var forward = MakeButton("›", "#eaf1fa", "#245281", 20, 35);
        forward.ToolTip = "Go forward";
        forward.Click += (_, _) => { if (_browserView.CoreWebView2?.CanGoForward == true) _browserView.CoreWebView2.GoForward(); };
        Grid.SetColumn(forward, 1);
        addressRow.Children.Add(forward);

        var reload = MakeButton("↻", "#eaf1fa", "#245281", 18, 35);
        reload.ToolTip = "Reload page";
        reload.Click += (_, _) => _browserView.CoreWebView2?.Reload();
        Grid.SetColumn(reload, 2);
        addressRow.Children.Add(reload);

        _browserAddress.Text = SettingsStore.TryWebAddress(_preferences.BrowserUrl, out var initialUrl)
            ? initialUrl.AbsoluteUri : TelegramUrl;
        _browserAddress.Margin = new Thickness(0, 0, 7, 0);
        Grid.SetColumn(_browserAddress, 3);
        addressRow.Children.Add(_browserAddress);
        _browserAddress.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) { await NavigateBrowserAsync(_browserAddress.Text); e.Handled = true; }
        };

        var go = MakeButton("Go", "#225fc0", "#ffffff", 12, 49);
        go.Click += async (_, _) => await NavigateBrowserAsync(_browserAddress.Text);
        Grid.SetColumn(go, 4);
        addressRow.Children.Add(go);

        // Save lives at the far end of the URL bar, not in the shortcut row.
        var saveUrl = MakeButton("☆ Save", "#e3f0fe", "#23558a", 11, 86);
        saveUrl.ToolTip = "Save the exact current page or Telegram chat as a shortcut";
        saveUrl.Click += (_, _) => BookmarkCurrent();
        Grid.SetColumn(saveUrl, 5);
        addressRow.Children.Add(saveUrl);

        var tools = new DockPanel
        {
            Background = Paint("#f4f7fc"),
            Margin = new Thickness(10, 0, 10, 3),
            LastChildFill = true
        };
        Grid.SetRow(tools, 2);
        panelGrid.Children.Add(tools);

        // Both Telegram Web and saved chats use the same shortcut controls.
        // Users can remove or rename the default Telegram Web button too.
        // New bookmarks appear as their own scrollable buttons beside Telegram.
        // The row contains shortcuts only: no Save, Rename, or dropdown controls.
        var shortcutScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            Content = _shortcutButtons
        };
        tools.Children.Add(shortcutScroller);
        RefreshBookmarks();

        _browserView.DefaultBackgroundColor = System.Drawing.Color.White;
        Grid.SetRow(_browserView, 3);
        panelGrid.Children.Add(_browserView);

        var foot = new Grid { Background = Paint("#eff4fb") };
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(foot, 4);
        panelGrid.Children.Add(foot);

        _notice.Text = "Native WebView2 • Login data remains on this device";
        _notice.Foreground = Paint("#5a6f88");
        _notice.FontSize = 10;
        _notice.VerticalAlignment = VerticalAlignment.Center;
        _notice.TextTrimming = TextTrimming.CharacterEllipsis;
        _notice.Margin = new Thickness(10, 0, 4, 0);
        foot.Children.Add(_notice);

        var grip = new Thumb
        {
            Width = 24, Height = 23, Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = Paint("#aec4de"),
            ToolTip = "Drag to resize this browser"
        };
        _resizeGrip = grip;
        grip.DragStarted += (_, _) => { _resizing = true; };
        grip.LostMouseCapture += (_, _) => { _resizing = false; };
        grip.DragDelta += (_, e) =>
        {
            var minWidth = Math.Min(360, _overlay.ActualWidth - 18);
            var maxWidth = Math.Max(minWidth, _overlay.ActualWidth - Canvas.GetLeft(_browserPanel) - 8);
            var maxHeight = Math.Max(220, _overlay.ActualHeight - Canvas.GetTop(_browserPanel) - 8);
            _browserPanel.Width = Bound(_browserPanel.Width + e.HorizontalChange, minWidth, maxWidth);
            _browserPanel.Height = Bound(_browserPanel.Height + e.VerticalChange, 220, maxHeight);
            PositionNativeBrowserWindows();
        };
        grip.DragCompleted += (_, _) =>
        {
            _resizing = false;
            if (grip.IsMouseCaptured) grip.ReleaseMouseCapture();
            RememberPanel();
        };
        Grid.SetColumn(grip, 1);
        foot.Children.Add(grip);

        // Browser panel belongs to a small, non-topmost native owned Window.
        // Its separate HWND avoids WPF/WebView2 airspace conflicts, while
        // Hide()/Show() preserves the live WebView and Telegram session.
    }

    private sealed record BookmarkItem(string Url, string Name)
    {
        public override string ToString() => Name;
    }

    private void RefreshBookmarks()
    {
        // Retain existing saved URL and names. Render the optional default
        // Telegram button and every saved page as one consistent shortcut row.
        foreach (var timer in _shortcutClickTimers) timer.Stop();
        _shortcutClickTimers.Clear();
        _shortcutButtons.Children.Clear();

        if (_preferences.TelegramShortcutVisible)
            AddShortcutButton(new BookmarkItem(TelegramUrl,
                string.IsNullOrWhiteSpace(_preferences.TelegramShortcutName)
                ? "Telegram Web" : _preferences.TelegramShortcutName), isBuiltIn: true);

        foreach (var savedUrl in (_preferences.Bookmarks ?? new List<string>()).Take(15))
        {
            if (!SettingsStore.TryWebAddress(savedUrl, out var uri)) continue;
            var name = _preferences.BookmarkTitles != null &&
                _preferences.BookmarkTitles.TryGetValue(savedUrl, out var savedName) &&
                !string.IsNullOrWhiteSpace(savedName)
                ? savedName : uri.Host;
            AddShortcutButton(new BookmarkItem(savedUrl, name), isBuiltIn: false);
        }
    }

    private void AddShortcutButton(BookmarkItem item, bool isBuiltIn)
    {
        var button = MakeButton(item.Name, "#e3f0fe", "#23558a", 11);
        button.MinWidth = 76;
        button.MaxWidth = 190;
        button.ToolTip = "Click to open • triple-click to rename • right-click to manage\n" + item.Url;
        button.Content = new TextBlock
        {
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Distinguish a deliberate triple-click from ordinary navigation.
        // A quick double-click must never navigate to the wrong saved chat.
        var pendingClicks = 0;
        var clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _shortcutClickTimers.Add(clickTimer);
        clickTimer.Tick += async (_, _) =>
        {
            clickTimer.Stop();
            pendingClicks = 0;
            await NavigateBrowserAsync(item.Url);
        };
        button.Click += (_, _) =>
        {
            clickTimer.Stop();
            pendingClicks++;
            if (pendingClicks >= 3)
            {
                pendingClicks = 0;
                RenameBookmark(item, isBuiltIn);
            }
            else clickTimer.Start();
        };
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Rename shortcut (triple-click)" };
        rename.Click += (_, _) =>
        {
            clickTimer.Stop();
            pendingClicks = 0;
            RenameBookmark(item, isBuiltIn);
        };
        menu.Items.Add(rename);
        var remove = new MenuItem { Header = "Remove shortcut" };
        remove.Click += (_, _) =>
        {
            clickTimer.Stop();
            pendingClicks = 0;
            RemoveBookmark(item, isBuiltIn);
        };
        menu.Items.Add(remove);
        button.ContextMenu = menu;
        _shortcutButtons.Children.Add(button);
    }

    private bool? ShowBrowserDialog(Window dialog)
    {
        // An active, explicitly requested bookmark dialog counts as an owned
        // browser surface. Do not hide its owner as a ghost overlay while the
        // prompt itself has focus. Switching to another app still hides it.
        _browserDialog = dialog;
        try { return dialog.ShowDialog(); }
        finally
        {
            _browserDialog = null;
            Dispatcher.BeginInvoke(new Action(RefreshNativeBrowserWindows),
                DispatcherPriority.ContextIdle);
        }
    }

    private string? PromptBookmarkName(string title, string initialName)
    {
        // Native modal is temporary and owned by the small floating browser;
        // it never creates a persistent full-screen window or global overlay.
        var prompt = new Window
        {
            Title = title,
            Owner = _browserWindow?.IsVisible == true ? _browserWindow : this,
            Width = 390, Height = 176,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, Background = Paint("#f4f7fc")
        };
        var layout = new StackPanel { Margin = new Thickness(18, 13, 18, 10) };
        layout.Children.Add(Text("Shortcut name", 12, "#234a78", FontWeights.SemiBold));
        var input = MakeAddressBox();
        input.Text = initialName.Length > 80 ? initialName[..80] : initialName;
        input.Margin = new Thickness(0, 7, 0, 11);
        input.MaxLength = 80;
        layout.Children.Add(input);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = MakeButton("Cancel", "#edf1f7", "#234a78", 11, 76);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => prompt.DialogResult = false;
        buttons.Children.Add(cancel);
        var accept = MakeButton("Save name", "#225fc0", "#ffffff", 11, 96);
        accept.IsDefault = true;
        accept.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                input.Focus();
                return;
            }
            prompt.DialogResult = true;
        };
        buttons.Children.Add(accept);
        layout.Children.Add(buttons);
        prompt.Content = layout;
        prompt.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return ShowBrowserDialog(prompt) == true ? input.Text.Trim() : null;
    }

    private void BookmarkCurrent()
    {
        // Save what WebView2 is actually showing, not text still being edited
        // in the Go field. Do not persist sensitive URL query/fragment tokens.
        var liveUrl = _browserView.CoreWebView2?.Source;
        if (!SettingsStore.TryWebAddress(liveUrl, out var current))
        {
            _notice.Text = "Open a website first, then press Save";
            return;
        }
        // Explicit shortcuts retain the FULL live address, including the
        // Telegram chat path, query string, and #fragment. Automatic browser
        // session restore still uses a sanitized URL.
        var url = current.AbsoluteUri;
        _preferences.Bookmarks ??= new List<string>();
        _preferences.BookmarkTitles ??= new Dictionary<string, string>();
        var name = _preferences.BookmarkTitles.TryGetValue(url, out var existing)
            ? existing
            : (string.IsNullOrWhiteSpace(_browserView.CoreWebView2?.DocumentTitle)
                ? current.Host : _browserView.CoreWebView2.DocumentTitle.Trim());
        var chosen = PromptBookmarkName("Save website shortcut", name);
        if (chosen is null) return;

        _preferences.Bookmarks.RemoveAll(x => string.Equals(x, url,
            StringComparison.Ordinal));
        _preferences.Bookmarks.Insert(0, url);
        _preferences.Bookmarks = _preferences.Bookmarks.Take(15).ToList();
        _preferences.BookmarkTitles[url] = chosen;
        RefreshBookmarks();
        SettingsStore.Save(_preferences);
        _notice.Text = "Saved shortcut: " + chosen;
    }

    private void RenameBookmark(BookmarkItem item, bool isBuiltIn)
    {
        var newName = PromptBookmarkName("Rename website shortcut", item.Name);
        if (newName is null) return;
        if (isBuiltIn) _preferences.TelegramShortcutName = newName;
        else
        {
            _preferences.BookmarkTitles ??= new Dictionary<string, string>();
            _preferences.BookmarkTitles[item.Url] = newName;
        }
        RefreshBookmarks();
        SettingsStore.Save(_preferences);
        _notice.Text = "Renamed shortcut to " + newName;
    }

    private void RemoveBookmark(BookmarkItem item, bool isBuiltIn)
    {
        var confirmation = new Window
        {
            Title = "Remove website shortcut",
            Owner = _browserWindow?.IsVisible == true ? _browserWindow : this,
            Width = 390, Height = 156,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, Background = Paint("#f4f7fc")
        };
        var layout = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };
        layout.Children.Add(Text("Remove shortcut '" + item.Name + "'?", 12,
            "#234a78", FontWeights.SemiBold));
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = MakeButton("Cancel", "#edf1f7", "#234a78", 11, 76);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => confirmation.DialogResult = false;
        buttons.Children.Add(cancel);
        var remove = MakeButton("Remove", "#b23434", "#ffffff", 11, 82);
        remove.Click += (_, _) => confirmation.DialogResult = true;
        buttons.Children.Add(remove);
        layout.Children.Add(buttons);
        confirmation.Content = layout;
        if (ShowBrowserDialog(confirmation) != true) return;
        if (isBuiltIn)
        {
            // Hide only the optional default shortcut, never remove the
            // actual browser login, saved chat buttons or WebView2 profile.
            _preferences.TelegramShortcutVisible = false;
        }
        else
        {
            _preferences.Bookmarks?.RemoveAll(x => string.Equals(x, item.Url,
                StringComparison.Ordinal));
            _preferences.BookmarkTitles?.Remove(item.Url);
        }
        RefreshBookmarks();
        SettingsStore.Save(_preferences);
        _notice.Text = "Removed shortcut: " + item.Name;
    }

    private void SyncBrowserAddressFromWeb()
    {
        // SourceChanged fires for redirects, history traversal and SPA
        // pushState/replaceState routes, not only full page loads.
        if (!SettingsStore.TryWebAddress(_browserView.CoreWebView2?.Source,
                out var current)) return;
        _browserAddress.Text = current.AbsoluteUri;
        var safe = SettingsStore.RememberableUrl(current.AbsoluteUri);
        if (!string.Equals(_preferences.BrowserUrl, safe, StringComparison.Ordinal))
        {
            _preferences.BrowserUrl = safe;
            SettingsStore.Save(_preferences);
        }
    }

    private void WirePanelDrag(Border surface)
    {
        _dragSurface = surface;
        surface.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_panelOpen) return;
            _dragging = surface.CaptureMouse();
            if (!_dragging) return;
            _dragFrom = MouseScreenPointDip();
            _panelFrom = new Point(Canvas.GetLeft(_browserPanel), Canvas.GetTop(_browserPanel));
            e.Handled = true;
        };
        surface.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            if (e.LeftButton != MouseButtonState.Pressed) { EndPanelDrag(surface); return; }
            var current = MouseScreenPointDip();
            var x = _panelFrom.X + (current.X - _dragFrom.X);
            var y = _panelFrom.Y + (current.Y - _dragFrom.Y);
            Canvas.SetLeft(_browserPanel, Bound(x, 7, _overlay.ActualWidth - _browserPanel.Width - 7));
            Canvas.SetTop(_browserPanel, Bound(y, 7, _overlay.ActualHeight - _browserPanel.Height - 7));
            PositionNativeBrowserWindows();
        };
        surface.MouseLeftButtonUp += (_, _) => EndPanelDrag(surface);
        // WPF can deliver the button-release outside the captured element.
        // Explicitly release then rather than relying on MouseLeftButtonUp.
        surface.AddHandler(Mouse.PreviewMouseUpOutsideCapturedElementEvent,
            new MouseButtonEventHandler((_, _) => EndPanelDrag(surface)), true);
        surface.LostMouseCapture += (_, _) =>
        {
            if (_dragging) RememberPanel();
            _dragging = false;
        };
    }

    private void EndPanelDrag(Border surface)
    {
        var wasDragging = _dragging;
        _dragging = false;
        // Always release if captured, even if a previous scope change has
        // already cleared the state flag.
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture();
        if (wasDragging) RememberPanel();
    }

    private void ReleaseBrowserMouseCapture()
    {
        var remember = _dragging || _resizing;
        _dragging = false;
        _resizing = false;
        if (_dragSurface?.IsMouseCaptured == true)
            _dragSurface.ReleaseMouseCapture();
        if (_resizeGrip?.IsMouseCaptured == true)
            _resizeGrip.ReleaseMouseCapture();
        if (remember) RememberPanel();
    }

    private static double Bound(double value, double minimum, double maximum) =>
        Math.Clamp(value, minimum, Math.Max(minimum, maximum));

    private void FitOverlay()
    {
        if (_overlay.ActualWidth < 100 || _overlay.ActualHeight < 100) return;
        Canvas.SetLeft(_bubble, _overlay.ActualWidth - _bubble.Width - 18);
        Canvas.SetTop(_bubble, 17);

        var maxW = Math.Max(320, _overlay.ActualWidth - 16);
        var maxH = Math.Max(230, _overlay.ActualHeight - 16);
        _browserPanel.Width = Bound(
            _browserPanel.Width > 0 ? _browserPanel.Width : _preferences.PanelWidth,
            Math.Min(330, maxW), maxW);
        _browserPanel.Height = Bound(
            _browserPanel.Height > 0 ? _browserPanel.Height : _preferences.PanelHeight,
            Math.Min(230, maxH), maxH);
        var preferredX = Canvas.GetLeft(_browserPanel);
        if (double.IsNaN(preferredX)) preferredX = _preferences.PanelX;
        if (preferredX < 0) preferredX = _overlay.ActualWidth - _browserPanel.Width - 20;
        var preferredY = Canvas.GetTop(_browserPanel);
        if (double.IsNaN(preferredY)) preferredY = _preferences.PanelY;
        Canvas.SetLeft(_browserPanel, Bound(preferredX, 8, _overlay.ActualWidth - _browserPanel.Width - 8));
        Canvas.SetTop(_browserPanel, Bound(preferredY, 8, _overlay.ActualHeight - _browserPanel.Height - 8));
        PositionNativeBrowserWindows();
    }

    private void RememberPanel()
    {
        if (_overlay.ActualWidth < 100 || _overlay.ActualHeight < 100) return;
        _preferences.PanelWidth = _browserPanel.Width;
        _preferences.PanelHeight = _browserPanel.Height;
        _preferences.PanelX = Canvas.GetLeft(_browserPanel);
        _preferences.PanelY = Canvas.GetTop(_browserPanel);
        SettingsStore.Save(_preferences);
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            _workspaceStatus.Text = "Initializing local accounting workspace…";
            var environment = await CoreWebView2Environment.CreateAsync(
                null, SettingsStore.WorkspaceProfile);
            await _workspaceView.EnsureCoreWebView2Async(environment);
            var web = _workspaceView.CoreWebView2;
            var assetsFolder = Path.Combine(AppContext.BaseDirectory, "web");
            if (!File.Exists(Path.Combine(assetsFolder, "big-brother-dashboard", "index.html")))
                throw new FileNotFoundException("Installed Dashboard assets are missing. Reinstall BIG BROTHER Desktop.");
            web.SetVirtualHostNameToFolderMapping("app.bigbrother.test", assetsFolder,
                CoreWebView2HostResourceAccessKind.DenyCors);
            // Fail closed if an unconverted module still tries to use GitHub Pages.
            web.AddWebResourceRequestedFilter("https://angsokhey11-cloud.github.io/*",
                CoreWebView2WebResourceContext.All);
            web.WebResourceRequested += (_, args) =>
            {
                args.Response = web.Environment.CreateWebResourceResponse(null, 403,
                    "GitHub Pages unavailable in installed application", "Content-Type: text/plain");
            };
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.AreHostObjectsAllowed = false;
            web.Settings.IsWebMessageEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            // Remove old Invoice-scoped iframe polling and use the global
            // native Browser Workspace instead.
            await web.AddScriptToExecuteOnDocumentCreatedAsync(HideWebInvoiceBubbleScript);
            web.NavigationCompleted += async (_, e) =>
            {
                _workspaceStatus.Text = e.IsSuccess ? "Installed Dashboard / function loaded" : "Local accounting page failed to load";
                try
                {
                    await web.ExecuteScriptAsync(HideWebInvoiceBubbleScript);
                }
                catch { /* This may occur during navigation. */ }
            };
            NavigateWorkspace();
        }
        catch (Exception e)
        {
            _workspaceStatus.Text = "Desktop workspace initialization failed";
            MessageBox.Show("Desktop application could not open: " + e.Message +
                "\n\nInstall or repair Microsoft Edge WebView2 Runtime and try again.",
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NavigateWorkspace()
    {
        if (_workspaceView.CoreWebView2 == null) return;
        _workspaceView.CoreWebView2.Navigate(DefaultWorkspaceUrl);
        _workspaceStatus.Text = "Loading installed Dashboard…";
    }

    private void ShowUpdateStatus(string status)
    {
        _workspaceStatus.Text = status;
        // The Dashboard remains edge-to-edge. The native Windows titlebar
        // gives users lightweight update feedback without an extra app header.
        var inProgress = status.Contains("Checking", StringComparison.OrdinalIgnoreCase) ||
                         status.Contains("Downloading", StringComparison.OrdinalIgnoreCase);
        Title = inProgress ? DesktopTitle + " • " + status : DesktopTitle;
    }

    private async Task CheckUpdatesAsync()
    {
        await UpdateService.CheckForUpdatesAsync(this, ShowUpdateStatus);
    }

    private async Task OpenBrowserPanelAsync()
    {
        if (!_invoiceScopeActive) return;
        _panelOpen = true;
        _browserPanel.Visibility = Visibility.Visible;
        _bubble.Visibility = Visibility.Collapsed;
        FitOverlay();
        RefreshNativeBrowserWindows();
        if (_browserReady || _browserStarting) return;
        _browserStarting = true;
        _notice.Text = "Starting native WebView2 browser…";
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(
                null, SettingsStore.BrowserProfile);
            await _browserView.EnsureCoreWebView2Async(environment);
            var web = _browserView.CoreWebView2;
            web.Settings.AreHostObjectsAllowed = false;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.NewWindowRequested += (_, e) =>
            {
                // Most sites use same-page browsing. Keep links in the native
                // browser rather than opening an external Chrome window.
                if (SettingsStore.TryWebAddress(e.Uri, out var target))
                {
                    e.Handled = true;
                    web.Navigate(target.AbsoluteUri);
                }
            };
            web.NavigationStarting += (_, e) =>
            {
                if (SettingsStore.TryWebAddress(e.Uri, out var destination))
                    _browserAddress.Text = destination.AbsoluteUri;
            };
            web.SourceChanged += (_, _) => SyncBrowserAddressFromWeb();
            web.NavigationCompleted += (_, e) =>
            {
                SyncBrowserAddressFromWeb();
                _notice.Text = e.IsSuccess
                    ? "WebView2 browser • Private device profile retained"
                    : "Website navigation failed; check the address or your connection";
            };
            _browserReady = true;
            await NavigateBrowserAsync(_browserAddress.Text);
        }
        catch (Exception e)
        {
            _notice.Text = "Native browser failed to start";
            MessageBox.Show("Native browser unavailable: " + e.Message +
                "\n\nMicrosoft Edge WebView2 Runtime is required.",
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _browserStarting = false;
        }
    }

    private async Task NavigateBrowserAsync(string requestedUrl)
    {
        if (!SettingsStore.TryWebAddress(requestedUrl, out var url))
        {
            _notice.Text = "Enter an http:// or https:// website address";
            return;
        }

        _browserAddress.Text = url.AbsoluteUri;
        _preferences.BrowserUrl = SettingsStore.RememberableUrl(url.AbsoluteUri);
        SettingsStore.Save(_preferences);
        if (!_browserReady)
        {
            if (!_browserStarting) await OpenBrowserPanelAsync();
            return;
        }

        var web = _browserView.CoreWebView2;
        // Telegram Web A parses its chat hash only on APP BOOT (routing.ts
        // captureInitialLocation / Main.tsx parseLocationHash). A hash-only
        // change displays the new ID briefly, but does NOT open that chat:
        // Telegram then overwrites it with the previously selected chat.
        //
        // For saved Telegram chat URLs, atomically stage the full target URL
        // in the current document history and force a real document reload.
        // The new app boot consumes the requested chat ID. Keep the existing
        // WebView2 instance/profile: no logout, no new popup or overlay.
        var isTelegramChat = string.Equals(url.Host, "web.telegram.org",
                StringComparison.OrdinalIgnoreCase) &&
            (url.AbsolutePath.StartsWith("/a/", StringComparison.OrdinalIgnoreCase) ||
             url.AbsolutePath.StartsWith("/k/", StringComparison.OrdinalIgnoreCase) ||
             url.AbsolutePath.StartsWith("/z/", StringComparison.OrdinalIgnoreCase)) &&
            System.Text.RegularExpressions.Regex.IsMatch(url.Fragment,
                @"^#-?\d+(?:[_?].*)?$");

        if (isTelegramChat && SettingsStore.TryWebAddress(web.Source, out var current) &&
            string.Equals(current.GetLeftPart(UriPartial.Query),
                url.GetLeftPart(UriPartial.Query), StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(current.Fragment, url.Fragment, StringComparison.Ordinal))
            {
                _notice.Text = "Telegram chat already selected in the address bar";
                return;
            }
            try
            {
                // history.replaceState alone is NOT navigation; reload is
                // essential to force Telegram's initial-chat parser to run.
                // Both operations run in one script task, before the SPA can
                // restore its previous hash between separate host calls.
                var targetLiteral = JsonSerializer.Serialize(url.AbsoluteUri);
                await web.ExecuteScriptAsync(
                    "window.history.replaceState(window.history.state, '', " +
                    targetLiteral + "); window.location.reload();");
                _notice.Text = "Loading saved Telegram chat from its exact URL…";
                return;
            }
            catch (InvalidOperationException)
            {
                // Script context could be between navigations. Use a new
                // document navigation instead of silently dropping the click.
            }
        }
        web.Navigate(url.AbsoluteUri);
        _notice.Text = "Navigating to " + url.Host + "…";
    }

    private void MinimizeBrowser()
    {
        RememberPanel();
        _panelOpen = false;
        ReleaseBrowserMouseCapture();
        _browserPanel.Visibility = Visibility.Collapsed;
        _bubble.Visibility = _invoiceScopeActive ? Visibility.Visible : Visibility.Collapsed;
        RefreshNativeBrowserWindows();
        // Keep the same WebView2 instance alive: no Dispose, no Navigate,
        // no profile wipe, no page reload, no invoice state changes.
    }
}
