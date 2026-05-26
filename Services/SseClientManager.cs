using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Threading.Channels;

namespace SseDemo.Services;

public class SseClientManager
{
    private readonly ConcurrentDictionary<string, Channel<SseItem<string>>> _clients = new();

    public (string Id, ChannelReader<SseItem<string>> Reader) AddClient()
    {
        var id = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<SseItem<string>>(
            new UnboundedChannelOptions { SingleReader = true });
        _clients[id] = channel;
        return (id, channel.Reader);
    }

    public void RemoveClient(string id) => _clients.TryRemove(id, out _);

    public int ConnectedClients => _clients.Count;

    public async Task BroadcastAsync(SseItem<string> item)
    {
        foreach (var channel in _clients.Values)
            await channel.Writer.WriteAsync(item);
    }
}
