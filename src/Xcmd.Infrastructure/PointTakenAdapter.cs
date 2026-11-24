// @implements ReportControl, KeepFeedback
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace Xcmd.Infrastructure;

public sealed class PointTakenAdapter : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Func<long, bool, (int X, int Y)?, bool, Task<ChannelReader<object?>?>> begin;
    private readonly Func<long, int, int, Task<(int X, int Y, int Width, int Height, string SourceId)?>> preview;
    private readonly Func<Task> finish;
    private readonly Func<string> build;
    private readonly Action<string> trace;
    private uint tracker;
    public PointTakenAdapter(Func<long, bool, (int X, int Y)?, bool, Task<ChannelReader<object?>?>> begin,
        Func<long, int, int, Task<(int X, int Y, int Width, int Height, string SourceId)?>> preview, Func<Task> finish, Func<string> build, Action<string> trace)
    {
        this.begin = begin;
        this.preview = preview;
        this.finish = finish;
        this.build = build;
        this.trace = trace;
        _ = ListenAsync(false);
        _ = ListenAsync(true);
    }

    private static string ConfigFile => Path.Combine(Environment.GetEnvironmentVariable("POINT_TAKEN_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PointTaken"), "config.json");
    public static bool Installed => File.Exists(ConfigFile);

    public static string Shortcut()
    {
        var file = ConfigFile;
        if (!File.Exists(file)) return "Ctrl+F12";
        using var config = JsonDocument.Parse(File.ReadAllText(file));
        return config.RootElement.TryGetProperty("shortcut", out var value) ? value.GetString() ?? "Ctrl+F12" : "Ctrl+F12";
    }

    public static async Task ActivateAsync(long hwnd)
    {
        using var process = Process.GetCurrentProcess();
        using var pipe = new NamedPipeClientStream(".", "PointTaken.activate." + process.SessionId, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await pipe.ConnectAsync(800, timeout.Token);
        await Send(pipe, new { hwnd }, timeout.Token);
    }

    public void AllowTrackerFocus()
    {
        if (tracker != 0) AllowSetForegroundWindow(tracker);
    }
    private async Task ListenAsync(bool previewOnly)
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                var name = previewOnly ? "feedback.preview." : "feedback.";
                using var pipe = new NamedPipeServerStream(name + Environment.ProcessId, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                if (previewOnly) await PreviewAsync(pipe);
                else await ServeAsync(pipe);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                if (lifetime.IsCancellationRequested) return;
                trace(error.Message);
                await Task.Delay(150);
            }
        }
    }

    private async Task PreviewAsync(NamedPipeServerStream pipe)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        var (request, session) = await ReadFirstMessage(pipe, "PreviewTarget", timeout.Token);
        if (request.GetProperty("version").GetInt32() != 1) return;
        var point = request.GetProperty("screenPoint");
        var target = await preview(request.GetProperty("hwnd").GetInt64(), point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32());
        await Send(pipe, new
        {
            type = "TargetBounds",
            sessionId = session,
            bounds = target is { } r ? new { x = r.X, y = r.Y, width = r.Width, height = r.Height } : null,
            sourceId = target?.SourceId
        }, timeout.Token);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        handshake.CancelAfter(TimeSpan.FromSeconds(3));
        var (hello, session) = await ReadFirstMessage(pipe, "Hello", handshake.Token);
        if (hello.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Unsupported Point Taken version.");
        var repeat = hello.TryGetProperty("repeatTargets", out var flag) && flag.ValueKind == JsonValueKind.True;
        await Send(pipe, new { type = "HelloAck", sessionId = session, version = 1, appId = "xcmd", projectId = "xcmd", build = build(), repeatTargets = repeat, pickAtPoint = true, useAtPoint = true }, handshake.Token);
        var request = await Read(pipe, handshake.Token);
        Expect(request, "BeginPick", session);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out tracker)) throw new IOException("Could not identify Point Taken.");
        try
        {
            (int X, int Y)? point = request.TryGetProperty("screenPoint", out var p)
                ? (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32()) : null;
            var selection = await begin(request.GetProperty("hwnd").GetInt64(), repeat, point,
                request.TryGetProperty("useControl", out var use) && use.ValueKind == JsonValueKind.True);
            if (selection == null) throw new InvalidOperationException("The selected Xcmd window is no longer active.");
            await Send(pipe, new { type = "Ready", sessionId = session }, handshake.Token);
            using var watch = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var disconnected = Read(pipe, watch.Token);
            try
            {
                var hasTarget = false;
                do
                {
                    var next = selection.ReadAsync(watch.Token).AsTask();
                    if (await Task.WhenAny(next, disconnected) == disconnected) break;
                    var snapshot = await next;
                    if (snapshot == null)
                    {
                        await Send(pipe, new { type = "PickEnded", sessionId = session }, lifetime.Token);
                        if (!hasTarget) return;
                    }
                    else
                    {
                        hasTarget = true;
                        await Send(pipe, new { type = "TargetSelected", sessionId = session, snapshot }, lifetime.Token);
                    }
                }
                while (repeat);
                Expect(await disconnected, "DraftFinished", session);
            }
            finally
            {
                await watch.CancelAsync();
                try { await disconnected; }
                catch (Exception e) when (e is IOException or OperationCanceledException) { }
            }
        }
        finally { tracker = 0; await finish(); }
    }

    private static async Task<(JsonElement Message, string Session)> ReadFirstMessage(Stream pipe, string type, CancellationToken token)
    {
        var message = await Read(pipe, token);
        var session = message.GetProperty("sessionId").GetString() ?? throw new InvalidDataException("Missing session ID.");
        Expect(message, type, session);
        return (message, session);
    }

    private static void Expect(JsonElement message, string type, string session)
    {
        if (message.GetProperty("type").GetString() != type || message.GetProperty("sessionId").GetString() != session)
            throw new InvalidDataException("Unexpected Point Taken message.");
    }
    private static async Task<JsonElement> Read(Stream pipe, CancellationToken token)
    {
        var header = new byte[4];
        await pipe.ReadExactlyAsync(header, token);
        var size = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (size is <= 0 or > 262144) throw new InvalidDataException("Invalid Point Taken message size.");
        var bytes = new byte[size];
        await pipe.ReadExactlyAsync(bytes, token);
        return JsonSerializer.Deserialize<JsonElement>(bytes);
    }
    private static async Task Send(Stream pipe, object message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, WireJson);
        if (bytes.Length > 262144) throw new InvalidDataException("The target snapshot is too large.");
        await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length), token);
        await pipe.WriteAsync(bytes, token);
    }
    private static readonly JsonSerializerOptions WireJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    [DllImport("user32")] private static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("kernel32")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    public void Dispose() => lifetime.Cancel();
}
