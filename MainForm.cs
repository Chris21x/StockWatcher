using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using StockWatcher.Forms;
using StockWatcher.Models;
using StockWatcher.Services;

namespace StockWatcher
{
	public class MainForm : Form
	{
		// P/Invoke: Fenster wirklich in den Vordergrund bringen
		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		// UI-Elemente
		private ListView _listView;
		private ToolStrip _toolStrip;
		private StatusStrip _statusStrip;
		private ToolStripStatusLabel _lblStatus;
		private ToolStripStatusLabel _lblPortfolioSummary;
		private ToolStripStatusLabel _lblNextUpdate;
		private System.Windows.Forms.Timer _timer;
		private System.Windows.Forms.Timer _countdownTimer;
		private System.Windows.Forms.Timer _layoutSaveTimer;
		private NotifyIcon _notifyIcon;
		private ContextMenuStrip _trayMenu;

		// Sortierung
		private readonly ListViewSorter _sorter = new ListViewSorter();
		private int _sortCol = -1;
		private SortOrder _sortDir = SortOrder.None;

		// Daten & Dienste
		private AppSettings _settings;
		private readonly StockFrankfurtClient _client;
		private bool _fetchRunning = false;
		private DateTime _nextFetchTime = DateTime.MinValue;
		private bool _restoringLayout = false;
		private double? _previousPortfolioMarketValueEur = null;
		private string _portfolioTrendIndicator = "◀▶";
		private readonly Dictionary<WatchlistEntry, string> _priceTrendIndicators =
			new Dictionary<WatchlistEntry, string>();
		private readonly Dictionary<WatchlistEntry, int> _priceTrendDirections =
			new Dictionary<WatchlistEntry, int>();
		private readonly Dictionary<WatchlistEntry, int> _priceTrendCounts =
			new Dictionary<WatchlistEntry, int>();

		// Icons
		private Icon _baseIcon;
		private Icon _dotIcon;    // _baseIcon + roter Punkt unten rechts
		private bool _dotActive = false;
		private Font _disabledLimitFont;

		// ntfy-Push: eigener HttpClient (kein Cookie/Crumb-Handling nötig)
		private static readonly HttpClient _ntfyClient =
			new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

		public MainForm()
		{
			_settings = AppSettings.Load();
			_client   = new StockFrankfurtClient();

			_baseIcon = LoadAppIcon();
			_dotIcon  = BuildDotIcon(_baseIcon);
			Icon      = _baseIcon;

			BuildUi();
			RestoreWindowLayout();
			RestoreColumnOrder();
			RestoreColumnWidths();
			BuildTrayIcon();
			RefreshListView();
			StartTimers();

			if (_settings.StartMinimized)
				WindowState = FormWindowState.Minimized;

			_ = FetchAllQuotesAsync();
		}

		// -----------------------------------------------------------------------
		// UI-Aufbau
		// -----------------------------------------------------------------------

		private void BuildUi()
		{
			Text = "Stock Watcher";
			Size = new Size(1080, 520);
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(700, 400);

			// Menü
			var menuStrip = new MenuStrip();
			var menuAction = new ToolStripMenuItem("Aktion");
			var miRefresh = new ToolStripMenuItem("Jetzt abrufen", null, (s, e) => _ = FetchAllQuotesAsync());
			miRefresh.ShortcutKeys = Keys.F5;
			var miSettings = new ToolStripMenuItem("Einstellungen…", null, OpenSettings);
			miSettings.ShortcutKeys = Keys.Control | Keys.E;
			var miExit = new ToolStripMenuItem("Beenden", null, (s, e) => Application.Exit());
			menuAction.DropDownItems.AddRange(new ToolStripItem[]
				{ miRefresh, miSettings, new ToolStripSeparator(), miExit });
			menuStrip.Items.Add(menuAction);
			MainMenuStrip = menuStrip;

			// Toolbar
			_toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

			var btnRefresh = new ToolStripButton("↻  Abrufen (F5)")
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = "Kurse sofort abrufen"
			};
			btnRefresh.Click += (s, e) => _ = FetchAllQuotesAsync();

			var btnAdd = new ToolStripButton("＋  Hinzufügen")
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = "Neues Wertpapier hinzufügen"
			};
			btnAdd.Click += BtnAdd_Click;

			var btnEdit = new ToolStripButton("✎  Bearbeiten")
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = "Ausgewählten Eintrag bearbeiten (oder Doppelklick)"
			};
			btnEdit.Click += (s, e) => OpenEditDialog();

			var btnRemove = new ToolStripButton("✕  Entfernen")
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = "Ausgewählten Eintrag entfernen"
			};
			btnRemove.Click += BtnRemove_Click;

			_toolStrip.Items.Add(btnRefresh);
			_toolStrip.Items.Add(new ToolStripSeparator());
			_toolStrip.Items.Add(btnAdd);
			_toolStrip.Items.Add(btnEdit);
			_toolStrip.Items.Add(btnRemove);

			// ListView
			_listView = new ListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				GridLines = true,
				Font = new Font("Consolas", 9.5f),
				AllowColumnReorder = true
			};
			_listView.Columns.Add("Name", 240);
			_listView.Columns.Add("ISIN", 120);
			_listView.Columns.Add("Stk.", 55);
			_listView.Columns.Add("Kauf-/Referenzkurs", 140);
			_listView.Columns.Add("Kauf-/Referenzwert", 150);
			_listView.Columns.Add("▲▼", 45, HorizontalAlignment.Center);
			_listView.Columns.Add("Akt. Kurs", 130);
			_listView.Columns.Add("Marktwert", 135);
			_listView.Columns.Add("Diff. EUR", 105);
			_listView.Columns.Add("Diff. %", 80);
			_listView.Columns.Add("Limit ▼", 110);
			_listView.Columns.Add("Limit ▲", 110);
			_listView.Columns.Add("Eintragsart", 105);
			_listView.Columns.Add("Bemerkung", 300);
			_listView.Columns.Add("Status", 160); 
			_disabledLimitFont = new Font(_listView.Font, FontStyle.Italic);
			_listView.ListViewItemSorter = _sorter;
			_listView.DoubleClick += (s, e) => OpenEditDialog();
			_listView.ColumnClick += ListView_ColumnClick;
			_listView.ColumnReordered += (s, e) =>
			{
				if (!_restoringLayout)
					BeginInvoke(new Action(SaveColumnOrder));
			};
			_listView.ColumnWidthChanged += (s, e) => ScheduleLayoutSave();

			// Originaltexte der Spaltenköpfe sichern (für Pfeil-Indikator)
			foreach (ColumnHeader col in _listView.Columns)
				col.Tag = col.Text;

			// Statusleiste
			_lblStatus = new ToolStripStatusLabel("Bereit")
			{
				Spring = false,
				TextAlign = ContentAlignment.MiddleLeft
			};
			_lblPortfolioSummary = new ToolStripStatusLabel("| ◀▶ | 0 Pos. | – EUR | – EUR")
			{
				Spring = false,
				TextAlign = ContentAlignment.MiddleLeft
			};
			_lblNextUpdate = new ToolStripStatusLabel("Nächster Abruf: –")
			{
				Spring = true,
				TextAlign = ContentAlignment.MiddleRight
			};
			_statusStrip = new StatusStrip();
			_statusStrip.Items.AddRange(new ToolStripItem[]
			{
				_lblStatus,
				_lblPortfolioSummary,
				_lblNextUpdate
			});

			// Reihenfolge in Controls entscheidet über Dock-Anordnung (letzte = unten/aussen)
			Controls.Add(_listView);      // Fill – wird als letztes platziert
			Controls.Add(_toolStrip);     // Top – unter Menü
			Controls.Add(menuStrip);      // Top – ganz oben
			Controls.Add(_statusStrip);   // Bottom – ganz unten

			FormClosing += MainForm_FormClosing;
			LocationChanged += (s, e) => ScheduleLayoutSave();
			SizeChanged += (s, e) => ScheduleLayoutSave();
			ResizeEnd += (s, e) => ScheduleLayoutSave();
		}

		private void BuildTrayIcon()
		{
			_trayMenu = new ContextMenuStrip();
			_trayMenu.Items.Add("App anzeigen", null, (s, e) => ShowMainWindow());
			_trayMenu.Items.Add("Jetzt abrufen", null, (s, e) => _ = FetchAllQuotesAsync());
			_trayMenu.Items.Add(new ToolStripSeparator());
			_trayMenu.Items.Add("Beenden", null, (s, e) => Application.Exit());

			_notifyIcon = new NotifyIcon
			{
				Icon             = _baseIcon,
				Text             = "Stock Watcher",
				ContextMenuStrip = _trayMenu,
				Visible          = true
			};
			_notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
		}

		private void ShowMainWindow()
		{
			if (!Visible) Show();
			if (WindowState == FormWindowState.Minimized)
				WindowState = FormWindowState.Normal;
			Activate();
			SetForegroundWindow(Handle);
			ClearTrayDot();
		}

		// -----------------------------------------------------------------------
		// Fensterlayout / Spaltenbreiten speichern und wiederherstellen
		// -----------------------------------------------------------------------

		private void ScheduleLayoutSave()
		{
			if (_restoringLayout || IsDisposed) return;

			if (_layoutSaveTimer == null)
			{
				_layoutSaveTimer = new System.Windows.Forms.Timer { Interval = 500 };
				_layoutSaveTimer.Tick += (s, e) =>
				{
					_layoutSaveTimer.Stop();
					SaveUiLayout();
				};
			}

			_layoutSaveTimer.Stop();
			_layoutSaveTimer.Start();
		}

		private void SaveUiLayout()
		{
			if (_restoringLayout || _settings == null || _listView == null) return;

			SaveColumnWidths();
			SaveWindowLayout();
			_settings.Save();
		}

		private void SaveColumnWidths()
		{
			var parts = new string[_listView.Columns.Count];
			for (int i = 0; i < _listView.Columns.Count; i++)
				parts[i] = _listView.Columns[i].Width.ToString(CultureInfo.InvariantCulture);
			_settings.ColumnWidths = string.Join(",", parts);
		}

		private void RestoreColumnWidths()
		{
			if (string.IsNullOrWhiteSpace(_settings.ColumnWidths)) return;

			string[] parts = _settings.ColumnWidths.Split(',');

			// Bei geänderter Spaltenstruktur bewusst das neue Default-Layout verwenden.
			if (parts.Length != _listView.Columns.Count) return;

			_restoringLayout = true;
			try
			{
				for (int i = 0; i < parts.Length; i++)
				{
					if (int.TryParse(parts[i].Trim(), NumberStyles.Integer,
						CultureInfo.InvariantCulture, out int width) && width > 0)
					{
						_listView.Columns[i].Width = Math.Max(30, Math.Min(5000, width));
					}
				}
			}
			finally
			{
				_restoringLayout = false;
			}
		}

		private void SaveWindowLayout()
		{
			Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
			if (bounds.Width > 0 && bounds.Height > 0)
			{
				_settings.MainWindowLeft = bounds.Left;
				_settings.MainWindowTop = bounds.Top;
				_settings.MainWindowWidth = bounds.Width;
				_settings.MainWindowHeight = bounds.Height;
			}

			// Minimiert wird nie als Startzustand gespeichert. Beim Minimieren bleibt
			// der zuletzt bekannte Normal-/Maximiert-Zustand erhalten.
			if (WindowState != FormWindowState.Minimized)
				_settings.MainWindowMaximized = WindowState == FormWindowState.Maximized;
		}

		private void RestoreWindowLayout()
		{
			if (_settings.MainWindowWidth <= 0 || _settings.MainWindowHeight <= 0) return;

			var requested = new Rectangle(
				_settings.MainWindowLeft,
				_settings.MainWindowTop,
				_settings.MainWindowWidth,
				_settings.MainWindowHeight);

			Rectangle safe = GetSafeWindowBounds(requested);
			_restoringLayout = true;
			try
			{
				StartPosition = FormStartPosition.Manual;
				Bounds = safe;
				if (_settings.MainWindowMaximized)
					WindowState = FormWindowState.Maximized;
			}
			finally
			{
				_restoringLayout = false;
			}
		}

		private Rectangle GetSafeWindowBounds(Rectangle requested)
		{
			Screen target = null;
			long bestVisibleArea = 0;

			foreach (Screen screen in Screen.AllScreens)
			{
				Rectangle intersection = Rectangle.Intersect(requested, screen.WorkingArea);
				long area = (long)Math.Max(0, intersection.Width) * Math.Max(0, intersection.Height);
				if (area > bestVisibleArea)
				{
					bestVisibleArea = area;
					target = screen;
				}
			}

			// Wurde der frühere Monitor entfernt, existiert keine Überschneidung mehr.
			// Dann auf dem Primärmonitor zentrieren statt das Fenster unsichtbar zu öffnen.
			if (target == null || bestVisibleArea == 0)
			{
				target = Screen.PrimaryScreen ?? Screen.AllScreens[0];
				Rectangle work = target.WorkingArea;
				int width = Math.Min(Math.Max(requested.Width, Math.Min(MinimumSize.Width, work.Width)), work.Width);
				int height = Math.Min(Math.Max(requested.Height, Math.Min(MinimumSize.Height, work.Height)), work.Height);
				return new Rectangle(
					work.Left + (work.Width - width) / 2,
					work.Top + (work.Height - height) / 2,
					width, height);
			}

			Rectangle workingArea = target.WorkingArea;
			int safeWidth = Math.Min(
				Math.Max(requested.Width, Math.Min(MinimumSize.Width, workingArea.Width)),
				workingArea.Width);
			int safeHeight = Math.Min(
				Math.Max(requested.Height, Math.Min(MinimumSize.Height, workingArea.Height)),
				workingArea.Height);

			int safeLeft = Math.Max(workingArea.Left,
				Math.Min(requested.Left, workingArea.Right - safeWidth));
			int safeTop = Math.Max(workingArea.Top,
				Math.Min(requested.Top, workingArea.Bottom - safeHeight));

			return new Rectangle(safeLeft, safeTop, safeWidth, safeHeight);
		}

		// -----------------------------------------------------------------------
		// Spaltenreihenfolge speichern / wiederherstellen
		// -----------------------------------------------------------------------

		private void SaveColumnOrder()
		{
			var parts = new string[_listView.Columns.Count];
			for (int i = 0; i < _listView.Columns.Count; i++)
				parts[i] = _listView.Columns[i].DisplayIndex.ToString();
			_settings.ColumnOrder = string.Join(",", parts);
			_settings.Save();
		}

		private void RestoreColumnOrder()
		{
			if (string.IsNullOrWhiteSpace(_settings.ColumnOrder)) return;
			string[] parts = _settings.ColumnOrder.Split(',');

			// Bei geänderter Spaltenstruktur bewusst das neue Default-Layout verwenden.
			if (parts.Length != _listView.Columns.Count) return;

			try
			{
				// Aufsteigend nach Ziel-DisplayIndex setzen, um Konflikte zu vermeiden
				var pairs = new System.Collections.Generic.List<(int col, int disp)>();
				for (int i = 0; i < parts.Length; i++)
				{
					if (!int.TryParse(parts[i].Trim(), out int d)) return;
					pairs.Add((i, d));
				}

				pairs.Sort((a, b) => a.disp.CompareTo(b.disp));

				foreach (var (col, disp) in pairs)
					_listView.Columns[col].DisplayIndex = disp;
			}
			catch
			{
				/* Fehler beim Wiederherstellen ignorieren */
			}
		}

		// -----------------------------------------------------------------------
		// Spalten sortieren
		// -----------------------------------------------------------------------

		private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			// Gleiche Spalte → Richtung umkehren; neue Spalte → aufsteigend
			if (e.Column == _sortCol)
				_sortDir = _sortDir == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
			else
			{
				_sortCol = e.Column;
				_sortDir = SortOrder.Ascending;
			}

			// Pfeil-Indikator in Spaltenköpfen aktualisieren
			foreach (ColumnHeader col in _listView.Columns)
				col.Text = (string)col.Tag;
			_listView.Columns[_sortCol].Text = (string)_listView.Columns[_sortCol].Tag +
				(_sortDir == SortOrder.Ascending ? "  ▲" : "  ▼");

			_sorter.Column = _sortCol;
			_sorter.Order  = _sortDir;
			_listView.Sort();
		}

		// -----------------------------------------------------------------------
		// Timer
		// -----------------------------------------------------------------------

		private void StartTimers()
		{
			_timer = new System.Windows.Forms.Timer();
			_timer.Tick += async (s, e) => await FetchAllQuotesAsync();
			ApplyInterval();

			_countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
			_countdownTimer.Tick += (s, e) => UpdateCountdown();
			_countdownTimer.Start();
		}

		private void ApplyInterval()
		{
			_timer.Stop();
			_timer.Interval = _settings.IntervalMinutes * 60 * 1000;
			_nextFetchTime = DateTime.Now.AddMilliseconds(_timer.Interval);
			_timer.Start();
		}

		private void UpdateCountdown()
		{
			if (_nextFetchTime == DateTime.MinValue) return;
			TimeSpan remaining = _nextFetchTime - DateTime.Now;
			if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
			_lblNextUpdate.Text = $"Nächster Abruf: {remaining:mm\\:ss}";
		}

		// -----------------------------------------------------------------------
		// Kursabruf
		// -----------------------------------------------------------------------

		private async Task FetchAllQuotesAsync()
		{
			if (_fetchRunning) return;
			_fetchRunning = true;
			_lblStatus.Text = "Abruf läuft…";
			_nextFetchTime = DateTime.Now.AddMilliseconds(_timer.Interval);

			List<WatchlistEntry> watchlist = _settings.Watchlist;
			for (int i = 0; i < watchlist.Count; i++)
			{
				WatchlistEntry entry = watchlist[i];
				_lblStatus.Text = $"Abruf {i + 1}/{watchlist.Count}: {entry.Name}…";
				await FetchQuoteIntoEntry(entry);
			}

			_settings.Save();
			RefreshListView(updatePortfolioTrend: true);
			_lblStatus.Text = $"Zuletzt aktualisiert: {DateTime.Now:HH:mm:ss}";
			_fetchRunning = false;
		}

		private async Task FetchSingleAsync(WatchlistEntry entry)
		{
			await FetchQuoteIntoEntry(entry);
			_settings.Save();
			RefreshListView();
		}

		// Nach dieser Anzahl Fehlschlägen wird der Lookup-Rhythmus auf 1h gedrosselt
		private const int LookupMaxFails = 3;
		private static readonly TimeSpan LookupRetryInterval = TimeSpan.FromHours(1);

		private async Task FetchQuoteIntoEntry(WatchlistEntry entry)
		{
			// Gedrosselter Eintrag: Symbol unbekannt und Wartezeit noch nicht abgelaufen
			if (string.IsNullOrEmpty(entry.YahooSymbol) &&
				entry.LookupFailCount >= LookupMaxFails &&
				DateTime.Now < entry.NextLookupAttempt)
			{
				TimeSpan wait = entry.NextLookupAttempt - DateTime.Now;
				entry.StatusText = $"Symbol unbekannt – nächster Versuch in {(int)wait.TotalMinutes} Min.";
				return;
			}

			QuoteResult result = await _client.GetQuoteAsync(entry.Isin, entry.YahooSymbol);
			if (result.Success)
			{
				double previousDisplayedPrice = GetDisplayedPriceForTrend(entry);

				entry.QuoteFetchAttemptedThisSession = true;
				entry.LastSuccessfulQuoteFetch = DateTime.Now;
				entry.DataRetrievalFailureSince = DateTime.MinValue;
				entry.LookupFailCount = 0;
				entry.NextLookupAttempt = DateTime.MinValue;

				bool persistChanged = false;

				// Yahoo-Symbol persistieren wenn neu aufgelöst
				if (!string.IsNullOrEmpty(result.ResolvedSymbol) &&
					!string.Equals(entry.YahooSymbol, result.ResolvedSymbol, StringComparison.OrdinalIgnoreCase))
				{
					entry.YahooSymbol = result.ResolvedSymbol;
					persistChanged = true;
				}

				// Kurs-/Listingwährung nur aktualisieren, wenn die Query tatsächlich eine liefert.
				// Ein leerer Query-Wert darf eine manuell gepflegte QuoteCurrency nie löschen.
				if (!string.IsNullOrWhiteSpace(result.Currency))
				{
					string queryCurrency = result.Currency.Trim().ToUpperInvariant();
					if (!string.Equals(entry.QuoteCurrency, queryCurrency, StringComparison.OrdinalIgnoreCase))
					{
						entry.QuoteCurrency = queryCurrency;
						persistChanged = true;
					}
				}

				entry.LastPrice = result.Price;
				entry.LastUpdate = result.Timestamp;

				// LastPriceEur wird unabhängig von der Anzeigeoption immer als echter EUR-Wert geführt.
				// Das wird für P&L und für Prozent-Limits mit abweichender Referenzwährung benötigt.
				string quoteCurrency = (entry.QuoteCurrency ?? "").Trim().ToUpperInvariant();
				if (string.IsNullOrEmpty(quoteCurrency))
				{
					entry.FxRate = 0;
					entry.LastPriceEur = 0;
					entry.StatusText = $"OK {result.Timestamp:HH:mm}  (Kurswährung n.v.)";
				}
				else if (string.Equals(quoteCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
				{
					entry.FxRate = 1.0;
					entry.LastPriceEur = result.Price;
					entry.StatusText = $"OK  {result.Timestamp:HH:mm}";
				}
				else
				{
					double rate = await _client.GetFxToEurAsync(quoteCurrency);
					if (rate > 0)
					{
						entry.FxRate = rate;
						entry.LastPriceEur = result.Price * rate;
						entry.StatusText = entry.ConvertToEur
							? $"OK {result.Timestamp:HH:mm}  {result.Price:N2} {quoteCurrency} (×{rate:N4})"
							: $"OK  {result.Timestamp:HH:mm}";
					}
					else
					{
						entry.FxRate = 0;
						entry.LastPriceEur = 0;
						entry.StatusText = entry.ConvertToEur
							? $"OK {result.Timestamp:HH:mm}  {result.Price:N2} {quoteCurrency} (FX n.v.)"
							: $"OK  {result.Timestamp:HH:mm}";
					}
				}

				double currentDisplayedPrice = GetDisplayedPriceForTrend(entry);
				UpdatePriceTrendIndicator(
					entry, previousDisplayedPrice, currentDisplayedPrice);

				if (persistChanged)
					_settings.Save();

				await CheckLimitsAsync(entry);
			}
			else
			{
				entry.QuoteFetchAttemptedThisSession = true;
				if (entry.DataRetrievalFailureSince == DateTime.MinValue)
					entry.DataRetrievalFailureSince = DateTime.Now;

				entry.LookupFailCount++;
				string errDetail = !string.IsNullOrEmpty(result.ErrorMessage)
					? $" [{result.ErrorMessage}]" : "";
				if (entry.LookupFailCount >= LookupMaxFails)
				{
					entry.NextLookupAttempt = DateTime.Now.Add(LookupRetryInterval);
					entry.StatusText = $"Fehler – nächster Versuch in 1h{errDetail}";
				}
				else
				{
					entry.StatusText = $"Nicht gefunden ({entry.LookupFailCount}/{LookupMaxFails}){errDetail}";
				}
			}
		}

		// -----------------------------------------------------------------------
		// Einträge hinzufügen / bearbeiten / entfernen
		// -----------------------------------------------------------------------

		private void BtnAdd_Click(object sender, EventArgs e)
		{
			using (var dlg = new EditEntryDialog(_client))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
				{
					_settings.Watchlist.Add(dlg.Result);
					_settings.Save();
					RefreshListView();
					_ = FetchSingleAsync(dlg.Result);
				}
			}
		}

		private void OpenEditDialog()
		{
			if (_listView.SelectedItems.Count == 0) return;
			WatchlistEntry entry = (WatchlistEntry)_listView.SelectedItems[0].Tag;

			using (var dlg = new EditEntryDialog(_client, entry))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
				{
					// Persistierbare Felder aktualisieren, Laufzeitdaten behalten
					entry.Isin             = dlg.Result.Isin;
					entry.Name             = dlg.Result.Name;
					entry.EntryType        = dlg.Result.EntryType;
					entry.Note             = dlg.Result.Note;
					entry.YahooSymbol          = dlg.Result.YahooSymbol;
					entry.QuoteCurrency        = dlg.Result.QuoteCurrency;
					entry.LimitUpper           = dlg.Result.LimitUpper;
					entry.LimitUpperType       = dlg.Result.LimitUpperType;
					entry.LimitUpperEnabled    = dlg.Result.LimitUpperEnabled;
					entry.LimitLower           = dlg.Result.LimitLower;
					entry.LimitLowerType       = dlg.Result.LimitLowerType;
					entry.LimitLowerEnabled    = dlg.Result.LimitLowerEnabled;
					entry.ConvertToEur     = dlg.Result.ConvertToEur;
					entry.Quantity         = dlg.Result.Quantity;
					entry.ReferencePrice    = dlg.Result.ReferencePrice;
					entry.ReferenceCurrency = dlg.Result.ReferenceCurrency;
					entry.ReferenceDate     = dlg.Result.ReferenceDate;
					entry.ReferenceFxRate   = dlg.Result.ReferenceFxRate;
					_settings.Save();
					_ = FetchSingleAsync(entry);  // Kurs + FX sofort neu laden
				}
			}
		}

		private void BtnRemove_Click(object sender, EventArgs e)
		{
			if (_listView.SelectedItems.Count == 0) return;
			WatchlistEntry entry = (WatchlistEntry)_listView.SelectedItems[0].Tag;

			if (MessageBox.Show($"'{entry.Name}' aus der Watchlist entfernen?",
					"Bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				_settings.Watchlist.Remove(entry);
				_priceTrendIndicators.Remove(entry);
				_priceTrendDirections.Remove(entry);
				_priceTrendCounts.Remove(entry);
				_settings.Save();
				RefreshListView();
			}
		}

		// -----------------------------------------------------------------------
		// Limit-Prüfung & Alarm
		// -----------------------------------------------------------------------

		private sealed class LimitEvaluation
		{
			public bool Reached { get; set; }
			public double CurrentPrice { get; set; }
			public double EffectiveLimit { get; set; }
			public string Currency { get; set; } = "";
		}

		private async Task CheckLimitsAsync(WatchlistEntry entry)
		{
			bool needsReferencePrice =
				(entry.LimitUpperEnabled && entry.LimitUpperType == LimitValueType.Percent) ||
				(entry.LimitLowerEnabled && entry.LimitLowerType == LimitValueType.Percent);

			double currentReferencePrice = 0.0;
			string referenceCurrency = entry.EffectiveReferenceCurrency;
			if (needsReferencePrice && entry.ReferencePrice > 0)
				currentReferencePrice = await GetCurrentPriceInCurrencyAsync(entry, referenceCurrency);

			LimitEvaluation upper = EvaluateLimit(entry, true, currentReferencePrice, referenceCurrency);
			LimitEvaluation lower = EvaluateLimit(entry, false, currentReferencePrice, referenceCurrency);

			entry.UpperLimitReached = upper.Reached;
			entry.LowerLimitReached = lower.Reached;

			if (upper.Reached)
			{
				if (!entry.AlarmUpperFired)
				{
					entry.AlarmUpperFired = true;
					FireAlarm(entry, true, upper);
				}
			}
			else
			{
				entry.AlarmUpperFired = false;
			}

			if (lower.Reached)
			{
				if (!entry.AlarmLowerFired)
				{
					entry.AlarmLowerFired = true;
					FireAlarm(entry, false, lower);
				}
			}
			else
			{
				entry.AlarmLowerFired = false;
			}
		}

		private LimitEvaluation EvaluateLimit(
			WatchlistEntry entry,
			bool isUpper,
			double currentReferencePrice,
			string referenceCurrency)
		{
			bool enabled = isUpper ? entry.LimitUpperEnabled : entry.LimitLowerEnabled;
			if (!enabled) return new LimitEvaluation();

			double rawLimit = isUpper ? entry.LimitUpper : entry.LimitLower;
			LimitValueType type = isUpper ? entry.LimitUpperType : entry.LimitLowerType;

			double currentPrice;
			double effectiveLimit;
			string currency;

			if (type == LimitValueType.Percent)
			{
				if (entry.ReferencePrice <= 0 || currentReferencePrice <= 0)
					return new LimitEvaluation();

				currentPrice = currentReferencePrice;
				effectiveLimit = entry.ReferencePrice * (1.0 + rawLimit / 100.0);
				currency = referenceCurrency;
			}
			else
			{
				currentPrice = entry.ComparePrice;
				effectiveLimit = rawLimit;
				currency = entry.AbsoluteLimitCurrency;
				if (currentPrice <= 0 || string.IsNullOrWhiteSpace(currency))
					return new LimitEvaluation();
			}

			bool reached = isUpper
				? currentPrice >= effectiveLimit
				: currentPrice <= effectiveLimit;

			return new LimitEvaluation
			{
				Reached = reached,
				CurrentPrice = currentPrice,
				EffectiveLimit = effectiveLimit,
				Currency = currency ?? ""
			};
		}

		private async Task<double> GetCurrentPriceInCurrencyAsync(WatchlistEntry entry, string targetCurrency)
		{
			if (entry.LastPrice <= 0 || string.IsNullOrWhiteSpace(targetCurrency)) return 0.0;

			string quoteCurrency = (entry.QuoteCurrency ?? "").Trim().ToUpperInvariant();
			targetCurrency = targetCurrency.Trim().ToUpperInvariant();
			if (string.IsNullOrEmpty(quoteCurrency)) return 0.0;

			if (string.Equals(quoteCurrency, targetCurrency, StringComparison.OrdinalIgnoreCase))
				return entry.LastPrice;

			if (string.Equals(targetCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
				return entry.LastPriceEur;

			if (entry.LastPriceEur <= 0) return 0.0;

			double targetToEur = await _client.GetFxToEurAsync(targetCurrency);
			if (targetToEur <= 0) return 0.0;

			return entry.LastPriceEur / targetToEur;
		}

		private void FireAlarm(WatchlistEntry entry, bool isUpperAlarm, LimitEvaluation evaluation)
		{
			string alarmPrefix = entry.EntryType == WatchlistEntryType.BuyCandidate
				? "Alarm Watchlist" : "Alarm Bestand";
			string direction = isUpperAlarm ? "▲ Oberes Limit" : "▼ Unteres Limit";
			string limitText = FormatAlarmLimit(entry, isUpperAlarm, evaluation);
			string currentText = FormatPrice(evaluation.CurrentPrice, evaluation.Currency);

			if (_settings.NotifyTrayDot)
				SetTrayDot();

			if (_settings.NotifyBalloon)
				_notifyIcon.ShowBalloonTip(8000,
					$"{alarmPrefix}: {entry.Name}",
					$"{direction} {limitText} – aktuell {currentText}",
					isUpperAlarm ? ToolTipIcon.Info : ToolTipIcon.Warning);

			if (_settings.NotifyAlarmDialog)
				BeginInvoke(new Action(() =>
				{
					using (var dlg = new AlarmDialog(entry, isUpperAlarm, limitText, currentText))
					{
						dlg.ShowDialog(this);

						// Snooze (1 Zyklus): Alarm wieder scharf schalten, damit er beim
						// nächsten erfolgreichen Abruf erneut auslöst, falls das Limit
						// weiterhin verletzt ist.
						if (dlg.Snoozed)
						{
							if (isUpperAlarm)
								entry.AlarmUpperFired = false;
							else
								entry.AlarmLowerFired = false;
						}
					}
				}));

			if (_settings.NtfyEnabled)
				_ = SendNtfyAsync(entry, isUpperAlarm, limitText, currentText);
		}

		private static string FormatAlarmLimit(WatchlistEntry entry, bool isUpperAlarm, LimitEvaluation evaluation)
		{
			double rawLimit = isUpperAlarm ? entry.LimitUpper : entry.LimitLower;
			LimitValueType type = isUpperAlarm ? entry.LimitUpperType : entry.LimitLowerType;
			if (type == LimitValueType.Percent)
				return $"{FormatSignedPercent(rawLimit)} ({FormatPrice(evaluation.EffectiveLimit, evaluation.Currency)})";

			return FormatPrice(evaluation.EffectiveLimit, evaluation.Currency);
		}

		private static double NormalizeTwoDecimalDisplay(double value) =>
			Math.Abs(value) < 0.005 ? 0.0 : value;

		private static string FormatSignedPercent(double value)
		{
			double displayValue = NormalizeTwoDecimalDisplay(value);
			return $"{(displayValue > 0 ? "+" : "")}{displayValue:N2} %";
		}

		private static string FormatPrice(double value, string currency)
		{
			string ccy = (currency ?? "").Trim().ToUpperInvariant();
			return string.IsNullOrEmpty(ccy) ? $"{value:N2}" : $"{value:N2} {ccy}";
		}

		private async Task SendNtfyAsync(
			WatchlistEntry entry,
			bool isUpperAlarm,
			string limitText,
			string currentText)
		{
			string topic = _settings.NtfyTopic?.Trim();
			string url   = (_settings.NtfyUrl?.TrimEnd('/') ?? "https://ntfy.sh");
			if (string.IsNullOrEmpty(topic)) return;

			try
			{
				string alarmPrefix = entry.EntryType == WatchlistEntryType.BuyCandidate
					? "Alarm Watchlist" : "Alarm Bestand";
				string direction = isUpperAlarm ? "▲ Oberes Limit" : "▼ Unteres Limit";

				// ntfy: Titel bewusst als echter Titelparameter übertragen.
				// Uri.EscapeDataString gehört in die URL, nicht in den HTTP-Header;
				// so zeigt ntfy Leerzeichen/Umlaute korrekt statt als %20/%C3... an.
				string title = $"{alarmPrefix}: {entry.Name}";
				string body  = $"{direction} {limitText} erreicht\n" +
				               $"Aktuell: {currentText}";
				string requestUrl = $"{url}/{topic}?title={Uri.EscapeDataString(title)}&priority=high";

				var req = new System.Net.Http.HttpRequestMessage(
					System.Net.Http.HttpMethod.Post, requestUrl);
				req.Content = new System.Net.Http.StringContent(
					body, System.Text.Encoding.UTF8, "text/plain");

				await _ntfyClient.SendAsync(req);
			}
			catch { /* Push-Fehler nie zum Absturz führen lassen */ }
		}

		private void SetTrayDot()
		{
			if (_dotActive) return;
			_dotActive          = true;
			_notifyIcon.Icon    = _dotIcon;
			_notifyIcon.Text    = "Stock Watcher – Kurs-Alarm!";
		}

		private void ClearTrayDot()
		{
			if (!_dotActive) return;
			_dotActive          = false;
			_notifyIcon.Icon    = _baseIcon;
			_notifyIcon.Text    = "Stock Watcher";
		}

		// -----------------------------------------------------------------------
		// ListView aktualisieren
		// -----------------------------------------------------------------------

		private static double GetDisplayedPriceForTrend(WatchlistEntry entry)
		{
			if (entry == null)
				return 0.0;

			if (entry.ConvertToEur)
				return entry.LastPriceEur > 0.0 ? entry.LastPriceEur : 0.0;

			return entry.LastPrice > 0.0 ? entry.LastPrice : 0.0;
		}

		private void UpdatePriceTrendIndicator(
			WatchlistEntry entry,
			double previousValue,
			double currentValue)
		{
			if (previousValue <= 0.0 || currentValue <= 0.0)
			{
				_priceTrendIndicators[entry] = "◀▶";
				_priceTrendDirections[entry] = 0;
				_priceTrendCounts[entry] = 0;
				return;
			}

			double previousComparable = Math.Round(previousValue, 2, MidpointRounding.AwayFromZero);
			double currentComparable = Math.Round(currentValue, 2, MidpointRounding.AwayFromZero);

			int direction = currentComparable > previousComparable
				? 1
				: currentComparable < previousComparable ? -1 : 0;

			if (direction == 0)
			{
				_priceTrendIndicators[entry] = "◀▶";
				_priceTrendDirections[entry] = 0;
				_priceTrendCounts[entry] = 0;
				return;
			}

			int previousDirection = _priceTrendDirections.TryGetValue(entry, out int storedDirection)
				? storedDirection
				: 0;
			int count = previousDirection == direction &&
				_priceTrendCounts.TryGetValue(entry, out int storedCount)
				? storedCount + 1
				: 1;

			_priceTrendDirections[entry] = direction;
			_priceTrendCounts[entry] = count;

			char triangle = direction > 0 ? '▲' : '▼';
			string indicator = new string(triangle, Math.Min(count, 3));
			if (count >= 4)
				indicator += "+";

			_priceTrendIndicators[entry] = indicator;
		}

		private static string FormatListLimit(WatchlistEntry entry, bool isUpper)
		{
			double value = isUpper ? entry.LimitUpper : entry.LimitLower;
			LimitValueType type = isUpper ? entry.LimitUpperType : entry.LimitLowerType;
			if (type == LimitValueType.Percent)
				return FormatSignedPercent(value);

			string currency = entry.AbsoluteLimitCurrency;
			return string.IsNullOrWhiteSpace(currency)
				? $"{value:N2} [Währung fehlt]"
				: $"{value:N2} {currency}";
		}

		private void RefreshListView(bool updatePortfolioTrend = false)
		{
			_listView.BeginUpdate();
			_listView.Items.Clear();

			foreach (WatchlistEntry entry in _settings.Watchlist)
			{
				bool dataRetrievalTimedOut = IsDataRetrievalTimedOut(entry);
				string displayedStatusText = dataRetrievalTimedOut
					? "NOK: Timeout for Data Retrieval"
					: entry.StatusText;

				string priceTrendText = _priceTrendIndicators.TryGetValue(entry, out string priceTrend)
					? priceTrend
					: "◀▶";

				// Kurs-Spalte: einheitlich mit ISO-Währungscode, kein €-Symbol
				string priceText;
				if (entry.LastPrice <= 0.0)
				{
					priceText = "–";
				}
				else if (entry.ConvertToEur && entry.LastPriceEur > 0)
				{
					priceText = $"{entry.LastPriceEur:N2} EUR";
				}
				else
				{
					priceText = string.IsNullOrEmpty(entry.QuoteCurrency)
						? entry.LastPrice.ToString("N2")
						: $"{entry.LastPrice:N2} {entry.QuoteCurrency}";
				}

				string marketValueText = "–";
				if (entry.Quantity > 0 && entry.LastPrice > 0)
				{
					if (entry.ConvertToEur && entry.LastPriceEur > 0)
					{
						marketValueText = $"{entry.Quantity * entry.LastPriceEur:N2} EUR";
					}
					else
					{
						double marketValue = entry.Quantity * entry.LastPrice;

						marketValueText = string.IsNullOrWhiteSpace(entry.QuoteCurrency)
							? $"{marketValue:N2}"
							: $"{marketValue:N2} {entry.QuoteCurrency}";
					}
				}

				string upperText = FormatListLimit(entry, true);
				string lowerText = FormatListLimit(entry, false);

				string qtyText = entry.Quantity > 0
					? entry.Quantity.ToString("N0")
					: "–";
				string referenceText = entry.ReferencePrice > 0
					? $"{entry.ReferencePrice:N2} {entry.EffectiveReferenceCurrency}"
					: "–";
				string referenceValueText =
					entry.Quantity > 0 && entry.ReferencePrice > 0
						? $"{entry.Quantity * entry.ReferencePrice:N2} {entry.EffectiveReferenceCurrency}"
						: "–";

				// P&L immer in EUR (Kauf-/Referenzkurs × historischer FX → EUR-Basis)
				double effectiveFx = entry.EffectiveReferenceFxRate;
				bool hasDiff = entry.Quantity > 0 &&
				               entry.ReferencePrice > 0 &&
				               entry.LastPriceEur > 0 &&
				               effectiveFx > 0;

				double referenceEur = hasDiff ? entry.Quantity * entry.ReferencePrice * effectiveFx : 0;
				double currentEur   = hasDiff ? entry.Quantity * entry.LastPriceEur : 0;
				double diffAmt      = hasDiff ? currentEur - referenceEur : 0;
				double diffPct      = hasDiff ? (currentEur - referenceEur) / referenceEur * 100.0 : 0;

				double displayDiffAmt = NormalizeTwoDecimalDisplay(diffAmt);
				double displayDiffPct = NormalizeTwoDecimalDisplay(diffPct);
				string S(double v) => v > 0 ? "+" : "";
				string diffAmtText = hasDiff ? $"{S(displayDiffAmt)}{displayDiffAmt:N2} EUR" : "–";
				string diffPctText = hasDiff ? $"{S(displayDiffPct)}{displayDiffPct:N2} %" : "–";
				Color diffColor = hasDiff ? (diffAmt >= 0 ? Color.DarkGreen : Color.Firebrick) : SystemColors.WindowText;

				string typeText = entry.EntryType == WatchlistEntryType.BuyCandidate
					? "Kaufkandidat" : "Bestand";
				string noteText = (entry.Note ?? "")
					.Replace("\r\n", " ")
					.Replace("\r", " ")
					.Replace("\n", " ");

				var item = new ListViewItem(entry.Name) { UseItemStyleForSubItems = false };

				var siIsin = new ListViewItem.ListViewSubItem
				{
					Text = entry.Isin
				};
				item.SubItems.Add(siIsin);
				item.SubItems.Add(qtyText);
				item.SubItems.Add(referenceText);
				item.SubItems.Add(referenceValueText);
				item.SubItems.Add(priceTrendText);
				item.SubItems.Add(priceText);
				item.SubItems.Add(marketValueText);

				var siDiffAmt = new ListViewItem.ListViewSubItem
				{
					Text = diffAmtText,
					ForeColor = diffColor
				};

				var siDiffPct = new ListViewItem.ListViewSubItem
				{
					Text = diffPctText,
					ForeColor = diffColor
				};

				item.SubItems.Add(siDiffAmt);
				item.SubItems.Add(siDiffPct);

				// Unteres Limit zuerst
				var siLower = new ListViewItem.ListViewSubItem { Text = lowerText };
				if (!entry.LimitLowerEnabled)
				{
					siLower.ForeColor = Color.Gray;
					siLower.Font = _disabledLimitFont;
				}
				item.SubItems.Add(siLower);

				// Oberes Limit danach
				var siUpper = new ListViewItem.ListViewSubItem { Text = upperText };
				if (!entry.LimitUpperEnabled)
				{
					siUpper.ForeColor = Color.Gray;
					siUpper.Font = _disabledLimitFont;
				}
				item.SubItems.Add(siUpper);

				item.SubItems.Add(typeText);
				item.SubItems.Add(noteText);
				var siStatus = new ListViewItem.ListViewSubItem
				{
					Text = displayedStatusText
				};
				item.SubItems.Add(siStatus);

				if (entry.UpperLimitReached)
					item.BackColor = Color.FromArgb(200, 255, 200);
				else if (entry.LowerLimitReached)
					item.BackColor = Color.FromArgb(255, 200, 200);

				if (dataRetrievalTimedOut)
				{
					Color warningColor = Color.LightYellow;
					item.SubItems[0].BackColor = warningColor;
					siIsin.BackColor = warningColor;
					siStatus.BackColor = warningColor;
				}

				item.Tag = entry;
				_listView.Items.Add(item);
			}

			UpdatePortfolioSummary(updatePortfolioTrend);
			_listView.EndUpdate();
		}

		// -----------------------------------------------------------------------
		private bool IsDataRetrievalTimedOut(WatchlistEntry entry)
		{
			if (_settings.DataRetrievalTimeoutMinutes <= 0 ||
				!entry.QuoteFetchAttemptedThisSession)
				return false;

			DateTime referenceTime = entry.LastSuccessfulQuoteFetch != DateTime.MinValue
				? entry.LastSuccessfulQuoteFetch
				: entry.DataRetrievalFailureSince;

			if (referenceTime == DateTime.MinValue)
				return false;

			return DateTime.Now - referenceTime >=
				TimeSpan.FromMinutes(_settings.DataRetrievalTimeoutMinutes);
		}

		private void UpdatePortfolioSummary(bool updateTrend)
		{
			int positionCount = 0;
			double totalMarketValueEur = 0.0;
			double totalDiffEur = 0.0;
			bool marketValueComplete = true;
			bool diffComplete = true;

			foreach (WatchlistEntry entry in _settings.Watchlist)
			{
				if (entry.EntryType != WatchlistEntryType.Holding || entry.Quantity <= 0)
					continue;

				positionCount++;

				if (entry.LastPriceEur > 0)
					totalMarketValueEur += entry.Quantity * entry.LastPriceEur;
				else
					marketValueComplete = false;

				double effectiveFx = entry.EffectiveReferenceFxRate;
				if (entry.ReferencePrice > 0 && entry.LastPriceEur > 0 && effectiveFx > 0)
				{
					double referenceEur = entry.Quantity * entry.ReferencePrice * effectiveFx;
					double currentEur = entry.Quantity * entry.LastPriceEur;
					totalDiffEur += currentEur - referenceEur;
				}
				else
				{
					diffComplete = false;
				}
			}

			if (updateTrend && marketValueComplete)
			{
				double comparableValue = Math.Round(totalMarketValueEur, 2, MidpointRounding.AwayFromZero);
				if (_previousPortfolioMarketValueEur.HasValue)
				{
					double previousValue = Math.Round(
						_previousPortfolioMarketValueEur.Value, 2, MidpointRounding.AwayFromZero);
					_portfolioTrendIndicator = comparableValue > previousValue
						? "▲"
						: comparableValue < previousValue ? "▼" : "◀▶";
				}
				else
				{
					_portfolioTrendIndicator = "◀▶";
				}

				_previousPortfolioMarketValueEur = comparableValue;
			}
			else if (updateTrend)
			{
				_portfolioTrendIndicator = "◀▶";
			}

			string marketValueText = marketValueComplete
				? $"{totalMarketValueEur:N2} EUR"
				: "– EUR";

			string diffText;
			if (!diffComplete)
			{
				diffText = "– EUR";
			}
			else
			{
				double displayDiff = NormalizeTwoDecimalDisplay(totalDiffEur);
				string sign = displayDiff > 0 ? "+" : "";
				diffText = $"{sign}{displayDiff:N2} EUR";
			}

			_lblPortfolioSummary.Text =
				$"| {_portfolioTrendIndicator} | {positionCount} Pos. | {marketValueText} | {diffText}";
		}

		// -----------------------------------------------------------------------
		// Einstellungen (Intervall)
		// -----------------------------------------------------------------------

		private void OpenSettings(object sender, EventArgs e)
		{
			using (var form = new SettingsForm(_settings))
			{
				if (form.ShowDialog(this) == DialogResult.OK)
				{
					_settings = form.Settings;
					_settings.Save();
					ApplyInterval();
					RefreshListView();
				}
			}
		}

		// -----------------------------------------------------------------------
		// Fenster schliessen → in Tray minimieren
		// -----------------------------------------------------------------------

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			SaveUiLayout();

			if (e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				Hide();
				_notifyIcon.ShowBalloonTip(2000, "Stock Watcher",
					"Läuft im Hintergrund weiter.", ToolTipIcon.Info);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_client?.Dispose();
				_disabledLimitFont?.Dispose();
				_notifyIcon?.Dispose();
				_timer?.Dispose();
				_countdownTimer?.Dispose();
				_layoutSaveTimer?.Dispose();
				_dotIcon?.Dispose();
				// _baseIcon ist als Embedded Resource geöffnet, nicht self-owned → kein Dispose
			}
			base.Dispose(disposing);
		}

		// -----------------------------------------------------------------------
		// Icon-Hilfsmethoden
		// -----------------------------------------------------------------------

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		/// <summary>
		/// Lädt das App-Icon aus den Embedded Resources.
		/// Fallback: SystemIcons.Application.
		/// </summary>
		private static Icon LoadAppIcon()
		{
			try
			{
				Assembly asm    = Assembly.GetExecutingAssembly();
				string   name   = asm.GetName().Name + ".app.ico";
				using (Stream s = asm.GetManifestResourceStream(name))
					if (s != null) return new Icon(s);
			}
			catch { }
			return SystemIcons.Application;
		}

		/// <summary>
		/// Erzeugt eine Variante des Icons mit einem roten Punkt unten rechts (16×16).
		/// </summary>
		private static Icon BuildDotIcon(Icon baseIcon)
		{
			try
			{
				const int sz = 16;
				using (var bmp = new Bitmap(sz, sz))
				using (var g   = Graphics.FromImage(bmp))
				{
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.DrawIcon(baseIcon, new Rectangle(0, 0, sz, sz));

					// Roter Kreis, 5×5 Pixel, unten rechts
					using (var brush = new SolidBrush(Color.FromArgb(230, 220, 30, 30)))
						g.FillEllipse(brush, sz - 6, sz - 6, 5, 5);
					using (var pen = new Pen(Color.FromArgb(180, 160, 0, 0), 0.8f))
						g.DrawEllipse(pen,   sz - 6, sz - 6, 5, 5);

					IntPtr hIcon = bmp.GetHicon();
					try   { return (Icon)Icon.FromHandle(hIcon).Clone(); }
					finally { DestroyIcon(hIcon); }
				}
			}
			catch
			{
				return baseIcon;
			}
		}

		// -----------------------------------------------------------------------
		// ListView-Sorter
		// -----------------------------------------------------------------------

		private sealed class ListViewSorter : IComparer
		{
			public int Column { get; set; } = 0;
			public SortOrder Order { get; set; } = SortOrder.Ascending;

			public int Compare(object x, object y)
			{
				var ix = (ListViewItem)x;
				var iy = (ListViewItem)y;

				string sx = Column < ix.SubItems.Count ? ix.SubItems[Column].Text : "";
				string sy = Column < iy.SubItems.Count ? iy.SubItems[Column].Text : "";

				// "–" immer ans Ende, unabhängig von der Richtung
				bool emptyX = sx == "–" || string.IsNullOrWhiteSpace(sx);
				bool emptyY = sy == "–" || string.IsNullOrWhiteSpace(sy);
				if (emptyX && emptyY) return 0;
				if (emptyX) return 1;
				if (emptyY) return -1;

				// Numerisch vergleichen wenn möglich, sonst alphabetisch
				int result;
				if (TryNum(sx, out double dx) && TryNum(sy, out double dy))
					result = dx.CompareTo(dy);
				else
					result = string.Compare(sx, sy, StringComparison.CurrentCultureIgnoreCase);

				return Order == SortOrder.Ascending ? result : -result;
			}

			/// <summary>
			/// Extrahiert den ersten Zahlenwert aus einem Anzeigetext.
			/// Beispiele: "+150.30" → 150.30 | "219.30 € (325.60 CHF)" → 219.30
			///            "-20.00" → -20.00  | "+15.30 %" → 15.30
			/// </summary>
			private static bool TryNum(string s, out double value)
			{
				value = 0;
				s = s.Trim();

				// Führendes + entfernen (negatives - behalten)
				if (s.StartsWith("+")) s = s.Substring(1);

				// Nur bis zum ersten Leerzeichen (ignoriert Einheiten wie "€", "%", "CHF")
				int sp = s.IndexOf(' ');
				if (sp > 0) s = s.Substring(0, sp);

				return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
					|| double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
			}
		}
	}
}
