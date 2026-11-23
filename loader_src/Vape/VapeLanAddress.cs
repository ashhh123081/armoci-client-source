using System;
using System.Net;
using System.Net.Sockets;

namespace Vape
{
    internal static class VapeLanAddress
    {
        public static string Get()
        {
            try
            {
                using (UdpClient probe = new UdpClient())
                {
                    probe.Connect("8.8.8.8", 65530);
                    IPEndPoint local = (IPEndPoint)probe.Client.LocalEndPoint;
                    if (local != null && !IPAddress.IsLoopback(local.Address))
                    {
                        return local.Address.ToString();
                    }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch (Exception)
            {
            }
            return "127.0.0.1";
        }
    }
}
