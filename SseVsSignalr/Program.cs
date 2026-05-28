using Microsoft.AspNetCore.SignalR;
using System.Runtime.CompilerServices;
using SSEvsSignalR.Hubs;
using SSEvsSignalR.Models;
using SSEvsSignalR.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<BroadcastService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ThroughputHub>("/hubs/throughput");

app.MapGet("/sse", (BroadcastService broadcaster, CancellationToken ct) =>
{
    var (id, channel) = broadcaster.Subscribe();

    async IAsyncEnumerable<ThroughputMessage> Stream([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        // Yield a sentinel immediately so Kestrel flushes response headers and
        // the browser's EventSource fires onopen without waiting for a test run.
        yield return new ThroughputMessage(-1, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(cancellation))
                yield return msg;
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }
    }

    return TypedResults.ServerSentEvents(Stream(ct), eventType: "message");
});

app.MapPost("/api/start", (BroadcastService broadcaster, IHubContext<ThroughputHub> hubContext, StartRequest request) =>
    broadcaster.TryStartBroadcast(hubContext, request.Count)
        ? Results.Accepted()
        : Results.Conflict("A test is already in progress"));

app.Run();

record StartRequest(int Count = 1_000_000);
