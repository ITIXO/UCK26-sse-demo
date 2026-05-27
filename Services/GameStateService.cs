using System.Collections.Concurrent;

namespace SseDemo.Services;

public record GameObject(string Id, string Type, double Px, double Py);

public class GameStateService
{
    private static readonly string[] ObjectTypes =
        ["🚀", "✈️", "🪐", "⭐", "🛸", "🐉", "🦄", "🔥", "💎", "🎯", "🐸", "🦋", "🤖", "👾",
         "🌈", "🐋", "🦊", "🧨", "🎪", "🌋", "🦅", "🐙", "🧊", "🌊", "🎸", "🏆", "🦁", "🐺", "🌙"];

    private readonly ConcurrentDictionary<string, GameObject> _objects = new();

    public GameObject Spawn(string? type = null)
    {
        var t = string.IsNullOrWhiteSpace(type)
            ? ObjectTypes[Random.Shared.Next(ObjectTypes.Length)]
            : type;
        // Spawn within the inner 40–60% of the arena so objects don't pile up at the edge
        var px = 0.3 + Random.Shared.NextDouble() * 0.4;
        var py = 0.3 + Random.Shared.NextDouble() * 0.4;
        var obj = new GameObject(Guid.NewGuid().ToString("N")[..8], t, px, py);
        _objects[obj.Id] = obj;
        return obj;
    }

    public GameObject? Move(string id, string direction)
    {
        const double step = 0.05;
        if (!_objects.TryGetValue(id, out var obj)) return null;
        var (px, py) = (obj.Px, obj.Py);
        if (direction == "up" || direction == "down" || direction == "left" || direction == "right")
        {
            // Cardinal: full step on one axis
            if (direction == "up")    py = Math.Clamp(py - step, 0, 1);
            if (direction == "down")  py = Math.Clamp(py + step, 0, 1);
            if (direction == "left")  px = Math.Clamp(px - step, 0, 1);
            if (direction == "right") px = Math.Clamp(px + step, 0, 1);
        }
        else
        {
            // Diagonal: normalize so speed matches cardinal movement
            double diag = step * 0.7071067811865476;
            if (direction == "up-left"    || direction == "up-right")   py = Math.Clamp(py - diag, 0, 1);
            if (direction == "down-left"  || direction == "down-right")  py = Math.Clamp(py + diag, 0, 1);
            if (direction == "up-left"    || direction == "down-left")   px = Math.Clamp(px - diag, 0, 1);
            if (direction == "up-right"   || direction == "down-right")  px = Math.Clamp(px + diag, 0, 1);
        }
        var updated = obj with { Px = px, Py = py };
        _objects[id] = updated;
        return updated;
    }

    public bool Remove(string id) => _objects.TryRemove(id, out _);

    public bool Exists(string id) => _objects.ContainsKey(id);

    public void Clear() => _objects.Clear();

    public IEnumerable<GameObject> GetAll() => _objects.Values;
}
