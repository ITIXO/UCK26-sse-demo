using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SseDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<SseClientManager>();
builder.Services.AddSingleton<GameStateService>();

var app = builder.Build();

// camelCase for SSE payloads to match ASP.NET Core's JSON responses
var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// ── SSE streaming endpoint ──────────────────────────────────────────────────
app.MapGet("/sse", (SseClientManager manager, CancellationToken ct) =>
{
    var (id, reader) = manager.AddClient();
    return TypedResults.ServerSentEvents(GetEvents(id, reader, manager, ct));
});

// ── Event trigger endpoints ──────────────────────────────────────────────────
app.MapPost("/api/notify", async (NotificationRequest req, SseClientManager manager) =>
{
    if (string.IsNullOrWhiteSpace(req.Message)) return Results.BadRequest("Message is required.");
    await manager.BroadcastAsync(new SseItem<string>(req.Message.Trim(), eventType: "notification"));
    return Results.Ok(new { clients = manager.ConnectedClients });
});

app.MapPost("/api/sale", async (SaleRequest req, SseClientManager manager) =>
{
    await manager.BroadcastAsync(new SseItem<string>(req.Amount.ToString("F2"), eventType: "sale"));
    return Results.Ok(new { clients = manager.ConnectedClients });
});

app.MapPost("/api/move", async (MoveRequest req, GameStateService game, SseClientManager sse) =>
{
    var dir = req.Direction?.ToLowerInvariant();
    if (dir is not ("up" or "down" or "left" or "right"))
        return Results.BadRequest("Direction must be up, down, left, or right.");
    if (string.IsNullOrWhiteSpace(req.Id))
        return Results.BadRequest("Id is required.");
    var updated = game.Move(req.Id, dir);
    if (updated is null) return Results.NotFound("Object not found.");
    await sse.BroadcastAsync(new SseItem<string>(JsonSerializer.Serialize(updated, jsonOpts), eventType: "move"));
    return Results.Ok(updated);
});

app.MapGet("/api/state", (GameStateService game) => Results.Ok(game.GetAll()));

app.MapPost("/api/spawn", async (SpawnRequest req, GameStateService game, SseClientManager sse) =>
{
    var obj = game.Spawn(req.Type);
    await sse.BroadcastAsync(new SseItem<string>(JsonSerializer.Serialize(obj, jsonOpts), eventType: "spawn"));
    return Results.Ok(obj);
});

app.MapDelete("/api/objects/{id}", async (string id, GameStateService game, SseClientManager sse) =>
{
    if (!game.Remove(id)) return Results.NotFound("Object not found.");
    await sse.BroadcastAsync(new SseItem<string>(id, eventType: "despawn"));
    return Results.Ok();
});

app.MapDelete("/api/objects", async (GameStateService game, SseClientManager sse) =>
{
    game.Clear();
    await sse.BroadcastAsync(new SseItem<string>("", eventType: "clear"));
    return Results.Ok();
});

app.Run();

static async IAsyncEnumerable<SseItem<string>> GetEvents(
    string id,
    ChannelReader<SseItem<string>> reader,
    SseClientManager manager,
    [EnumeratorCancellation] CancellationToken ct)
{
    yield return new SseItem<string>("ok", eventType: "connected");
    try
    {
        await foreach (var item in reader.ReadAllAsync())
            yield return item;
    }
    finally
    {
        manager.RemoveClient(id);
    }
}

record NotificationRequest(string Message);
record SaleRequest(decimal Amount);
record MoveRequest(string? Id, string? Direction);
record SpawnRequest(string? Type);

