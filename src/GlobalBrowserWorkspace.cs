using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace BBDesktop;

/// <summary>
/// A dashboard-wide, non-topmost, bounded WebView2 browser with a tiny bubble.
/// This is deliberately NOT a WebView2 composition overlay: it cannot block
/// clicks in other desktop applications. Every tab shares the existing browser
/// profile but has an independent WebView2 document and history.
/// </summary>
internal sealed class GlobalBrowserWorkspace : IDisposable
{
    private const string TelegramUrl = "https://web.telegram.org/";
    private const int MaxTabs = 8;
    private readonly Window _owner;
    private readonly WorkstationSettings _settings;
    private Window? _bubbleWindow;
    private Window? _browserWindow;
    private Window? _dialog;
    private readonly Grid _layout = new();
    private readonly Grid _content = new();
    private readonly Border _home = new();
    private readonly StackPanel _tabButtons = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _shortcutButtons = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _homeButtons = new();
    private readonly TextBox _address = new();
    private readonly TextBlock _status = new();
    private readonly List<BrowserTab> _tabs = new();
    private readonly List<DispatcherTimer> _clickTimers = new();
    private Task<CoreWebView2Environment>? _environmentTask;
    private BrowserTab? _active;
    private RowDefinition? _addressRowDefinition;
    private Grid? _addressRow;
    private Thumb? _resizeGrip;
    private bool _panelOpen;
    private bool _closed;

    private sealed class BrowserTab
    {
        public WebView2 View { get; } = new();
        public bool IsHome { get; set; } = true;
        public bool Ready { get; set; }
        public bool Starting { get; set; }
        public string Title { get; set; } = "Home";
        public string? PendingUrl { get; set; }
    }

    private sealed record Shortcut(string Name, string Url, bool IsBuiltIn);

    public GlobalBrowserWorkspace(Window owner, WorkstationSettings settings)
    {
        _owner = owner;
        _settings = settings;
    }

    private static Brush Paint(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

    private static TextBlock Label(string value, double size, string color, bool bold = false) =>
        new()
        {
            Text = value, FontSize = size, Foreground = Paint(color),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static Button Button(string text, string background, string foreground, double width)
    {
        return new Button
        {
            Content = text, Width = width, Height = 32, FontSize = 11,
            Background = Paint(background), Foreground = Paint(foreground),
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(7, 3, 7, 3),
            BorderBrush = Paint("#bacfe6"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 5, 0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
        };
    }

    public void Initialize()
    {
        if (_bubbleWindow is not null) return;
        BuildBrowserPanel();

        var bubble = Button("◎", "#225fc0", "#ffffff", 56);
        bubble.Height = 56;
        bubble.FontSize = 30;
        bubble.BorderBrush = Brushes.White;
        bubble.BorderThickness = new Thickness(2);
        bubble.ToolTip = "Open BIG BROTHER Browser from any function";
        bubble.Click += (_, _) => Open();

        _bubbleWindow = new Window
        {
            Owner = _owner, Content = bubble,
            Width = 58, Height = 58,
            Background = Brushes.Transparent, AllowsTransparency = true,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            ShowActivated = false, Topmost = false
        };
        _browserWindow = new Window
        {
            Owner = _owner, Title = "BIG BROTHER Browser",
            Content = _layout,
            Width = _settings.PanelWidth > 0 ? _settings.PanelWidth : 740,
            Height = _settings.PanelHeight > 0 ? _settings.PanelHeight : 540,
            Background = Brushes.White, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            ShowActivated = false, Topmost = false
        };
        _browserWindow.Deactivated += (_, _) =>
            _owner.Dispatcher.BeginInvoke(new Action(HideIfOtherAppActive),
                DispatcherPriority.ContextIdle);
        _bubbleWindow.Deactivated += (_, _) =>
            _owner.Dispatcher.BeginInvoke(new Action(HideIfOtherAppActive),
                DispatcherPriority.ContextIdle);
        _owner.Dispatcher.BeginInvoke(new Action(Refresh),
            DispatcherPriority.Loaded);
    }

    private void BuildBrowserPanel()
    {
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(37) });
        _addressRowDefinition = new RowDefinition { Height = new GridLength(43) };
        _layout.RowDefinitions.Add(_addressRowDefinition);
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) });

        var title = new DockPanel
        {
            Background = Paint("#174888"),
            LastChildFill = true
        };
        Grid.SetRow(title, 0);
        _layout.Children.Add(title);

        var close = Button("×", "#a83c49", "#ffffff", 37);
        close.ToolTip = "Close browser completely (end all tabs)";
        close.FontSize = 18;
        close.Click += (_, _) => CloseAll();
        DockPanel.SetDock(close, Dock.Right);
        title.Children.Add(close);

        var minimize = Button("—", "#255b9f", "#ffffff", 37);
        minimize.ToolTip = "Minimize to the circle (keep all tabs open)";
        minimize.FontSize = 18;
        minimize.Click += (_, _) => Minimize();
        DockPanel.SetDock(minimize, Dock.Right);
        title.Children.Add(minimize);

        var toggle = Button("URL ▴", "#255b9f", "#ffffff", 67);
        toggle.ToolTip = "Show or hide address bar";
        toggle.Click += (_, _) =>
        {
            _settings.BrowserAddressBarVisible = !_settings.BrowserAddressBarVisible;
            SetAddressVisibility();
            SettingsStore.Save(_settings);
        };
        DockPanel.SetDock(toggle, Dock.Right);
        title.Children.Add(toggle);
        _urlToggle = toggle;

        var drag = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(12, 0, 0, 0),
            Cursor = Cursors.SizeAll,
            Child = Label("◉  BIG BROTHER Browser   •   drag to move", 13, "#ffffff", true)
        };
        drag.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _browserWindow is null) return;
            try { _browserWindow.DragMove(); }
            catch (InvalidOperationException) { /* released between events */ }
            RememberBounds();
        };
        title.Children.Add(drag);

        var tabsRow = new DockPanel { Background = Paint("#e9f0f9"), LastChildFill = true };
        Grid.SetRow(tabsRow, 1);
        _layout.Children.Add(tabsRow);
        var newTab = Button("+ New Tab", "#ffffff", "#174888", 100);
        newTab.ToolTip = "Create a fresh Home tab";
        newTab.Click += (_, _) => CreateTab();
        DockPanel.SetDock(newTab, Dock.Right);
        tabsRow.Children.Add(newTab);
        var tabScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(5, 2, 0, 0),
            Content = _tabButtons
        };
        tabsRow.Children.Add(tabScroll);

        var urlRow = new Grid { Margin = new Thickness(9, 4, 9, 4) };
        _addressRow = urlRow;
        Grid.SetRow(urlRow, 2);
        _layout.Children.Add(urlRow);
        for (var i = 0; i < 3; i++)
            urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 2; i++)
            urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var back = Button("‹", "#e8f1fc", "#23558a", 35);
        back.FontSize = 18;
        back.Click += (_, _) =>
        {
            var web = _active?.View.CoreWebView2;
            if (web?.CanGoBack == true) web.GoBack();
        };
        Grid.SetColumn(back, 0);
        urlRow.Children.Add(back);

        var forward = Button("›", "#e8f1fc", "#23558a", 35);
        forward.FontSize = 18;
        forward.Click += (_, _) =>
        {
            var web = _active?.View.CoreWebView2;
            if (web?.CanGoForward == true) web.GoForward();
        };
        Grid.SetColumn(forward, 1);
        urlRow.Children.Add(forward);

        var reload = Button("↻", "#e8f1fc", "#23558a", 35);
        reload.FontSize = 17;
        reload.Click += (_, _) => _active?.View.CoreWebView2?.Reload();
        Grid.SetColumn(reload, 2);
        urlRow.Children.Add(reload);

        _address.Height = 32;
        _address.FontSize = 12;
        _address.Text = "";
        _address.ToolTip = "Enter website URL • your last visited chat is not restored automatically";
        _address.Background = Brushes.White;
        _address.Foreground = Paint("#193653");
        _address.BorderBrush = Paint("#b9cbe0");
        _address.Padding = new Thickness(7, 4, 7, 3);
        _address.VerticalContentAlignment = VerticalAlignment.Center;
        _address.Margin = new Thickness(0, 0, 5, 0);
        _address.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await NavigateAsync(_address.Text);
        };
        Grid.SetColumn(_address, 3);
        urlRow.Children.Add(_address);

        var go = Button("Go", "#225fc0", "#ffffff", 49);
        go.Click += async (_, _) => await NavigateAsync(_address.Text);
        Grid.SetColumn(go, 4);
        urlRow.Children.Add(go);

        var save = Button("☆ Save", "#e3f0fe", "#23558a", 84);
        save.ToolTip = "Save the exact current URL, including Telegram chat ID";
        save.Click += (_, _) => SaveShortcut();
        Grid.SetColumn(save, 5);
        urlRow.Children.Add(save);
        SetAddressVisibility();

        var shortcutRow = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Paint("#f4f7fc"),
            Margin = new Thickness(9, 1, 9, 1),
            Content = _shortcutButtons
        };
        Grid.SetRow(shortcutRow, 3);
        _layout.Children.Add(shortcutRow);

        _home.Background = Brushes.White;
        var welcome = new StackPanel { Margin = new Thickness(20, 24, 20, 14) };
        welcome.Children.Add(Label("BIG BROTHER Browser", 22, "#174888", true));
        var guide = Label("Open a saved website, or type a URL above. New tabs start here.", 12, "#677c94");
        guide.Margin = new Thickness(0, 9, 0, 19);
        welcome.Children.Add(guide);
        var heading = Label("YOUR SHORTCUTS", 12, "#325b88", true);
        heading.Margin = new Thickness(0, 0, 0, 12);
        welcome.Children.Add(heading);
        welcome.Children.Add(_homeButtons);
        var hint = Label("Close starts fresh next time. Minimize keeps your tabs open.", 11, "#71859b");
        hint.Margin = new Thickness(0, 20, 0, 0);
        welcome.Children.Add(hint);
        _home.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = welcome
        };
        _content.Children.Add(_home);
        Grid.SetRow(_content, 4);
        _layout.Children.Add(_content);

        var footer = new Grid { Background = Paint("#eff4fb") };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 5);
        _layout.Children.Add(footer);
        _status.Text = "Home • saved websites stay on this device";
        _status.FontSize = 10;
        _status.Foreground = Paint("#5b7087");
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        _status.Margin = new Thickness(8, 0, 0, 0);
        footer.Children.Add(_status);

        _resizeGrip = new Thumb
        {
            Width = 24, Height = 24, Cursor = Cursors.SizeNWSE,
            Background = Paint("#aec4de"), VerticalAlignment = VerticalAlignment.Bottom,
            ToolTip = "Drag to resize this browser"
        };
        _resizeGrip.DragDelta += (_, e) =>
        {
            if (_browserWindow is null) return;
            _browserWindow.Width = Math.Clamp(_browserWindow.Width + e.HorizontalChange,
                390, Math.Max(390, _owner.ActualWidth - 16));
            _browserWindow.Height = Math.Clamp(_browserWindow.Height + e.VerticalChange,
                260, Math.Max(260, _owner.ActualHeight - 16));
        };
        _resizeGrip.DragCompleted += (_, _) =>
        {
            if (_resizeGrip.IsMouseCaptured) _resizeGrip.ReleaseMouseCapture();
            RememberBounds();
        };
        Grid.SetColumn(_resizeGrip, 1);
        footer.Children.Add(_resizeGrip);
        RefreshShortcuts();
    }

    private Button? _urlToggle;

    private void SetAddressVisibility()
    {
        if (_addressRow is null || _addressRowDefinition is null) return;
        var show = _settings.BrowserAddressBarVisible;
        _addressRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _addressRowDefinition.Height = new GridLength(show ? 43 : 0);
        if (_urlToggle is not null)
            _urlToggle.Content = show ? "URL ▴" : "URL ▾";
    }

    private static Point OwnerOrigin(Window owner)
    {
        var screen = owner.PointToScreen(new Point(0, 0));
        var transform = PresentationSource.FromVisual(owner)?
            .CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return transform.Transform(screen);
    }

    private void MigratePosition()
    {
        if (_settings.BrowserGlobalPositionMigrated) return;
        // Previous browser X/Y values were relative to the Invoice iframe.
        if (_settings.PanelX >= 0) _settings.PanelX += 210;
        if (_settings.PanelY >= 0) _settings.PanelY += 110;
        _settings.BrowserGlobalPositionMigrated = true;
        SettingsStore.Save(_settings);
    }

    public void Position()
    {
        if (_closed || _owner.WindowState == WindowState.Minimized ||
            !_owner.IsLoaded || _bubbleWindow is null || _browserWindow is null) return;
        var origin = OwnerOrigin(_owner);
        _bubbleWindow.Left = origin.X + Math.Min(160, Math.Max(8, _owner.ActualWidth - 66));
        _bubbleWindow.Top = origin.Y + 8;
        MigratePosition();
        var width = Math.Clamp(_browserWindow.Width, 390, Math.Max(390, _owner.ActualWidth - 12));
        var height = Math.Clamp(_browserWindow.Height, 260, Math.Max(260, _owner.ActualHeight - 12));
        _browserWindow.Width = width;
        _browserWindow.Height = height;
        var preferredX = _settings.PanelX >= 0 ? _settings.PanelX :
            Math.Max(8, _owner.ActualWidth - width - 20);
        var preferredY = _settings.PanelY >= 0 ? _settings.PanelY : 70;
        _browserWindow.Left = origin.X + Math.Clamp(preferredX, 8,
            Math.Max(8, _owner.ActualWidth - width - 8));
        _browserWindow.Top = origin.Y + Math.Clamp(preferredY, 8,
            Math.Max(8, _owner.ActualHeight - height - 8));
    }

    private void RememberBounds()
    {
        if (_browserWindow is null || !_owner.IsLoaded) return;
        var origin = OwnerOrigin(_owner);
        _settings.PanelX = _browserWindow.Left - origin.X;
        _settings.PanelY = _browserWindow.Top - origin.Y;
        _settings.PanelWidth = _browserWindow.Width;
        _settings.PanelHeight = _browserWindow.Height;
        SettingsStore.Save(_settings);
    }

    public void Refresh()
    {
        if (_closed || _bubbleWindow is null || _browserWindow is null) return;
        if (_owner.WindowState == WindowState.Minimized ||
            (!_owner.IsActive && !_browserWindow.IsActive &&
             !_bubbleWindow.IsActive && _dialog?.IsActive != true))
        {
            Hide();
            return;
        }
        Position();
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

    public void HideIfOtherAppActive()
    {
        if (_closed || _owner.IsActive || _browserWindow?.IsActive == true ||
            _bubbleWindow?.IsActive == true || _dialog?.IsActive == true) return;
        Hide();
    }

    public void Hide()
    {
        if (_browserWindow?.IsVisible == true) _browserWindow.Hide();
        if (_bubbleWindow?.IsVisible == true) _bubbleWindow.Hide();
        ReleaseGrip();
    }

    private void ReleaseGrip()
    {
        if (_resizeGrip?.IsMouseCaptured == true) _resizeGrip.ReleaseMouseCapture();
    }

    private void Open()
    {
        if (_closed) return;
        if (_active is null) CreateTab();
        _panelOpen = true;
        Position();
        Refresh();
    }

    private void Minimize()
    {
        RememberBounds();
        ReleaseGrip();
        _panelOpen = false;
        Refresh();
        // Tabs and authentication stay alive in the same WebView2 profile.
    }

    private void CloseAll()
    {
        RememberBounds();
        ReleaseGrip();
        DisposeTabs();
        _panelOpen = false;
        Refresh();
        // Unlike Minimize, every WebView2 tab is disposed. Reopen -> Home.
    }

    private BrowserTab? CreateTab()
    {
        if (_tabs.Count >= MaxTabs)
        {
            _status.Text = "Maximum of eight tabs. Close one before opening another.";
            return null;
        }
        var tab = new BrowserTab();
        tab.View.DefaultBackgroundColor = System.Drawing.Color.White;
        tab.View.Visibility = Visibility.Collapsed;
        _tabs.Add(tab);
        _content.Children.Add(tab.View);
        Activate(tab);
        return tab;
    }

    private void Activate(BrowserTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        _active = tab;
        foreach (var each in _tabs)
            each.View.Visibility = ReferenceEquals(each, tab) && !tab.IsHome
                ? Visibility.Visible : Visibility.Collapsed;
        _home.Visibility = tab.IsHome ? Visibility.Visible : Visibility.Collapsed;
        _address.Text = tab.IsHome ? "" : tab.View.CoreWebView2?.Source ?? tab.PendingUrl ?? "";
        RenderTabs();
    }

    private void RenderTabs()
    {
        _tabButtons.Children.Clear();
        foreach (var tab in _tabs)
        {
            var group = new StackPanel { Orientation = Orientation.Horizontal };
            var isCurrent = ReferenceEquals(tab, _active);
            var text = tab.Title.Length > 16 ? tab.Title[..15] + "…" : tab.Title;
            var open = Button(text, isCurrent ? "#225fc0" : "#ffffff",
                isCurrent ? "#ffffff" : "#214c80", 119);
            open.ToolTip = tab.Title;
            open.Margin = new Thickness(0);
            open.Click += (_, _) => Activate(tab);
            group.Children.Add(open);
            var close = Button("×", isCurrent ? "#225fc0" : "#ffffff",
                isCurrent ? "#ffffff" : "#214c80", 28);
            close.ToolTip = "Close this tab";
            close.FontSize = 14;
            close.Click += (_, _) => CloseTab(tab);
            group.Children.Add(close);
            _tabButtons.Children.Add(group);
        }
    }

    private void CloseTab(BrowserTab tab)
    {
        if (!_tabs.Remove(tab)) return;
        _content.Children.Remove(tab.View);
        tab.View.Dispose();
        if (_tabs.Count == 0) CreateTab();
        else if (ReferenceEquals(_active, tab)) Activate(_tabs[^1]);
        else RenderTabs();
    }

    private void DisposeTabs()
    {
        foreach (var timer in _clickTimers) timer.Stop();
        foreach (var tab in _tabs)
        {
            _content.Children.Remove(tab.View);
            tab.View.Dispose();
        }
        _tabs.Clear();
        _active = null;
        _environmentTask = null;
        _tabButtons.Children.Clear();
        _address.Text = "";
        _home.Visibility = Visibility.Visible;
    }

    private async Task InitializeTabAsync(BrowserTab tab)
    {
        if (tab.Ready || tab.Starting || !_tabs.Contains(tab)) return;
        tab.Starting = true;
        try
        {
            _environmentTask ??= CoreWebView2Environment.CreateAsync(
                null, SettingsStore.BrowserProfile);
            var environment = await _environmentTask;
            if (!_tabs.Contains(tab)) return;
            await tab.View.EnsureCoreWebView2Async(environment);
            if (!_tabs.Contains(tab)) return;
            var web = tab.View.CoreWebView2;
            web.Settings.AreHostObjectsAllowed = false;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.NewWindowRequested += (_, e) =>
            {
                if (!SettingsStore.TryWebAddress(e.Uri, out var requested)) return;
                e.Handled = true;
                _owner.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (CreateTab() is not null)
                        await NavigateAsync(requested.AbsoluteUri);
                }), DispatcherPriority.ContextIdle);
            };
            web.NavigationStarting += (_, e) =>
            {
                if (ReferenceEquals(_active, tab) &&
                    SettingsStore.TryWebAddress(e.Uri, out var next))
                    _address.Text = next.AbsoluteUri;
            };
            web.SourceChanged += (_, _) =>
            {
                if (ReferenceEquals(_active, tab)) SyncAddress();
            };
            web.DocumentTitleChanged += (_, _) =>
            {
                if (!_tabs.Contains(tab)) return;
                if (!string.IsNullOrWhiteSpace(web.DocumentTitle))
                {
                    tab.Title = web.DocumentTitle;
                    RenderTabs();
                }
            };
            web.NavigationCompleted += (_, e) =>
            {
                if (!ReferenceEquals(_active, tab)) return;
                SyncAddress();
                _status.Text = e.IsSuccess
                    ? "WebView2 • Device browser profile retained"
                    : "Website navigation failed";
            };
            tab.Ready = true;
            if (!string.IsNullOrWhiteSpace(tab.PendingUrl))
            {
                var url = tab.PendingUrl;
                tab.PendingUrl = null;
                web.Navigate(url);
            }
        }
        catch (Exception e)
        {
            if (ReferenceEquals(_active, tab))
                _status.Text = "Could not open browser tab: " + e.Message;
        }
        finally { tab.Starting = false; }
    }

    private void SyncAddress()
    {
        if (!SettingsStore.TryWebAddress(_active?.View.CoreWebView2?.Source,
            out var current)) return;
        _address.Text = current.AbsoluteUri;
        var safe = SettingsStore.RememberableUrl(current.AbsoluteUri);
        if (!string.Equals(_settings.BrowserUrl, safe, StringComparison.Ordinal))
        {
            _settings.BrowserUrl = safe;
            SettingsStore.Save(_settings);
        }
    }

    private async Task NavigateAsync(string value)
    {
        if (!SettingsStore.TryWebAddress(value, out var target))
        {
            _status.Text = "Enter a valid http:// or https:// website address";
            return;
        }
        if (_active is null) CreateTab();
        var tab = _active!;
        tab.IsHome = false;
        tab.Title = target.Host;
        Activate(tab);
        _address.Text = target.AbsoluteUri;
        _settings.BrowserUrl = SettingsStore.RememberableUrl(target.AbsoluteUri);
        SettingsStore.Save(_settings);
        if (!tab.Ready)
        {
            tab.PendingUrl = target.AbsoluteUri;
            await InitializeTabAsync(tab);
            return;
        }
        var web = tab.View.CoreWebView2;
        // Telegram Web A consumes chat hashes at document initialization.
        // Changing location.hash alone flashes an ID and returns to old chat.
        var telegramChat = string.Equals(target.Host, "web.telegram.org",
            StringComparison.OrdinalIgnoreCase) &&
            (target.AbsolutePath.StartsWith("/a/", StringComparison.OrdinalIgnoreCase) ||
             target.AbsolutePath.StartsWith("/k/", StringComparison.OrdinalIgnoreCase) ||
             target.AbsolutePath.StartsWith("/z/", StringComparison.OrdinalIgnoreCase)) &&
            System.Text.RegularExpressions.Regex.IsMatch(target.Fragment,
                @"^#-?\d+(?:[_?].*)?$");
        if (telegramChat &&
            SettingsStore.TryWebAddress(web.Source, out var current) &&
            string.Equals(current.GetLeftPart(UriPartial.Query),
                target.GetLeftPart(UriPartial.Query), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var exactUrl = JsonSerializer.Serialize(target.AbsoluteUri);
                await web.ExecuteScriptAsync(
                    "window.history.replaceState(window.history.state, '', " +
                    exactUrl + "); window.location.reload();");
                _status.Text = "Opening saved Telegram chat…";
                return;
            }
            catch (InvalidOperationException) { /* document changed while clicking */ }
        }
        web.Navigate(target.AbsoluteUri);
        _status.Text = "Navigating to " + target.Host + "…";
    }

    private List<Shortcut> GetShortcuts()
    {
        var result = new List<Shortcut>();
        if (_settings.TelegramShortcutVisible)
            result.Add(new Shortcut(string.IsNullOrWhiteSpace(_settings.TelegramShortcutName)
                ? "Telegram Web" : _settings.TelegramShortcutName, TelegramUrl, true));
        foreach (var url in (_settings.Bookmarks ?? new List<string>()).Take(15))
        {
            if (!SettingsStore.TryWebAddress(url, out var uri)) continue;
            var name = _settings.BookmarkTitles != null &&
                _settings.BookmarkTitles.TryGetValue(url, out var saved) &&
                !string.IsNullOrWhiteSpace(saved) ? saved : uri.Host;
            result.Add(new Shortcut(name, uri.AbsoluteUri, false));
        }
        return result;
    }

    private void RefreshShortcuts()
    {
        foreach (var timer in _clickTimers) timer.Stop();
        _clickTimers.Clear();
        _shortcutButtons.Children.Clear();
        _homeButtons.Children.Clear();
        foreach (var item in GetShortcuts())
        {
            AddShortcutButton(item);
            var homeButton = Button(item.Name, "#e9f2fe", "#214c80", 166);
            homeButton.Height = 43;
            homeButton.ToolTip = item.Url;
            homeButton.Margin = new Thickness(0, 0, 9, 9);
            homeButton.Click += async (_, _) => await NavigateAsync(item.Url);
            _homeButtons.Children.Add(homeButton);
        }
    }

    private void AddShortcutButton(Shortcut item)
    {
        var button = Button(item.Name, "#e3f0fe", "#23558a", 166);
        button.ToolTip = "Click to open • triple-click to rename • right-click to manage\n" + item.Url;
        var clicks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _clickTimers.Add(timer);
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            clicks = 0;
            await NavigateAsync(item.Url);
        };
        button.Click += (_, _) =>
        {
            timer.Stop();
            clicks++;
            if (clicks >= 3)
            {
                clicks = 0;
                RenameShortcut(item);
            }
            else timer.Start();
        };
        var context = new ContextMenu();
        var rename = new MenuItem { Header = "Rename shortcut (triple-click)" };
        rename.Click += (_, _) => { timer.Stop(); clicks = 0; RenameShortcut(item); };
        context.Items.Add(rename);
        var remove = new MenuItem { Header = "Remove shortcut" };
        remove.Click += (_, _) => { timer.Stop(); clicks = 0; RemoveShortcut(item); };
        context.Items.Add(remove);
        button.ContextMenu = context;
        _shortcutButtons.Children.Add(button);
    }

    private bool? ShowDialog(Window dialog)
    {
        _dialog = dialog;
        try { return dialog.ShowDialog(); }
        finally
        {
            _dialog = null;
            _owner.Dispatcher.BeginInvoke(new Action(Refresh), DispatcherPriority.ContextIdle);
        }
    }

    private string? AskShortcutName(string title, string initial)
    {
        if (_browserWindow is null) return null;
        var dialog = new Window
        {
            Title = title, Owner = _browserWindow,
            Width = 390, Height = 174, Background = Paint("#f4f7fc"),
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var layout = new StackPanel { Margin = new Thickness(18, 12, 18, 9) };
        layout.Children.Add(Label("Shortcut name", 12, "#234a78", true));
        var input = new TextBox
        {
            Text = initial.Length > 80 ? initial[..80] : initial,
            MaxLength = 80, Height = 32,
            Margin = new Thickness(0, 8, 0, 10)
        };
        layout.Children.Add(input);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = Button("Cancel", "#edf1f7", "#234a78", 76);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.DialogResult = false;
        actions.Children.Add(cancel);
        var save = Button("Save name", "#225fc0", "#ffffff", 92);
        save.IsDefault = true;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) { input.Focus(); return; }
            dialog.DialogResult = true;
        };
        actions.Children.Add(save);
        layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return ShowDialog(dialog) == true ? input.Text.Trim() : null;
    }

    private void SaveShortcut()
    {
        var liveUrl = _active?.View.CoreWebView2?.Source;
        if (!SettingsStore.TryWebAddress(liveUrl, out var current))
        {
            _status.Text = "Open a website before saving its shortcut";
            return;
        }
        // Explicit Save intentionally retains chat-specific query and hash.
        var url = current.AbsoluteUri;
        _settings.Bookmarks ??= new List<string>();
        _settings.BookmarkTitles ??= new Dictionary<string, string>();
        var existing = _settings.BookmarkTitles.TryGetValue(url, out var title)
            ? title : _active?.View.CoreWebView2?.DocumentTitle ?? current.Host;
        var chosen = AskShortcutName("Save website shortcut", existing);
        if (chosen is null) return;
        _settings.Bookmarks.RemoveAll(x => string.Equals(x, url, StringComparison.Ordinal));
        _settings.Bookmarks.Insert(0, url);
        _settings.Bookmarks = _settings.Bookmarks.Take(15).ToList();
        _settings.BookmarkTitles[url] = chosen;
        SettingsStore.Save(_settings);
        RefreshShortcuts();
        _status.Text = "Saved " + chosen;
    }

    private void RenameShortcut(Shortcut item)
    {
        var chosen = AskShortcutName("Rename website shortcut", item.Name);
        if (chosen is null) return;
        if (item.IsBuiltIn) _settings.TelegramShortcutName = chosen;
        else
        {
            _settings.BookmarkTitles ??= new Dictionary<string, string>();
            _settings.BookmarkTitles[item.Url] = chosen;
        }
        SettingsStore.Save(_settings);
        RefreshShortcuts();
        _status.Text = "Renamed to " + chosen;
    }

    private void RemoveShortcut(Shortcut item)
    {
        if (_browserWindow is null) return;
        var confirmation = new Window
        {
            Title = "Remove shortcut",
            Owner = _browserWindow, Width = 380, Height = 148,
            Background = Paint("#f4f7fc"),
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var content = new StackPanel { Margin = new Thickness(17) };
        content.Children.Add(Label("Remove shortcut '" + item.Name + "'?", 12, "#234a78", true));
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var cancel = Button("Cancel", "#edf1f7", "#234a78", 78);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => confirmation.DialogResult = false;
        buttons.Children.Add(cancel);
        var remove = Button("Remove", "#b23434", "#ffffff", 78);
        remove.Click += (_, _) => confirmation.DialogResult = true;
        buttons.Children.Add(remove);
        content.Children.Add(buttons);
        confirmation.Content = content;
        if (ShowDialog(confirmation) != true) return;
        if (item.IsBuiltIn) _settings.TelegramShortcutVisible = false;
        else
        {
            _settings.Bookmarks?.RemoveAll(x =>
                string.Equals(x, item.Url, StringComparison.Ordinal));
            _settings.BookmarkTitles?.Remove(item.Url);
        }
        SettingsStore.Save(_settings);
        RefreshShortcuts();
        _status.Text = "Removed shortcut " + item.Name;
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        Hide();
        DisposeTabs();
        if (_browserWindow is not null)
        {
            _browserWindow.Content = null;
            _browserWindow.Close();
        }
        if (_bubbleWindow is not null)
        {
            _bubbleWindow.Content = null;
            _bubbleWindow.Close();
        }
    }
}
