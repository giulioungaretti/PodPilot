// WinPods Device Communication - C# Implementation
// Advertisement watcher status enumeration
// File: Advertisement/AdvertisementWatcherStatus.cs

namespace DeviceCommunication.Advertisement
{
    /// <summary>
    /// Represents the operational status of an advertisement watcher.
    /// </summary>
    public enum AdvertisementWatcherStatus
    {
        /// <summary>
        /// The watcher is actively scanning for BLE advertisements.
        /// </summary>
        Started,

        /// <summary>
        /// The watcher is not scanning for advertisements.
        /// </summary>
        Stopped
    }
}
