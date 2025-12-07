using System;
using WinUIEx;

namespace GUI.Services;

/// <summary>
/// Service for managing window visibility and minimize behavior using WinUIEx.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly WindowEx _mainWindow;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(WindowEx mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        _mainWindow = mainWindow;
    }

    public void Show()
    {
        _mainWindow.Show();
        _mainWindow.BringToFront();
    }

    public void Hide()
    {
        _mainWindow.Hide();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
    }
}
