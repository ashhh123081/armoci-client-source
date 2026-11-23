using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Vape;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		string[] arguments = Environment.GetCommandLineArgs();
		if (arguments.Length > 1 && arguments[1] == "--service")
		{
			RunService();
			return;
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new Form1());
	}

	private static void RunService()
	{
		VapeStore.EnsureLoaded();
		VapeService.Start();
		VapeZeusServer.Start();
		VapeTrace.Write("service mode: rest=" + VapeService.IsRunning
				+ " zeus=" + VapeZeusServer.IsRunning);
		while (true)
		{
			Thread.Sleep(15000);
			if (Process.GetProcessesByName("java").Length == 0
					&& Process.GetProcessesByName("javaw").Length == 0)
			{
				return;
			}
		}
	}
}
