namespace PodPilot.Core.Services;

/// <summary>
/// Abstraction for dispatching work to the UI thread.
/// Allows ViewModels to be tested without WinUI3 dependencies.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Queues work to run on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    void TryEnqueue(Action action);

    /// <summary>
    /// Gets whether the current thread has access to the UI thread.
    /// </summary>
    bool HasAccess { get; }
}
