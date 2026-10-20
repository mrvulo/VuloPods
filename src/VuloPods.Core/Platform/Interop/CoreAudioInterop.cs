using global::System.Runtime.InteropServices;

namespace VuloPods.Core.Platform.Interop;

// COM-Deklarationen für MMDevice-API, DeviceTopology-API und Kernel-Streaming.
// Vtable-Reihenfolge exakt nach Windows SDK 10.0.26100:
//   mmdeviceapi.h    IMMDevice (Z. 426), IMMDeviceCollection (Z. 567), IMMDeviceEnumerator (Z. 756),
//                    MMDeviceEnumerator-CLSID (Z. 1540)
//   propsys.h        IPropertyStore (Z. 516)
//   devicetopology.h IKsControl (Z. 584), IConnector (Z. 3300), IDeviceTopology (Z. 3735)
//   ks.h             KSPROPERTY/KSIDENTIFIER (Z. 114), KSPROPERTY_TYPE_GET (Z. 143)
//   ksmedia.h        KSPROPSETID_BtAudio, KSPROPERTY_BTAUDIO (Z. 1569-1577)
//   functiondiscoverykeys_devpkey.h PKEY_Device_ContainerId (Z. 115), PKEY_DeviceInterface_FriendlyName (Z. 210)

internal static class CoreAudioConstants
{
    public static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid IidIDeviceTopology = new("2A07407E-6497-4A18-9787-32F79BD0D98F");
    public static readonly Guid IidIKsControl = new("28F54685-06FD-11D2-B27A-00A0C9223196");

    public static readonly Guid KsPropSetIdBtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

    public const uint KsPropertyOneShotReconnect = 0;
    public const uint KsPropertyOneShotDisconnect = 1;
    public const uint KsPropertyTypeGet = 0x00000001;

    public const int EDataFlowAll = 2;
    public const uint DeviceStateActive = 0x00000001;
    public const uint DeviceStateUnplugged = 0x00000008;

    public const uint ClsCtxAll = 0x17;
    public const uint StgmRead = 0;

    public const int SOk = 0;
    public const int ENotFound = unchecked((int)0x80070490);

    public const ushort VtEmpty = 0;
    public const ushort VtLpwstr = 31;
    public const ushort VtClsid = 72;

    public static readonly PropertyKey PkeyDeviceContainerId =
        new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);

    public static readonly PropertyKey PkeyDeviceInterfaceFriendlyName =
        new(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FmtId;
    public uint PId;

    public PropertyKey(Guid fmtId, uint pid)
    {
        FmtId = fmtId;
        PId = pid;
    }
}

/// <summary>
/// Minimale PROPVARIANT-Abbildung: 8 Byte Header (vt + 3 reservierte WORDs) plus
/// 16 Byte Union auf x64 (CA*-Varianten: ULONG + Zeiger), 8 Byte auf x86.
/// Nur VT_LPWSTR und VT_CLSID werden ausgewertet; danach immer <see cref="Clear"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort Vt;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public IntPtr Data;
    public IntPtr Data2;

    public readonly string? GetLpwstr() =>
        Vt == CoreAudioConstants.VtLpwstr && Data != IntPtr.Zero
            ? Marshal.PtrToStringUni(Data)
            : null;

    public readonly Guid? GetClsid() =>
        Vt == CoreAudioConstants.VtClsid && Data != IntPtr.Zero
            ? Marshal.PtrToStructure<Guid>(Data)
            : null;

    public void Clear()
    {
        if (Vt != CoreAudioConstants.VtEmpty)
        {
            _ = NativeMethods.PropVariantClear(ref this);
        }
    }
}

/// <summary>KSPROPERTY / KSIDENTIFIER: GUID Set, ULONG Id, ULONG Flags = 24 Byte.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsProperty
{
    public Guid Set;
    public uint Id;
    public uint Flags;

    public const uint Size = 24;
}

internal static class NativeMethods
{
    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection? devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice? device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int Item(uint index, out IMMDevice? device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out IPropertyStore? properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? id);

    [PreserveSig]
    int GetState(out uint state);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("2A07407E-6497-4A18-9787-32F79BD0D98F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceTopology
{
    [PreserveSig]
    int GetConnectorCount(out uint count);

    [PreserveSig]
    int GetConnector(uint index, out IConnector? connector);

    [PreserveSig]
    int GetSubunitCount(out uint count);

    // Platzhalter: ISubunit wird nicht benötigt.
    [PreserveSig]
    int GetSubunit(uint index, out IntPtr subunit);

    // Platzhalter: IPart wird nicht benötigt.
    [PreserveSig]
    int GetPartById(uint id, out IntPtr part);

    [PreserveSig]
    int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string? deviceId);

    // Platzhalter: IPart/IPartsList werden nicht benötigt.
    [PreserveSig]
    int GetSignalPath(IntPtr partFrom, IntPtr partTo, int rejectMixedPaths, out IntPtr parts);
}

[ComImport]
[Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IConnector
{
    // Slot 3 = IConnector::GetType (Name hier abweichend, um object.GetType nicht zu verdecken).
    [PreserveSig]
    int GetConnectorType(out int type);

    [PreserveSig]
    int GetDataFlow(out int flow);

    // Platzhalter: IConnector-Argument wird nicht benötigt.
    [PreserveSig]
    int ConnectTo(IntPtr connectTo);

    [PreserveSig]
    int Disconnect();

    [PreserveSig]
    int IsConnected(out int connected);

    // Platzhalter: IConnector-Rückgabe wird nicht benötigt.
    [PreserveSig]
    int GetConnectedTo(out IntPtr connectedTo);

    [PreserveSig]
    int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string? connectorId);

    [PreserveSig]
    int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string? deviceId);
}

[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty(ref KsProperty property, uint propertyLength, IntPtr propertyData, uint dataLength,
        out uint bytesReturned);

    // Platzhalter: KSMETHOD wird nicht benötigt.
    [PreserveSig]
    int KsMethod(IntPtr method, uint methodLength, IntPtr methodData, uint dataLength, out uint bytesReturned);

    // Platzhalter: KSEVENT wird nicht benötigt.
    [PreserveSig]
    int KsEvent(IntPtr evt, uint eventLength, IntPtr eventData, uint dataLength, out uint bytesReturned);
}
