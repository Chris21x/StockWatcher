using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StockWatcher.Models
{
	public class AppSettings
	{
		// ---- Einstellungen ----
		public int    IntervalMinutes  { get; set; } = 10;
		public int    DataRetrievalTimeoutMinutes { get; set; } = 240;
		public string ColumnOrder      { get; set; } = ""; // Legacy: Übersicht bis V1.1.3.x
		public string ColumnWidths     { get; set; } = ""; // Legacy: Übersicht bis V1.1.3.x
		public Dictionary<string, List<ColumnLayoutItem>> ColumnLayouts { get; } =
			new Dictionary<string, List<ColumnLayoutItem>>(StringComparer.OrdinalIgnoreCase);
		public bool OverviewFilterHolding { get; set; } = true;
		public bool OverviewFilterBuyCandidate { get; set; } = true;
		public bool OverviewFilterRealized { get; set; } = false;
		public int    MainWindowLeft   { get; set; } = 0;
		public int    MainWindowTop    { get; set; } = 0;
		public int    MainWindowWidth  { get; set; } = 0;
		public int    MainWindowHeight { get; set; } = 0;
		public bool   MainWindowMaximized { get; set; } = false;
		public bool   StartMinimized      { get; set; } = false;
		public List<WatchlistEntry> Watchlist { get; set; } = new List<WatchlistEntry>();

		/// <summary>Pfad zur XML-Datendatei (frei wählbar, auch Cloud-Ordner).</summary>
		public string DataFilePath { get; set; }

		// ---- Benachrichtigungsoptionen (lokal) ----
		/// <summary>Balloon-Tipp im Tray bei Kurs-Alarm anzeigen.</summary>
		public bool NotifyBalloon     { get; set; } = true;
		/// <summary>AlarmDialog (modales Fenster) bei Kurs-Alarm anzeigen.</summary>
		public bool NotifyAlarmDialog { get; set; } = true;
		/// <summary>Rote Markierung (Dot) am Tray-Icon bei Kurs-Alarm anzeigen.</summary>
		public bool NotifyTrayDot     { get; set; } = true;

		// ---- Push-Benachrichtigungen via ntfy.sh ----
		/// <summary>Push-Benachrichtigungen via ntfy aktivieren.</summary>
		public bool   NtfyEnabled { get; set; } = false;
		/// <summary>ntfy-Topic (frei wählbar, z.B. "boerse-alarm-xyz42").</summary>
		public string NtfyTopic   { get; set; } = "";
		/// <summary>ntfy-Server-URL. Standard: https://ntfy.sh</summary>
		public string NtfyUrl     { get; set; } = "https://ntfy.sh";

		// Bootstrap-INI: liegt immer im App-Ordner; DataFile wird hier verwaltet, weitere Sektionen/Schlüssel bleiben erhalten.
		private static readonly string BootstrapPath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"StockWatcher.ini"
		);

		private static readonly string DefaultXmlPath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"StockWatcher.xml"
		);

		// -----------------------------------------------------------------------
		// Laden
		// -----------------------------------------------------------------------

		public static AppSettings Load()
		{
			string xmlPath = ReadBootstrap();
			var s = new AppSettings { DataFilePath = xmlPath };

			if (!File.Exists(xmlPath))
				return s;

			try
			{
				XDocument doc = XDocument.Load(xmlPath);
				XElement root = doc.Root;
				if (root == null) return s;

				XElement general = root.Element("General");
				if (general != null)
				{
					if (int.TryParse(general.Element("IntervalMinutes")?.Value, out int iv))
						s.IntervalMinutes = Math.Max(1, Math.Min(60, iv));
					if (int.TryParse(general.Element("DataRetrievalTimeoutMinutes")?.Value, out int timeoutMinutes))
						s.DataRetrievalTimeoutMinutes = Math.Max(0, timeoutMinutes);
					s.ColumnOrder      = general.Element("ColumnOrder")?.Value ?? "";
					s.ColumnWidths     = general.Element("ColumnWidths")?.Value ?? "";
					s.OverviewFilterHolding = ReadBool(general.Element("OverviewFilterHolding"), true);
					s.OverviewFilterBuyCandidate = ReadBool(general.Element("OverviewFilterBuyCandidate"), true);
					s.OverviewFilterRealized = ReadBool(general.Element("OverviewFilterRealized"), false);
					ReadColumnLayouts(general.Element("ColumnLayouts"), s.ColumnLayouts);
					if (int.TryParse(general.Element("MainWindowLeft")?.Value, out int wl))
						s.MainWindowLeft = wl;
					if (int.TryParse(general.Element("MainWindowTop")?.Value, out int wt))
						s.MainWindowTop = wt;
					if (int.TryParse(general.Element("MainWindowWidth")?.Value, out int ww))
						s.MainWindowWidth = ww;
					if (int.TryParse(general.Element("MainWindowHeight")?.Value, out int wh))
						s.MainWindowHeight = wh;
					s.MainWindowMaximized = general.Element("MainWindowMaximized")?.Value == "true";
					s.StartMinimized      = general.Element("StartMinimized")?.Value == "true";
					// Benachrichtigungen: Default true → false nur wenn explizit "false"
					s.NotifyBalloon     = general.Element("NotifyBalloon")?.Value     != "false";
					s.NotifyAlarmDialog = general.Element("NotifyAlarmDialog")?.Value != "false";
					s.NotifyTrayDot     = general.Element("NotifyTrayDot")?.Value     != "false";
					// ntfy-Push: Default false → true nur wenn explizit "true"
					s.NtfyEnabled = general.Element("NtfyEnabled")?.Value == "true";
					s.NtfyTopic   = general.Element("NtfyTopic")?.Value   ?? "";
					s.NtfyUrl     = general.Element("NtfyUrl")?.Value?.Trim() is string u && u.Length > 0
					                ? u : "https://ntfy.sh";
				}

				XElement watchlist = root.Element("Watchlist");
				if (watchlist != null)
				{
					foreach (XElement el in watchlist.Elements("Entry"))
					{
						var e = new WatchlistEntry
						{
							Isin                 = el.Element("ISIN")?.Value ?? "",
							Name                 = el.Element("Name")?.Value ?? "",
							EntryType            = ParseEntryType(el.Element("EntryType")?.Value),
							Note                 = el.Element("Note")?.Value ?? "",
							YahooSymbol          = el.Element("YahooSymbol")?.Value ?? "",
							QuoteCurrency        = (el.Element("QuoteCurrency")?.Value ?? "").Trim().ToUpperInvariant(),
							LimitUpper           = ParseInv(el.Element("LimitUpper")?.Value),
							LimitUpperType       = ParseLimitValueType(el.Element("LimitUpperType")?.Value),
							LimitUpperEnabled    = el.Element("LimitUpperEnabled")?.Value == "true",
							LimitLower           = ParseInv(el.Element("LimitLower")?.Value),
							LimitLowerType       = ParseLimitValueType(el.Element("LimitLowerType")?.Value),
							LimitLowerEnabled    = el.Element("LimitLowerEnabled")?.Value == "true",
							ConvertToEur         = el.Element("ConvertToEur")?.Value == "true",
							Quantity             = ParseInv(el.Element("Quantity")?.Value),
							ReferencePrice       = ParseInv(el.Element("ReferencePrice")?.Value),
							ReferenceCurrency    = (el.Element("ReferenceCurrency")?.Value ?? "").Trim().ToUpperInvariant(),
							ReferenceDate        = ParseDate(el.Element("ReferenceDate")?.Value),
							ReferenceFxRate      = ParseInv(el.Element("ReferenceFxRate")?.Value),
							IncomeEur            = ParseInv(el.Element("IncomeEur")?.Value),
							SalePrice            = ParseInv(el.Element("SalePrice")?.Value),
							SaleCurrency         = (el.Element("SaleCurrency")?.Value ?? "").Trim().ToUpperInvariant(),
							SaleDate             = ParseDate(el.Element("SaleDate")?.Value),
							SaleFxRate           = ParseInv(el.Element("SaleFxRate")?.Value),
							LastPrice            = ParseInv(el.Element("LastPrice")?.Value),
							LastPriceEur         = ParseInv(el.Element("LastPriceEur")?.Value),
							FxRate               = ParseInv(el.Element("FxRate")?.Value) is double fx && fx > 0 ? fx : 1.0,
							LastUpdate           = ParseDate(el.Element("LastUpdate")?.Value),
							LastSuccessfulQuoteFetch = ParseDate(el.Element("LastSuccessfulQuoteFetch")?.Value),
							StatusText           = el.Element("StatusText")?.Value ?? "–"
						};

						// Historische Semantik: eine leere Kaufwährung bedeutete EUR.
						// Bei vorhandener Referenz wird das nun explizit gemacht, damit die XML eindeutig bleibt.
						if (e.ReferencePrice > 0 && string.IsNullOrWhiteSpace(e.ReferenceCurrency))
							e.ReferenceCurrency = "EUR";

						if (!string.IsNullOrWhiteSpace(e.Isin))
							s.Watchlist.Add(e);
					}
				}
			}
			catch { /* defektes XML → Defaults */ }

			return s;
		}

		// -----------------------------------------------------------------------
		// Speichern
		// -----------------------------------------------------------------------

		public void Save()
		{
			try
			{
				var doc = new XDocument(
					new XDeclaration("1.0", "utf-8", null),
					new XComment(
						"\r\n" +
						"\tStockWatcher Datendatei\r\n" +
						"\tSpeicherort frei wählbar – auch in einer Cloud-Synchronisations-Ablage.\r\n" +
						"\tQuoteCurrency = Kurs-/Listingwährung des ausgewählten Symbols.\r\n" +
						"\tReferencePrice/ReferenceCurrency = Kauf-/Referenzbasis; ReferenceDate ist optional.\r\n" +
						"\tIncomeEur = manuell erfasste Erträge/Dividenden in EUR.\r\n" +
						"\tSalePrice/SaleCurrency/SaleDate/SaleFxRate = Verkaufsdaten realisierter Positionen.\r\n" +
						"\tAbsolute Limits: EUR wenn ConvertToEur=true, sonst QuoteCurrency.\r\n" +
						"\tPercent Limits: Prozentänderung relativ zu ReferencePrice, Vergleich in ReferenceCurrency; aktiv nur über Limit*Enabled.\r\n" +
						"\tDezimaltrennzeichen: Punkt (Invariant-Format). Manuelle Bearbeitung möglich.\r\n"
					),
					new XElement("StockWatcher",
						new XElement("General",
							new XElement("IntervalMinutes",  IntervalMinutes),
							new XElement("DataRetrievalTimeoutMinutes", DataRetrievalTimeoutMinutes),
							new XElement("ColumnOrder",      ColumnOrder ?? ""),
							new XElement("ColumnWidths",     ColumnWidths ?? ""),
							new XElement("OverviewFilterHolding", OverviewFilterHolding ? "true" : "false"),
							new XElement("OverviewFilterBuyCandidate", OverviewFilterBuyCandidate ? "true" : "false"),
							new XElement("OverviewFilterRealized", OverviewFilterRealized ? "true" : "false"),
							BuildColumnLayoutsElement(),
							new XElement("MainWindowLeft",   MainWindowWidth > 0 ? MainWindowLeft.ToString() : ""),
							new XElement("MainWindowTop",    MainWindowHeight > 0 ? MainWindowTop.ToString() : ""),
							new XElement("MainWindowWidth",  MainWindowWidth > 0 ? MainWindowWidth.ToString() : ""),
							new XElement("MainWindowHeight", MainWindowHeight > 0 ? MainWindowHeight.ToString() : ""),
							new XElement("MainWindowMaximized", MainWindowMaximized ? "true" : "false"),
							new XElement("StartMinimized",      StartMinimized ? "true" : "false"),
							new XElement("NotifyBalloon",     NotifyBalloon     ? "true" : "false"),
							new XElement("NotifyAlarmDialog", NotifyAlarmDialog ? "true" : "false"),
							new XElement("NotifyTrayDot",     NotifyTrayDot     ? "true" : "false"),
							new XElement("NtfyEnabled",       NtfyEnabled       ? "true" : "false"),
							new XElement("NtfyTopic",         NtfyTopic  ?? ""),
							new XElement("NtfyUrl",           NtfyUrl    ?? "https://ntfy.sh")
						),
						BuildWatchlistElement()
					)
				);

				string dir = Path.GetDirectoryName(DataFilePath);
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				var xmlSettings = new XmlWriterSettings
				{
					Encoding        = new UTF8Encoding(false), // UTF-8 ohne BOM
					Indent          = true,
					IndentChars     = "\t",
					OmitXmlDeclaration = false
				};

				using (XmlWriter writer = XmlWriter.Create(DataFilePath, xmlSettings))
					doc.Save(writer);

				WriteBootstrap(DataFilePath);
			}
			catch { /* Kein Absturz bei Schreibfehler */ }
		}

		private XElement BuildColumnLayoutsElement()
		{
			var root = new XElement("ColumnLayouts");
			string[] tabKeys = { "Overview", "Holding", "BuyCandidate", "Realized" };

			foreach (string tabKey in tabKeys)
			{
				var tab = new XElement(tabKey);
				if (ColumnLayouts.TryGetValue(tabKey, out List<ColumnLayoutItem> items) && items != null)
				{
					var ordered = new List<ColumnLayoutItem>(items);
					ordered.Sort((a, b) => a.Order.CompareTo(b.Order));
					foreach (ColumnLayoutItem item in ordered)
					{
						if (item == null || string.IsNullOrWhiteSpace(item.Id))
							continue;

						tab.Add(new XElement("Column",
							new XAttribute("id", item.Id),
							new XAttribute("visible", item.Visible ? "true" : "false"),
							new XAttribute("width", Math.Max(30, item.Width)),
							new XAttribute("order", Math.Max(0, item.Order))));
					}
				}
				root.Add(tab);
			}

			return root;
		}

		private static void ReadColumnLayouts(
			XElement layoutsElement,
			Dictionary<string, List<ColumnLayoutItem>> target)
		{
			if (layoutsElement == null || target == null)
				return;

			string[] tabKeys = { "Overview", "Holding", "BuyCandidate", "Realized" };
			foreach (string tabKey in tabKeys)
			{
				XElement tab = layoutsElement.Element(tabKey);
				if (tab == null)
					continue;

				var items = new List<ColumnLayoutItem>();
				foreach (XElement column in tab.Elements("Column"))
				{
					string id = (column.Attribute("id")?.Value ?? "").Trim();
					if (string.IsNullOrEmpty(id))
						continue;

					int width = 100;
					int order = items.Count;
					if (int.TryParse(column.Attribute("width")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth))
						width = parsedWidth;
					if (int.TryParse(column.Attribute("order")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedOrder))
						order = parsedOrder;

					items.Add(new ColumnLayoutItem
					{
						Id = id,
						Visible = string.Equals(column.Attribute("visible")?.Value, "true", StringComparison.OrdinalIgnoreCase),
						Width = Math.Max(30, width),
						Order = Math.Max(0, order)
					});
				}

				if (items.Count > 0)
					target[tabKey] = items;
			}
		}

		private static bool ReadBool(XElement element, bool defaultValue)
		{
			if (element == null)
				return defaultValue;

			if (bool.TryParse(element.Value, out bool value))
				return value;

			return defaultValue;
		}

		private XElement BuildWatchlistElement()
		{
			var el = new XElement("Watchlist");
			foreach (WatchlistEntry e in Watchlist)
			{
				el.Add(new XElement("Entry",
					new XElement("ISIN",                 e.Isin),
					new XElement("Name",                 e.Name),
					new XElement("YahooSymbol",          e.YahooSymbol ?? ""),
					new XElement("QuoteCurrency",        e.QuoteCurrency ?? ""),
					new XElement("LimitUpper",           Inv(e.LimitUpper)),
					new XElement("LimitUpperType",       e.LimitUpperType.ToString()),
					new XElement("LimitUpperEnabled",    e.LimitUpperEnabled ? "true" : "false"),
					new XElement("LimitLower",           Inv(e.LimitLower)),
					new XElement("LimitLowerType",       e.LimitLowerType.ToString()),
					new XElement("LimitLowerEnabled",    e.LimitLowerEnabled ? "true" : "false"),
					new XElement("ConvertToEur",         e.ConvertToEur ? "true" : "false"),
					new XElement("Quantity",             Inv(e.Quantity)),
					new XElement("ReferencePrice",       Inv(e.ReferencePrice)),
					new XElement("ReferenceCurrency",    e.ReferenceCurrency ?? ""),
					new XElement("ReferenceDate",        e.ReferenceDate == DateTime.MinValue
					                                            ? "" : e.ReferenceDate.ToString("yyyy-MM-dd")),
					new XElement("ReferenceFxRate",      Inv(e.ReferenceFxRate)),
					new XElement("IncomeEur",            Inv(e.IncomeEur)),
					new XElement("SalePrice",            Inv(e.SalePrice)),
					new XElement("SaleCurrency",         e.SaleCurrency ?? ""),
					new XElement("SaleDate",             e.SaleDate == DateTime.MinValue
					                                            ? "" : e.SaleDate.ToString("yyyy-MM-dd")),
					new XElement("SaleFxRate",           Inv(e.SaleFxRate)),
					new XElement("EntryType",            e.EntryType.ToString()),
					new XElement("Note",                 e.Note ?? ""),
					new XElement("LastPrice",            Inv(e.LastPrice)),
					new XElement("LastPriceEur",         Inv(e.LastPriceEur)),
					new XElement("FxRate",               Inv(e.FxRate)),
					new XElement("LastUpdate",           e.LastUpdate == DateTime.MinValue
					                                            ? "" : e.LastUpdate.ToString("o")),
					new XElement("LastSuccessfulQuoteFetch", e.LastSuccessfulQuoteFetch == DateTime.MinValue
					                                            ? "" : e.LastSuccessfulQuoteFetch.ToString("o")),
					new XElement("StatusText",           e.StatusText ?? "–")
				));
			}
			return el;
		}

		// -----------------------------------------------------------------------
		// Bootstrap-INI (verwaltet DataFile; weitere lokale Konfiguration bleibt erhalten)
		// -----------------------------------------------------------------------

		private static string ReadBootstrap()
		{
			if (File.Exists(BootstrapPath))
			{
				foreach (string line in File.ReadAllLines(BootstrapPath, Encoding.UTF8))
				{
					string t = line.Trim();
					if (t.StartsWith("DataFile=", StringComparison.OrdinalIgnoreCase))
					{
						string path = t.Substring("DataFile=".Length).Trim();
						if (!string.IsNullOrEmpty(path))
						{
							// Relative Pfade werden gegen den INI-Ordner (= App-Ordner) aufgelöst
							if (!System.IO.Path.IsPathRooted(path))
								path = System.IO.Path.GetFullPath(
									System.IO.Path.Combine(
										System.IO.Path.GetDirectoryName(BootstrapPath), path));
							return path;
						}
					}
				}
			}
			return DefaultXmlPath;
		}

		private static void WriteBootstrap(string xmlPath)
		{
			try
			{
				if (!File.Exists(BootstrapPath))
				{
					File.WriteAllLines(BootstrapPath,
						new[] { "[StockWatcher]", $"DataFile={xmlPath}" },
						Encoding.UTF8);
					return;
				}

				var lines = new List<string>(File.ReadAllLines(BootstrapPath, Encoding.UTF8));

				int stockWatcherSection = -1;
				int nextSection = lines.Count;
				int dataFileLine = -1;

				for (int i = 0; i < lines.Count; i++)
				{
					string trimmed = lines[i].Trim();

					if (trimmed.Equals("[StockWatcher]", StringComparison.OrdinalIgnoreCase))
					{
						stockWatcherSection = i;
						nextSection = lines.Count;

						for (int j = i + 1; j < lines.Count; j++)
						{
							string sectionLine = lines[j].Trim();
							if (sectionLine.StartsWith("[", StringComparison.Ordinal) &&
								sectionLine.EndsWith("]", StringComparison.Ordinal))
							{
								nextSection = j;
								break;
							}

							if (sectionLine.StartsWith("DataFile=", StringComparison.OrdinalIgnoreCase))
								dataFileLine = j;
						}

						break;
					}
				}

				if (stockWatcherSection < 0)
				{
					if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
						lines.Add("");

					lines.Add("[StockWatcher]");
					lines.Add($"DataFile={xmlPath}");
				}
				else if (dataFileLine >= 0)
				{
					lines[dataFileLine] = $"DataFile={xmlPath}";
				}
				else
				{
					lines.Insert(nextSection, $"DataFile={xmlPath}");
				}

				File.WriteAllLines(BootstrapPath, lines, Encoding.UTF8);
			}
			catch { }
		}

		// -----------------------------------------------------------------------
		// Hilfsmethoden
		// -----------------------------------------------------------------------

		private static string Inv(double v) =>
			v.ToString(System.Globalization.CultureInfo.InvariantCulture);

		private static DateTime ParseDate(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
			if (DateTime.TryParse(s,
				System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
				return dt;
			return DateTime.MinValue;
		}

		private static WatchlistEntryType ParseEntryType(string raw)
		{
			if (Enum.TryParse(raw, true, out WatchlistEntryType value) &&
				Enum.IsDefined(typeof(WatchlistEntryType), value))
				return value;
			return WatchlistEntryType.Holding;
		}

		private static LimitValueType ParseLimitValueType(string raw)
		{
			if (Enum.TryParse(raw, true, out LimitValueType value) &&
				Enum.IsDefined(typeof(LimitValueType), value))
				return value;
			return LimitValueType.Absolute;
		}

		private static double ParseInv(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return 0.0;
			if (double.TryParse(s,
				System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture, out double v))
				return v;
			return 0.0;
		}
	}
}
