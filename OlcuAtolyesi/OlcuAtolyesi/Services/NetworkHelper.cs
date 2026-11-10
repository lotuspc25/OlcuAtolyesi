using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OlcuAtolyesi.Services;

/// <summary>
/// Yerel ağ IP adresi sorgulama ve yardımcı ağ fonksiyonları.
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Cihazın yerel ağdaki etkin IPv4 adresini bulur (Wi-Fi veya Ethernet).
    /// Bulunamazsa "127.0.0.1" döner.
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // 1. Önce Wi-Fi (Wireless80211) arayüzlerini tara
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    var ip = GetIpv4FromInterface(ni);
                    if (!string.IsNullOrEmpty(ip))
                    {
                        return ip;
                    }
                }
            }

            // 2. Wi-Fi bulunamazsa Ethernet veya diğer etkin arayüzleri tara
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ip = GetIpv4FromInterface(ni);
                if (!string.IsNullOrEmpty(ip))
                {
                    return ip;
                }
            }
        }
        catch
        {
            // İzin veya platform kısıtlaması durumunda
        }

        return "127.0.0.1";
    }

    private static string? GetIpv4FromInterface(NetworkInterface ni)
    {
        var props = ni.GetIPProperties();
        foreach (var addr in props.UnicastAddresses)
        {
            if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(addr.Address))
            {
                var text = addr.Address.ToString();
                if (!text.StartsWith("169.254.", StringComparison.Ordinal)) // APIPA hariç
                {
                    return text;
                }
            }
        }

        return null;
    }
}
