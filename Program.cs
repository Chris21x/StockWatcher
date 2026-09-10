using System;
using System.Windows.Forms;
using StockWatcher.Localization;
using StockWatcher.Services;

namespace StockWatcher
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			LanguageManager.Initialize();
			WindowsToastNotifier.InitializeProcessIdentity();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
