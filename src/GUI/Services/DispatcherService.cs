using System;
using Microsoft.UI.Dispatching;
using PodPilot.Core.Services;

namespace GUI.Services;

/// <summary>
/// WinUI3 implementation of <see cref="IDispatcherService"/> that wraps <see cref="DispatcherQueue"/>.
/// </summary>
public sealed class DispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;
    
    public DispatcherService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }
    
    /// <inheritdoc />
    public bool HasAccess => _dispatcherQueue.HasThreadAccess;
    
    /// <inheritdoc />
    public void TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }
}
