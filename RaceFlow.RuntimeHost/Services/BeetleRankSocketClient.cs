using System.Net.WebSockets;
using System.Text;

namespace RaceFlow.RuntimeHost.Services;

public sealed class BeetleRankSocketClient
{
    public async Task RunAsync(
        Uri socketUri,
        Func<string, Task> onMessage,
        CancellationToken cancellationToken = default)
    {
        using var socket = new ClientWebSocket();

        Console.WriteLine($"Connecting to WebSocket: {socketUri}");
        await socket.ConnectAsync(socketUri, cancellationToken);

        Console.WriteLine($"WebSocket connected. State={socket.State}");
        Console.WriteLine("Listening for messages...");
        Console.WriteLine();

        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            string message = await ReceiveMessageAsync(socket, buffer, cancellationToken);

            if (string.IsNullOrWhiteSpace(message))
                continue;

            await onMessage(message);
        }

        Console.WriteLine($"WebSocket closed. State={socket.State}");
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        while (true)
        {
            var segment = new ArraySegment<byte>(buffer);
            WebSocketReceiveResult result = await socket.ReceiveAsync(segment, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    cancellationToken);

                return string.Empty;
            }

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}