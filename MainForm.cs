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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using StockWatcher.Controls;
using StockWatcher.Localization;
using StockWatcher.Forms;
using StockWatcher.Models;
using StockWatcher.Services;

namespace StockWatcher
{
	public partial class MainForm : Form
	{
		// P/Invoke: Fenster wirklich in den Vordergrund bringen
		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		// UI-Elemente
		private ColumnSelectableListView _listView;
		private TabControl _tabControl;
		private TabPage _tabOverview;
		private TabPage _tabHolding;
		private TabPage _tabBuyCandidate;
		private TabPage _tabRealized;
		private FlowLayoutPanel _overviewFilterPanel;
		private CheckBox _chkOverviewHolding;
		private CheckBox _chkOverviewBuyCandidate;
		private CheckBox _chkOverviewRealized;
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
		private ContextMenuStrip _entryContextMenu;
		private ContextMenuStrip _columnHeaderContextMenu;
		private Button _btnColumnChooser;
		private int _columnHeaderContextIndex = -1;

		// Sortierung
		private readonly ListViewSorter _sorter = new ListViewSorter();
		private int _sortCol = -1;
		private SortOrder _sortDir = SortOrder.None;

		// Daten & Dienste
		private AppSettings _settings;
		private readonly StockFrankfurtClient _client;
		private bool _fetchRunning = false;
		private DateTime _nextFetchTime = DateTime.MinValue;
		private static readonly TimeSpan EntryFetchTimeout = TimeSpan.FromSeconds(30);
		private static readonly TimeSpan ConnectivityRetryInterval = TimeSpan.FromMinutes(1);
		private bool _restoringLayout = false;
		private bool _allowMainWindowVisible = true;
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
			_allowMainWindowVisible = !_settings.StartMinimized;
			_client   = new StockFrankfurtClient();

			_baseIcon = LoadAppIcon();
			_dotIcon  = BuildDotIcon(_baseIcon);
			Icon      = _baseIcon;

			BuildUi();
			RestoreWindowLayout();
			BuildTrayIcon();
			RefreshListView();
			StartTimers();

			_ = FetchAllQuotesAsync();
		}

		// -----------------------------------------------------------------------
		// UI-Aufbau
		// -----------------------------------------------------------------------

		private void BuildUi()
		{
			Text = L10n.Text("AppTitle");
			Size = new Size(1080, 520);
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(700, 400);

			// Menü
			var menuStrip = new MenuStrip();
			var menuAction = new ToolStripMenuItem(L10n.Text("MenuAction"));
			var miRefresh = new ToolStripMenuItem(L10n.Text("MenuRefreshNow"), null, (s, e) => _ = FetchAllQuotesAsync());
			miRefresh.ShortcutKeys = Keys.F5;
			var miSettings = new ToolStripMenuItem(L10n.Text("MenuSettings"), null, OpenSettings);
			miSettings.ShortcutKeys = Keys.Control | Keys.E;
			var miExit = new ToolStripMenuItem(L10n.Text("MenuExit"), null, (s, e) => Application.Exit());
			menuAction.DropDownItems.AddRange(new ToolStripItem[]
				{ miRefresh, miSettings, new ToolStripSeparator(), miExit });
			menuStrip.Items.Add(menuAction);
			MainMenuStrip = menuStrip;

			// Toolbar
			_toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

			var btnRefresh = new ToolStripButton(L10n.Text("ToolbarRefresh"))
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = L10n.Text("TipRefresh")
			};
			btnRefresh.Click += (s, e) => _ = FetchAllQuotesAsync();

			var btnAdd = new ToolStripButton(L10n.Text("ToolbarAdd"))
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = L10n.Text("TipAdd")
			};
			btnAdd.Click += BtnAdd_Click;

			var btnEdit = new ToolStripButton(L10n.Text("ToolbarEdit"))
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = L10n.Text("TipEdit")
			};
			btnEdit.Click += (s, e) => OpenEditDialog();

			var btnRemove = new ToolStripButton(L10n.Text("ToolbarRemove"))
			{
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ToolTipText = L10n.Text("TipRemove")
			};
			btnRemove.Click += BtnRemove_Click;

			_toolStrip.Items.Add(btnRefresh);
			_toolStrip.Items.Add(new ToolStripSeparator());
			_toolStrip.Items.Add(btnAdd);
			_toolStrip.Items.Add(btnEdit);
			_toolStrip.Items.Add(btnRemove);

			// ListView
			_listView = new ColumnSelectableListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				MultiSelect = false,
				GridLines = true,
				Font = new Font("Consolas", 9.5f),
				AllowColumnReorder = true
			};
			_disabledLimitFont = new Font(_listView.Font, FontStyle.Italic);
			_listView.ListViewItemSorter = _sorter;
			_listView.DoubleClick += (s, e) => OpenEditDialog();
			_listView.ColumnClick += ListView_ColumnClick;
			_listView.MouseDown += ListView_MouseDown;
			_listView.ColumnHeaderRightClicked += ListView_ColumnHeaderRightClicked;
			BuildEntryContextMenu();
			BuildColumnHeaderContextMenu();
			_listView.ColumnReordered += (s, e) =>
			{
				if (!_restoringLayout)
					BeginInvoke(new Action(SaveColumnOrder));
			};
			_listView.ColumnWidthChanged += (s, e) => ScheduleLayoutSave();

			// Reiter: Übersicht + Detailansichten
			_tabControl = new TabControl { Dock = DockStyle.Fill };
			_tabOverview = new TabPage(L10n.Text("TabOverview"));
			_tabHolding = new TabPage(L10n.Text("TabHolding"));
			_tabBuyCandidate = new TabPage(L10n.Text("TabBuyCandidate"));
			_tabRealized = new TabPage(L10n.Text("TabRealized"));

			_overviewFilterPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				Height = 31,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(6, 4, 0, 0)
			};
			_chkOverviewHolding = new CheckBox { Text = L10n.Text("TabHolding"), AutoSize = true, Checked = _settings.OverviewFilterHolding };
			_chkOverviewBuyCandidate = new CheckBox { Text = L10n.Text("TabBuyCandidate"), AutoSize = true, Checked = _settings.OverviewFilterBuyCandidate };
			_chkOverviewRealized = new CheckBox { Text = L10n.Text("TabRealized"), AutoSize = true, Checked = _settings.OverviewFilterRealized };
			_chkOverviewHolding.CheckedChanged += OverviewFilter_CheckedChanged;
			_chkOverviewBuyCandidate.CheckedChanged += OverviewFilter_CheckedChanged;
			_chkOverviewRealized.CheckedChanged += OverviewFilter_CheckedChanged;
			_overviewFilterPanel.Controls.Add(_chkOverviewHolding);
			_overviewFilterPanel.Controls.Add(_chkOverviewBuyCandidate);
			_overviewFilterPanel.Controls.Add(_chkOverviewRealized);

			_tabOverview.Controls.Add(_listView);
			_tabOverview.Controls.Add(_overviewFilterPanel);
			_tabControl.TabPages.AddRange(new[] { _tabOverview, _tabHolding, _tabBuyCandidate, _tabRealized });
			_tabControl.Deselecting += TabControl_Deselecting;
			_tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

			_btnColumnChooser = new Button
			{
				Text = "▾",
				Size = new Size(22, 21),
				TabStop = false,
				Anchor = AnchorStyles.Top | AnchorStyles.Right
			};
			_btnColumnChooser.Click += (s, e) => ShowColumnChooser();
			_tabOverview.Controls.Add(_btnColumnChooser);

			ConfigureColumnsForSelectedTab();
			UpdateColumnChooserButtonBounds();

			// Statusleiste
			_lblStatus = new ToolStripStatusLabel(L10n.Text("StatusReady"))
			{
				Spring = false,
				TextAlign = ContentAlignment.MiddleLeft
			};
			_lblPortfolioSummary = new ToolStripStatusLabel(L10n.Text("PortfolioInitial"))
			{
				Spring = false,
				TextAlign = ContentAlignment.MiddleLeft
			};
			_lblNextUpdate = new ToolStripStatusLabel(L10n.Text("NextFetchNone"))
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
			Controls.Add(_tabControl);   // Fill – Reiter mit der gemeinsamen Listenansicht
			Controls.Add(_toolStrip);     // Top – unter Menü
			Controls.Add(menuStrip);      // Top – ganz oben
			Controls.Add(_statusStrip);   // Bottom – ganz unten

			FormClosing += MainForm_FormClosing;
			LocationChanged += (s, e) => ScheduleLayoutSave();
			SizeChanged += (s, e) => ScheduleLayoutSave();
			ResizeEnd += (s, e) => ScheduleLayoutSave();

			// Bei Tray-only-Start ist das endgültige TabPage-Layout beim BuildUi()
			// noch nicht verfügbar. Den Feldauswahl-Button nach der ersten
			// tatsächlichen Sichtbarschaltung deshalb nochmals positionieren.
			Shown += (s, e) =>
			{
				UpdateColumnChooserButtonBounds();
				_btnColumnChooser?.BringToFront();
			};
		}

		private bool IsOverviewTab => _tabControl != null && _tabControl.SelectedTab == _tabOverview;

		private void TabControl_Deselecting(object sender, TabControlCancelEventArgs e)
		{
			if (_restoringLayout || _settings == null || _listView == null) return;

			_layoutSaveTimer?.Stop();
			SaveCurrentColumnLayout();
			SaveOverviewFilterSettings();
			_settings.Save();
		}

		private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_tabControl.SelectedTab != null)
			{
				_tabControl.SelectedTab.Controls.Add(_listView);
				_tabControl.SelectedTab.Controls.Add(_btnColumnChooser);

				if (IsOverviewTab)
				{
					// In der Übersicht Platz für die Filterzeile oberhalb der Spaltenköpfe lassen.
					_listView.Dock = DockStyle.None;
					_listView.Anchor =
						AnchorStyles.Top |
						AnchorStyles.Bottom |
						AnchorStyles.Left |
						AnchorStyles.Right;

					_listView.Location = new Point(0, _overviewFilterPanel.Height);
					_listView.Size = new Size(
						_tabOverview.ClientSize.Width,
						Math.Max(0, _tabOverview.ClientSize.Height - _overviewFilterPanel.Height));
				}
				else
				{
					_listView.Dock = DockStyle.Fill;
				}
			}

			ConfigureColumnsForSelectedTab();
			UpdateColumnChooserButtonBounds();
			_btnColumnChooser?.BringToFront();
			RefreshListView();
		}

		private void ConfigureColumnsForSelectedTab()
		{
			ConfigureColumnsForSelectedTabCore();
		}


		private void BuildEntryContextMenu()
		{
			_entryContextMenu = new ContextMenuStrip();
			_entryContextMenu.Items.Add(L10n.Text("EntryMenuReload"), null,
				(s, e) => RefreshSelectedEntry());
			_entryContextMenu.Items.Add(new ToolStripSeparator());
			_entryContextMenu.Items.Add(L10n.Text("EntryMenuCopyHolding"), null,
				(s, e) => CopySelectedEntry(WatchlistEntryType.Holding));
			_entryContextMenu.Items.Add(L10n.Text("EntryMenuCopyWatchlist"), null,
				(s, e) => CopySelectedEntry(WatchlistEntryType.BuyCandidate));
			_entryContextMenu.Items.Add(L10n.Text("EntryMenuCopyRealized"), null,
				(s, e) => CopySelectedEntry(WatchlistEntryType.Realized));
			_entryContextMenu.Opening += (s, e) =>
			{
				if (_listView.SelectedItems.Count == 0)
					e.Cancel = true;
			};

			_listView.ContextMenuStrip = _entryContextMenu;
		}

		private void ListView_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right) return;

			ListViewItem item = _listView.GetItemAt(e.X, e.Y);
			if (item == null)
			{
				if (_listView.SelectedItems.Count > 0)
					_listView.SelectedItems[0].Selected = false;
				return;
			}

			item.Selected = true;
			item.Focused = true;
		}

		private void RefreshSelectedEntry()
		{
			if (_listView.SelectedItems.Count == 0) return;
			WatchlistEntry entry = (WatchlistEntry)_listView.SelectedItems[0].Tag;
			_ = FetchSingleAsync(entry);
		}

		private void CopySelectedEntry(WatchlistEntryType targetType)
		{
			if (_listView.SelectedItems.Count == 0) return;

			WatchlistEntry source = (WatchlistEntry)_listView.SelectedItems[0].Tag;
			WatchlistEntry copy = CreateEntryCopyForType(source, targetType);

			using (var dlg = new EditEntryDialog(_client, copy))
			{
				dlg.Text = targetType == WatchlistEntryType.Holding
					? L10n.Text("CopyTitleHolding")
					: targetType == WatchlistEntryType.BuyCandidate
						? L10n.Text("CopyTitleWatchlist")
						: L10n.Text("CopyTitleRealized");

				if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
				{
					_settings.Watchlist.Add(dlg.Result);
					_settings.Save();
					RefreshListView();
					_ = FetchSingleAsync(dlg.Result);
				}
			}
		}

		private static WatchlistEntry CreateEntryCopyForType(
			WatchlistEntry source,
			WatchlistEntryType targetType)
		{
			bool realizedTarget = targetType == WatchlistEntryType.Realized;
			bool keepSaleData = realizedTarget && source.EntryType == WatchlistEntryType.Realized;

			return new WatchlistEntry
			{
				Isin = source.Isin,
				Name = source.Name,
				EntryType = targetType,
				Note = source.Note,
				YahooSymbol = source.YahooSymbol,
				QuoteCurrency = source.QuoteCurrency,
				LimitUpper = realizedTarget ? 0.0 : source.LimitUpper,
				LimitUpperType = source.LimitUpperType,
				LimitUpperEnabled = !realizedTarget && source.LimitUpperEnabled,
				LimitLower = realizedTarget ? 0.0 : source.LimitLower,
				LimitLowerType = source.LimitLowerType,
				LimitLowerEnabled = !realizedTarget && source.LimitLowerEnabled,
				ConvertToEur = source.ConvertToEur,
				Quantity = targetType == WatchlistEntryType.BuyCandidate ? 0.0 : source.Quantity,
				ReferencePrice = source.ReferencePrice,
				ReferenceCurrency = source.ReferenceCurrency,
				ReferenceDate = source.ReferenceDate,
				ReferenceFxRate = source.ReferenceFxRate,
				// Erträge nie automatisch duplizieren, damit beim Kopieren keine
				// bereits realisierten Cashflows versehentlich doppelt gezählt werden.
				IncomeEur = 0.0,
				SalePrice = keepSaleData ? source.SalePrice : 0.0,
				SaleCurrency = keepSaleData ? source.SaleCurrency : "",
				SaleDate = keepSaleData ? source.SaleDate : DateTime.MinValue,
				SaleFxRate = keepSaleData ? source.SaleFxRate : 0.0,
				LastPrice = source.LastPrice,
				LastPriceEur = source.LastPriceEur,
				FxRate = source.FxRate,
				LastUpdate = source.LastUpdate,
				LastSuccessfulQuoteFetch = source.LastSuccessfulQuoteFetch,
				StatusText = source.StatusText
			};
		}

		private void BuildTrayIcon()
		{
			_trayMenu = new ContextMenuStrip();
			_trayMenu.Items.Add(L10n.Text("TrayShowApp"), null, (s, e) => ShowMainWindow());
			_trayMenu.Items.Add(L10n.Text("MenuRefreshNow"), null, (s, e) => _ = FetchAllQuotesAsync());
			_trayMenu.Items.Add(new ToolStripSeparator());
			_trayMenu.Items.Add(L10n.Text("MenuExit"), null, (s, e) => Application.Exit());

			_notifyIcon = new NotifyIcon
			{
				Icon             = _baseIcon,
				Text             = L10n.Text("AppTitle"),
				ContextMenuStrip = _trayMenu,
				Visible          = true
			};
			_notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
		}

		private void ShowMainWindow()
		{
			_allowMainWindowVisible = true;

			if (!Visible)
				Show();

			if (WindowState == FormWindowState.Minimized)
				WindowState = FormWindowState.Normal;

			// Beim Start nur im Tray wird das Fenster erst jetzt erstmals vollständig
			// dargestellt. Die Listenansicht deshalb nach der Fensterinitialisierung
			// nochmals aus dem aktuellen Datenbestand aufbauen.
			RefreshListView();

			Activate();
			SetForegroundWindow(Handle);
			ClearTrayDot();
		}

		protected override void SetVisibleCore(bool value)
		{
			// "Starte minimiert" bedeutet bewusst: nur im Tray starten.
			// Die erste Sichtbarschaltung durch Application.Run wird unterdrückt,
			// damit weder Fenster noch Taskleisten-Vorschau erzeugt werden.
			if (!_allowMainWindowVisible)
			{
				base.SetVisibleCore(false);
				return;
			}

			base.SetVisibleCore(value);
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

			SaveCurrentColumnLayout();
			SaveOverviewFilterSettings();
			SaveWindowLayout();
			_settings.Save();
		}

		private void SaveColumnWidths()
		{
			SaveCurrentColumnLayout();
		}

		private void RestoreColumnWidths()
		{
			// V1.1.4: Wiederherstellung erfolgt ID-basiert in ConfigureColumnsForSelectedTabCore().
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
			SaveCurrentColumnLayout();
			_settings.Save();
		}

		private void RestoreColumnOrder()
		{
			// V1.1.4: Wiederherstellung erfolgt ID-basiert in ConfigureColumnsForSelectedTabCore().
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
			{
				ColumnDefinition definition = col.Tag as ColumnDefinition;
				if (definition != null)
					col.Text = definition.Header;
			}

			ColumnDefinition sortDefinition = _listView.Columns[_sortCol].Tag as ColumnDefinition;
			string baseColumnText = sortDefinition?.Header ?? _listView.Columns[_sortCol].Text;
			_listView.Columns[_sortCol].Text = baseColumnText +
				(_sortDir == SortOrder.Ascending ? "  ▲" : "  ▼");

			_sorter.Column = _sortCol;
			_sorter.Order  = _sortDir;
			_sorter.TreatAsDate = sortDefinition?.TreatAsDate ??
				baseColumnText.IndexOf("datum", StringComparison.OrdinalIgnoreCase) >= 0;
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
			ScheduleNextFetch(TimeSpan.FromMinutes(_settings.IntervalMinutes));
		}

		private void ScheduleNextFetch(TimeSpan delay)
		{
			_timer.Stop();
			double milliseconds = Math.Max(1000.0, Math.Min(int.MaxValue, delay.TotalMilliseconds));
			_timer.Interval = (int)milliseconds;
			_nextFetchTime = DateTime.Now.AddMilliseconds(_timer.Interval);
			_timer.Start();
		}

		private void UpdateCountdown()
		{
			if (_nextFetchTime == DateTime.MinValue) return;
			TimeSpan remaining = _nextFetchTime - DateTime.Now;
			if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
			_lblNextUpdate.Text = L10n.Format("NextFetch", remaining);
		}

		// -----------------------------------------------------------------------
		// Kursabruf
		// -----------------------------------------------------------------------

		private enum FetchEntryOutcome
		{
			Completed,
			Failed,
			TransientFailure,
			TimedOut
		}

		private async Task FetchAllQuotesAsync()
		{
			if (_fetchRunning) return;

			_fetchRunning = true;
			_timer.Stop();
			_nextFetchTime = DateTime.MinValue;
			_lblNextUpdate.Text = L10n.Text("NextFetchNone");
			_lblStatus.Text = L10n.Text("FetchRunning");

			bool retrySoon = false;

			try
			{
				var fetchEntries = new List<WatchlistEntry>(_settings.Watchlist);

				for (int i = 0; i < fetchEntries.Count; i++)
				{
					WatchlistEntry entry = fetchEntries[i];
					_lblStatus.Text = L10n.Format("FetchProgress", i + 1, fetchEntries.Count, entry.Name);

					FetchEntryOutcome outcome;
					using (var cts = new CancellationTokenSource(EntryFetchTimeout))
					{
						outcome = await FetchQuoteIntoEntry(entry, cts.Token);
					}

					if (outcome == FetchEntryOutcome.TransientFailure ||
						outcome == FetchEntryOutcome.TimedOut)
					{
						retrySoon = true;
						string reason = outcome == FetchEntryOutcome.TimedOut
							? L10n.Text("TimeoutData")
							: L10n.Text("NetworkYahooUnavailable");
						_lblStatus.Text = L10n.Format("FetchAbortedRetry", reason);
						break;
					}
				}

				try { _settings.Save(); }
				catch (Exception ex)
				{
					_lblStatus.Text = L10n.Format("SaveError", ex.Message);
				}

				RefreshListView(updatePortfolioTrend: !retrySoon);
				if (!retrySoon)
					_lblStatus.Text = L10n.Format("LastUpdated", DateTime.Now);
			}
			catch (Exception ex)
			{
				retrySoon = true;
				_lblStatus.Text = L10n.Format("FetchErrorRetry", ex.Message);
			}
			finally
			{
				_fetchRunning = false;
				ScheduleNextFetch(retrySoon
					? ConnectivityRetryInterval
					: TimeSpan.FromMinutes(_settings.IntervalMinutes));
			}
		}

		private async Task FetchSingleAsync(WatchlistEntry entry)
		{
			try
			{
				using (var cts = new CancellationTokenSource(EntryFetchTimeout))
				{
					await FetchQuoteIntoEntry(entry, cts.Token);
				}

				_settings.Save();
				RefreshListView();
			}
			catch (Exception ex)
			{
				entry.StatusText = L10n.Format("FetchSingleError", ex.Message);
				RefreshListView();
			}
		}

		// Nach dieser Anzahl Fehlschlägen wird der Lookup-Rhythmus auf 1h gedrosselt
		private const int LookupMaxFails = 3;
		private static readonly TimeSpan LookupRetryInterval = TimeSpan.FromHours(1);

		private async Task<FetchEntryOutcome> FetchQuoteIntoEntry(WatchlistEntry entry, CancellationToken cancellationToken)
		{
			// Gedrosselter Eintrag: Symbol unbekannt und Wartezeit noch nicht abgelaufen
			if (string.IsNullOrEmpty(entry.YahooSymbol) &&
				entry.LookupFailCount >= LookupMaxFails &&
				DateTime.Now < entry.NextLookupAttempt)
			{
				TimeSpan wait = entry.NextLookupAttempt - DateTime.Now;
				entry.StatusText = L10n.Format("SymbolUnknownRetry", (int)wait.TotalMinutes);
				return FetchEntryOutcome.Failed;
			}

			QuoteResult result;
			try
			{
				result = await _client.GetQuoteAsync(entry.Isin, entry.YahooSymbol, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				MarkRetrievalFailure(entry);
				entry.StatusText = L10n.Text("TimeoutData");
				return FetchEntryOutcome.TimedOut;
			}
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
					entry.StatusText = L10n.Format("StatusQuoteCurrencyUnavailable", result.Timestamp);
				}
				else if (string.Equals(quoteCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
				{
					entry.FxRate = 1.0;
					entry.LastPriceEur = result.Price;
					entry.StatusText = $"OK  {result.Timestamp:HH:mm}";
				}
				else
				{
					double rate;
					try
					{
						rate = await _client.GetFxToEurAsync(quoteCurrency, cancellationToken);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						MarkRetrievalFailure(entry);
						entry.StatusText = L10n.Text("TimeoutData");
						return FetchEntryOutcome.TimedOut;
					}

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
							? $"OK {result.Timestamp:HH:mm}  {result.Price:N2} {quoteCurrency} ({L10n.Text("StatusFxUnavailable")})"
							: $"OK  {result.Timestamp:HH:mm}";
					}
				}

				double currentDisplayedPrice = GetDisplayedPriceForTrend(entry);
				UpdatePriceTrendIndicator(
					entry, previousDisplayedPrice, currentDisplayedPrice);

				if (persistChanged)
					_settings.Save();

				try
				{
					await CheckLimitsAsync(entry, cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					MarkRetrievalFailure(entry);
					entry.StatusText = L10n.Text("TimeoutData");
					return FetchEntryOutcome.TimedOut;
				}

				return FetchEntryOutcome.Completed;
			}
			else
			{
				MarkRetrievalFailure(entry);

				string errDetail = !string.IsNullOrEmpty(result.ErrorMessage)
					? $" [{result.ErrorMessage}]" : "";

				if (result.IsTransientFailure)
				{
					entry.StatusText = L10n.Format("StatusNetworkUnavailable", errDetail);
					return FetchEntryOutcome.TransientFailure;
				}

				entry.LookupFailCount++;
				if (entry.LookupFailCount >= LookupMaxFails)
				{
					entry.NextLookupAttempt = DateTime.Now.Add(LookupRetryInterval);
					entry.StatusText = L10n.Format("StatusErrorRetry1h", errDetail);
				}
				else
				{
					entry.StatusText = L10n.Format("StatusNotFound", entry.LookupFailCount, LookupMaxFails, errDetail);
				}

				return FetchEntryOutcome.Failed;
			}
		}

		private static void MarkRetrievalFailure(WatchlistEntry entry)
		{
			entry.QuoteFetchAttemptedThisSession = true;
			if (entry.DataRetrievalFailureSince == DateTime.MinValue)
				entry.DataRetrievalFailureSince = DateTime.Now;
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
					entry.IncomeEur         = dlg.Result.IncomeEur;
					entry.SalePrice         = dlg.Result.SalePrice;
					entry.SaleCurrency      = dlg.Result.SaleCurrency;
					entry.SaleDate          = dlg.Result.SaleDate;
					entry.SaleFxRate        = dlg.Result.SaleFxRate;

					if (entry.EntryType == WatchlistEntryType.Realized)
					{
						entry.UpperLimitReached = false;
						entry.LowerLimitReached = false;
						entry.AlarmUpperFired = false;
						entry.AlarmLowerFired = false;
					}

					_settings.Save();
					_ = FetchSingleAsync(entry);  // Kurs + FX sofort neu laden
				}
			}
		}

		private void BtnRemove_Click(object sender, EventArgs e)
		{
			if (_listView.SelectedItems.Count == 0) return;
			WatchlistEntry entry = (WatchlistEntry)_listView.SelectedItems[0].Tag;

			if (MessageBox.Show(L10n.Format("RemoveConfirm", entry.Name),
					L10n.Text("ConfirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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

		private async Task CheckLimitsAsync(WatchlistEntry entry, CancellationToken cancellationToken)
		{
			if (entry.EntryType == WatchlistEntryType.Realized) return;

			bool needsReferencePrice =
				(entry.LimitUpperEnabled && entry.LimitUpperType == LimitValueType.Percent) ||
				(entry.LimitLowerEnabled && entry.LimitLowerType == LimitValueType.Percent);

			double currentReferencePrice = 0.0;
			string referenceCurrency = entry.EffectiveReferenceCurrency;
			if (needsReferencePrice && entry.ReferencePrice > 0)
				currentReferencePrice = await GetCurrentPriceInCurrencyAsync(entry, referenceCurrency, cancellationToken);

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

		private async Task<double> GetCurrentPriceInCurrencyAsync(WatchlistEntry entry, string targetCurrency, CancellationToken cancellationToken)
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

			double targetToEur = await _client.GetFxToEurAsync(targetCurrency, cancellationToken);
			if (targetToEur <= 0) return 0.0;

			return entry.LastPriceEur / targetToEur;
		}

		private void FireAlarm(WatchlistEntry entry, bool isUpperAlarm, LimitEvaluation evaluation)
		{
			string alarmPrefix = entry.EntryType == WatchlistEntryType.BuyCandidate
				? L10n.Text("AlarmWatchlist") : L10n.Text("AlarmHolding");
			string direction = isUpperAlarm ? L10n.Text("UpperLimitArrow") : L10n.Text("LowerLimitArrow");
			string limitText = FormatAlarmLimit(entry, isUpperAlarm, evaluation);
			string currentText = FormatPrice(evaluation.CurrentPrice, evaluation.Currency);

			if (_settings.NotifyTrayDot)
				SetTrayDot();

			if (_settings.NotifyBalloon)
				_notifyIcon.ShowBalloonTip(8000,
					L10n.Format("TrayBalloonAlarm", alarmPrefix, entry.Name),
					L10n.Format("TrayBalloonBody", direction, limitText, currentText),
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
					? L10n.Text("AlarmWatchlist") : L10n.Text("AlarmHolding");
				string direction = isUpperAlarm ? L10n.Text("UpperLimitArrow") : L10n.Text("LowerLimitArrow");

				// ntfy: Titel bewusst als echter Titelparameter übertragen.
				// Uri.EscapeDataString gehört in die URL, nicht in den HTTP-Header;
				// so zeigt ntfy Leerzeichen/Umlaute korrekt statt als %20/%C3... an.
				string title = $"{alarmPrefix}: {entry.Name}";
				string body = L10n.Format("AlarmReached", direction, limitText, currentText);
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
			_notifyIcon.Text    = L10n.Text("TrayAlarmText");
		}

		private void ClearTrayDot()
		{
			if (!_dotActive) return;
			_dotActive          = false;
			_notifyIcon.Icon    = _baseIcon;
			_notifyIcon.Text    = L10n.Text("AppTitle");
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
				? $"{value:N2} [{L10n.Text("CurrencyMissingShort")}]"
				: $"{value:N2} {currency}";
		}

		private void RefreshListView(bool updatePortfolioTrend = false)
		{
			_listView.BeginUpdate();
			_listView.Items.Clear();

			if (_tabControl.SelectedTab == _tabRealized)
			{
				foreach (WatchlistEntry entry in _settings.Watchlist)
				{
					if (entry.EntryType == WatchlistEntryType.Realized)
						AddRealizedDetailItem(entry);
				}
			}
			else
			{
				foreach (WatchlistEntry entry in _settings.Watchlist)
				{
					if (!ShouldDisplayEntry(entry))
						continue;

					AddStandardListItem(entry);
				}
			}

			UpdatePortfolioSummary(updatePortfolioTrend);
			_listView.EndUpdate();
		}

		private bool ShouldDisplayEntry(WatchlistEntry entry)
		{
			if (_tabControl.SelectedTab == _tabHolding)
				return entry.EntryType == WatchlistEntryType.Holding;
			if (_tabControl.SelectedTab == _tabBuyCandidate)
				return entry.EntryType == WatchlistEntryType.BuyCandidate;
			if (_tabControl.SelectedTab != _tabOverview)
				return false;

			return entry.EntryType == WatchlistEntryType.Holding
				? _chkOverviewHolding.Checked
				: entry.EntryType == WatchlistEntryType.BuyCandidate
					? _chkOverviewBuyCandidate.Checked
					: entry.EntryType == WatchlistEntryType.Realized && _chkOverviewRealized.Checked;
		}

		private void AddStandardListItem(WatchlistEntry entry)
		{
			AddDynamicListItem(entry, false);
		}

		private void AddRealizedDetailItem(WatchlistEntry entry)
		{
			AddDynamicListItem(entry, true);
		}


		private static string NormalizeNote(string note) =>
			(note ?? "")
				.Replace("\r\n", " ")
				.Replace("\r", " ")
				.Replace("\n", " ");

		private static int CompareCurrentPriceToSalePrice(WatchlistEntry entry)
		{
			if (entry == null || entry.LastPrice <= 0.0 || entry.SalePrice <= 0.0)
				return 0;

			string quoteCurrency = (entry.QuoteCurrency ?? "").Trim().ToUpperInvariant();
			string saleCurrency = (entry.EffectiveSaleCurrency ?? "").Trim().ToUpperInvariant();
			double saleFx = entry.EffectiveSaleFxRate;

			// Wenn der aktuelle Kurs in der Liste als EUR angezeigt wird, auch den
			// Verkaufskurs für den Farbvergleich auf EUR normalisieren.
			if (entry.ConvertToEur && entry.LastPriceEur > 0.0 && saleFx > 0.0)
			{
				double currentComparable = Math.Round(
					entry.LastPriceEur, 2, MidpointRounding.AwayFromZero);
				double saleComparable = Math.Round(
					entry.SalePrice * saleFx, 2, MidpointRounding.AwayFromZero);
				return currentComparable.CompareTo(saleComparable);
			}

			// Gleiche Währung: den tatsächlichen Kurs direkt vergleichen.
			if (!string.IsNullOrEmpty(quoteCurrency) &&
				string.Equals(quoteCurrency, saleCurrency, StringComparison.OrdinalIgnoreCase))
			{
				double currentComparable = Math.Round(
					entry.LastPrice, 2, MidpointRounding.AwayFromZero);
				double saleComparable = Math.Round(
					entry.SalePrice, 2, MidpointRounding.AwayFromZero);
				return currentComparable.CompareTo(saleComparable);
			}

			// Unterschiedliche Währungen: auf EUR normalisieren, sofern beide Werte
			// verfügbar sind.
			if (entry.LastPriceEur > 0.0 && saleFx > 0.0)
			{
				double currentComparable = Math.Round(
					entry.LastPriceEur, 2, MidpointRounding.AwayFromZero);
				double saleComparable = Math.Round(
					entry.SalePrice * saleFx, 2, MidpointRounding.AwayFromZero);
				return currentComparable.CompareTo(saleComparable);
			}

			return 0;
		}

		private static bool TryGetReferenceValueEur(WatchlistEntry entry, out double valueEur)
		{
			valueEur = 0.0;
			double fx = entry.EffectiveReferenceFxRate;
			if (entry.Quantity <= 0 || entry.ReferencePrice <= 0 || fx <= 0)
				return false;

			valueEur = entry.Quantity * entry.ReferencePrice * fx;
			return true;
		}

		private static bool TryGetSaleValueEur(WatchlistEntry entry, out double valueEur)
		{
			valueEur = 0.0;
			double fx = entry.EffectiveSaleFxRate;
			if (entry.Quantity <= 0 || entry.SalePrice <= 0 || fx <= 0)
				return false;

			valueEur = entry.Quantity * entry.SalePrice * fx;
			return true;
		}

		private static bool TryGetRealizedGainLossEur(
			WatchlistEntry entry,
			out double gainLossEur,
			out double gainLossPct)
		{
			gainLossEur = 0.0;
			gainLossPct = 0.0;
			if (!TryGetReferenceValueEur(entry, out double referenceValueEur) ||
				!TryGetSaleValueEur(entry, out double saleValueEur) ||
				referenceValueEur <= 0)
				return false;

			gainLossEur = saleValueEur - referenceValueEur + entry.IncomeEur;
			gainLossPct = gainLossEur / referenceValueEur * 100.0;
			return true;
		}

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
			double totalOpenGainLossEur = 0.0;
			double totalRealizedGainLossEur = 0.0;
			double totalIncomeEur = 0.0;
			bool marketValueComplete = true;
			bool openGainLossComplete = true;
			bool realizedGainLossComplete = true;

			foreach (WatchlistEntry entry in _settings.Watchlist)
			{
				totalIncomeEur += entry.IncomeEur;

				if (entry.EntryType == WatchlistEntryType.Holding && entry.Quantity > 0)
				{
					positionCount++;

					if (entry.LastPriceEur > 0)
						totalMarketValueEur += entry.Quantity * entry.LastPriceEur;
					else
						marketValueComplete = false;

					if (TryGetReferenceValueEur(entry, out double referenceValueEur) && entry.LastPriceEur > 0)
					{
						double currentValueEur = entry.Quantity * entry.LastPriceEur;
						totalOpenGainLossEur += currentValueEur - referenceValueEur;
					}
					else
					{
						openGainLossComplete = false;
					}
				}
				else if (entry.EntryType == WatchlistEntryType.Realized)
				{
					if (TryGetReferenceValueEur(entry, out double referenceValueEur) &&
						TryGetSaleValueEur(entry, out double saleValueEur))
					{
						totalRealizedGainLossEur += saleValueEur - referenceValueEur;
					}
					else
					{
						realizedGainLossComplete = false;
					}
				}
			}

			totalRealizedGainLossEur += totalIncomeEur;

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
			string openText = openGainLossComplete
				? FormatSignedEur(totalOpenGainLossEur)
				: "– EUR";
			string realizedText = realizedGainLossComplete
				? FormatSignedEur(totalRealizedGainLossEur)
				: "– EUR";
			string totalText = openGainLossComplete && realizedGainLossComplete
				? FormatSignedEur(totalOpenGainLossEur + totalRealizedGainLossEur)
				: "– EUR";

			_lblPortfolioSummary.Text = L10n.Format("PortfolioSummary",
				_portfolioTrendIndicator, positionCount, marketValueText, openText, realizedText, totalText);
		}

		private static string FormatSignedEur(double value)
		{
			double displayValue = NormalizeTwoDecimalDisplay(value);
			return $"{(displayValue > 0 ? "+" : "")}{displayValue:N2} EUR";
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
				_notifyIcon.ShowBalloonTip(2000, L10n.Text("AppTitle"),
					L10n.Text("TrayBackground"), ToolTipIcon.Info);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_client?.Dispose();
				_disabledLimitFont?.Dispose();
				_notifyIcon?.Dispose();
				_entryContextMenu?.Dispose();
				_columnHeaderContextMenu?.Dispose();
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
			public bool TreatAsDate { get; set; } = false;

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

				// Datumsspalten chronologisch vergleichen; andere Spalten numerisch,
				// wenn möglich, sonst alphabetisch.
				int result;
				if (TreatAsDate && TryDate(sx, out DateTime dateX) && TryDate(sy, out DateTime dateY))
					result = dateX.CompareTo(dateY);
				else if (TryNum(sx, out double dx) && TryNum(sy, out double dy))
					result = dx.CompareTo(dy);
				else
					result = string.Compare(sx, sy, StringComparison.CurrentCultureIgnoreCase);

				return Order == SortOrder.Ascending ? result : -result;
			}

			private static bool TryDate(string s, out DateTime value)
			{
				string[] formats =
				{
					"dd.MM.yyyy",
					"d.M.yyyy",
					"d.MM.yyyy",
					"dd.M.yyyy",
					"yyyy-MM-dd",
					"dd.MM.yyyy HH:mm:ss",
					"yyyy-MM-dd HH:mm:ss"
				};

				return DateTime.TryParseExact(
					s.Trim(),
					formats,
					CultureInfo.InvariantCulture,
					DateTimeStyles.None,
					out value);
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
