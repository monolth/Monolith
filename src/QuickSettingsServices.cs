using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Monolith
{
    // =========================================================================
    // 1. AUDIO OUTPUT DEVICE MANAGER (CoreAudio IMMDeviceEnumerator + IPolicyConfig)
    // =========================================================================

    public class AudioOutputDeviceInfo
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string IconGlyph;
        public bool IsDefault;
    }

    internal static class AudioDeviceManager
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            int GetCount(out uint pcDevices);
            int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr p;
            public int p2;
        }

        [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class PolicyConfigClientComObject { }

        [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig10
        {
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);
            int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
        }

        [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfigVista
        {
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr key, IntPtr pv);
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr key, IntPtr pv);
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        public static List<AudioOutputDeviceInfo> GetActiveOutputDevices()
        {
            List<AudioOutputDeviceInfo> list = new List<AudioOutputDeviceInfo>();
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                string defaultId = "";
                IMMDevice defDev;
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out defDev) == 0 && defDev != null)
                {
                    defDev.GetId(out defaultId);
                }

                IMMDeviceCollection col;
                // eRender = 0, DEVICE_STATE_ACTIVE = 1
                if (enumerator.EnumAudioEndpoints(0, 1, out col) == 0 && col != null)
                {
                    uint count;
                    col.GetCount(out count);
                    PROPERTYKEY pkeyFriendly = new PROPERTYKEY
                    {
                        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                        pid = 14
                    };

                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice dev;
                        if (col.Item(i, out dev) != 0 || dev == null) continue;
                        string id;
                        dev.GetId(out id);
                        if (string.IsNullOrEmpty(id)) continue;

                        string name = id;
                        IPropertyStore store;
                        if (dev.OpenPropertyStore(0, out store) == 0 && store != null)
                        {
                            PROPVARIANT pv;
                            if (store.GetValue(ref pkeyFriendly, out pv) == 0)
                            {
                                try
                                {
                                    if (pv.vt == 31 && pv.p != IntPtr.Zero)
                                    {
                                        string s = Marshal.PtrToStringUni(pv.p);
                                        if (!string.IsNullOrEmpty(s)) name = s;
                                    }
                                }
                                finally
                                {
                                    try { PropVariantClear(ref pv); } catch { }
                                }
                            }
                        }

                        // Skip virtual microphone loopback endpoints appearing under render
                        if (name.IndexOf(" - Microphone", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        bool isDef = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase);
                        list.Add(new AudioOutputDeviceInfo
                        {
                            Id = id,
                            Name = name,
                            ShortName = MakeShortAudioName(name),
                            IconGlyph = PickAudioDeviceGlyph(name),
                            IsDefault = isDef
                        });
                    }
                }
            }
            catch { }

            list.Sort(delegate(AudioOutputDeviceInfo a, AudioOutputDeviceInfo b)
            {
                if (a.IsDefault != b.IsDefault) return a.IsDefault ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public static string GetDefaultOutputShortName()
        {
            try
            {
                List<AudioOutputDeviceInfo> list = GetActiveOutputDevices();
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].IsDefault) return list[i].ShortName;
                }
            }
            catch { }
            return null;
        }

        public static bool SetDefaultOutputDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            try
            {
                object policyObj = new PolicyConfigClientComObject();
                IPolicyConfig10 p10 = policyObj as IPolicyConfig10;
                if (p10 != null)
                {
                    p10.SetDefaultEndpoint(deviceId, 0); // eConsole
                    p10.SetDefaultEndpoint(deviceId, 1); // eMultimedia
                    p10.SetDefaultEndpoint(deviceId, 2); // eCommunications
                    return true;
                }
                IPolicyConfigVista pVista = policyObj as IPolicyConfigVista;
                if (pVista != null)
                {
                    pVista.SetDefaultEndpoint(deviceId, 0);
                    pVista.SetDefaultEndpoint(deviceId, 1);
                    pVista.SetDefaultEndpoint(deviceId, 2);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string PickAudioDeviceGlyph(string name)
        {
            if (string.IsNullOrEmpty(name)) return "\uE7F5";
            string lower = name.ToLowerInvariant();
            if (lower.Contains("kulak") || lower.Contains("headphone") || lower.Contains("headset") ||
                lower.Contains("arctis") || lower.Contains("airpods") || lower.Contains("buds") ||
                lower.Contains("earphone") || lower.Contains("wireless"))
                return "\uE7F6";
            if (lower.Contains("monit") || lower.Contains("hdmi") || lower.Contains("display") ||
                lower.Contains("nvidia") || lower.Contains("amd") || lower.Contains("tv"))
                return "\uE7F4";
            return "\uE7F5";
        }

        private static string MakeShortAudioName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int open = name.IndexOf('(');
            int close = name.LastIndexOf(')');
            if (open > 0 && close > open + 1)
            {
                string inside = name.Substring(open + 1, close - open - 1).Trim();
                if (inside.Length >= 3 && inside.Length <= 26)
                    return inside;
            }
            return name.Length > 24 ? (name.Substring(0, 22) + "…") : name;
        }
    }

    // =========================================================================
    // 2. BLUETOOTH DEVICE MANAGER (BluetoothApis.dll + Registry + CoreAudio)
    // =========================================================================

    public class BluetoothDeviceItem
    {
        public ulong Address;
        public string Name;
        public string CategoryTr;
        public string CategoryEn;
        public string IconGlyph;
        public bool IsConnected;
        public bool IsRemembered;
        public string AudioEndpointId;
    }

    public class BluetoothScanResult
    {
        public bool HasRadio;
        public bool RadioEnabled;
        public List<BluetoothDeviceItem> Devices = new List<BluetoothDeviceItem>();
    }

    internal static class BluetoothManager
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_FIND_RADIO_PARAMS
        {
            public uint dwSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fConnected;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fRemembered;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnAuthenticated;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnRemembered;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnUnknown;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnConnected;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        private static extern bool BluetoothFindRadioClose(IntPtr hFind);

        [DllImport("BluetoothApis.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("BluetoothApis.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport("BluetoothApis.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint BluetoothSetServiceState(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi, ref Guid pGuidService, uint dwServiceFlags);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        private static extern bool BluetoothEnableDiscovery(IntPtr hRadio, bool fEnabled);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        private static extern bool BluetoothEnableIncomingConnections(IntPtr hRadio, bool fEnabled);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static bool _cachedBtEnabled = true;

        public static bool IsBluetoothEnabled()
        {
            try
            {
                BLUETOOTH_FIND_RADIO_PARAMS rp = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)) };
                IntPtr hRadio;
                IntPtr hFind = BluetoothFindFirstRadio(ref rp, out hRadio);
                if (hFind != IntPtr.Zero)
                {
                    CloseHandle(hRadio);
                    BluetoothFindRadioClose(hFind);
                    _cachedBtEnabled = true;
                    return true;
                }
            }
            catch { }
            return _cachedBtEnabled;
        }

        public static void SetBluetoothEnabledAsync(bool enable, Action<bool> onDone)
        {
            _cachedBtEnabled = enable;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                try
                {
                    BLUETOOTH_FIND_RADIO_PARAMS rp = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)) };
                    IntPtr hRadio;
                    IntPtr hFind = BluetoothFindFirstRadio(ref rp, out hRadio);
                    if (hFind != IntPtr.Zero)
                    {
                        try
                        {
                            BluetoothEnableIncomingConnections(hRadio, enable);
                            BluetoothEnableDiscovery(hRadio, enable);
                            ok = true;
                        }
                        finally
                        {
                            CloseHandle(hRadio);
                            BluetoothFindRadioClose(hFind);
                        }
                    }
                }
                catch { }
                if (onDone != null) onDone(ok);
            });
        }

        public static BluetoothScanResult GetPairedDevices()
        {
            BluetoothScanResult res = new BluetoothScanResult();
            res.RadioEnabled = _cachedBtEnabled;
            Dictionary<string, BluetoothDeviceItem> byName = new Dictionary<string, BluetoothDeviceItem>(StringComparer.OrdinalIgnoreCase);

            // 1. Native BluetoothApis.dll enumeration
            try
            {
                BLUETOOTH_FIND_RADIO_PARAMS rp = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)) };
                IntPtr hRadio = IntPtr.Zero;
                IntPtr hRadioFind = BluetoothFindFirstRadio(ref rp, out hRadio);
                if (hRadioFind != IntPtr.Zero)
                {
                    res.HasRadio = true;
                    res.RadioEnabled = _cachedBtEnabled;
                }

                try
                {
                    BLUETOOTH_DEVICE_SEARCH_PARAMS sp = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
                    sp.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS));
                    sp.fReturnAuthenticated = true;
                    sp.fReturnRemembered = true;
                    sp.fReturnUnknown = false;
                    sp.fReturnConnected = true;
                    sp.fIssueInquiry = false;
                    sp.cTimeoutMultiplier = 1;
                    sp.hRadio = hRadio;

                    BLUETOOTH_DEVICE_INFO di = new BLUETOOTH_DEVICE_INFO();
                    di.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));

                    IntPtr hFind = BluetoothFindFirstDevice(ref sp, ref di);
                    if (hFind != IntPtr.Zero)
                    {
                        try
                        {
                            do
                            {
                                string dName = (di.szName ?? "").Trim();
                                if (!string.IsNullOrEmpty(dName))
                                {
                                    string catTr;
                                    string catEn;
                                    string glyph;
                                    ClassifyBluetoothDevice(dName, di.ulClassofDevice, out glyph, out catTr, out catEn);

                                    byName[dName] = new BluetoothDeviceItem
                                    {
                                        Address = di.Address,
                                        Name = dName,
                                        CategoryTr = catTr,
                                        CategoryEn = catEn,
                                        IconGlyph = glyph,
                                        IsConnected = di.fConnected && _cachedBtEnabled,
                                        IsRemembered = di.fRemembered || di.fAuthenticated
                                    };
                                }
                            }
                            while (BluetoothFindNextDevice(hFind, ref di));
                        }
                        finally
                        {
                            BluetoothFindDeviceClose(hFind);
                        }
                    }
                }
                finally
                {
                    if (hRadio != IntPtr.Zero) CloseHandle(hRadio);
                    if (hRadioFind != IntPtr.Zero) BluetoothFindRadioClose(hRadioFind);
                }
            }
            catch { }

            // 2. Registry BTHPORT paired devices fallback (captures BLE / dual-mode paired peripherals)
            try
            {
                using (RegistryKey bthKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices", false))
                {
                    if (bthKey != null)
                    {
                        res.HasRadio = true;
                        string[] subNames = bthKey.GetSubKeyNames();
                        for (int i = 0; i < subNames.Length; i++)
                        {
                            using (RegistryKey devKey = bthKey.OpenSubKey(subNames[i], false))
                            {
                                if (devKey == null) continue;
                                byte[] nameBytes = devKey.GetValue("Name") as byte[];
                                if (nameBytes == null || nameBytes.Length == 0) continue;
                                string dName = Encoding.UTF8.GetString(nameBytes).Trim('\0').Trim();
                                if (string.IsNullOrEmpty(dName) || byName.ContainsKey(dName)) continue;

                                ulong addr = 0;
                                ulong.TryParse(subNames[i], System.Globalization.NumberStyles.HexNumber, null, out addr);
                                string catTr;
                                string catEn;
                                string glyph;
                                ClassifyBluetoothDevice(dName, 0, out glyph, out catTr, out catEn);

                                byName[dName] = new BluetoothDeviceItem
                                {
                                    Address = addr,
                                    Name = dName,
                                    CategoryTr = catTr,
                                    CategoryEn = catEn,
                                    IconGlyph = glyph,
                                    IsConnected = false,
                                    IsRemembered = true
                                };
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Cross-check active wireless/Bluetooth audio endpoints so wireless headsets (e.g. Arctis Nova, AirPods, Sony WH) appear & can be switched/toggled in 1 click!
            try
            {
                List<AudioOutputDeviceInfo> audioDevs = AudioDeviceManager.GetActiveOutputDevices();
                for (int i = 0; i < audioDevs.Count; i++)
                {
                    AudioOutputDeviceInfo ad = audioDevs[i];
                    string lower = ad.Name.ToLowerInvariant();
                    if (lower.Contains("wireless") || lower.Contains("bluetooth") || lower.Contains("arctis") ||
                        lower.Contains("airpods") || lower.Contains("buds") || lower.Contains("kulak"))
                    {
                        res.HasRadio = true;
                        BluetoothDeviceItem existing;
                        if (byName.TryGetValue(ad.ShortName, out existing) || byName.TryGetValue(ad.Name, out existing))
                        {
                            existing.AudioEndpointId = ad.Id;
                            if (ad.IsDefault && _cachedBtEnabled) existing.IsConnected = true;
                        }
                        else
                        {
                            byName[ad.ShortName] = new BluetoothDeviceItem
                            {
                                Address = 0,
                                Name = ad.ShortName,
                                CategoryTr = "Kablosuz Ses",
                                CategoryEn = "Wireless Audio",
                                IconGlyph = "\uE7F6",
                                IsConnected = ad.IsDefault && _cachedBtEnabled,
                                IsRemembered = true,
                                AudioEndpointId = ad.Id
                            };
                        }
                    }
                }
            }
            catch { }

            List<BluetoothDeviceItem> list = new List<BluetoothDeviceItem>(byName.Values);
            list.Sort(delegate(BluetoothDeviceItem a, BluetoothDeviceItem b)
            {
                if (a.IsConnected != b.IsConnected) return a.IsConnected ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            res.Devices = list;
            return res;
        }

        public static void ToggleDeviceConnectionAsync(BluetoothDeviceItem item, Action<bool, bool> onDone)
        {
            if (item == null)
            {
                if (onDone != null) onDone(false, false);
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool targetConnect = !item.IsConnected;
                bool ok = false;

                try
                {
                    // 1. If device has a Bluetooth MAC address, toggle its standard BT profile services via BluetoothSetServiceState
                    if (item.Address != 0)
                    {
                        BLUETOOTH_FIND_RADIO_PARAMS rp = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)) };
                        IntPtr hRadio;
                        IntPtr hFind = BluetoothFindFirstRadio(ref rp, out hRadio);
                        if (hFind != IntPtr.Zero)
                        {
                            try
                            {
                                BLUETOOTH_DEVICE_INFO di = new BLUETOOTH_DEVICE_INFO();
                                di.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));
                                di.Address = item.Address;
                                di.szName = item.Name;

                                uint flags = targetConnect ? 1u : 0u; // BLUETOOTH_SERVICE_ENABLE = 1, DISABLE = 0
                                Guid[] services = new Guid[]
                                {
                                    new Guid("0000110B-0000-1000-8000-00805F9B34FB"), // AudioSink (A2DP)
                                    new Guid("0000111E-0000-1000-8000-00805F9B34FB"), // Handsfree (HFP)
                                    new Guid("00001108-0000-1000-8000-00805F9B34FB"), // Headset (HSP)
                                    new Guid("00001124-0000-1000-8000-00805F9B34FB")  // HID (Mouse/Keyboard/Gamepad)
                                };

                                for (int s = 0; s < services.Length; s++)
                                {
                                    Guid g = services[s];
                                    uint rc = BluetoothSetServiceState(hRadio, ref di, ref g, flags);
                                    if (rc == 0) ok = true;
                                }
                            }
                            finally
                            {
                                CloseHandle(hRadio);
                                BluetoothFindRadioClose(hFind);
                            }
                        }
                    }

                    // 2. If it corresponds to an active wireless audio endpoint, switch default audio output to/from it!
                    if (!string.IsNullOrEmpty(item.AudioEndpointId))
                    {
                        if (targetConnect)
                        {
                            if (AudioDeviceManager.SetDefaultOutputDevice(item.AudioEndpointId))
                                ok = true;
                        }
                        else
                        {
                            List<AudioOutputDeviceInfo> allAudio = AudioDeviceManager.GetActiveOutputDevices();
                            for (int i = 0; i < allAudio.Count; i++)
                            {
                                if (!string.Equals(allAudio[i].Id, item.AudioEndpointId, StringComparison.OrdinalIgnoreCase))
                                {
                                    AudioDeviceManager.SetDefaultOutputDevice(allAudio[i].Id);
                                    ok = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                item.IsConnected = ok ? targetConnect : item.IsConnected;
                if (onDone != null) onDone(ok, item.IsConnected);
            });
        }

        private static void ClassifyBluetoothDevice(string name, uint cod, out string glyph, out string catTr, out string catEn)
        {
            string lower = (name ?? "").ToLowerInvariant();
            uint major = (cod >> 8) & 0x1F;
            if (major == 4 || lower.Contains("head") || lower.Contains("kulak") || lower.Contains("buds") ||
                lower.Contains("airpods") || lower.Contains("arctis") || lower.Contains("speaker") || lower.Contains("audio"))
            {
                glyph = "\uE7F6";
                catTr = "Ses Cihazı";
                catEn = "Audio";
                return;
            }
            if (lower.Contains("mouse") || lower.Contains("fare"))
            {
                glyph = "\uE962";
                catTr = "Fare";
                catEn = "Mouse";
                return;
            }
            if (lower.Contains("key") || lower.Contains("klavye"))
            {
                glyph = "\uE765";
                catTr = "Klavye";
                catEn = "Keyboard";
                return;
            }
            if (lower.Contains("controller") || lower.Contains("xbox") || lower.Contains("dualshock") || lower.Contains("dualsense") || lower.Contains("gamepad"))
            {
                glyph = "\uE7FC";
                catTr = "Oyun Kolu";
                catEn = "Controller";
                return;
            }
            if (major == 2 || lower.Contains("phone") || lower.Contains("iphone") || lower.Contains("galaxy"))
            {
                glyph = "\uE8EA";
                catTr = "Telefon";
                catEn = "Phone";
                return;
            }
            glyph = "\uE702";
            catTr = "Bluetooth Cihazı";
            catEn = "Bluetooth Device";
        }
    }

    // =========================================================================
    // 3. DISPLAY PROJECTION MANAGER (DisplaySwitch.exe /internal /clone /extend /external)
    // =========================================================================

    public class ProjectionModeOption
    {
        public string ModeId;
        public string SwitchArg;
        public string IconGlyph;
        public string TitleTr;
        public string TitleEn;
        public string SubTr;
        public string SubEn;
    }

    internal static class DisplayProjectManager
    {
        public static readonly List<ProjectionModeOption> Modes = new List<ProjectionModeOption>
        {
            new ProjectionModeOption
            {
                ModeId = "internal",
                SwitchArg = "/internal",
                IconGlyph = "\uE7F4",
                TitleTr = "Sadece bilgisayar ekranı",
                TitleEn = "PC screen only",
                SubTr = "Yalnızca ana ekranı kullan",
                SubEn = "Use primary display only"
            },
            new ProjectionModeOption
            {
                ModeId = "clone",
                SwitchArg = "/clone",
                IconGlyph = "\uE8B9",
                TitleTr = "Yinele",
                TitleEn = "Duplicate",
                SubTr = "Her iki ekranda aynı görüntüyü göster",
                SubEn = "Show the same desktop on all displays"
            },
            new ProjectionModeOption
            {
                ModeId = "extend",
                SwitchArg = "/extend",
                IconGlyph = "\uE7F3",
                TitleTr = "Uzat",
                TitleEn = "Extend",
                SubTr = "Masaüstünü ekranlar arasında genişlet",
                SubEn = "Extend desktop across displays"
            },
            new ProjectionModeOption
            {
                ModeId = "external",
                SwitchArg = "/external",
                IconGlyph = "\uE7F8",
                TitleTr = "Sadece ikinci ekran",
                TitleEn = "Second screen only",
                SubTr = "Yalnızca harici monitörü/TV'yi kullan",
                SubEn = "Use external display only"
            }
        };

        private static string _currentMode = null;

        public static string GetCurrentMode()
        {
            if (string.IsNullOrEmpty(_currentMode))
            {
                try
                {
                    int screens = System.Windows.Forms.Screen.AllScreens.Length;
                    _currentMode = screens > 1 ? "extend" : "internal";
                }
                catch { _currentMode = "internal"; }
            }
            return _currentMode;
        }

        public static bool IsProjectionActive()
        {
            return GetCurrentMode() != "internal";
        }

        public static void ToggleProjection()
        {
            string next = IsProjectionActive() ? "internal" : "extend";
            ApplyProjectionMode(next);
        }

        public static bool ApplyProjectionMode(string modeId)
        {
            ProjectionModeOption opt = null;
            for (int i = 0; i < Modes.Count; i++)
            {
                if (string.Equals(Modes[i].ModeId, modeId, StringComparison.OrdinalIgnoreCase))
                {
                    opt = Modes[i];
                    break;
                }
            }
            if (opt == null) return false;

            _currentMode = opt.ModeId;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string exePath = System.IO.Path.Combine(Environment.SystemDirectory, "DisplaySwitch.exe");
                    ProcessStartInfo psi = new ProcessStartInfo(exePath, opt.SwitchArg);
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit(2500);
                    }
                }
                catch { }
            });
            return true;
        }
    }

    // =========================================================================
    // 4. DIRECT BACKGROUND TOGGLES (Night Light, Focus Mode, Battery Saver, Airplane)
    // =========================================================================

    internal static class QuickToggleManager
    {
        private static Window _nightLightOverlay;
        private static bool _nightLightEnabled = false;
        private static bool _focusEnabled = false;
        private static bool _airplaneEnabled = false;
        private static bool _batterySaverEnabled = false;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static bool IsNightLightEnabled { get { return _nightLightEnabled; } }
        public static bool IsFocusModeEnabled { get { return _focusEnabled; } }
        public static bool IsAirplaneModeEnabled { get { return _airplaneEnabled; } }
        public static bool IsBatterySaverEnabled { get { return _batterySaverEnabled; } }

        public static void ToggleNightLight()
        {
            _nightLightEnabled = !_nightLightEnabled;
            try
            {
                if (_nightLightEnabled)
                {
                    if (_nightLightOverlay == null)
                    {
                        _nightLightOverlay = new Window
                        {
                            WindowStyle = WindowStyle.None,
                            AllowsTransparency = true,
                            Background = new SolidColorBrush(Color.FromArgb(42, 255, 138, 36)),
                            ShowInTaskbar = false,
                            ShowActivated = false,
                            Topmost = true,
                            ResizeMode = ResizeMode.NoResize,
                            Left = SystemParameters.VirtualScreenLeft,
                            Top = SystemParameters.VirtualScreenTop,
                            Width = SystemParameters.VirtualScreenWidth,
                            Height = SystemParameters.VirtualScreenHeight,
                            IsHitTestVisible = false
                        };
                        _nightLightOverlay.SourceInitialized += delegate
                        {
                            IntPtr hwnd = new WindowInteropHelper(_nightLightOverlay).Handle;
                            // WS_EX_TRANSPARENT (0x20) | WS_EX_LAYERED (0x80000) | WS_EX_TOOLWINDOW (0x80) | WS_EX_NOACTIVATE (0x08000000)
                            int ex = GetWindowLong(hwnd, -20);
                            SetWindowLong(hwnd, -20, ex | 0x00000020 | 0x00080000 | 0x00000080 | 0x08000000);
                        };
                    }
                    _nightLightOverlay.Show();
                }
                else
                {
                    if (_nightLightOverlay != null)
                    {
                        _nightLightOverlay.Hide();
                    }
                }
            }
            catch { }
        }

        public static void ToggleFocusMode()
        {
            _focusEnabled = !_focusEnabled;
            bool suppressToasts = _focusEnabled;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings"))
                    {
                        if (k != null)
                        {
                            k.SetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED", suppressToasts ? 0 : 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            });
        }

        public static void ToggleBatterySaver(MonolithConfig cfg)
        {
            _batterySaverEnabled = !_batterySaverEnabled;
            if (cfg != null)
            {
                cfg.LowResourceMode = _batterySaverEnabled;
                ConfigStore.Save(cfg);
            }
            bool powerSave = _batterySaverEnabled;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // Power Saver GUID vs Balanced GUID
                    string scheme = powerSave
                        ? "a1841308-3541-4fab-bc81-f71556f20b4a"
                        : "381b4222-f694-41f0-9685-ff5bb260df2e";
                    ProcessStartInfo psi = new ProcessStartInfo("powercfg.exe", "/setactive " + scheme);
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit(2000);
                    }
                }
                catch { }
            });
        }

        public static void ToggleAirplaneModeAsync(Action onDone)
        {
            _airplaneEnabled = !_airplaneEnabled;
            bool enableRadios = !_airplaneEnabled;
            WifiManager.SetWifiEnabledAsync(enableRadios, delegate
            {
                BluetoothManager.SetBluetoothEnabledAsync(enableRadios, delegate
                {
                    if (onDone != null) onDone();
                });
            });
        }
    }
}
