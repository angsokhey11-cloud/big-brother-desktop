using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace BBDesktop;

internal sealed partial class GlobalBrowserWorkspace
{
    private Button? _pdfButton;
    private string? _lastDownloadedPdf;

    private void BuildPdfButton(DockPanel tabsRow)
    {
        _pdfButton = Button("Open PDF", "#e4eefb", "#244363", 82);
        _pdfButton.Margin = new Thickness(3, 1, 4, 1);
        _pdfButton.ToolTip = "View the latest downloaded Telegram PDF or choose a local PDF";
        _pdfButton.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = _pdfButton };
            var latest = new MenuItem {
                Header = "View latest Telegram PDF",
                IsEnabled = _lastDownloadedPdf is not null && File.Exists(_lastDownloadedPdf)
            };
            latest.Click += async (_, _) =>
            {
                var path = _lastDownloadedPdf;
                if (path is not null) await OpenPdfAsync(path);
            };
            var choose = new MenuItem { Header = "Choose a PDF file…" };
            choose.Click += async (_, _) =>
            {
                var dialog = new OpenFileDialog {
                    Filter = "PDF documents (*.pdf)|*.pdf",
                    CheckFileExists = true, Multiselect = false
                };
                if (dialog.ShowDialog(_browserWindow) == true)
                    await OpenPdfAsync(dialog.FileName);
            };
            menu.Items.Add(latest);
            menu.Items.Add(choose);
            menu.IsOpen = true;
        };
        DockPanel.SetDock(_pdfButton, Dock.Right);
        tabsRow.Children.Add(_pdfButton);
    }

    // Telegram Web can download documents rather than opening them inline.
    // Keep its normal download workflow. Offer an explicit native View action
    // only after the actual download has completed; never auto-open content.
    private void TrackTelegramPdfDownload(CoreWebView2 web, CoreWebView2DownloadStartingEventArgs e)
    {
        if (!IsTelegramWeb(web.Source)) return;
        var operation = e.DownloadOperation;
        if (!string.Equals(operation.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetExtension(e.ResultFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            return;
        void OnStateChanged(object? sender, object args)
        {
            if (operation.State == CoreWebView2DownloadState.InProgress) return;
            operation.StateChanged -= OnStateChanged;
            if (_closed || operation.State != CoreWebView2DownloadState.Completed) return;
            var path = operation.ResultFilePath;
            if (!IsLocalPdf(path)) return;
            _lastDownloadedPdf = path;
            if (_pdfButton is not null) _pdfButton.Content = "View PDF";
            _status.Text = "Telegram PDF downloaded. Click View PDF to open it inside Browser.";
        }
        operation.StateChanged += OnStateChanged;
        OnStateChanged(operation, EventArgs.Empty);
    }

    private static bool IsLocalPdf(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
                path.StartsWith(@"\\", StringComparison.Ordinal) ||
                !Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return false;
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[5];
            return stream.Read(header) == 5 && header.SequenceEqual("%PDF-"u8);
        }
        catch (Exception) { return false; }
    }

    private async Task OpenPdfAsync(string path)
    {
        if (_closed || !IsLocalPdf(path))
        {
            _status.Text = "The local PDF is unavailable or invalid.";
            return;
        }
        try
        {
            var address = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            var existing = _tabs.Find(tab => string.Equals(
                tab.View.CoreWebView2?.Source ?? tab.PendingUrl, address, StringComparison.Ordinal));
            if (existing is not null) { Activate(existing); return; }
            var tab = CreateTab();
            if (tab is null) return;
            tab.IsHome = false;
            tab.Title = Path.GetFileName(path);
            Activate(tab);
            // No file URI is passed through the website address parser or saved
            // as a shortcut. Only a native-selected or completed local PDF.
            await InitializeTabAsync(tab);
            if (!_tabs.Contains(tab) || !tab.Ready) return;
            tab.View.CoreWebView2.Navigate(address);
            _address.Text = address;
            _status.Text = "Opening PDF in Browser…";
        }
        catch (Exception) { _status.Text = "Could not open the PDF in Browser."; }
    }

    private static bool IsTelegramPreviewPopup(string? opener, string? target, bool userInitiated)
    {
        if (!IsTelegramWeb(opener) || string.IsNullOrEmpty(target)) return false;
        // Telegram may create a blank window and fill it after a user's click.
        if (target == "about:blank") return userInitiated;
        // A temporary document URL is meaningful only in the original browser
        // environment. Restrict these popups to Telegram's own blob origin.
        return target.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(target[5..], UriKind.Absolute, out var origin) &&
            origin.Scheme == Uri.UriSchemeHttps && origin.IsDefaultPort &&
            origin.UserInfo.Length == 0 &&
            origin.Host.Equals("web.telegram.org", StringComparison.OrdinalIgnoreCase);
    }

    private async Task OpenTelegramPreviewPopupAsync(CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        BrowserTab? popup = null;
        try
        {
            e.Handled = true;
            popup = CreateTab();
            if (popup is null) return;
            popup.IsHome = false;
            popup.Title = "Telegram preview";
            Activate(popup);
            await InitializeTabAsync(popup);
            if (!_tabs.Contains(popup) || !popup.Ready || _closed) return;
            // Let WebView2 attach the actual popup (including its opener/blob
            // context). Do not manually navigate the new WebView beforehand.
            e.NewWindow = popup.View.CoreWebView2;
            e.Handled = false;
        }
        catch (Exception)
        {
            if (popup is not null && _tabs.Contains(popup)) CloseTab(popup);
            _status.Text = "Could not open Telegram preview. Download the PDF and use Open PDF.";
        }
        finally { deferral.Complete(); }
    }
}
