using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace Monolith
{
    public class WifiNetworkInfo
    {
        public string Ssid;
        public string ProfileName;
        public int SignalQuality; // 0..100
        public bool IsConnected;
        public bool HasProfile;
        public bool IsSecured;
        public string AuthLabel;
    }

    public class WifiScanResult
    {
        public bool HasWifiAdapter;
        public bool AccessBlockedByLocation;
        public string InterfaceName;
        public string ConnectedSsid;
        public List<WifiNetworkInfo> Networks = new List<WifiNetworkInfo>();
    }

    /// <summary>
    /// Lightweight zero-dependency Windows 10/11 Wi-Fi manager powered by native wlanapi.dll
    /// with seamless netsh fallback (handles saved networks, new password connections, disconnect & live scan).
    /// </summary>
    internal static class WifiManager
    {
        private const uint WLAN_AVAILABLE_NETWORK_CONNECTED = 0x00000001;
        private const uint WLAN_AVAILABLE_NETWORK_HAS_PROFILE = 0x00000002;
        private const int DOT11_AUTH_ALGO_80211_OPEN = 1;
        private const int DOT11_AUTH_ALGO_WPA3 = 8;
        private const int DOT11_AUTH_ALGO_WPA3_SAE = 9;
        private const int DOT11_AUTH_ALGO_RSNA_PSK = 7;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public int isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public DOT11_SSID dot11Ssid;
            public int dot11BssType;
            public uint uNumberOfBssids;
            public bool bNetworkConnectable;
            public uint wlanNotConnectableReason;
            public uint uNumberOfPhyTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public int[] dot11PhyTypes;
            public bool bMorePhyTypes;
            public uint wlanSignalQuality;
            public bool bSecurityEnabled;
            public int dot11DefaultAuthAlgorithm;
            public int dot11DefaultCipherAlgorithm;
            public uint dwFlags;
            public uint dwReserved;
        }

        [DllImport("wlanapi.dll")]
        private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanDisconnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        public static string GetQuickConnectedSsid()
        {
            try
            {
                WifiScanResult r = ScanNetworks(false);
                if (!string.IsNullOrEmpty(r.ConnectedSsid)) return r.ConnectedSsid;
            }
            catch { }
            return null;
        }

        public static WifiScanResult ScanNetworks(bool triggerActiveScan)
        {
            WifiScanResult result = new WifiScanResult();
            Dictionary<string, WifiNetworkInfo> bySsid = new Dictionary<string, WifiNetworkInfo>(StringComparer.OrdinalIgnoreCase);

            uint ver;
            IntPtr hClient = IntPtr.Zero;
            uint openRc = 1;
            try { openRc = WlanOpenHandle(2, IntPtr.Zero, out ver, out hClient); } catch { openRc = 1; }

            if (openRc == 0 && hClient != IntPtr.Zero)
            {
                try
                {
                    IntPtr pIfList;
                    if (WlanEnumInterfaces(hClient, IntPtr.Zero, out pIfList) == 0 && pIfList != IntPtr.Zero)
                    {
                        try
                        {
                            int ifCount = Marshal.ReadInt32(pIfList, 0);
                            int ifSize = Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO));
                            if (ifCount > 0) result.HasWifiAdapter = true;

                            for (int i = 0; i < ifCount; i++)
                            {
                                IntPtr pIf = new IntPtr(pIfList.ToInt64() + 8 + i * ifSize);
                                WLAN_INTERFACE_INFO info = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(pIf, typeof(WLAN_INTERFACE_INFO));
                                if (string.IsNullOrEmpty(result.InterfaceName))
                                    result.InterfaceName = info.strInterfaceDescription;

                                if (triggerActiveScan)
                                {
                                    try { WlanScan(hClient, ref info.InterfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); } catch { }
                                }

                                IntPtr pNetList;
                                uint listRc = WlanGetAvailableNetworkList(hClient, ref info.InterfaceGuid, 0, IntPtr.Zero, out pNetList);
                                if (listRc == 5)
                                {
                                    result.AccessBlockedByLocation = true;
                                }
                                else if (listRc == 0 && pNetList != IntPtr.Zero)
                                {
                                    try
                                    {
                                        int netCount = Marshal.ReadInt32(pNetList, 0);
                                        int netSize = Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
                                        for (int n = 0; n < netCount; n++)
                                        {
                                            IntPtr pNet = new IntPtr(pNetList.ToInt64() + 8 + n * netSize);
                                            WLAN_AVAILABLE_NETWORK net = (WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(pNet, typeof(WLAN_AVAILABLE_NETWORK));
                                            if (net.dot11Ssid.uSSIDLength == 0 || net.dot11Ssid.ucSSID == null) continue;
                                            int len = (int)Math.Min(32, net.dot11Ssid.uSSIDLength);
                                            string ssid = Encoding.UTF8.GetString(net.dot11Ssid.ucSSID, 0, len).Trim('\0');
                                            if (string.IsNullOrEmpty(ssid)) continue;

                                            bool connected = (net.dwFlags & WLAN_AVAILABLE_NETWORK_CONNECTED) != 0;
                                            bool saved = (net.dwFlags & WLAN_AVAILABLE_NETWORK_HAS_PROFILE) != 0 || !string.IsNullOrEmpty(net.strProfileName);
                                            int sig = (int)Math.Min(100, net.wlanSignalQuality);
                                            string authLbl = !net.bSecurityEnabled || net.dot11DefaultAuthAlgorithm == DOT11_AUTH_ALGO_80211_OPEN
                                                ? "Open"
                                                : (net.dot11DefaultAuthAlgorithm == DOT11_AUTH_ALGO_WPA3 || net.dot11DefaultAuthAlgorithm == DOT11_AUTH_ALGO_WPA3_SAE
                                                    ? "WPA3"
                                                    : (net.dot11DefaultAuthAlgorithm == DOT11_AUTH_ALGO_RSNA_PSK ? "WPA2" : "Secured"));

                                            WifiNetworkInfo existing;
                                            if (bySsid.TryGetValue(ssid, out existing))
                                            {
                                                if (connected) existing.IsConnected = true;
                                                if (saved)
                                                {
                                                    existing.HasProfile = true;
                                                    if (!string.IsNullOrEmpty(net.strProfileName))
                                                        existing.ProfileName = net.strProfileName;
                                                }
                                                if (sig > existing.SignalQuality) existing.SignalQuality = sig;
                                            }
                                            else
                                            {
                                                WifiNetworkInfo item = new WifiNetworkInfo
                                                {
                                                    Ssid = ssid,
                                                    ProfileName = !string.IsNullOrEmpty(net.strProfileName) ? net.strProfileName : ssid,
                                                    SignalQuality = sig,
                                                    IsConnected = connected,
                                                    HasProfile = saved,
                                                    IsSecured = net.bSecurityEnabled,
                                                    AuthLabel = authLbl
                                                };
                                                bySsid[ssid] = item;
                                            }

                                            if (connected) result.ConnectedSsid = ssid;
                                        }
                                    }
                                    finally
                                    {
                                        WlanFreeMemory(pNetList);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            WlanFreeMemory(pIfList);
                        }
                    }
                }
                finally
                {
                    WlanCloseHandle(hClient, IntPtr.Zero);
                }
            }

            // Fallback: if Win11 24H2 Location permission blocked nearby scan, still list active + saved profiles via netsh!
            if (bySsid.Count == 0)
            {
                MergeNetshSavedFallback(result, bySsid);
            }

            List<WifiNetworkInfo> list = new List<WifiNetworkInfo>(bySsid.Values);
            list.Sort(delegate(WifiNetworkInfo a, WifiNetworkInfo b)
            {
                if (a.IsConnected != b.IsConnected) return a.IsConnected ? -1 : 1;
                if (a.HasProfile != b.HasProfile) return a.HasProfile ? -1 : 1;
                int cmpSig = b.SignalQuality.CompareTo(a.SignalQuality);
                if (cmpSig != 0) return cmpSig;
                return string.Compare(a.Ssid, b.Ssid, StringComparison.OrdinalIgnoreCase);
            });
            result.Networks = list;
            return result;
        }

        private static void MergeNetshSavedFallback(WifiScanResult result, Dictionary<string, WifiNetworkInfo> bySsid)
        {
            try
            {
                string ifaceOut = RunNetsh("wlan show interfaces");
                string connectedSsid = null;
                int connectedSig = 75;
                if (!string.IsNullOrEmpty(ifaceOut))
                {
                    string[] lines = ifaceOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ln = lines[i].Trim();
                        int colon = ln.IndexOf(':');
                        if (colon <= 0) continue;
                        string k = ln.Substring(0, colon).Trim();
                        string v = ln.Substring(colon + 1).Trim();
                        if (k.Equals("SSID", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(v))
                        {
                            connectedSsid = v;
                            result.HasWifiAdapter = true;
                        }
                        else if ((k.IndexOf("Signal", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("Sinyal", StringComparison.OrdinalIgnoreCase) >= 0) && v.EndsWith("%"))
                        {
                            int s;
                            if (int.TryParse(v.TrimEnd('%').Trim(), out s)) connectedSig = s;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(connectedSsid))
                {
                    result.ConnectedSsid = connectedSsid;
                    bySsid[connectedSsid] = new WifiNetworkInfo
                    {
                        Ssid = connectedSsid,
                        ProfileName = connectedSsid,
                        SignalQuality = connectedSig,
                        IsConnected = true,
                        HasProfile = true,
                        IsSecured = true,
                        AuthLabel = "WPA2"
                    };
                }

                string profOut = RunNetsh("wlan show profiles");
                if (!string.IsNullOrEmpty(profOut))
                {
                    string[] lines = profOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ln = lines[i];
                        int colon = ln.IndexOf(':');
                        if (colon <= 0 || colon >= ln.Length - 1) continue;
                        string left = ln.Substring(0, colon).Trim();
                        if (left.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            left.IndexOf("Profil", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string pName = ln.Substring(colon + 1).Trim();
                            if (!string.IsNullOrEmpty(pName) && !bySsid.ContainsKey(pName))
                            {
                                result.HasWifiAdapter = true;
                                bySsid[pName] = new WifiNetworkInfo
                                {
                                    Ssid = pName,
                                    ProfileName = pName,
                                    SignalQuality = 55,
                                    IsConnected = false,
                                    HasProfile = true,
                                    IsSecured = true,
                                    AuthLabel = "Saved"
                                };
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static void ConnectSavedAsync(string ssid, string profileName, Action<bool, string> onDone)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                string msg = "";
                try
                {
                    string p = string.IsNullOrEmpty(profileName) ? ssid : profileName;
                    string outText = RunNetsh("wlan connect name=\"" + p.Replace("\"", "") + "\" ssid=\"" + ssid.Replace("\"", "") + "\"");
                    ok = !string.IsNullOrEmpty(outText) &&
                         (outText.IndexOf("successfully", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          outText.IndexOf("başarıyla", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          outText.IndexOf("tamamlandı", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!ok)
                    {
                        // Wait briefly and check if actually connected
                        Thread.Sleep(900);
                        string cur = GetQuickConnectedSsid();
                        ok = string.Equals(cur, ssid, StringComparison.OrdinalIgnoreCase);
                    }
                    msg = ok ? "OK" : outText;
                }
                catch (Exception ex) { msg = ex.Message; }
                if (onDone != null) onDone(ok, msg);
            });
        }

        public static void ConnectWithPasswordAsync(string ssid, string password, bool isOpen, Action<bool, string> onDone)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                string msg = "";
                string tmpXml = Path.Combine(Path.GetTempPath(), "monolith_wlan_" + Guid.NewGuid().ToString("N") + ".xml");
                try
                {
                    string safeSsidXml = SecurityElement.Escape(ssid);
                    string hexSsid = BytesToHex(Encoding.UTF8.GetBytes(ssid));
                    string xml;
                    if (isOpen || string.IsNullOrEmpty(password))
                    {
                        xml = "<?xml version=\"1.0\"?>\r\n" +
                              "<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\r\n" +
                              "  <name>" + safeSsidXml + "</name>\r\n" +
                              "  <SSIDConfig><SSID><hex>" + hexSsid + "</hex><name>" + safeSsidXml + "</name></SSID></SSIDConfig>\r\n" +
                              "  <connectionType>ESS</connectionType>\r\n" +
                              "  <connectionMode>auto</connectionMode>\r\n" +
                              "  <MSM><security><authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption></security></MSM>\r\n" +
                              "</WLANProfile>";
                    }
                    else
                    {
                        string safeKeyXml = SecurityElement.Escape(password);
                        xml = "<?xml version=\"1.0\"?>\r\n" +
                              "<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\r\n" +
                              "  <name>" + safeSsidXml + "</name>\r\n" +
                              "  <SSIDConfig><SSID><hex>" + hexSsid + "</hex><name>" + safeSsidXml + "</name></SSID></SSIDConfig>\r\n" +
                              "  <connectionType>ESS</connectionType>\r\n" +
                              "  <connectionMode>auto</connectionMode>\r\n" +
                              "  <MSM><security>\r\n" +
                              "    <authEncryption><authentication>WPA2PSK</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption>\r\n" +
                              "    <sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>" + safeKeyXml + "</keyMaterial></sharedKey>\r\n" +
                              "  </security></MSM>\r\n" +
                              "</WLANProfile>";
                    }

                    File.WriteAllText(tmpXml, xml, Encoding.UTF8);
                    RunNetsh("wlan add profile filename=\"" + tmpXml + "\" user=current");
                    string connOut = RunNetsh("wlan connect name=\"" + ssid.Replace("\"", "") + "\" ssid=\"" + ssid.Replace("\"", "") + "\"");
                    Thread.Sleep(1100);
                    string cur = GetQuickConnectedSsid();
                    ok = string.Equals(cur, ssid, StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrEmpty(connOut) &&
                          (connOut.IndexOf("successfully", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           connOut.IndexOf("başarıyla", StringComparison.OrdinalIgnoreCase) >= 0));
                    msg = ok ? "OK" : connOut;
                }
                catch (Exception ex) { msg = ex.Message; }
                finally
                {
                    try { if (File.Exists(tmpXml)) File.Delete(tmpXml); } catch { }
                }
                if (onDone != null) onDone(ok, msg);
            });
        }

        public static void DisconnectAsync(Action<bool> onDone)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                try
                {
                    uint ver;
                    IntPtr hClient;
                    if (WlanOpenHandle(2, IntPtr.Zero, out ver, out hClient) == 0 && hClient != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr pIfList;
                            if (WlanEnumInterfaces(hClient, IntPtr.Zero, out pIfList) == 0 && pIfList != IntPtr.Zero)
                            {
                                try
                                {
                                    int ifCount = Marshal.ReadInt32(pIfList, 0);
                                    int ifSize = Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO));
                                    for (int i = 0; i < ifCount; i++)
                                    {
                                        IntPtr pIf = new IntPtr(pIfList.ToInt64() + 8 + i * ifSize);
                                        WLAN_INTERFACE_INFO info = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(pIf, typeof(WLAN_INTERFACE_INFO));
                                        if (WlanDisconnect(hClient, ref info.InterfaceGuid, IntPtr.Zero) == 0)
                                            ok = true;
                                    }
                                }
                                finally { WlanFreeMemory(pIfList); }
                            }
                        }
                        finally { WlanCloseHandle(hClient, IntPtr.Zero); }
                    }
                    if (!ok)
                    {
                        RunNetsh("wlan disconnect");
                        ok = true;
                    }
                }
                catch { }
                if (onDone != null) onDone(ok);
            });
        }

        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2"));
            return sb.ToString();
        }

        private static string RunNetsh(string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh.exe", args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return "";
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3500);
                    return output ?? "";
                }
            }
            catch { return ""; }
        }
    }
}
