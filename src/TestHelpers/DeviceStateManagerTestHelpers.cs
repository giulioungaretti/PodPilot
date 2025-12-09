using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PodPilot.Core.Models;
using PodPilot.Core.Services;

namespace TestHelpers;

/// <summary>
/// Helper methods for testing components that depend on IDeviceStateManager.
/// Provides factory methods to create real AirPodsStateService and DeviceStateManager
/// with mocked low-level dependencies.
/// </summary>
public static class DeviceStateManagerTestHelpers
{
    /// <summary>
    /// Creates a fully wired test fixture with real AirPodsStateService and DeviceStateManager.
    /// Use the returned mocks to simulate device events via AirPodsStateServiceTestHelpers.
    /// </summary>
    public static DeviceStateManagerTestFixture CreateTestFixture()
    {
        var logger = Substitute.For<ILogger<AirPodsStateService>>();
        var pairedDeviceWatcher = Substitute.For<IPairedDeviceWatcher>();
        var bleDataProvider = Substitute.For<IBleDataProvider>();
        var audioOutputMonitor = Substitute.For<IDefaultAudioOutputMonitorService>();
        var dispatcher = new SynchronousDispatcherService();

        // Setup: Return empty list initially
        pairedDeviceWatcher.GetPairedDevices().Returns(Array.Empty<PairedDeviceInfo>() as IReadOnlyList<PairedDeviceInfo>);

        var airPodsStateService = new AirPodsStateService(
            logger,
            pairedDeviceWatcher,
            bleDataProvider,
            audioOutputMonitor);

        var stateManagerLogger = Substitute.For<ILogger<DeviceStateManager>>();
        var deviceStateManager = new DeviceStateManager(
            stateManagerLogger,
            dispatcher,
            airPodsStateService);

        return new DeviceStateManagerTestFixture
        {
            DeviceStateManager = deviceStateManager,
            AirPodsStateService = airPodsStateService,
            PairedDeviceWatcher = pairedDeviceWatcher,
            BleDataProvider = bleDataProvider,
            AudioOutputMonitor = audioOutputMonitor,
            Dispatcher = dispatcher
        };
    }
}

/// <summary>
/// Contains all components of a test fixture for DeviceStateManager testing.
/// </summary>
public sealed class DeviceStateManagerTestFixture : IAsyncDisposable
{
    public required IDeviceStateManager DeviceStateManager { get; init; }
    public required IAirPodsStateService AirPodsStateService { get; init; }
    public required IPairedDeviceWatcher PairedDeviceWatcher { get; init; }
    public required IBleDataProvider BleDataProvider { get; init; }
    public required IDefaultAudioOutputMonitorService AudioOutputMonitor { get; init; }
    public required IDispatcherService Dispatcher { get; init; }

    public async Task StartAsync()
    {
        await DeviceStateManager.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        DeviceStateManager.Dispose();
        AirPodsStateService.Dispose();
    }
}

/// <summary>
/// Synchronous dispatcher for unit tests. Executes actions immediately on the calling thread.
/// </summary>
public sealed class SynchronousDispatcherService : IDispatcherService
{
    public bool HasAccess => true;
    public void TryEnqueue(Action action) => action();
}
