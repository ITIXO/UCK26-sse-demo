namespace SSEvsSignalR.Services;

using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Channels;
using SSEvsSignalR.Hubs;
using SSEvsSignalR.Models;

public class BroadcastService
{
    private readonly ConcurrentDictionary<Guid, Channel<ThroughputMessage>> _sseClients = new();
    private int _isBroadcasting;

    public (Guid Id, Channel<ThroughputMessage> Channel) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<ThroughputMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _sseClients[id] = channel;
        return (id, channel);
    }

    public void Unsubscribe(Guid id)
    {
        if (_sseClients.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    public bool TryStartBroadcast(IHubContext<ThroughputHub> hubContext, int count)
    {
        if (Interlocked.CompareExchange(ref _isBroadcasting, 1, 0) != 0)
            return false;

        _ = Task.Run(async () =>
        {
            try { await BroadcastAsync(hubContext, count); }
            finally { Interlocked.Exchange(ref _isBroadcasting, 0); }
        });

        return true;
    }

    private async Task BroadcastAsync(IHubContext<ThroughputHub> hubContext, int count)
    {
        var sseTask = Task.Run(() =>
        {
            for (int i = 0; i < count; i++)
            {
                var msg = new ThroughputMessage(i, count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                foreach (var ch in _sseClients.Values)
                    ch.Writer.TryWrite(msg);
            }
            var done = new ThroughputMessage(count, count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Done: true);
            foreach (var ch in _sseClients.Values)
                ch.Writer.TryWrite(done);
        });

        var signalrTask = Task.Run(async () =>
        {
            for (int i = 0; i < count; i++)
                await hubContext.Clients.All.SendAsync("ReceiveMessage",
                    new ThroughputMessage(i, count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

            await hubContext.Clients.All.SendAsync("ReceiveMessage",
                new ThroughputMessage(count, count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Done: true));
        });

        await Task.WhenAll(sseTask, signalrTask);
    }
}
