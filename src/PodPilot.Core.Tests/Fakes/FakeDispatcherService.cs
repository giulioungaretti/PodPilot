using PodPilot.Core.Services;

namespace PodPilot.Core.Tests.Fakes;

/// <summary>
/// Fake implementation of <see cref="IDispatcherService"/> for unit testing.
/// Executes actions synchronously on the calling thread.
/// </summary>
public sealed class FakeDispatcherService : IDispatcherService
{
    /// <inheritdoc />
    public bool HasAccess => true;

    /// <inheritdoc />
    public void TryEnqueue(Action action) => action();
}
