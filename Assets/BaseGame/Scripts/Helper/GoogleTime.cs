using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

public static class GoogleTime
{
    public static async UniTask<DateTime> GetUtcTime()
    {
        const string ntpServer = "time.google.com";
        const int ntpPort = 123;

        // Build NTP request packet
        byte[] ntpData = new byte[48];
        ntpData[0] = 0x1B; // LI, Version, Mode

        // Resolve addresses, prefer IPv4 then fallback to IPv6
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(ntpServer);
        if (addresses == null || addresses.Length == 0)
        {
            // As a last resort, return local UTC to avoid crashing callers
            return DateTime.UtcNow;
        }

        IPAddress ip =
            addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ??
            addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);

        if (ip == null)
        {
            // No supported address families found
            return DateTime.UtcNow;
        }

        IPEndPoint ipEndPoint = new IPEndPoint(ip, ntpPort);

        using (var udp = new UdpClient(ip.AddressFamily))
        {
            // For IPv6 sockets, try DualMode (ignore if not supported on target)
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                try { udp.Client.DualMode = true; } catch { /* ignore */ }
            }

            udp.Connect(ipEndPoint);
            await udp.SendAsync(ntpData, ntpData.Length);

            // Receive
            UdpReceiveResult result = await udp.ReceiveAsync();
            ntpData = result.Buffer;
        }

        // Parse NTP response
        const byte serverReplyTime = 40;
        ulong intPart =
            ((ulong)ntpData[serverReplyTime] << 24) |
            ((ulong)ntpData[serverReplyTime + 1] << 16) |
            ((ulong)ntpData[serverReplyTime + 2] << 8) |
            ntpData[serverReplyTime + 3];

        ulong fractPart =
            ((ulong)ntpData[serverReplyTime + 4] << 24) |
            ((ulong)ntpData[serverReplyTime + 5] << 16) |
            ((ulong)ntpData[serverReplyTime + 6] << 8) |
            ntpData[serverReplyTime + 7];

        ulong milliseconds = (intPart * 1000UL) + ((fractPart * 1000UL) / 0x100000000UL);

        DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return ntpEpoch.AddMilliseconds((long)milliseconds);
    }
}