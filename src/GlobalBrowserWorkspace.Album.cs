using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace BBDesktop;

internal sealed partial class GlobalBrowserWorkspace
{
    private readonly RowDefinition _albumRow = new() { Height = new GridLength(0) };
    private readonly TextBlock _albumHint = new();
    private DockPanel? _albumBar;
    private Button? _albumAttach;
    private Button? _albumDiscard;
    private string[]? _preparedAlbum;
    private AlbumArm? _albumArm;
    private bool _albumBusy;

    private sealed class AlbumArm
    {
        public required BrowserTab Tab { get; init; }
        public required CoreWebView2 Web { get; init; }
        public required string Address { get; init; }
        public required string[] Files { get; init; }
        public required CoreWebView2DevToolsProtocolEventReceiver Receiver { get; init; }
        public required DispatcherTimer Timer { get; init; }
        public EventHandler<CoreWebView2DevToolsProtocolEventReceivedEventArgs>? Handler { get; set; }
        public bool Consuming { get; set; }
    }

    private static bool IsPdfPackage(string[]? paths) => paths is { Length: 1 } &&
        Path.GetExtension(paths[0]).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public bool HasPreparedAlbum => _preparedAlbum is not null || _albumBusy;

    private static bool IsTelegramWeb(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var u) &&
        u.Scheme == Uri.UriSchemeHttps && u.IsDefaultPort && u.UserInfo.Length == 0 &&
        u.Host.Equals("web.telegram.org", StringComparison.OrdinalIgnoreCase);

    private void BuildAlbumBar()
    {
        // Keep sharing controls above the native WebView, not below the footer
        // where a full-height Browser may extend beyond the visible screen.
        _layout.RowDefinitions.Insert(4, _albumRow);
        var bar = new DockPanel { Background = Paint("#e2f4ec"), LastChildFill = true, ClipToBounds = true };
        _albumBar = bar;
        _albumAttach = Button("Attach album", "#14834c", "#ffffff", 116);
        _albumAttach.Height = 30;
        _albumAttach.Click += async (_, _) => await ArmAlbumAsync();
        _albumDiscard = Button("Discard", "#e9eff6", "#244363", 70);
        _albumDiscard.Height = 30;
        _albumDiscard.Click += async (_, _) =>
        {
            if (_albumBusy) return;
            await DisarmAlbumAsync();
            var discarded = _preparedAlbum;
            _preparedAlbum = null;
            if (discarded is not null) NativeAlbumTransfer.DiscardUnattached(discarded);
            RefreshAlbumBar();
        };
        DockPanel.SetDock(_albumDiscard, Dock.Right);
        bar.Children.Add(_albumDiscard);
        DockPanel.SetDock(_albumAttach, Dock.Right);
        bar.Children.Add(_albumAttach);
        _albumHint.Margin = new Thickness(8, 0, 4, 0);
        _albumHint.FontSize = 11;
        _albumHint.Foreground = Paint("#195a3d");
        _albumHint.TextWrapping = TextWrapping.Wrap;
        _albumHint.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(_albumHint);
        Grid.SetRow(bar, 4);
        _layout.Children.Add(bar);
        RefreshAlbumBar();
    }

    private void DiscardAlbumOnBrowserClose()
    {
        if (_albumBusy) return;
        _ = DisarmAlbumAsync();
        var discarded = _preparedAlbum;
        _preparedAlbum = null;
        if (discarded is not null) NativeAlbumTransfer.DiscardUnattached(discarded);
        RefreshAlbumBar();
    }

    private void RefreshAlbumBar(string? hint = null)
    {
        _albumRow.Height = new GridLength(_preparedAlbum is null ? 0 : 56);
        if (_albumBar is not null) _albumBar.Visibility = _preparedAlbum is null ? Visibility.Collapsed : Visibility.Visible;
        if (_albumAttach is null || _albumDiscard is null) return;
        _albumAttach.Content = _albumArm is null
            ? (IsPdfPackage(_preparedAlbum) ? "Attach PDF" : $"Attach {_preparedAlbum?.Length ?? 0} photos") : "Cancel attach";
        _albumAttach.IsEnabled = !_albumBusy;
        _albumDiscard.IsEnabled = !_albumBusy;
        _albumHint.Text = hint ?? (IsPdfPackage(_preparedAlbum) ? "Select the customer chat, then Attach PDF." : "Select the customer chat, then Attach photos.");
        _layout.InvalidateMeasure();
        _layout.InvalidateArrange();
    }

    public bool TrySetPreparedAlbum(string[] paths)
    {
        if (_closed || HasPreparedAlbum || paths.Length is < 1 or > 10) return false;
        _preparedAlbum = paths.ToArray();
        RefreshAlbumBar();
        _ = _owner.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await OpenTelegramShareAsync(new Uri(TelegramUrl)); }
            catch (Exception) { RefreshAlbumBar("Open Telegram in Browser, then attach the prepared file(s)."); }
        }), DispatcherPriority.ContextIdle);
        return true;
    }

    private async Task ArmAlbumAsync()
    {
        if (_albumBusy || _preparedAlbum is null) return;
        if (_albumArm is not null)
        {
            await DisarmAlbumAsync();
            RefreshAlbumBar("Attachment canceled. Choose the customer chat and retry.");
            return;
        }
        var tab = _active;
        var web = tab?.View.CoreWebView2;
        var address = web?.Source;
        var files = _preparedAlbum;
        if (tab is null || web is null || address is null || files is null || !IsTelegramWeb(address))
        {
            RefreshAlbumBar("Select a Telegram Web tab and open the correct customer chat first.");
            return;
        }
        _albumBusy = true;
        RefreshAlbumBar(IsPdfPackage(files) ? "Preparing the Telegram document picker…" : "Preparing the Telegram photo picker…");
        try
        {
            await web.CallDevToolsProtocolMethodAsync("Page.enable", "{}");
            var receiver = web.GetDevToolsProtocolEventReceiver("Page.fileChooserOpened");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            var arm = new AlbumArm {
                Tab = tab, Web = web, Address = address, Files = files.ToArray(),
                Receiver = receiver, Timer = timer
            };
            arm.Handler = async (_, args) => await HandleAlbumChooserAsync(arm, args);
            receiver.DevToolsProtocolEventReceived += arm.Handler;
            timer.Tick += async (_, _) =>
            {
                if (!ReferenceEquals(_albumArm, arm) || arm.Consuming) return;
                await DisarmAlbumAsync();
                RefreshAlbumBar("Attachment picker timed out. Click Attach to retry.");
            };
            _albumArm = arm;
            await web.CallDevToolsProtocolMethodAsync("Page.setInterceptFileChooserDialog", "{\"enabled\":true}");
            if (!ReferenceEquals(_active, tab) || web.Source != arm.Address || _closed)
                throw new InvalidOperationException("Telegram chat changed. Retry attachment.");
            timer.Start();
            RefreshAlbumBar(IsPdfPackage(arm.Files) ? "Now click Telegram’s paperclip → Document or File. Check the PDF before sending." : "Now click Telegram's paperclip → Photo or Video. Review order before sending.");
        }
        catch (Exception)
        {
            await DisarmAlbumAsync();
            RefreshAlbumBar("Could not prepare the attachment picker. Retry or use the save/folder fallback.");
        }
        finally { _albumBusy = false; if (_albumAttach is not null) _albumAttach.IsEnabled = true; if (_albumDiscard is not null) _albumDiscard.IsEnabled = true; }
    }

    private async Task HandleAlbumChooserAsync(AlbumArm arm, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
    {
        if (!ReferenceEquals(_albumArm, arm) || arm.Consuming) return;
        arm.Consuming = true;
        arm.Timer.Stop();
        _albumBusy = true;
        var isPdf = IsPdfPackage(arm.Files);
        RefreshAlbumBar(isPdf ? "Attaching the PDF…" : "Attaching the ordered photos…");
        try
        {
            if (!ReferenceEquals(_active, arm.Tab) || _browserWindow?.IsActive != true ||
                arm.Web.Source != arm.Address || !IsTelegramWeb(arm.Web.Source))
                throw new InvalidOperationException("Chat or Browser focus changed.");
            using var payload = JsonDocument.Parse(args.ParameterObjectAsJson);
            var root = payload.RootElement;
            var mode = root.GetProperty("mode").GetString();
            if ((mode != "selectMultiple" && !(isPdf && mode == "selectSingle")) ||
                !root.TryGetProperty("backendNodeId", out var backendId))
                throw new InvalidOperationException(isPdf ? "Choose Telegram’s document picker." : "Choose the multi-photo picker, not a single-file picker.");
            int id = backendId.GetInt32();
            // Never attach customer pictures to a nested third-party frame.
            using var tree = JsonDocument.Parse(await arm.Web.CallDevToolsProtocolMethodAsync("Page.getFrameTree", "{}"));
            var mainFrame = tree.RootElement.GetProperty("frameTree").GetProperty("frame");
            if (mainFrame.GetProperty("id").GetString() != root.GetProperty("frameId").GetString() ||
                !IsTelegramWeb(mainFrame.GetProperty("url").GetString()))
                throw new InvalidOperationException("Only the main Telegram Web attachment picker is permitted.");
            using var described = JsonDocument.Parse(await arm.Web.CallDevToolsProtocolMethodAsync("DOM.describeNode",
                JsonSerializer.Serialize(new { backendNodeId = id })));
            var node = described.RootElement.GetProperty("node");
            var attributes = node.GetProperty("attributes").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            string Attr(string name)
            {
                for (int i = 0; i + 1 < attributes.Length; i += 2)
                    if (attributes[i] == name) return attributes[i + 1];
                return "";
            }
            var accepts = Attr("accept");
            var acceptsPhotos = accepts.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                accepts.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
                accepts.Contains(".png", StringComparison.OrdinalIgnoreCase);
            // PDF goes only to a generic document input or an explicitly PDF-capable input.
            // An image-only or video-only picker must never receive our PDF.
            var acceptsPdf = string.IsNullOrWhiteSpace(accepts) ||
                accepts.Split(',').Any(x => x.Trim().ToLowerInvariant() is
                    "*" or "*/*" or ".pdf" or "application/pdf" or "application/*");
            if (node.GetProperty("nodeName").GetString() != "INPUT" ||
                Attr("type") != "file" || (isPdf ? !acceptsPdf : !acceptsPhotos))
                throw new InvalidOperationException(isPdf ? "Choose Telegram’s Document or File picker." : "Choose Telegram's Photo or Video picker.");
            if (!ReferenceEquals(_albumArm, arm) || !ReferenceEquals(_active, arm.Tab) ||
                arm.Web.Source != arm.Address || _browserWindow?.IsActive != true ||
                arm.Files.Any(p => !File.Exists(p)))
                throw new InvalidOperationException("Album or chat changed.");
            // Pass the files in the original A/R order; Telegram still owns its
            // preview, sorting and final Send action. No click on Send is issued.
            await arm.Web.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles",
                JsonSerializer.Serialize(new { files = arm.Files, backendNodeId = id }));
            _preparedAlbum = null;
            _status.Text = isPdf ? "PDF handed to Telegram preview. Check file and recipient before Send." : "Photos handed to Telegram preview. Check order and recipient before Send.";
        }
        catch (Exception error)
        {
            _status.Text = error is InvalidOperationException ? error.Message : "Telegram picker did not accept the album.";
        }
        finally
        {
            await DisarmAlbumAsync();
            _albumBusy = false;
            RefreshAlbumBar(_preparedAlbum is null ? null : (isPdf ? "PDF not confirmed. Choose Document or File and retry, or use Save PDF." : "Photos not confirmed. Choose Photo or Video and retry, or use folder fallback."));
        }
    }

    private async Task DisarmAlbumAsync()
    {
        var arm = _albumArm;
        _albumArm = null;
        if (arm is null) return;
        arm.Timer.Stop();
        if (arm.Handler is not null) arm.Receiver.DevToolsProtocolEventReceived -= arm.Handler;
        try { await arm.Web.CallDevToolsProtocolMethodAsync("Page.setInterceptFileChooserDialog", "{\"enabled\":false}"); }
        catch (Exception) { /* Tab may already be closed. */ }
    }

    private void CancelArmIfTabChanges(BrowserTab tab)
    {
        if (_albumArm is { } arm && !ReferenceEquals(arm.Tab, tab) && !arm.Consuming)
        {
            _ = DisarmAlbumAsync();
            RefreshAlbumBar("Tab changed. Select Telegram and retry attachment.");
        }
    }
}
