using System.Runtime.InteropServices;

namespace DeviceCommunication.Services;

/// <summary>
/// COM interop wrapper for the undocumented IPolicyConfig interface.
/// Used to set the default audio endpoint on Windows.
/// </summary>
/// <remarks>
/// <para>
/// This interface is undocumented and unsupported by Microsoft. It has worked
/// reliably from Windows Vista through Windows 11, but may break in future updates.
/// </para>
/// <para>
/// The same technique is used by popular utilities like SoundSwitch, AudioSwitch,
/// and AudioDeviceCmdlets PowerShell module.
/// </para>
/// </remarks>
internal static class PolicyConfigClient
{
    /// <summary>
    /// Audio device roles for SetDefaultEndpoint.
    /// </summary>
    internal enum ERole : uint
    {
        /// <summary>Games, system sounds, command line.</summary>
        Console = 0,
        /// <summary>Music, movies, narration.</summary>
        Multimedia = 1,
        /// <summary>Voice communications (calls).</summary>
        Communications = 2
    }

    /// <summary>
    /// Sets the default audio endpoint for the specified role.
    /// </summary>
    /// <param name="deviceId">The audio device ID (from MediaDevice.GetAudioRenderSelector enumeration).</param>
    /// <param name="role">The role to set the default for.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    internal static bool SetDefaultEndpoint(string deviceId, ERole role)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        try
        {
            var policyConfig = (IPolicyConfig)new PolicyConfigClass();
            int hr = policyConfig.SetDefaultEndpoint(deviceId, role);
            return hr >= 0; // S_OK or other success codes
        }
        catch (COMException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PolicyConfigClient] COM error setting default endpoint: 0x{ex.HResult:X8} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PolicyConfigClient] Error setting default endpoint: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets the default audio endpoint for all roles (Console, Multimedia, Communications).
    /// </summary>
    /// <param name="deviceId">The audio device ID.</param>
    /// <returns>True if all roles were set successfully; otherwise, false.</returns>
    internal static bool SetDefaultEndpointForAllRoles(string deviceId)
    {
        bool success = true;
        success &= SetDefaultEndpoint(deviceId, ERole.Console);
        success &= SetDefaultEndpoint(deviceId, ERole.Multimedia);
        success &= SetDefaultEndpoint(deviceId, ERole.Communications);
        return success;
    }

    #region COM Interop Declarations

    // IPolicyConfig interface GUID - this is the Windows 10/11 version
    // Earlier versions may use different GUIDs (IPolicyConfigVista, etc.)
    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // We only need SetDefaultEndpoint, but must declare preceding methods
        // to maintain correct vtable order

        [PreserveSig]
        int GetMixFormat(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [Out] out IntPtr ppFormat);

        [PreserveSig]
        int GetDeviceFormat(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
            [Out] out IntPtr ppFormat);

        [PreserveSig]
        int ResetDeviceFormat(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

        [PreserveSig]
        int SetDeviceFormat(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In] IntPtr pEndpointFormat,
            [In] IntPtr mixFormat);

        [PreserveSig]
        int GetProcessingPeriod(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
            [Out] out long pmftDefaultPeriod,
            [Out] out long pmftMinimumPeriod);

        [PreserveSig]
        int SetProcessingPeriod(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In] long pmftPeriod);

        [PreserveSig]
        int GetShareMode(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [Out] out IntPtr pMode);

        [PreserveSig]
        int SetShareMode(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In] IntPtr mode);

        [PreserveSig]
        int GetPropertyValue(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
            [In] IntPtr pKey,
            [Out] out IntPtr pv);

        [PreserveSig]
        int SetPropertyValue(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
            [In] IntPtr pKey,
            [In] IntPtr pv);

        [PreserveSig]
        int SetDefaultEndpoint(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In] ERole role);

        [PreserveSig]
        int SetEndpointVisibility(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            [In][MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    // PolicyConfig COM class
    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClass
    {
    }

    #endregion
}
