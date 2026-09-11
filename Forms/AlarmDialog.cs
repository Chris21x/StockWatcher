using System;
using System.Drawing;
using System.Windows.Forms;
using StockWatcher.Localization;
using StockWatcher.Models;

namespace StockWatcher.Forms
{
	public class AlarmDialog : Form
	{
		private Button _btnOk;
		private Button _btnSnooze;

		public bool Snoozed { get; private set; } = false;

		public AlarmDialog(
			WatchlistEntry entry,
			bool isUpperAlarm,
			string dialogTitle,
			string limitText,
			string currentText)
		{
			Text = dialogTitle;
			ClientSize = new Size(540, 250);
			StartPosition = FormStartPosition.CenterScreen;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			TopMost = false;

			Color alarmColor = isUpperAlarm ? Color.DarkGreen : Color.DarkRed;
			var normalFont = new Font("Segoe UI", 10f);
			var boldFont = new Font("Segoe UI", 10f, FontStyle.Bold);

			string securityLine = L10n.Format("AlarmDialogSecurity", entry.Name, entry.Isin);
			string limitLine = L10n.Format(
				isUpperAlarm ? "AlarmDialogLimitUpper" : "AlarmDialogLimitLower",
				limitText);
			string currentLine = L10n.Format("AlarmDialogCurrent", currentText);
			string asOfLine = L10n.Format("AlarmDialogAsOf", entry.LastUpdate);

			Controls.Add(MakeLabel(
				dialogTitle,
				16, 16, 508, 22, boldFont, alarmColor));

			// Sachlicher Titel, danach bewusst eine Leerzeile vor dem Wertpapier.
			Controls.Add(MakeLabel(
				securityLine,
				16, 52, 508, 22, normalFont, alarmColor));

			Controls.Add(MakeLabel(
				limitLine,
				16, 82, 508, 22, normalFont, alarmColor));

			Controls.Add(MakeLabel(
				currentLine,
				16, 104, 508, 22, normalFont, alarmColor));

			Controls.Add(MakeLabel(
				asOfLine,
				16, 148, 508, 22, normalFont, alarmColor));

			_btnOk = new Button
			{
				Text = L10n.Text("ButtonOk"),
				Location = new Point(284, 205),
				Size = new Size(90, 32),
				UseVisualStyleBackColor = true
			};
			_btnOk.Click += (s, e) => Close();

			_btnSnooze = new Button
			{
				Text = L10n.Text("SnoozeOneCycle"),
				Location = new Point(384, 205),
				Size = new Size(140, 32),
				UseVisualStyleBackColor = true
			};
			_btnSnooze.Click += (s, e) =>
			{
				Snoozed = true;
				Close();
			};

			Controls.Add(_btnOk);
			Controls.Add(_btnSnooze);
			AcceptButton = _btnOk;
		}

		private static Label MakeLabel(
			string text,
			int x,
			int y,
			int width,
			int height,
			Font font,
			Color color)
		{
			return new Label
			{
				Text = text ?? "",
				Location = new Point(x, y),
				Size = new Size(width, height),
				Font = font,
				ForeColor = color,
				AutoEllipsis = true
			};
		}
	}
}
