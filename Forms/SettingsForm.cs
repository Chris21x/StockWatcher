using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using StockWatcher.Models;

namespace StockWatcher.Forms
{
	public class SettingsForm : Form
	{
		// Allgemein
		private NumericUpDown _nudInterval;
		private NumericUpDown _nudDataTimeout;
		private CheckBox      _chkStartMinimized;
		private TextBox       _txtDataFile;

		// Benachrichtigungen
		private CheckBox _chkBalloon;
		private CheckBox _chkAlarmDialog;
		private CheckBox _chkTrayDot;

		// Alarme (ntfy-Push)
		private CheckBox _chkNtfy;
		private TextBox  _txtNtfyTopic;
		private TextBox  _txtNtfyUrl;
		private Button   _btnNtfyTest;

		// Watchlist
		private DataGridView _grid;
		private Button       _btnAdd;
		private Button       _btnRemove;
		private Button       _btnOk;
		private Button       _btnCancel;

		public AppSettings Settings { get; private set; }

		public SettingsForm(AppSettings current)
		{
			Settings = current;

			Text            = "Einstellungen – Stock Watcher";
			Size            = new Size(700, 720);
			MinimumSize     = new Size(620, 650);
			StartPosition   = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.Sizable;
			MaximizeBox     = true;
			MinimizeBox     = false;

			BuildUi();
			LoadFromSettings();
		}

		private void BuildUi()
		{
			var font   = new Font("Segoe UI", 9f);
			var fontSm = new Font("Segoe UI", 8.5f);

			// ---- Abrufintervall ----
			Controls.Add(MakeLbl("Abrufintervall (Minuten):", 12, 16, 185, font));
			_nudInterval = new NumericUpDown
			{
				Location = new Point(200, 12),
				Size     = new Size(70, 24),
				Minimum  = 1, Maximum = 60, Value = 10,
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left
			};
			Controls.Add(_nudInterval);

			// ---- Daten-Timeout ----
			Controls.Add(MakeLbl("Daten-Timeout (Min.):", 290, 16, 145, font));
			_nudDataTimeout = new NumericUpDown
			{
				Location = new Point(435, 12),
				Size     = new Size(70, 24),
				Minimum  = 0, Maximum = 10080, Value = 240,
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left
			};
			Controls.Add(_nudDataTimeout);

			_chkStartMinimized = new CheckBox
			{
				Text     = "Starte minimiert",
				Location = new Point(525, 14),
				AutoSize = true,
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left
			};
			Controls.Add(_chkStartMinimized);

			// ---- Datendatei ----
			Controls.Add(MakeLbl("Datendatei (XML):", 12, 54, 135, font));
			_txtDataFile = new TextBox
			{
				Location = new Point(150, 50),
				Size     = new Size(416, 24),
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
			};
			Controls.Add(_txtDataFile);
			var btnBrowse = new Button
			{
				Text   = "Durchsuchen…",
				Location = new Point(574, 49),
				Size   = new Size(110, 26),
				Font   = font,
				Anchor = AnchorStyles.Top | AnchorStyles.Right
			};
			btnBrowse.Click += BtnBrowse_Click;
			Controls.Add(btnBrowse);

			// ---- Gruppe: Benachrichtigungen ----
			var grpNotify = new GroupBox
			{
				Text     = "Benachrichtigungen bei Kurs-Alarm",
				Location = new Point(12, 88),
				Size     = new Size(660, 56),
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
			};
			_chkBalloon = new CheckBox { Text = "Balloon-Tipp",        Location = new Point(12, 24), AutoSize = true, Font = font };
			_chkAlarmDialog = new CheckBox { Text = "AlarmDialog",     Location = new Point(130, 24), AutoSize = true, Font = font };
			_chkTrayDot = new CheckBox { Text = "Rote Markierung am Tray-Icon", Location = new Point(248, 24), AutoSize = true, Font = font };
			grpNotify.Controls.AddRange(new Control[] { _chkBalloon, _chkAlarmDialog, _chkTrayDot });
			Controls.Add(grpNotify);

			// ---- Gruppe: Alarme (ntfy-Push) ----
			var grpAlarme = new GroupBox
			{
				Text     = "Alarme – Push-Benachrichtigungen (ntfy.sh)",
				Location = new Point(12, 156),
				Size     = new Size(660, 106),
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
			};

			_chkNtfy = new CheckBox
			{
				Text     = "Push-Benachrichtigungen aktivieren",
				Location = new Point(12, 22),
				AutoSize = true,
				Font     = font
			};

			Controls.AddRange(new Control[] { grpAlarme });

			grpAlarme.Controls.Add(MakeLbl("Topic:", 12, 50, 48, font));
			_txtNtfyTopic = new TextBox
			{
				Location = new Point(62, 47),
				Size     = new Size(340, 22),
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
			};
			_btnNtfyTest = new Button
			{
				Text   = "Testen",
				Location = new Point(410, 46),
				Size   = new Size(76, 24),
				Font   = font,
				Anchor = AnchorStyles.Top | AnchorStyles.Right
			};
			_btnNtfyTest.Click += BtnNtfyTest_Click;

			grpAlarme.Controls.Add(MakeLbl("Server:", 12, 78, 48, font));
			_txtNtfyUrl = new TextBox
			{
				Location = new Point(62, 75),
				Size     = new Size(340, 22),
				Font     = font,
				Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
			};
			var lblNtfyHint = new Label
			{
				Text      = "Standard: https://ntfy.sh",
				Location  = new Point(410, 78),
				AutoSize  = true,
				Font      = fontSm,
				ForeColor = SystemColors.GrayText
			};

			grpAlarme.Controls.AddRange(new Control[]
			{
				_chkNtfy, _txtNtfyTopic, _btnNtfyTest, _txtNtfyUrl, lblNtfyHint
			});

			// ---- Watchlist-Grid ----
			Controls.Add(MakeLbl("Watchlist  (Limit-Einheit wird im Eintrag unter Bearbeiten festgelegt):", 12, 274, 400, font));
			_grid = new DataGridView
			{
				Location                    = new Point(12, 296),
				Size                        = new Size(660, 290),
				AllowUserToAddRows          = false,
				AllowUserToDeleteRows       = false,
				SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
				AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
				Font                        = font,
				RowHeadersVisible           = false,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
				Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
			};
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIsin",      HeaderText = "ISIN",          FillWeight = 22 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",      HeaderText = "Name",          FillWeight = 30 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUpper",     HeaderText = "Limit ▲",       FillWeight = 14 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUpperUnit", HeaderText = "Einheit ▲",     FillWeight = 10, ReadOnly = true });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLower",     HeaderText = "Limit ▼",       FillWeight = 14 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLowerUnit", HeaderText = "Einheit ▼",     FillWeight = 10, ReadOnly = true });
			Controls.Add(_grid);

			// ---- Buttons ----
			_btnAdd = new Button
			{
				Text = "Hinzufügen", Location = new Point(12, 600),
				Size = new Size(110, 30), Font = font,
				Anchor = AnchorStyles.Bottom | AnchorStyles.Left
			};
			_btnAdd.Click += BtnAdd_Click;

			_btnRemove = new Button
			{
				Text = "Entfernen", Location = new Point(130, 600),
				Size = new Size(110, 30), Font = font,
				Anchor = AnchorStyles.Bottom | AnchorStyles.Left
			};
			_btnRemove.Click += BtnRemove_Click;

			_btnOk = new Button
			{
				Text = "OK", Location = new Point(470, 600),
				Size = new Size(100, 30), Font = font,
				DialogResult = DialogResult.OK,
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right
			};
			_btnOk.Click += BtnOk_Click;

			_btnCancel = new Button
			{
				Text = "Abbrechen", Location = new Point(578, 600),
				Size = new Size(100, 30), Font = font,
				DialogResult = DialogResult.Cancel,
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right
			};

			Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnOk, _btnCancel });
			AcceptButton = _btnOk;
			CancelButton = _btnCancel;
		}

		private void LoadFromSettings()
		{
			_nudInterval.Value = Math.Max(1, Math.Min(60, Settings.IntervalMinutes));
			_nudDataTimeout.Value = Math.Max(0, Math.Min(10080, Settings.DataRetrievalTimeoutMinutes));
			_chkStartMinimized.Checked = Settings.StartMinimized;
			_txtDataFile.Text  = Settings.DataFilePath ?? "";

			_chkBalloon.Checked     = Settings.NotifyBalloon;
			_chkAlarmDialog.Checked = Settings.NotifyAlarmDialog;
			_chkTrayDot.Checked     = Settings.NotifyTrayDot;

			_chkNtfy.Checked   = Settings.NtfyEnabled;
			_txtNtfyTopic.Text = Settings.NtfyTopic ?? "";
			_txtNtfyUrl.Text   = string.IsNullOrWhiteSpace(Settings.NtfyUrl)
			                     ? "https://ntfy.sh" : Settings.NtfyUrl;

			_grid.Rows.Clear();
			foreach (WatchlistEntry e in Settings.Watchlist)
				_grid.Rows.Add(e.Isin, e.Name,
				               FormatLimitValue(e.LimitUpper, e.LimitUpperType), GetLimitUnit(e, true),
				               FormatLimitValue(e.LimitLower, e.LimitLowerType), GetLimitUnit(e, false));
		}

		private void BtnBrowse_Click(object sender, EventArgs e)
		{
			using (var dlg = new SaveFileDialog())
			{
				dlg.Title = "Datendatei wählen oder neu anlegen";
				dlg.Filter = "XML-Datei (*.xml)|*.xml|Alle Dateien (*.*)|*.*";
				dlg.DefaultExt = "xml";
				dlg.FileName = System.IO.Path.GetFileName(_txtDataFile.Text);
				string dir = string.IsNullOrEmpty(_txtDataFile.Text) ? ""
				             : System.IO.Path.GetDirectoryName(_txtDataFile.Text);
				if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
					dlg.InitialDirectory = dir;
				if (dlg.ShowDialog(this) == DialogResult.OK)
					_txtDataFile.Text = dlg.FileName;
			}
		}

		private async void BtnNtfyTest_Click(object sender, EventArgs e)
		{
			string topic = _txtNtfyTopic.Text.Trim();
			string url   = _txtNtfyUrl.Text.Trim().TrimEnd('/');
			if (string.IsNullOrEmpty(topic))
			{
				MessageBox.Show("Bitte einen Topic-Namen eingeben.", "Topic fehlt",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			if (string.IsNullOrEmpty(url)) url = "https://ntfy.sh";

			_btnNtfyTest.Enabled = false;
			_btnNtfyTest.Text    = "…";
			try
			{
				using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
				{
					string title = "Stock Watcher – Test";
					string requestUrl = $"{url}/{topic}?title={Uri.EscapeDataString(title)}&priority=default";
					var req = new HttpRequestMessage(HttpMethod.Post, requestUrl);
					req.Content = new StringContent(
						"Verbindungstest von Stock Watcher.",
						Encoding.UTF8, "text/plain");

					HttpResponseMessage resp = await http.SendAsync(req);
					if (resp.IsSuccessStatusCode)
						MessageBox.Show(
							"Test-Push wurde gesendet.\nPrüfe dein Mobilgerät.",
							"Erfolgreich", MessageBoxButtons.OK, MessageBoxIcon.Information);
					else
						MessageBox.Show(
							$"Server antwortete mit HTTP {(int)resp.StatusCode}.",
							"Fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Verbindungsfehler:\n{ex.Message}",
					"Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				_btnNtfyTest.Enabled = true;
				_btnNtfyTest.Text    = "Testen";
			}
		}

		private void BtnAdd_Click(object sender, EventArgs e)
		{
			_grid.Rows.Add("", "", "0,00", "?", "0,00", "?");
			int r = _grid.Rows.Count - 1;
			_grid.CurrentCell = _grid.Rows[r].Cells["colIsin"];
			_grid.BeginEdit(true);
		}

		private void BtnRemove_Click(object sender, EventArgs e)
		{
			if (_grid.SelectedRows.Count == 0) return;
			foreach (DataGridViewRow row in _grid.SelectedRows)
				if (!row.IsNewRow) _grid.Rows.Remove(row);
		}

		private void BtnOk_Click(object sender, EventArgs e)
		{
			_grid.EndEdit();

			Settings.IntervalMinutes = (int)_nudInterval.Value;
			Settings.DataRetrievalTimeoutMinutes = (int)_nudDataTimeout.Value;
			Settings.StartMinimized = _chkStartMinimized.Checked;
			Settings.NotifyBalloon    = _chkBalloon.Checked;
			Settings.NotifyAlarmDialog = _chkAlarmDialog.Checked;
			Settings.NotifyTrayDot    = _chkTrayDot.Checked;

			Settings.NtfyEnabled = _chkNtfy.Checked;
			Settings.NtfyTopic   = _txtNtfyTopic.Text.Trim();
			Settings.NtfyUrl     = _txtNtfyUrl.Text.Trim().TrimEnd('/');

			string newPath = _txtDataFile.Text.Trim();
			if (!string.IsNullOrEmpty(newPath)) Settings.DataFilePath = newPath;

			// Alle nicht im Grid bearbeiteten Daten bestehender Einträge erhalten
			var extra = new System.Collections.Generic.Dictionary<string, WatchlistEntry>(
				System.StringComparer.OrdinalIgnoreCase);
			foreach (WatchlistEntry ex in Settings.Watchlist)
				extra[ex.Isin] = ex;

			Settings.Watchlist.Clear();
			foreach (DataGridViewRow row in _grid.Rows)
			{
				string isin = (row.Cells["colIsin"].Value?.ToString() ?? "").Trim();
				string name = (row.Cells["colName"].Value?.ToString() ?? "").Trim();
				if (string.IsNullOrEmpty(isin)) continue;

				// Bestehenden Eintrag wiederverwenden, damit alle Felder erhalten bleiben,
				// die im Settings-Grid nicht bearbeitet werden (Symbol, Referenz-/FX-Daten, Laufzeitstatus).
				if (!extra.TryGetValue(isin, out WatchlistEntry entry))
					entry = new WatchlistEntry();

				entry.Isin       = isin;
				entry.Name       = string.IsNullOrEmpty(name) ? isin : name;
				entry.LimitUpper = ParseDouble(row.Cells["colUpper"].Value?.ToString());
				entry.LimitLower = ParseDouble(row.Cells["colLower"].Value?.ToString());

				Settings.Watchlist.Add(entry);
			}
		}

		// -----------------------------------------------------------------------
		// Hilfsmethoden
		// -----------------------------------------------------------------------


		private static string FormatLimitValue(double value, LimitValueType type)
		{
			if (type == LimitValueType.Percent)
			{
				double displayValue = Math.Abs(value) < 0.005 ? 0.0 : value;
				return $"{(displayValue > 0 ? "+" : "")}{displayValue:N2}";
			}
			return value.ToString("N2");
		}

		private static string GetLimitUnit(WatchlistEntry entry, bool isUpper)
		{
			LimitValueType type = isUpper ? entry.LimitUpperType : entry.LimitLowerType;
			if (type == LimitValueType.Percent) return "%";
			string currency = entry.AbsoluteLimitCurrency;
			return string.IsNullOrWhiteSpace(currency) ? "?" : currency;
		}

		private static Label MakeLbl(string text, int x, int y, int w, Font font)
		{
			return new Label { Text = text, Location = new Point(x, y), Width = w, Font = font, AutoSize = false };
		}

		private static double ParseDouble(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return 0.0;
			if (double.TryParse(raw.Replace(",", "."),
				System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture,
				out double v)) return v;
			return 0.0;
		}
	}
}
