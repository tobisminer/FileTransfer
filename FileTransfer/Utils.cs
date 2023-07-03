using CommunityToolkit.Maui.Alerts;
using System.Net;
using System.Net.Sockets;
#if ANDROID
using Java.Net;
using Java.Util;
#endif

namespace FileTransfer;

public static class Utils
{
    public class Files
    {
        public string FileName { get; set; }
        public string FileSize { get; set; }
        public string Progress { get; set; }
    }

    public class Log
    {
        public string Message { get; set; }
    }

    public static CancellationToken CancellationToken = new CancellationTokenSource().Token;

    private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB" };

    public static string SizeSuffix(long value, int decimalPlaces = 0, bool addSuffix = true)
    {
        if (value < 0) throw new ArgumentException("Bytes should not be negative", "value");
        var mag = (int)Math.Max(0, Math.Log(value, 1024));
        var adjustedSize = Math.Round(value / Math.Pow(1024, mag), decimalPlaces);
        return $"{adjustedSize} " + (addSuffix ? SizeSuffixes[mag] : "");
    }

    public static async void MakeToast(string message)
    {
        var toast = Toast.Make(message);
        await toast.Show(CancellationToken);
    }
#if ANDROID
    public static string GetLocalIpAddressForAndroid()
    {
        var interfaces = Collections.List(Java.Net.NetworkInterface.NetworkInterfaces);
        foreach (var @interface in interfaces.OfType<Java.Net.NetworkInterface>())
        {
            var addresses = Collections.List(@interface.InetAddresses);
            foreach (var address in addresses.OfType<Inet4Address>())
            {
                if (!address.IsLinkLocalAddress && !address.IsLoopbackAddress)
                {
                    return address.HostAddress;
                }
            }
        }

        return null;
    }
#endif
    public static IPAddress GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip;
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
    public static bool ValidateIPv4(string ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString)) return false;
        var splitValues = ipString.Split('.');
        return splitValues.Length == 4 && splitValues.All(r => byte.TryParse(r, out _));
    }
    public static string GetIPAdress()
    {
#if WINDOWS
        return Utils.GetLocalIpAddress().ToString();
#endif
#if ANDROID
        return Utils.GetLocalIpAddressForAndroid();
#endif
        return null;
    }
    public static void HandleException(Exception e)
    {
        MakeToast(e.Message);
    }

}