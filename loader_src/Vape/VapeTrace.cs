using System;
using System.IO;

namespace Vape
{
    internal static class VapeTrace
    {
        public static void Write(string text)
        {
            try
            {
                File.AppendAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "vape-loader-debug.log"),
                        "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] "
                                + text + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }
}
