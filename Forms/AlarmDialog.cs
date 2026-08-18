using System;
using System.Drawing;
using System.Windows.Forms;
using StockWatcher.Models;

namespace StockWatcher.Forms
{
	public class AlarmDialog : Form
	{
		private Label _lblInfo;
		private Button _btnOk;
		private Button _btnSnooze;

		public bool Snoozed { get; private set; } = false;

		public AlarmDialog(
			WatchlistEntry entry,
			bool isUpperAlarm,
			string limitText,
			string currentText)
		{
			string alarmPrefix = entry.EntryType == WatchlistEntryType.BuyCandidate
				? "Alarm Watchlist" : "Alarm Bestand";
			Text = $"⚠ {alarmPrefix}";
			ClientSize = new Size(440, 180);
			StartPosition = FormStartPosition.CenterScreen;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			TopMost = true;

			string direction = isUpperAlarm ? "Oberes Limit" : "Unteres Limit";

			_lblInfo = new Label
			{
				Text = $"{entry.Name} ({entry.Isin})\r\n\r\n" +
				       $"Kurs: {currentText}  |  {direction}: {limitText}\r\n\r\n" +
				       $"Stand: {entry.LastUpdate:HH:mm:ss}",
				Location = new Point(16, 16),
				Size = new Size(400, 100),
				Font = new Font("Segoe UI", 10f),
				ForeColor = isUpperAlarm ? Color.DarkGreen : Color.DarkRed
			};

			_btnOk = new Button
			{
				Text = "OK",
				Location = new Point(184, 132),
				Size = new Size(90, 32),
				DialogResult = DialogResult.OK,
				UseVisualStyleBackColor = true
			};

			_btnSnooze = new Button
			{
				Text = "Snooze (1 Zyklus)",
				Location = new Point(284, 132),
				Size = new Size(140, 32),
				UseVisualStyleBackColor = true
			};
			_btnSnooze.Click += (s, e) =>
			{
				Snoozed = true;
				DialogResult = DialogResult.Cancel;
				Close();
			};

			Controls.Add(_lblInfo);
			Controls.Add(_btnOk);
			Controls.Add(_btnSnooze);
			AcceptButton = _btnOk;
		}
	}
}
