using System;
using WinUIEx;

namespace GUI.Services;

/// <summary>
/// Service for managing window visibility and minimize behavior using WinUIEx.
/// </summary>
internal sealed class WindowVisibilityService : IDisposable
{
    private readonly WindowEx _mainWindow;
    private readonly WindowManager _windowManager;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Gets a value indicating whether the main window is currently visible (not minimized or hidden).
    /// </summary>
    public bool IsVisible => _windowManager.WindowState != WindowState.Minimized 
                             && _mainWindow.Visible;

    public WindowVisibilityService(WindowEx mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        _mainWindow = mainWindow;
        _windowManager = WindowManager.Get(mainWindow);
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
