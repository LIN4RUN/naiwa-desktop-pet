using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NaiwaDesktop;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Native.SetProcessDPIAware();
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		if (args.Length >= 2 && args[0] == "--self-test")
		{
			Tests.Run(args[1]);
			return;
		}
		if (args.Length >= 2 && args[0] == "--demo")
		{
			Tests.Demo(args[1]);
			return;
		}
		if (args.Length >= 2 && args[0] == "--drop-test")
		{
			Tests.DropTest(args[1]);
			return;
		}
		if (args.Length >= 2 && args[0] == "--probe")
		{
			IntPtr intPtr = Native.FindWindow(null, "NaiwaDesktop.Source.v1");
			Native.GetWindowRect(intPtr, out var r);
			File.WriteAllText(args[1], "Found=" + (intPtr != IntPtr.Zero) + "\r\nVisible=" + Native.IsWindowVisible(intPtr) + "\r\nBounds=" + r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + "\r\nState=" + Native.SendMessage(intPtr, 32770, IntPtr.Zero, IntPtr.Zero), Encoding.UTF8);
			return;
		}
		if (args.Length == 1 && args[0] == "--quit")
		{
			Native.PostMessage(Native.FindWindow(null, "NaiwaDesktop.Source.v1"), 16, IntPtr.Zero, IntPtr.Zero);
			return;
		}
		if (args.Length == 1 && args[0] == "--hungry")
		{
			IntPtr intPtr2 = Native.FindWindow(null, "NaiwaDesktop.Source.v1");
			bool flag = Native.PostMessage(intPtr2, 32771, IntPtr.Zero, IntPtr.Zero);
			try
			{
				File.AppendAllText(Path.Combine(Settings.Folder, "hungry.log"), DateTime.Now.ToString("s") + " SEND hwnd=" + intPtr2 + " ok=" + flag + Environment.NewLine);
				return;
			}
			catch
			{
				return;
			}
		}
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "Local\\NaiwaDesktop.Source.v1", out createdNew))
		{
			if (!createdNew)
			{
				Native.PostMessage(Native.FindWindow(null, "NaiwaDesktop.Source.v1"), 32769, IntPtr.Zero, IntPtr.Zero);
				return;
			}
			Application.ThreadException += (object s, ThreadExceptionEventArgs e2) =>
			{
				Report(e2.Exception);
				Application.Exit();
			};
			try
			{
				using Assets a = new Assets(AppDomain.CurrentDomain.BaseDirectory);
				using PetWindow mainForm = new PetWindow(a, test: false);
				Application.Run(mainForm);
			}
			catch (Exception e)
			{
				Report(e);
			}
		}
	}

	private static void Report(Exception e)
	{
		try
		{
			Directory.CreateDirectory(Settings.Folder);
			File.AppendAllText(Path.Combine(Settings.Folder, "error.log"), string.Concat(DateTime.Now.ToString("s"), " ", e, Environment.NewLine));
		}
		catch
		{
		}
		MessageBox.Show("奶蛙暂时没能启动。请将程序、素材.nwp 和原版笑声.wav 放在同一个文件夹。\n\n" + e.Message, "奶蛙桌宠", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}
}
