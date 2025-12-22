namespace Tools.EventGrid;

/// <summary>
/// Interface for Event Grid client operations.
/// </summary>
public interface IEventGridClient
{
    /// <summary>
    /// Posts an event to Event Grid.
    /// </summary>
    Task<(bool Success, string ResponseBody)> PostEventsAsync<T>(
        T[] events, 
        CancellationToken cancellationToken = default);
}
