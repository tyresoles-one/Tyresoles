using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Tyresoles.Web.Features.RemoteAssist;

/// <summary>Relays WebRTC signaling messages between host and viewer for a session.
/// Buffers messages when the peer is not connected yet (e.g. viewer sends <c>ready</c> before host finishes capture).
/// Drops <c>control</c> messages from the viewer unless the host has approved remote control via the API.</summary>
public sealed class RemoteAssistSignalingHub
{
    private const int MaxBufferPerSide = 100;
    private readonly RemoteAssistControlGate _controlGate;
    private readonly ConcurrentDictionary<Guid, Room> _rooms = new();

    public RemoteAssistSignalingHub(RemoteAssistControlGate controlGate)
    {
        _controlGate = controlGate;
    }

    public bool TryRegister(Guid sessionId, bool isHost, WebSocket socket, out string? error)
    {
        error = null;
        var room = _rooms.GetOrAdd(sessionId, _ => new Room());
        return room.TryRegister(isHost, socket, out error);
    }

    /// <summary>Deliver messages that arrived while this peer was not connected.</summary>
    public async Task FlushPendingAsync(Guid sessionId, bool isHost, WebSocket socket, CancellationToken cancellationToken)
    {
        if (!_rooms.TryGetValue(sessionId, out var room))
            return;
        await room.FlushPendingAsync(isHost, socket, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(Guid sessionId, bool isHost, WebSocket socket)
    {
        if (_rooms.TryGetValue(sessionId, out var room))
        {
            room.Remove(isHost, socket);
            if (room.IsEmpty)
                _rooms.TryRemove(sessionId, out _);
        }
    }

    public async Task RelayLoopAsync(Guid sessionId, bool isHost, WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 512];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var ms = new MemoryStream();
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var payload = ms.ToArray();
                if (!isHost && IsControlMessage(payload) && !_controlGate.IsRelayAllowed(sessionId))
                    continue;

                if (!_rooms.TryGetValue(sessionId, out var room))
                    continue;

                var peer = room.GetPeer(isHost);
                if (peer is null || peer.State != WebSocketState.Open)
                {
                    room.EnqueueForPeer(isHost, payload);
                    continue;
                }

                await peer.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }
        }
    }

    private static bool IsControlMessage(ReadOnlySpan<byte> utf8)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8.ToArray());
            return doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "control";
        }
        catch
        {
            return false;
        }
    }

    private sealed class Room
    {
        private WebSocket? _host;
        private WebSocket? _viewer;
        private readonly ConcurrentQueue<byte[]> _pendingForHost = new();
        private readonly ConcurrentQueue<byte[]> _pendingForViewer = new();
        private readonly object _lock = new();

        public bool IsEmpty => _host is null && _viewer is null;

        public bool TryRegister(bool isHost, WebSocket socket, out string? error)
        {
            error = null;
            WebSocket? oldSocket = null;
            lock (_lock)
            {
                if (isHost)
                {
                    if (_host is not null && _host.State == WebSocketState.Open)
                    {
                        oldSocket = _host;
                    }
                    _host = socket;
                }
                else
                {
                    if (_viewer is not null && _viewer.State == WebSocketState.Open)
                    {
                        oldSocket = _viewer;
                    }
                    _viewer = socket;
                }
            }
            
            if (oldSocket is not null)
            {
                // Forcefully close the old stalled connection in the background so the new one takes over immediately
                _ = Task.Run(async () =>
                {
                    try { await oldSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Replaced by new connection", CancellationToken.None); }
                    catch { /* ignore */ }
                });
            }
            
            return true;
        }

        public void EnqueueForPeer(bool senderIsHost, byte[] payload)
        {
            var q = senderIsHost ? _pendingForViewer : _pendingForHost;
            if (q.Count >= MaxBufferPerSide)
            {
                q.TryDequeue(out _);
            }
            q.Enqueue(payload);
        }

        public async Task FlushPendingAsync(bool isHost, WebSocket socket, CancellationToken cancellationToken)
        {
            var q = isHost ? _pendingForHost : _pendingForViewer;
            while (q.TryDequeue(out var payload))
            {
                if (socket.State != WebSocketState.Open)
                    break;
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Remove(bool isHost, WebSocket socket)
        {
            lock (_lock)
            {
                if (isHost && ReferenceEquals(_host, socket))
                    _host = null;
                if (!isHost && ReferenceEquals(_viewer, socket))
                    _viewer = null;
            }
        }

        public WebSocket? GetPeer(bool senderIsHost) => senderIsHost ? _viewer : _host;
    }
}
