namespace SSEvsSignalR.Models;

public record ThroughputMessage(int Index, int Total, long Timestamp, bool Done = false);
