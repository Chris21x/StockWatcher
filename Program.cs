using System;
using System.Windows.Forms;
using StockWatcher.Localization;

namespace StockWatcher
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			LanguageManager.Initialize();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
