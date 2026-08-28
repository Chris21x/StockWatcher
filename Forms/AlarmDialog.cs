using System;
using System.Drawing;
using System.Windows.Forms;
using StockWatcher.Localization;
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
				? L10n.Text("AlarmWatchlist") : L10n.Text("AlarmHolding");
			Text = $"⚠ {alarmPrefix}";
			ClientSize = new Size(440, 180);
			StartPosition = FormStartPosition.CenterScreen;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			TopMost = true;

			string direction = isUpperAlarm ? L10n.Text("UpperLimit") : L10n.Text("LowerLimit");

			_lblInfo = new Label
			{
				Text = L10n.Format("AlarmDialogInfo", entry.Name, entry.Isin, currentText, direction, limitText, entry.LastUpdate),
				Location = new Point(16, 16),
				Size = new Size(400, 100),
				Font = new Font("Segoe UI", 10f),
				ForeColor = isUpperAlarm ? Color.DarkGreen : Color.DarkRed
			};

			_btnOk = new Button
			{
				Text = L10n.Text("ButtonOk"),
				Location = new Point(184, 132),
				Size = new Size(90, 32),
				DialogResult = DialogResult.OK,
				UseVisualStyleBackColor = true
			};

			_btnSnooze = new Button
			{
				Text = L10n.Text("SnoozeOneCycle"),
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
