using System;
using PodPilot.Core.Models;

namespace GUI.Services;

/// <summary>
/// Monitors for paired AirPods devices and raises events on connection state changes.
/// </summary>
public interface IBackgroundDeviceMonitoringService : IDisposable
{
    /// <summary>
    /// Occurs when a paired device is detected and needs attention.
    /// </summary>
    event EventHandler<AirPodsState>? PairedDeviceDetected;

    /// <summary>
    /// Starts monitoring for device connections.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring for device connections.
    /// </summary>
    void Stop();

    /// <summary>
    /// Resets notification tracking, allowing devices to trigger notifications again.
    /// </summary>
    void ResetNotifications();
}
