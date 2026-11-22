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
using System.Windows.Shapes;
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
    private const string ShortcutDragFormat = "BIGBROTHER_BROWSER_SHORTCUT_REORDER_V1";
    private const string DefaultShortcutKey = "builtin:telegram-web";
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
    private readonly TextBox _address = new();
    private readonly TextBlock _status = new();
    private readonly List<BrowserTab> _tabs = new();
    private readonly List<DispatcherTimer> _clickTimers = new();
    private string? _draggingShortcutKey;
    private Task<CoreWebView2Environment>? _environmentTask;
    private BrowserTab? _active;
    private RowDefinition? _addressRowDefinition;
    private RowDefinition? _shortcutRowDefinition;
    private ScrollViewer? _shortcutScroller;
    private Button? _shortcutToggle;
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

        // Unboxed white vector globe + Browser label; no blue tile,
        // background fill or heavyweight transparent overlay window.
        var globe = new Canvas { Width = 38, Height = 38 };
        globe.Children.Add(new Ellipse
        {
            Width = 34, Height = 34, Stroke = Brushes.White,
            StrokeThickness = 1.8, Fill = Brushes.Transparent
        });
        globe.Children.Add(new Ellipse
        {
            Width = 14, Height = 34, Stroke = Brushes.White,
            StrokeThickness = 1.5, Fill = Brushes.Transparent
        });
        Canvas.SetLeft(globe.Children[1], 10);
        foreach (var y in new[] { 11.0, 23.0 })
            globe.Children.Add(new Line
            {
                X1 = 2, X2 = 32, Y1 = y, Y2 = y,
                Stroke = Brushes.White, StrokeThickness = 1.4
            });
        var launcherContent = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        launcherContent.Children.Add(globe);
        var browserLabel = Label("Browser", 11, "#ffffff", true);
        browserLabel.HorizontalAlignment = HorizontalAlignment.Center;
        browserLabel.Margin = new Thickness(0, 3, 0, 0);
        launcherContent.Children.Add(browserLabel);
        var bubble = new Button
        {
            Content = launcherContent, Width = 76, Height = 73,
            BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            Padding = new Thickness(0), Cursor = Cursors.Hand,
            ToolTip = "Open BIG BROTHER Browser from any function",
            FocusVisualStyle = null
        };
        // WPF's default Button template draws a coloured hover rectangle.
        // Replace it with a fully transparent click target.
        var template = new ControlTemplate(typeof(Button));
        var backdrop = new FrameworkElementFactory(typeof(Border));
        backdrop.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);
        backdrop.AppendChild(presenter);
        template.VisualTree = backdrop;
        bubble.Template = template;
        bubble.Click += (_, _) => Open();

        _bubbleWindow = new Window
        {
            Owner = _owner, Content = bubble,
            Width = 78, Height = 77,
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
        _shortcutRowDefinition = new RowDefinition { Height = new GridLength(43) };
        _layout.RowDefinitions.Add(_shortcutRowDefinition);
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
            Child = Label("BIG BROTHER Browser   •   drag to move", 13, "#ffffff", true)
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

        // This toggle is intentionally in the always-visible tab row, so
        // collapsing shortcuts reclaims the ENTIRE row's vertical space.
        var shortcutsToggle = Button("Shortcuts ▴", "#e4eefb", "#245b95", 106);
        shortcutsToggle.ToolTip = "Show or hide the saved website shortcut strip";
        shortcutsToggle.Click += (_, _) =>
        {
            _settings.BrowserShortcutsExpanded = !_settings.BrowserShortcutsExpanded;
            SetShortcutVisibility();
            SettingsStore.Save(_settings);
        };
        DockPanel.SetDock(shortcutsToggle, Dock.Right);
        tabsRow.Children.Add(shortcutsToggle);
        _shortcutToggle = shortcutsToggle;

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
            // Hidden, not Disabled: mouse-wheel horizontal scrolling works,
            // but the distracting scrollbar never consumes an extra row.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Paint("#f4f7fc"),
            Margin = new Thickness(8, 1, 8, 1),
            Content = _shortcutButtons,
            ToolTip = "Mouse wheel: scroll shortcuts horizontally • Hold and drag: rearrange"
        };
        shortcutRow.PreviewMouseWheel += (_, e) =>
        {
            if (shortcutRow.ScrollableWidth <= 0) return;
            var next = Math.Clamp(shortcutRow.HorizontalOffset -
                (e.Delta / 120.0) * 96.0, 0, shortcutRow.ScrollableWidth);
            shortcutRow.ScrollToHorizontalOffset(next);
            e.Handled = true;
        };
        _shortcutScroller = shortcutRow;
        Grid.SetRow(shortcutRow, 3);
        _layout.Children.Add(shortcutRow);
        SetShortcutVisibility();

        BuildBrandedHome();
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

    private static Border HomePanel(string heading, string body, string background)
    {
        var contents = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
        contents.Children.Add(Label(heading, 15, "#164880", true));
        var detail = Label(body, 12, "#526a86");
        detail.Margin = new Thickness(0, 10, 0, 0);
        detail.TextWrapping = TextWrapping.Wrap;
        contents.Children.Add(detail);
        return new Border
        {
            Width = 335, MinHeight = 155,
            Background = Paint(background),
            BorderBrush = Paint("#d5e4f4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 12, 12),
            Child = contents
        };
    }

    private static Canvas DairyArtwork()
    {
        // Lightweight WPF vector art; no external network images or downloads.
        // All decorative shapes are non-interactive.
        var art = new Canvas { Width = 276, Height = 216, IsHitTestVisible = false };
        var sky = new Ellipse { Width = 252, Height = 158, Fill = Paint("#d7efff"), Opacity = .8 };
        Canvas.SetLeft(sky, 7);
        Canvas.SetTop(sky, 14);
        art.Children.Add(sky);

        var meadow = new Ellipse { Width = 270, Height = 64, Fill = Paint("#b9e4cf") };
        Canvas.SetLeft(meadow, 3);
        Canvas.SetTop(meadow, 152);
        art.Children.Add(meadow);

        var warehouse = new Border
        {
            Width = 92, Height = 65,
            Background = Paint("#f6fbff"),
            BorderBrush = Paint("#88b3d9"), BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(5)
        };
        Canvas.SetLeft(warehouse, 14);
        Canvas.SetTop(warehouse, 100);
        art.Children.Add(warehouse);
        var roof = new Polygon
        {
            Points = new PointCollection { new(9, 103), new(60, 67), new(111, 103) },
            Fill = Paint("#326cb0")
        };
        art.Children.Add(roof);
        var whLabel = Label("BIG BROTHER", 9, "#21528b", true);
        Canvas.SetLeft(whLabel, 21);
        Canvas.SetTop(whLabel, 123);
        art.Children.Add(whLabel);

        var truckBody = new Border
        {
            Width = 111, Height = 43, Background = Paint("#ffffff"),
            BorderBrush = Paint("#3979b9"), BorderThickness = new Thickness(1.3),
            CornerRadius = new CornerRadius(6)
        };
        Canvas.SetLeft(truckBody, 90);
        Canvas.SetTop(truckBody, 153);
        art.Children.Add(truckBody);
        var truckFront = new Border
        {
            Width = 33, Height = 34, Background = Paint("#3778bd"),
            CornerRadius = new CornerRadius(6)
        };
        Canvas.SetLeft(truckFront, 184);
        Canvas.SetTop(truckFront, 162);
        art.Children.Add(truckFront);
        var truckTitle = Label("MILK", 14, "#2867a9", true);
        Canvas.SetLeft(truckTitle, 113);
        Canvas.SetTop(truckTitle, 165);
        art.Children.Add(truckTitle);
        foreach (var left in new[] { 111.0, 183.0 })
        {
            var wheel = new Ellipse { Width = 17, Height = 17, Fill = Paint("#23456e") };
            Canvas.SetLeft(wheel, left);
            Canvas.SetTop(wheel, 189);
            art.Children.Add(wheel);
        }

        var bottle = new Border
        {
            Width = 76, Height = 126, Background = Paint("#fdfefe"),
            BorderBrush = Paint("#75add3"), BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(15)
        };
        Canvas.SetLeft(bottle, 186);
        Canvas.SetTop(bottle, 42);
        art.Children.Add(bottle);
        var cap = new Border
        {
            Width = 56, Height = 17, Background = Paint("#236db4"),
            CornerRadius = new CornerRadius(5)
        };
        Canvas.SetLeft(cap, 196);
        Canvas.SetTop(cap, 34);
        art.Children.Add(cap);
        var bottleLabel = new Border
        {
            Width = 70, Height = 53, Background = Paint("#e5f2ff"),
            CornerRadius = new CornerRadius(7)
        };
        Canvas.SetLeft(bottleLabel, 189);
        Canvas.SetTop(bottleLabel, 94);
        art.Children.Add(bottleLabel);
        var fresh = Label("FRESH", 11, "#195c9d", true);
        Canvas.SetLeft(fresh, 202);
        Canvas.SetTop(fresh, 101);
        art.Children.Add(fresh);
        var milk = Label("MILK", 13, "#195c9d", true);
        Canvas.SetLeft(milk, 204);
        Canvas.SetTop(milk, 119);
        art.Children.Add(milk);
        return art;
    }

    private void BuildBrandedHome()
    {
        // This is a local, decorative Home surface. Do not manufacture live
        // financial statistics or duplicate the clickable shortcuts strip.
        _home.Background = Paint("#f6f9fe");
        var page = new StackPanel { Margin = new Thickness(18, 18, 16, 17) };

        var hero = new Border
        {
            MinHeight = 242, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = Paint("#cbdff4"),
            Background = new LinearGradientBrush(
                Colors.White, Color.FromRgb(220, 238, 255),
                new Point(0, 0), new Point(1, 1))
        };
        var heroGrid = new Grid();
        var art = DairyArtwork();
        art.HorizontalAlignment = HorizontalAlignment.Right;
        art.VerticalAlignment = VerticalAlignment.Bottom;
        art.Margin = new Thickness(0, 0, 4, 0);
        heroGrid.Children.Add(art);

        var heroText = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(25, 22, 264, 24)
        };
        heroText.Children.Add(Label("BIG BROTHER  /  CAMBODIA", 11, "#2b6eb2", true));
        var title = Label("Welcome to BIG BROTHER Browser", 25, "#103d74", true);
        title.TextWrapping = TextWrapping.Wrap;
        title.Margin = new Thickness(0, 13, 0, 0);
        heroText.Children.Add(title);
        var subtitle = Label("Your workspace for fresh milk distribution, sales, payments and business coordination.", 12, "#486483");
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.Margin = new Thickness(0, 12, 0, 0);
        heroText.Children.Add(subtitle);

        var categories = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        foreach (var caption in new[] { "Fresh Milk", "Distribution", "Accounting" })
        {
            var pill = new Border
            {
                Background = Paint("#eff7ff"),
                BorderBrush = Paint("#c9dff5"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 7, 5),
                Child = Label(caption, 11, "#235d97", true)
            };
            categories.Children.Add(pill);
        }
        heroText.Children.Add(categories);
        heroGrid.Children.Add(heroText);
        hero.Child = heroGrid;
        hero.SizeChanged += (_, e) =>
        {
            var wide = e.NewSize.Width >= 610;
            art.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            heroText.Margin = wide
                ? new Thickness(25, 22, 264, 24)
                : new Thickness(20, 23, 20, 23);
        };
        page.Children.Add(hero);

        var section = Label("BUILT FOR BIG BROTHER", 11, "#315e8d", true);
        section.Margin = new Thickness(1, 22, 0, 12);
        page.Children.Add(section);
        var areas = new WrapPanel();
        foreach (var label in new[] { "Sales & Customers", "Stock & Distribution", "Bank & Receivables" })
        {
            var chip = new Border
            {
                Background = Brushes.White,
                BorderBrush = Paint("#d7e4f1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(13, 10, 13, 10),
                Margin = new Thickness(0, 0, 9, 9),
                Child = Label(label, 12, "#24558a", true)
            };
            areas.Children.Add(chip);
        }
        page.Children.Add(areas);

        var row = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(HomePanel(
            "Fresh Milk Distribution",
            "BIG BROTHER supports daily sales, warehouse stock, purchases and delivery operations across Cambodia.",
            "#ffffff"));
        row.Children.Add(HomePanel(
            "Browser Tips",
            "Use the shortcut strip above for frequently visited websites. New Tab starts at Home. Minimize keeps pages open; Close ends the tabs.",
            "#edf6ff"));
        page.Children.Add(row);

        var note = Label(
            "BIG BROTHER Browser  •  Private device profile retained  •  Saved shortcuts stay on this computer",
            10, "#69819c");
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(2, 12, 0, 0);
        page.Children.Add(note);

        _home.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = page
        };
    }

    private Button? _urlToggle;

    private void SetShortcutVisibility()
    {
        if (_shortcutRowDefinition is null ||
            _shortcutScroller is null || _shortcutToggle is null) return;
        var expanded = _settings.BrowserShortcutsExpanded;
        _shortcutScroller.Visibility = expanded
            ? Visibility.Visible : Visibility.Collapsed;
        // Fully collapse to zero; the WebView2/Home star row grows to
        // occupy the freed space, without leaving an empty shortcuts band.
        _shortcutRowDefinition.Height = new GridLength(expanded ? 43 : 0);
        _shortcutToggle.Content = expanded ? "Shortcuts ▴" : "Shortcuts ▾";
        _shortcutToggle.ToolTip = expanded
            ? "Hide saved website shortcuts" : "Show saved website shortcuts";
    }

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
        // Align with the BIG BROTHER brand header above the sidebar menu.
        // Separate the globe/Browser label from the BIG BROTHER brand name.
        // Keep its entire 78px click target within the right edge of the
        // dark sidebar, below the headline, and above the main navigation.
        _bubbleWindow.Left = origin.X + Math.Min(165, Math.Max(8, _owner.ActualWidth - 86));
        _bubbleWindow.Top = origin.Y + 87;
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

    private static string ShortcutKey(Shortcut item) =>
        item.IsBuiltIn ? DefaultShortcutKey : item.Url;

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

        // Upgrade-friendly: older users have no saved ordering. A removed or
        // renamed shortcut never corrupts order; newly saved links appear at
        // the end of any user-defined order without resetting it.
        var preferred = _settings.BrowserShortcutOrder ?? new List<string>();
        if (preferred.Count == 0) return result;
        var ordered = new List<Shortcut>();
        foreach (var key in preferred)
        {
            var found = result.FirstOrDefault(item =>
                string.Equals(ShortcutKey(item), key, StringComparison.Ordinal));
            if (found is not null && !ordered.Contains(found))
                ordered.Add(found);
        }
        foreach (var remaining in result)
            if (!ordered.Contains(remaining)) ordered.Add(remaining);
        return ordered;
    }

    private void ReorderShortcuts(string sourceKey, string destinationKey)
    {
        var items = GetShortcuts();
        var from = items.FindIndex(item =>
            string.Equals(ShortcutKey(item), sourceKey, StringComparison.Ordinal));
        var to = items.FindIndex(item =>
            string.Equals(ShortcutKey(item), destinationKey, StringComparison.Ordinal));
        if (from < 0 || to < 0 || from == to) return;
        var moving = items[from];
        items.RemoveAt(from);
        items.Insert(to, moving);
        _settings.BrowserShortcutOrder = items.Select(ShortcutKey).ToList();
        SettingsStore.Save(_settings);
        // Do not remove any dragged WPF Buttons inside an active native
        // DoDragDrop call. Rebuild after the drag gesture has unwound.
        _owner.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_closed) return;
            RefreshShortcuts();
            _status.Text = "Shortcut order saved";
        }), DispatcherPriority.ContextIdle);
    }

    private void RefreshShortcuts()
    {
        foreach (var timer in _clickTimers) timer.Stop();
        _clickTimers.Clear();
        _shortcutButtons.Children.Clear();
        // Shortcuts appear once, in the slim top strip. The branded Home page
        // is intentionally free of duplicate shortcut tiles.
        foreach (var item in GetShortcuts())
            AddShortcutButton(item);
    }

    private void AddShortcutButton(Shortcut item)
    {
        var button = Button(item.Name, "#e3f0fe", "#23558a", 166);
        button.ToolTip = "Click: open • triple-click: rename • left-hold and drag: reorder • right-click: manage\n" +
            item.Url;
        var clicks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _clickTimers.Add(timer);
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            clicks = 0;
            await NavigateAsync(item.Url);
        };

        // WPF's standard drag/drop owns pointer capture only for this gesture.
        // Never create a global overlay or manually capture the mouse (our
        // v0.3.0 ghost-screen fix relies on normal HWND focus behavior).
        Point? startPoint = null;
        var suppressClick = false;
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            startPoint = e.GetPosition(button);
        };
        button.PreviewMouseLeftButtonUp += (_, _) => startPoint = null;
        button.PreviewMouseMove += (_, e) =>
        {
            if (startPoint is null || e.LeftButton != MouseButtonState.Pressed) return;
            var moved = e.GetPosition(button) - startPoint.Value;
            if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            startPoint = null;
            timer.Stop();
            clicks = 0;
            suppressClick = true;
            _draggingShortcutKey = ShortcutKey(item);
            try
            {
                // The drag payload carries no URL/chat identifier. Only our
                // temporary in-process source key resolves the actual item.
                DragDrop.DoDragDrop(button,
                    new DataObject(ShortcutDragFormat, "shortcut"),
                    DragDropEffects.Move);
            }
            finally
            {
                _draggingShortcutKey = null;
                button.Dispatcher.BeginInvoke(new Action(() =>
                    suppressClick = false), DispatcherPriority.ContextIdle);
            }
        };

        button.AllowDrop = true;
        button.DragOver += (_, e) =>
        {
            var source = _draggingShortcutKey;
            if (e.Data.GetDataPresent(ShortcutDragFormat) && source is not null &&
                !string.Equals(source, ShortcutKey(item), StringComparison.Ordinal))
            {
                e.Effects = DragDropEffects.Move;
                if (_shortcutScroller is not null)
                {
                    var p = e.GetPosition(_shortcutScroller);
                    if (p.X < 28) _shortcutScroller.ScrollToHorizontalOffset(
                        _shortcutScroller.HorizontalOffset - 18);
                    else if (p.X > _shortcutScroller.ActualWidth - 28)
                        _shortcutScroller.ScrollToHorizontalOffset(
                            _shortcutScroller.HorizontalOffset + 18);
                }
            }
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        };
        button.Drop += (_, e) =>
        {
            if (e.Data.GetDataPresent(ShortcutDragFormat) &&
                _draggingShortcutKey is not null)
            {
                ReorderShortcuts(_draggingShortcutKey, ShortcutKey(item));
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        };

        button.Click += (_, _) =>
        {
            if (suppressClick) return;
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
