using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BBDesktop;

// Receives only explicitly selected A/R photo bytes, never arbitrary paths,
// credentials or commands. All paths are created by the native host.
internal sealed class NativeAlbumTransfer : IDisposable
{
    private const long MaxBytes = 65L * 1024 * 1024;
    private const long SessionLimit = 512L * 1024 * 1024;
    private const int ChunkLimit = 192 * 1024;
    private readonly GlobalBrowserWorkspace _browser;
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BIG BROTHER", "Desktop", "TemporaryAlbums");
    private string? _session;
    private FileStream? _sessionLock;
    private Transfer? _transfer;
    private long _sessionBytes;
    private bool _closed;

    private sealed class Transfer
    {
        public required CoreWebView2Frame Frame { get; init; }
        public required string Token { get; init; }
        public required string Folder { get; init; }
        public required string[] Paths { get; init; }
        public required long[] Sizes { get; init; }
        public int Index { get; set; }
        public long Offset { get; set; }
    }

    public NativeAlbumTransfer(GlobalBrowserWorkspace browser) => _browser = browser;

    internal static bool IsAllowedSource(string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var u) ||
            u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort ||
            u.UserInfo.Length != 0 ||
            !u.Host.Equals("app.bigbrother.test", StringComparison.OrdinalIgnoreCase)) return false;
        return u.AbsolutePath is "/big-brother-ar/" or "/big-brother-ar/index.html"
            or "/big-brother-ar/your-receivable.html";
    }

    public void Bind(CoreWebView2 web)
    {
        web.FrameCreated += (_, e) =>
        {
            var frame = e.Frame;
            frame.WebMessageReceived += (_, args) => Receive(frame, args);
            frame.NavigationStarting += (_, _) => CancelFrame(frame);
            frame.Destroyed += (_, _) => CancelFrame(frame);
        };
        web.NavigationStarting += (_, _) => Abort();
    }

    private void Receive(CoreWebView2Frame frame, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_closed || !IsAllowedSource(args.Source)) return;
        string? request = null;
        try
        {
            var raw = args.WebMessageAsJson;
            if (raw.Length > 300_000) return;
            using var json = JsonDocument.Parse(raw);
            var m = json.RootElement;
            if (m.ValueKind != JsonValueKind.Object ||
                !m.TryGetProperty("type", out var type) || type.GetString() != "bbNativeAlbumV1") return;
            request = m.GetProperty("request").GetString();
            if (request is null || request.Length > 80) return;
            var token = m.GetProperty("token").GetString();
            if (token is null || !Guid.TryParseExact(token, "D", out _))
                throw new InvalidOperationException("Invalid album session.");
            switch (m.GetProperty("op").GetString())
            {
                case "begin":
                    Begin(frame, token, m.GetProperty("files"));
                    break;
                case "chunk":
                    Append(Require(frame, token), m);
                    break;
                case "finish":
                    Finish(Require(frame, token));
                    break;
                case "abort":
                    Require(frame, token);
                    Abort();
                    break;
                default: throw new InvalidOperationException("Unsupported album action.");
            }
            Reply(frame, request, true, "");
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_transfer?.Frame, frame)) Abort();
            if (request is not null)
                Reply(frame, request, false, error is InvalidOperationException
                    ? error.Message : "Could not prepare the local album. Retry or use the folder fallback.");
        }
    }

    private static void Reply(CoreWebView2Frame frame, string request, bool ok, string message)
    {
        try { frame.PostWebMessageAsJson(JsonSerializer.Serialize(new {
            type = "bbNativeAlbumAckV1", request, ok, message
        })); }
        catch (Exception) { /* Frame may have navigated away. */ }
    }

    private Transfer Require(CoreWebView2Frame frame, string token) =>
        _transfer is { } t && ReferenceEquals(t.Frame, frame) && t.Token == token
            ? t : throw new InvalidOperationException("Album transfer expired. Retry.");

    private void Begin(CoreWebView2Frame frame, string token, JsonElement files)
    {
        if (_transfer is not null || _browser.HasPreparedAlbum)
            throw new InvalidOperationException("Attach or discard the previous album in Browser first.");
        if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() is < 1 or > 10)
            throw new InvalidOperationException("Prepare between 1 and 10 pictures per album.");
        var items = files.EnumerateArray().ToArray();
        var sizes = items.Select(x => x.GetProperty("size").GetInt64()).ToArray();
        if (sizes.Any(x => x <= 0 || x > MaxBytes) || sizes.Sum() > MaxBytes ||
            _sessionBytes + sizes.Sum() > SessionLimit)
            throw new InvalidOperationException("Album storage limit reached. Finish sharing and reopen the app, or use folder fallback.");
        var names = new List<string>();
        for (int i = 0; i < items.Length; i++)
        {
            var ext = items[i].GetProperty("extension").GetString()?.ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp"))
                throw new InvalidOperationException("This album accepts pictures only. Use the original-document fallback for PDFs.");
            var summary = items[i].TryGetProperty("summary", out var flag) &&
                flag.ValueKind == JsonValueKind.True;
            names.Add($"{i + 1:D2}_{(summary && i == 0 ? "Customer_Summary" : "Real_Invoice")}{ext}");
        }
        EnsureSession();
        var folder = Path.Combine(_session!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _transfer = new Transfer {
            Frame = frame, Token = token, Folder = folder,
            Paths = names.Select(name => Path.Combine(folder, name)).ToArray(), Sizes = sizes
        };
    }

    private static void Append(Transfer t, JsonElement m)
    {
        var index = m.GetProperty("index").GetInt32();
        var offset = m.GetProperty("offset").GetInt64();
        if (index != t.Index || index >= t.Paths.Length || offset != t.Offset)
            throw new InvalidOperationException("Album data arrived out of order.");
        var encoded = m.GetProperty("data").GetString() ?? "";
        if (encoded.Length > 4 * (ChunkLimit / 3 + 1))
            throw new InvalidOperationException("Invalid picture chunk.");
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length == 0 || bytes.Length > ChunkLimit ||
            t.Offset + bytes.Length > t.Sizes[index])
            throw new InvalidOperationException("Invalid picture size.");
        using (var stream = new FileStream(t.Paths[index], FileMode.Append, FileAccess.Write, FileShare.None))
        {
            if (stream.Length != offset) throw new InvalidOperationException("Album offset mismatch.");
            stream.Write(bytes);
        }
        t.Offset += bytes.Length;
        if (t.Offset == t.Sizes[index]) { t.Index++; t.Offset = 0; }
    }

    private void Finish(Transfer t)
    {
        if (t.Index != t.Paths.Length || t.Offset != 0 ||
            t.Paths.Where((p, i) => new FileInfo(p).Length != t.Sizes[i]).Any())
            throw new InvalidOperationException("Album is incomplete.");
        if (!_browser.TrySetPreparedAlbum(t.Paths))
            throw new InvalidOperationException("Attach or discard the previous album in Browser first.");
        _sessionBytes += t.Sizes.Sum();
        _transfer = null;
    }

    private void EnsureSession()
    {
        if (_session is not null) return;
        Directory.CreateDirectory(_root);
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Temporary album storage cannot be a redirected folder.");
        // A crashed session is cleaned on the next use, only when old and unlocked.
        foreach (var d in new DirectoryInfo(_root).EnumerateDirectories())
        {
            if (!Guid.TryParseExact(d.Name, "N", out _) ||
                d.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-24) ||
                (d.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            try
            {
                using (var lease = new FileStream(Path.Combine(d.FullName, "session.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                DeleteFolder(d.FullName);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var session = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        _sessionLock = new FileStream(Path.Combine(session, "session.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        _session = session;
    }

    private void CancelFrame(CoreWebView2Frame frame)
    {
        if (ReferenceEquals(_transfer?.Frame, frame)) Abort();
    }

    internal static void DiscardUnattached(string[] paths)
    {
        // Only a native-created album directory containing the supplied files.
        if (paths.Length == 0) return;
        var folder = Path.GetDirectoryName(paths[0]);
        if (folder is null || !Guid.TryParseExact(Path.GetFileName(folder), "N", out _) ||
            paths.Any(p => Path.GetDirectoryName(p) != folder)) return;
        var session = Path.GetDirectoryName(folder);
        if (session is null || !Guid.TryParseExact(Path.GetFileName(session), "N", out _)) return;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIG BROTHER", "Desktop", "TemporaryAlbums");
        if (!string.Equals(Path.GetDirectoryName(session), root, StringComparison.OrdinalIgnoreCase)) return;
        DeleteFolder(folder);
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            // Never traverse a directory link or remove anything outside our
            // own generated album/session tree.
            var info = new DirectoryInfo(folder);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) return;
            foreach (var file in info.EnumerateFiles())
                if ((file.Attributes & FileAttributes.ReparsePoint) == 0) file.Delete();
            foreach (var child in info.EnumerateDirectories())
                if (Guid.TryParseExact(child.Name, "N", out _)) DeleteFolder(child.FullName);
            info.Delete(false);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Abort()
    {
        var t = _transfer;
        _transfer = null;
        if (t is not null) DeleteFolder(t.Folder);
    }

    public void Dispose()
    {
        _closed = true;
        Abort();
        _sessionLock?.Dispose();
        if (_session is not null) DeleteFolder(_session);
    }
}
