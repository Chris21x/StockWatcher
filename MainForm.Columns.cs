using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StockWatcher.Controls;
using StockWatcher.Localization;
using StockWatcher.Forms;
using StockWatcher.Models;

namespace StockWatcher
{
	public partial class MainForm
	{
		private const string TabKeyOverview = "Overview";
		private const string TabKeyHolding = "Holding";
		private const string TabKeyBuyCandidate = "BuyCandidate";
		private const string TabKeyRealized = "Realized";

		private static class ColumnIds
		{
			public const string Name = "Name";
			public const string Isin = "Isin";
			public const string Quantity = "Quantity";
			public const string ReferenceDate = "ReferenceDate";
			public const string ReferencePrice = "ReferencePrice";
			public const string ReferenceValue = "ReferenceValue";
			public const string ReferenceValueEur = "ReferenceValueEur";
			public const string Trend = "Trend";
			public const string CurrentPrice = "CurrentPrice";
			public const string CurrentValue = "CurrentValue";
			public const string CurrentValueEur = "CurrentValueEur";
			public const string GainLossEur = "GainLossEur";
			public const string GainLossPct = "GainLossPct";
			public const string LimitLower = "LimitLower";
			public const string LimitUpper = "LimitUpper";
			public const string EntryType = "EntryType";
			public const string Note = "Note";
			public const string Status = "Status";
			public const string YahooSymbol = "YahooSymbol";
			public const string QuoteCurrency = "QuoteCurrency";
			public const string LimitUpperType = "LimitUpperType";
			public const string LimitUpperEnabled = "LimitUpperEnabled";
			public const string LimitLowerType = "LimitLowerType";
			public const string LimitLowerEnabled = "LimitLowerEnabled";
			public const string ConvertToEur = "ConvertToEur";
			public const string ReferenceCurrency = "ReferenceCurrency";
			public const string ReferenceFxRate = "ReferenceFxRate";
			public const string IncomeEur = "IncomeEur";
			public const string SalePrice = "SalePrice";
			public const string SaleCurrency = "SaleCurrency";
			public const string SaleDate = "SaleDate";
			public const string SaleFxRate = "SaleFxRate";
			public const string SaleValueEur = "SaleValueEur";
			public const string LastPrice = "LastPrice";
			public const string LastPriceEur = "LastPriceEur";
			public const string FxRate = "FxRate";
			public const string LastUpdate = "LastUpdate";
			public const string LastSuccessfulQuoteFetch = "LastSuccessfulQuoteFetch";
			public const string StatusText = "StatusText";
			public const string AlarmUpperFired = "AlarmUpperFired";
			public const string AlarmLowerFired = "AlarmLowerFired";
			public const string UpperLimitReached = "UpperLimitReached";
			public const string LowerLimitReached = "LowerLimitReached";
			public const string QuoteFetchAttemptedThisSession = "QuoteFetchAttemptedThisSession";
			public const string DataRetrievalFailureSince = "DataRetrievalFailureSince";
			public const string LookupFailCount = "LookupFailCount";
			public const string NextLookupAttempt = "NextLookupAttempt";
			public const string ComparePrice = "ComparePrice";
			public const string AbsoluteLimitCurrency = "AbsoluteLimitCurrency";
			public const string EffectiveReferenceCurrency = "EffectiveReferenceCurrency";
			public const string EffectiveReferenceFxRate = "EffectiveReferenceFxRate";
			public const string EffectiveSaleCurrency = "EffectiveSaleCurrency";
			public const string EffectiveSaleFxRate = "EffectiveSaleFxRate";
		}

		private sealed class ColumnDefinition
		{
			public string Id { get; }
			public string Header { get; }
			public string ChooserName { get; }
			public int DefaultWidth { get; }
			public bool DefaultVisible { get; }
			public HorizontalAlignment Alignment { get; }
			public bool TreatAsDate { get; }

			public ColumnDefinition(
				string id,
				string header,
				int defaultWidth,
				bool defaultVisible,
				string chooserName = null,
				HorizontalAlignment alignment = HorizontalAlignment.Left,
				bool treatAsDate = false)
			{
				Id = id;
				Header = header;
				ChooserName = string.IsNullOrWhiteSpace(chooserName) ? header : chooserName;
				DefaultWidth = defaultWidth;
				DefaultVisible = defaultVisible;
				Alignment = alignment;
				TreatAsDate = treatAsDate;
			}
		}

		private sealed class CellValue
		{
			public string Text { get; set; } = "–";
			public Color? ForeColor { get; set; }
			public Color? BackColor { get; set; }
			public Font Font { get; set; }
		}

		private string GetCurrentTabKey()
		{
			if (_tabControl?.SelectedTab == _tabHolding) return TabKeyHolding;
			if (_tabControl?.SelectedTab == _tabBuyCandidate) return TabKeyBuyCandidate;
			if (_tabControl?.SelectedTab == _tabRealized) return TabKeyRealized;
			return TabKeyOverview;
		}

		private List<ColumnDefinition> CreateColumnDefinitions(string tabKey)
		{
			var result = new List<ColumnDefinition>();
			bool overview = string.Equals(tabKey, TabKeyOverview, StringComparison.OrdinalIgnoreCase);
			bool realized = string.Equals(tabKey, TabKeyRealized, StringComparison.OrdinalIgnoreCase);

			if (realized)
			{
				AddDefinition(result, ColumnIds.Name, L10n.Text("ColName"), 240, true);
				AddDefinition(result, ColumnIds.Isin, L10n.Text("ColIsin"), 120, true);
				AddDefinition(result, ColumnIds.Quantity, L10n.Text("ColQuantity"), 60, true);
				AddDefinition(result, ColumnIds.ReferenceDate, L10n.Text("ColPurchaseDate"), 95, true, treatAsDate: true);
				AddDefinition(result, ColumnIds.ReferencePrice, L10n.Text("ColPurchasePrice"), 125, true);
				AddDefinition(result, ColumnIds.ReferenceValue, L10n.Text("ColPurchaseValueEur"), 125, true);
				AddDefinition(result, ColumnIds.SaleDate, L10n.Text("ColSaleDate"), 105, true, treatAsDate: true);
				AddDefinition(result, ColumnIds.SalePrice, L10n.Text("ColSalePrice"), 125, true);
				AddDefinition(result, ColumnIds.SaleValueEur, L10n.Text("ColSaleValueEur"), 135, true);
				AddDefinition(result, ColumnIds.GainLossEur, L10n.Text("ColGainLossEur"), 105, true);
				AddDefinition(result, ColumnIds.GainLossPct, L10n.Text("ColGainLossPct"), 80, true);
				AddDefinition(result, ColumnIds.CurrentPrice, L10n.Text("ColCurrentPrice"), 125, true);
				AddDefinition(result, ColumnIds.Note, L10n.Text("ColNote"), 300, true);
				AddDefinition(result, ColumnIds.Status, L10n.Text("ColStatus"), 170, true, L10n.Text("ColStatusDisplay"));
			}
			else
			{
				AddDefinition(result, ColumnIds.Name, L10n.Text("ColName"), 240, true);
				AddDefinition(result, ColumnIds.Isin, L10n.Text("ColIsin"), 120, true);
				AddDefinition(result, ColumnIds.Quantity, L10n.Text("ColQuantity"), 55, true);
				AddDefinition(result, ColumnIds.ReferencePrice, L10n.Text("ColReferencePrice"), 140, true);
				AddDefinition(result, ColumnIds.ReferenceValue, L10n.Text("ColReferenceValue"), 150, true);
				AddDefinition(result, ColumnIds.Trend, "▲▼", 45, true, L10n.Text("ColTrendChooser"), HorizontalAlignment.Center);
				AddDefinition(result, ColumnIds.CurrentPrice, overview ? L10n.Text("ColPrice") : L10n.Text("ColCurrentPrice"), 130, true);
				AddDefinition(result, ColumnIds.CurrentValue, overview ? L10n.Text("ColValue") : L10n.Text("ColMarketValue"), 135, true);
				AddDefinition(result, ColumnIds.GainLossEur, overview ? L10n.Text("ColGainLossEur") : L10n.Text("ColDiffEur"), 105, true);
				AddDefinition(result, ColumnIds.GainLossPct, overview ? L10n.Text("ColGainLossPct") : L10n.Text("ColDiffPct"), 80, true);
				AddDefinition(result, ColumnIds.LimitLower, "Limit ▼", 110, true);
				AddDefinition(result, ColumnIds.LimitUpper, "Limit ▲", 110, true);
				AddDefinition(result, ColumnIds.EntryType, L10n.Text("ColEntryType"), 105, true);
				AddDefinition(result, ColumnIds.Note, L10n.Text("ColNote"), 300, true);
				AddDefinition(result, ColumnIds.Status, L10n.Text("ColStatus"), 160, true, L10n.Text("ColStatusDisplay"));
			}

			// Zusätzliche Felder. Alle bereits intern geführten WatchlistEntry-Felder
			// sind damit auswählbar, ohne das bisherige Default-Layout zu verändern.
			AddDefinition(result, ColumnIds.ReferenceDate, L10n.Text("ColReferenceDate"), 115, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.ReferenceCurrency, L10n.Text("ColReferenceCurrency"), 125, false);
			AddDefinition(result, ColumnIds.ReferenceFxRate, L10n.Text("ColReferenceFx"), 100, false);
			AddDefinition(result, ColumnIds.ReferenceValueEur, L10n.Text("ColCalculatedCostEur"), 145, false);
			AddDefinition(result, ColumnIds.IncomeEur, L10n.Text("ColIncomeEur"), 105, false);
			AddDefinition(result, ColumnIds.SalePrice, L10n.Text("ColSalePrice"), 125, false);
			AddDefinition(result, ColumnIds.SaleCurrency, L10n.Text("ColSaleCurrency"), 125, false);
			AddDefinition(result, ColumnIds.SaleDate, L10n.Text("ColSaleDate"), 115, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.SaleFxRate, L10n.Text("ColSaleFx"), 100, false);
			AddDefinition(result, ColumnIds.SaleValueEur, L10n.Text("ColSaleValueEur"), 135, false);
			AddDefinition(result, ColumnIds.CurrentValueEur, L10n.Text("ColCurrentValueEur"), 125, false);
			AddDefinition(result, ColumnIds.YahooSymbol, L10n.Text("ColYahooSymbol"), 120, false);
			AddDefinition(result, ColumnIds.QuoteCurrency, L10n.Text("ColQuoteCurrency"), 105, false);
			AddDefinition(result, ColumnIds.LimitLowerType, L10n.Text("ColLimitLowerType"), 105, false);
			AddDefinition(result, ColumnIds.LimitLowerEnabled, L10n.Text("ColLimitLowerEnabled"), 105, false);
			AddDefinition(result, ColumnIds.LimitUpperType, L10n.Text("ColLimitUpperType"), 105, false);
			AddDefinition(result, ColumnIds.LimitUpperEnabled, L10n.Text("ColLimitUpperEnabled"), 105, false);
			AddDefinition(result, ColumnIds.ConvertToEur, L10n.Text("ColConvertToEur"), 125, false);
			AddDefinition(result, ColumnIds.LastPrice, L10n.Text("ColLastPrice"), 105, false, L10n.Text("ColLastPriceInternal"));
			AddDefinition(result, ColumnIds.LastPriceEur, L10n.Text("ColLastPriceEur"), 115, false, L10n.Text("ColLastPriceEurInternal"));
			AddDefinition(result, ColumnIds.FxRate, L10n.Text("ColCurrentFx"), 95, false, L10n.Text("ColFxInternal"));
			AddDefinition(result, ColumnIds.LastUpdate, L10n.Text("ColQuoteTimestamp"), 155, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.LastSuccessfulQuoteFetch, L10n.Text("ColLastSuccessfulFetch"), 190, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.StatusText, L10n.Text("ColInternalStatus"), 180, false, L10n.Text("ColStatusTextInternal"));
			AddDefinition(result, ColumnIds.AlarmUpperFired, L10n.Text("ColAlarmUpperFired"), 125, false);
			AddDefinition(result, ColumnIds.AlarmLowerFired, L10n.Text("ColAlarmLowerFired"), 125, false);
			AddDefinition(result, ColumnIds.UpperLimitReached, L10n.Text("ColUpperReached"), 120, false);
			AddDefinition(result, ColumnIds.LowerLimitReached, L10n.Text("ColLowerReached"), 120, false);
			AddDefinition(result, ColumnIds.QuoteFetchAttemptedThisSession, L10n.Text("ColFetchAttempted"), 165, false);
			AddDefinition(result, ColumnIds.DataRetrievalFailureSince, L10n.Text("ColDataFailureSince"), 155, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.LookupFailCount, L10n.Text("ColLookupFailures"), 105, false);
			AddDefinition(result, ColumnIds.NextLookupAttempt, L10n.Text("ColNextLookup"), 155, false, treatAsDate: true);
			AddDefinition(result, ColumnIds.ComparePrice, L10n.Text("ColComparePrice"), 110, false);
			AddDefinition(result, ColumnIds.AbsoluteLimitCurrency, L10n.Text("ColAbsoluteLimitCurrency"), 150, false);
			AddDefinition(result, ColumnIds.EffectiveReferenceCurrency, L10n.Text("ColEffectiveReferenceCurrency"), 145, false);
			AddDefinition(result, ColumnIds.EffectiveReferenceFxRate, L10n.Text("ColEffectiveReferenceFx"), 120, false);
			AddDefinition(result, ColumnIds.EffectiveSaleCurrency, L10n.Text("ColEffectiveSaleCurrency"), 145, false);
			AddDefinition(result, ColumnIds.EffectiveSaleFxRate, L10n.Text("ColEffectiveSaleFx"), 120, false);

			// Auch die regulären Anzeige-/Berechnungsfelder bleiben in jedem Reiter auswählbar.
			AddDefinition(result, ColumnIds.Trend, "▲▼", 45, false, L10n.Text("ColTrendChooser"), HorizontalAlignment.Center);
			AddDefinition(result, ColumnIds.CurrentValue, L10n.Text("ColMarketValue"), 135, false);
			AddDefinition(result, ColumnIds.GainLossEur, L10n.Text("ColGainLossEur"), 105, false);
			AddDefinition(result, ColumnIds.GainLossPct, L10n.Text("ColGainLossPct"), 80, false);
			AddDefinition(result, ColumnIds.LimitLower, "Limit ▼", 110, false);
			AddDefinition(result, ColumnIds.LimitUpper, "Limit ▲", 110, false);
			AddDefinition(result, ColumnIds.EntryType, L10n.Text("ColEntryType"), 105, false);

			return result;
		}

		private static void AddDefinition(
			List<ColumnDefinition> list,
			string id,
			string header,
			int width,
			bool visible,
			string chooserName = null,
			HorizontalAlignment alignment = HorizontalAlignment.Left,
			bool treatAsDate = false)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase))
					return;
			}

			list.Add(new ColumnDefinition(
				id,
				header,
				width,
				visible,
				chooserName,
				alignment,
				treatAsDate));
		}

		private void ConfigureColumnsForSelectedTabCore()
		{
			if (_listView == null || _tabControl == null || _settings == null)
				return;

			string tabKey = GetCurrentTabKey();
			List<ColumnDefinition> definitions = CreateColumnDefinitions(tabKey);
			List<ColumnLayoutItem> layout = EnsureColumnLayout(tabKey, definitions);
			var definitionById = BuildDefinitionMap(definitions);
			var ordered = new List<ColumnLayoutItem>(layout);
			ordered.Sort((a, b) => a.Order.CompareTo(b.Order));

			_restoringLayout = true;
			try
			{
				_listView.BeginUpdate();
				_listView.Items.Clear();
				_listView.Columns.Clear();

				foreach (ColumnLayoutItem item in ordered)
				{
					if (!item.Visible || !definitionById.TryGetValue(item.Id, out ColumnDefinition definition))
						continue;

					var header = new ColumnHeader
					{
						Text = definition.Header,
						Width = Math.Max(30, Math.Min(5000, item.Width)),
						TextAlign = definition.Alignment,
						Tag = definition
					};
					_listView.Columns.Add(header);
				}

				_sortCol = -1;
				_sortDir = SortOrder.None;
				_sorter.Column = 0;
				_sorter.Order = SortOrder.Ascending;
				_sorter.TreatAsDate = false;
			}
			finally
			{
				_listView.EndUpdate();
				_restoringLayout = false;
			}
		}

		private Dictionary<string, ColumnDefinition> BuildDefinitionMap(List<ColumnDefinition> definitions)
		{
			var map = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
			foreach (ColumnDefinition definition in definitions)
				map[definition.Id] = definition;
			return map;
		}

		private List<ColumnLayoutItem> EnsureColumnLayout(
			string tabKey,
			List<ColumnDefinition> definitions)
		{
			if (!_settings.ColumnLayouts.TryGetValue(tabKey, out List<ColumnLayoutItem> existing) ||
				existing == null || existing.Count == 0)
			{
				existing = CreateDefaultColumnLayout(tabKey, definitions);
				_settings.ColumnLayouts[tabKey] = existing;
				return existing;
			}

			var definitionById = BuildDefinitionMap(definitions);
			var known = new Dictionary<string, ColumnLayoutItem>(StringComparer.OrdinalIgnoreCase);
			var orderedExisting = new List<ColumnLayoutItem>(existing);
			orderedExisting.Sort((a, b) => a.Order.CompareTo(b.Order));
			var normalized = new List<ColumnLayoutItem>();

			foreach (ColumnLayoutItem item in orderedExisting)
			{
				if (item == null || string.IsNullOrWhiteSpace(item.Id) ||
					!definitionById.ContainsKey(item.Id) || known.ContainsKey(item.Id))
					continue;

				item.Width = Math.Max(30, Math.Min(5000, item.Width));
				known[item.Id] = item;
				normalized.Add(item);
			}

			foreach (ColumnDefinition definition in definitions)
			{
				if (known.ContainsKey(definition.Id))
					continue;

				var item = new ColumnLayoutItem
				{
					Id = definition.Id,
					Visible = definition.DefaultVisible,
					Width = definition.DefaultWidth,
					Order = normalized.Count
				};
				known[item.Id] = item;
				normalized.Add(item);
			}

			bool anyVisible = false;
			for (int i = 0; i < normalized.Count; i++)
			{
				normalized[i].Order = i;
				anyVisible |= normalized[i].Visible;
			}

			if (!anyVisible && normalized.Count > 0)
				normalized[0].Visible = true;

			_settings.ColumnLayouts[tabKey] = normalized;
			return normalized;
		}

		private List<ColumnLayoutItem> CreateDefaultColumnLayout(
			string tabKey,
			List<ColumnDefinition> definitions)
		{
			var result = new List<ColumnLayoutItem>();
			foreach (ColumnDefinition definition in definitions)
			{
				result.Add(new ColumnLayoutItem
				{
					Id = definition.Id,
					Visible = definition.DefaultVisible,
					Width = definition.DefaultWidth,
					Order = result.Count
				});
			}

			if (string.Equals(tabKey, TabKeyOverview, StringComparison.OrdinalIgnoreCase))
				ApplyLegacyOverviewLayout(result, definitions);

			return result;
		}

		private void ApplyLegacyOverviewLayout(
			List<ColumnLayoutItem> layout,
			List<ColumnDefinition> definitions)
		{
			var visibleDefinitions = new List<ColumnDefinition>();
			foreach (ColumnDefinition definition in definitions)
			{
				if (definition.DefaultVisible)
					visibleDefinitions.Add(definition);
			}

			string[] widths = string.IsNullOrWhiteSpace(_settings.ColumnWidths)
				? Array.Empty<string>()
				: _settings.ColumnWidths.Split(',');
			if (widths.Length == visibleDefinitions.Count)
			{
				for (int i = 0; i < widths.Length; i++)
				{
					if (int.TryParse(widths[i].Trim(), NumberStyles.Integer,
						CultureInfo.InvariantCulture, out int width) && width > 0)
					{
						ColumnLayoutItem item = FindLayoutItem(layout, visibleDefinitions[i].Id);
						if (item != null)
							item.Width = Math.Max(30, Math.Min(5000, width));
					}
				}
			}

			string[] orderParts = string.IsNullOrWhiteSpace(_settings.ColumnOrder)
				? Array.Empty<string>()
				: _settings.ColumnOrder.Split(',');
			if (orderParts.Length != visibleDefinitions.Count)
				return;

			var pairs = new List<Tuple<int, ColumnLayoutItem>>();
			for (int i = 0; i < orderParts.Length; i++)
			{
				if (!int.TryParse(orderParts[i].Trim(), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out int displayIndex))
					return;

				ColumnLayoutItem item = FindLayoutItem(layout, visibleDefinitions[i].Id);
				if (item == null)
					return;
				pairs.Add(Tuple.Create(displayIndex, item));
			}

			pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));
			var ordered = new List<ColumnLayoutItem>();
			foreach (Tuple<int, ColumnLayoutItem> pair in pairs)
				ordered.Add(pair.Item2);
			foreach (ColumnLayoutItem item in layout)
			{
				if (!item.Visible)
					ordered.Add(item);
			}

			layout.Clear();
			layout.AddRange(ordered);
			for (int i = 0; i < layout.Count; i++)
				layout[i].Order = i;
		}

		private static ColumnLayoutItem FindLayoutItem(List<ColumnLayoutItem> layout, string id)
		{
			for (int i = 0; i < layout.Count; i++)
			{
				if (string.Equals(layout[i].Id, id, StringComparison.OrdinalIgnoreCase))
					return layout[i];
			}
			return null;
		}

		private void SaveCurrentColumnLayout()
		{
			if (_restoringLayout || _settings == null || _listView == null || _tabControl == null)
				return;

			string tabKey = GetCurrentTabKey();
			List<ColumnDefinition> definitions = CreateColumnDefinitions(tabKey);
			List<ColumnLayoutItem> layout = EnsureColumnLayout(tabKey, definitions);
			var byId = new Dictionary<string, ColumnLayoutItem>(StringComparer.OrdinalIgnoreCase);
			foreach (ColumnLayoutItem item in layout)
				byId[item.Id] = item;

			for (int i = 0; i < _listView.Columns.Count; i++)
			{
				ColumnHeader header = _listView.Columns[i];
				ColumnDefinition definition = header.Tag as ColumnDefinition;
				if (definition != null && byId.TryGetValue(definition.Id, out ColumnLayoutItem item))
					item.Width = Math.Max(30, Math.Min(5000, header.Width));
			}

			var visibleHeaders = new List<ColumnHeader>();
			foreach (ColumnHeader header in _listView.Columns)
				visibleHeaders.Add(header);
			visibleHeaders.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

			var orderedAll = new List<ColumnLayoutItem>(layout);
			orderedAll.Sort((a, b) => a.Order.CompareTo(b.Order));
			var visibleSlots = new List<int>();
			for (int i = 0; i < orderedAll.Count; i++)
			{
				if (orderedAll[i].Visible)
					visibleSlots.Add(i);
			}

			if (visibleSlots.Count == visibleHeaders.Count)
			{
				for (int i = 0; i < visibleHeaders.Count; i++)
				{
					ColumnDefinition definition = visibleHeaders[i].Tag as ColumnDefinition;
					if (definition != null && byId.TryGetValue(definition.Id, out ColumnLayoutItem item))
						orderedAll[visibleSlots[i]] = item;
				}
			}

			for (int i = 0; i < orderedAll.Count; i++)
				orderedAll[i].Order = i;

			_settings.ColumnLayouts[tabKey] = orderedAll;
		}

		private void SaveOverviewFilterSettings()
		{
			if (_settings == null || _chkOverviewHolding == null)
				return;

			_settings.OverviewFilterHolding = _chkOverviewHolding.Checked;
			_settings.OverviewFilterBuyCandidate = _chkOverviewBuyCandidate.Checked;
			_settings.OverviewFilterRealized = _chkOverviewRealized.Checked;
		}

		private void OverviewFilter_CheckedChanged(object sender, EventArgs e)
		{
			SaveOverviewFilterSettings();
			ScheduleLayoutSave();
			RefreshListView();
		}

		private void BuildColumnHeaderContextMenu()
		{
			_columnHeaderContextMenu = new ContextMenuStrip();
			var hide = new ToolStripMenuItem(L10n.Text("ColumnHide"), null, (s, e) => HideHeaderContextColumn());
			var choose = new ToolStripMenuItem(L10n.Text("ColumnChoose"), null, (s, e) => ShowColumnChooser());
			_columnHeaderContextMenu.Items.Add(hide);
			_columnHeaderContextMenu.Items.Add(new ToolStripSeparator());
			_columnHeaderContextMenu.Items.Add(choose);
			_columnHeaderContextMenu.Opening += (s, e) =>
			{
				hide.Enabled = _columnHeaderContextIndex >= 0 && _listView.Columns.Count > 1;
			};
		}

		private void ListView_ColumnHeaderRightClicked(object sender, ColumnHeaderRightClickEventArgs e)
		{
			if (e.ColumnIndex < 0 || e.ColumnIndex >= _listView.Columns.Count)
				return;

			_columnHeaderContextIndex = e.ColumnIndex;
			_columnHeaderContextMenu.Show(Control.MousePosition);
		}

		private void HideHeaderContextColumn()
		{
			if (_columnHeaderContextIndex < 0 || _columnHeaderContextIndex >= _listView.Columns.Count ||
				_listView.Columns.Count <= 1)
				return;

			SaveCurrentColumnLayout();
			ColumnDefinition definition = _listView.Columns[_columnHeaderContextIndex].Tag as ColumnDefinition;
			if (definition == null)
				return;

			string tabKey = GetCurrentTabKey();
			List<ColumnLayoutItem> layout = EnsureColumnLayout(tabKey, CreateColumnDefinitions(tabKey));
			ColumnLayoutItem item = FindLayoutItem(layout, definition.Id);
			if (item == null)
				return;

			item.Visible = false;
			ConfigureColumnsForSelectedTabCore();
			RefreshListView();
			_settings.Save();
			_columnHeaderContextIndex = -1;
		}

		private void ShowColumnChooser()
		{
			SaveCurrentColumnLayout();
			string tabKey = GetCurrentTabKey();
			List<ColumnDefinition> definitions = CreateColumnDefinitions(tabKey);
			List<ColumnLayoutItem> layout = EnsureColumnLayout(tabKey, definitions);
			Dictionary<string, ColumnDefinition> definitionById = BuildDefinitionMap(definitions);
			var ordered = new List<ColumnLayoutItem>(layout);
			ordered.Sort((a, b) => a.Order.CompareTo(b.Order));
			var choices = new List<ColumnChoice>();

			foreach (ColumnLayoutItem item in ordered)
			{
				if (definitionById.TryGetValue(item.Id, out ColumnDefinition definition))
					choices.Add(new ColumnChoice(item.Id, definition.ChooserName, item.Visible));
			}

			using (var dialog = new ColumnChooserDialog(choices))
			{
				if (dialog.ShowDialog(this) != DialogResult.OK)
					return;

				foreach (ColumnLayoutItem item in layout)
					item.Visible = dialog.VisibleColumnIds.Contains(item.Id);
			}

			ConfigureColumnsForSelectedTabCore();
			RefreshListView();
			_settings.Save();
		}

		private void UpdateColumnChooserButtonBounds()
		{
			if (_btnColumnChooser == null || _tabControl?.SelectedTab == null)
				return;

			int top = IsOverviewTab ? _overviewFilterPanel.Height + 1 : 1;
			int left = Math.Max(0, _tabControl.SelectedTab.ClientSize.Width - _btnColumnChooser.Width - 2);
			_btnColumnChooser.Location = new Point(left, top);
			_btnColumnChooser.BringToFront();
		}

		private void AddDynamicListItem(WatchlistEntry entry, bool realizedDetail)
		{
			if (entry == null || _listView.Columns.Count == 0)
				return;

			var cells = new List<CellValue>();
			for (int i = 0; i < _listView.Columns.Count; i++)
			{
				ColumnDefinition definition = _listView.Columns[i].Tag as ColumnDefinition;
				cells.Add(CreateCellValue(entry, definition?.Id ?? "", realizedDetail));
			}

			var item = new ListViewItem(cells[0].Text)
			{
				UseItemStyleForSubItems = false,
				Tag = entry
			};
			ApplyCellStyle(item.SubItems[0], cells[0]);

			for (int i = 1; i < cells.Count; i++)
			{
				var subItem = new ListViewItem.ListViewSubItem { Text = cells[i].Text };
				ApplyCellStyle(subItem, cells[i]);
				item.SubItems.Add(subItem);
			}

			if (entry.EntryType != WatchlistEntryType.Realized)
			{
				if (entry.UpperLimitReached)
					item.BackColor = Color.FromArgb(200, 255, 200);
				else if (entry.LowerLimitReached)
					item.BackColor = Color.FromArgb(255, 200, 200);
			}

			_listView.Items.Add(item);
		}

		private static void ApplyCellStyle(ListViewItem.ListViewSubItem subItem, CellValue cell)
		{
			if (cell.ForeColor.HasValue)
				subItem.ForeColor = cell.ForeColor.Value;
			if (cell.BackColor.HasValue)
				subItem.BackColor = cell.BackColor.Value;
			if (cell.Font != null)
				subItem.Font = cell.Font;
		}

		private CellValue CreateCellValue(WatchlistEntry entry, string columnId, bool realizedDetail)
		{
			bool realized = entry.EntryType == WatchlistEntryType.Realized;
			bool dataRetrievalTimedOut = realizedDetail
				? IsDataRetrievalTimedOut(entry)
				: !realized && IsDataRetrievalTimedOut(entry);
			var cell = new CellValue();

			switch (columnId)
			{
				case ColumnIds.Name:
					cell.Text = entry.Name;
					break;
				case ColumnIds.Isin:
					cell.Text = entry.Isin;
					break;
				case ColumnIds.Quantity:
					cell.Text = entry.Quantity > 0 ? entry.Quantity.ToString("N0") : "–";
					break;
				case ColumnIds.ReferenceDate:
					cell.Text = FormatDate(entry.ReferenceDate);
					break;
				case ColumnIds.ReferencePrice:
					cell.Text = entry.ReferencePrice > 0
						? FormatPrice(entry.ReferencePrice, entry.EffectiveReferenceCurrency)
						: "–";
					break;
				case ColumnIds.ReferenceValue:
					if (realizedDetail)
						cell.Text = TryGetReferenceValueEur(entry, out double referenceValueEur)
							? $"{referenceValueEur:N2} EUR" : "–";
					else
						cell.Text = entry.Quantity > 0 && entry.ReferencePrice > 0
							? $"{entry.Quantity * entry.ReferencePrice:N2} {entry.EffectiveReferenceCurrency}" : "–";
					break;
				case ColumnIds.ReferenceValueEur:
					cell.Text = TryGetReferenceValueEur(entry, out double refEur)
						? $"{refEur:N2} EUR" : "–";
					break;
				case ColumnIds.Trend:
					cell.Text = realized
						? "–"
						: _priceTrendIndicators.TryGetValue(entry, out string trend) ? trend : "◀▶";
					break;
				case ColumnIds.CurrentPrice:
					cell.Text = GetCurrentPriceDisplayText(entry, realizedDetail);
					if (realizedDetail)
					{
						int currentVsSale = CompareCurrentPriceToSalePrice(entry);
						if (currentVsSale > 0) cell.ForeColor = Color.DarkGreen;
						else if (currentVsSale < 0) cell.ForeColor = Color.Firebrick;
					}
					break;
				case ColumnIds.CurrentValue:
					cell.Text = GetCurrentValueDisplayText(entry, realizedDetail);
					break;
				case ColumnIds.CurrentValueEur:
					cell.Text = entry.Quantity > 0 && entry.LastPriceEur > 0
						? $"{entry.Quantity * entry.LastPriceEur:N2} EUR" : "–";
					break;
				case ColumnIds.GainLossEur:
				case ColumnIds.GainLossPct:
					GetGainLossCell(entry, columnId == ColumnIds.GainLossPct, cell);
					break;
				case ColumnIds.LimitLower:
					cell.Text = realized ? "–" : FormatListLimit(entry, false);
					if (realized || !entry.LimitLowerEnabled)
					{
						cell.ForeColor = Color.Gray;
						cell.Font = _disabledLimitFont;
					}
					break;
				case ColumnIds.LimitUpper:
					cell.Text = realized ? "–" : FormatListLimit(entry, true);
					if (realized || !entry.LimitUpperEnabled)
					{
						cell.ForeColor = Color.Gray;
						cell.Font = _disabledLimitFont;
					}
					break;
				case ColumnIds.EntryType:
					cell.Text = GetEntryTypeText(entry.EntryType);
					break;
				case ColumnIds.Note:
					cell.Text = NormalizeNote(entry.Note);
					break;
				case ColumnIds.Status:
					cell.Text = GetDisplayedStatusText(entry, realizedDetail, dataRetrievalTimedOut);
					break;
				case ColumnIds.YahooSymbol:
					cell.Text = EmptyAsDash(entry.YahooSymbol);
					break;
				case ColumnIds.QuoteCurrency:
					cell.Text = EmptyAsDash(entry.QuoteCurrency);
					break;
				case ColumnIds.LimitUpperType:
					cell.Text = FormatLimitType(entry.LimitUpperType);
					break;
				case ColumnIds.LimitUpperEnabled:
					cell.Text = FormatBool(entry.LimitUpperEnabled);
					break;
				case ColumnIds.LimitLowerType:
					cell.Text = FormatLimitType(entry.LimitLowerType);
					break;
				case ColumnIds.LimitLowerEnabled:
					cell.Text = FormatBool(entry.LimitLowerEnabled);
					break;
				case ColumnIds.ConvertToEur:
					cell.Text = FormatBool(entry.ConvertToEur);
					break;
				case ColumnIds.ReferenceCurrency:
					cell.Text = EmptyAsDash(entry.ReferenceCurrency);
					break;
				case ColumnIds.ReferenceFxRate:
					cell.Text = FormatRawDouble(entry.ReferenceFxRate);
					break;
				case ColumnIds.IncomeEur:
					cell.Text = $"{entry.IncomeEur:N2} EUR";
					break;
				case ColumnIds.SalePrice:
					cell.Text = entry.SalePrice > 0
						? FormatPrice(entry.SalePrice, entry.EffectiveSaleCurrency) : "–";
					break;
				case ColumnIds.SaleCurrency:
					cell.Text = EmptyAsDash(entry.SaleCurrency);
					break;
				case ColumnIds.SaleDate:
					cell.Text = FormatDate(entry.SaleDate);
					break;
				case ColumnIds.SaleFxRate:
					cell.Text = FormatRawDouble(entry.SaleFxRate);
					break;
				case ColumnIds.SaleValueEur:
					cell.Text = TryGetSaleValueEur(entry, out double saleValueEur)
						? $"{saleValueEur:N2} EUR" : "–";
					break;
				case ColumnIds.LastPrice:
					cell.Text = FormatRawDouble(entry.LastPrice);
					break;
				case ColumnIds.LastPriceEur:
					cell.Text = FormatRawDouble(entry.LastPriceEur);
					break;
				case ColumnIds.FxRate:
					cell.Text = FormatRawDouble(entry.FxRate);
					break;
				case ColumnIds.LastUpdate:
					cell.Text = FormatDateTime(entry.LastUpdate);
					break;
				case ColumnIds.LastSuccessfulQuoteFetch:
					cell.Text = FormatDateTime(entry.LastSuccessfulQuoteFetch);
					break;
				case ColumnIds.StatusText:
					cell.Text = EmptyAsDash(entry.StatusText);
					break;
				case ColumnIds.AlarmUpperFired:
					cell.Text = FormatBool(entry.AlarmUpperFired);
					break;
				case ColumnIds.AlarmLowerFired:
					cell.Text = FormatBool(entry.AlarmLowerFired);
					break;
				case ColumnIds.UpperLimitReached:
					cell.Text = FormatBool(entry.UpperLimitReached);
					break;
				case ColumnIds.LowerLimitReached:
					cell.Text = FormatBool(entry.LowerLimitReached);
					break;
				case ColumnIds.QuoteFetchAttemptedThisSession:
					cell.Text = FormatBool(entry.QuoteFetchAttemptedThisSession);
					break;
				case ColumnIds.DataRetrievalFailureSince:
					cell.Text = FormatDateTime(entry.DataRetrievalFailureSince);
					break;
				case ColumnIds.LookupFailCount:
					cell.Text = entry.LookupFailCount.ToString(CultureInfo.InvariantCulture);
					break;
				case ColumnIds.NextLookupAttempt:
					cell.Text = FormatDateTime(entry.NextLookupAttempt);
					break;
				case ColumnIds.ComparePrice:
					cell.Text = FormatRawDouble(entry.ComparePrice);
					break;
				case ColumnIds.AbsoluteLimitCurrency:
					cell.Text = EmptyAsDash(entry.AbsoluteLimitCurrency);
					break;
				case ColumnIds.EffectiveReferenceCurrency:
					cell.Text = EmptyAsDash(entry.EffectiveReferenceCurrency);
					break;
				case ColumnIds.EffectiveReferenceFxRate:
					cell.Text = FormatRawDouble(entry.EffectiveReferenceFxRate);
					break;
				case ColumnIds.EffectiveSaleCurrency:
					cell.Text = EmptyAsDash(entry.EffectiveSaleCurrency);
					break;
				case ColumnIds.EffectiveSaleFxRate:
					cell.Text = FormatRawDouble(entry.EffectiveSaleFxRate);
					break;
				default:
					cell.Text = "–";
					break;
			}

			if (dataRetrievalTimedOut &&
				(columnId == ColumnIds.Name || columnId == ColumnIds.Isin || columnId == ColumnIds.Status ||
				(realizedDetail && columnId == ColumnIds.CurrentPrice)))
			{
				cell.BackColor = Color.LightYellow;
			}

			return cell;
		}

		private string GetCurrentPriceDisplayText(WatchlistEntry entry, bool realizedDetail)
		{
			if (entry.EntryType == WatchlistEntryType.Realized && !realizedDetail)
			{
				return entry.SalePrice > 0
					? FormatPrice(entry.SalePrice, entry.EffectiveSaleCurrency)
					: "–";
			}

			if (entry.LastPrice <= 0.0)
				return "–";
			if (entry.ConvertToEur && entry.LastPriceEur > 0.0)
				return $"{entry.LastPriceEur:N2} EUR";
			return string.IsNullOrWhiteSpace(entry.QuoteCurrency)
				? entry.LastPrice.ToString("N2")
				: $"{entry.LastPrice:N2} {entry.QuoteCurrency}";
		}

		private string GetCurrentValueDisplayText(WatchlistEntry entry, bool realizedDetail)
		{
			if (entry.EntryType == WatchlistEntryType.Realized && !realizedDetail)
				return TryGetSaleValueEur(entry, out double saleValueEur) ? $"{saleValueEur:N2} EUR" : "–";

			if (entry.Quantity <= 0 || entry.LastPrice <= 0)
				return "–";
			if (entry.ConvertToEur && entry.LastPriceEur > 0)
				return $"{entry.Quantity * entry.LastPriceEur:N2} EUR";

			double marketValue = entry.Quantity * entry.LastPrice;
			return string.IsNullOrWhiteSpace(entry.QuoteCurrency)
				? $"{marketValue:N2}"
				: $"{marketValue:N2} {entry.QuoteCurrency}";
		}

		private void GetGainLossCell(WatchlistEntry entry, bool percent, CellValue cell)
		{
			double gainLossEur;
			double gainLossPct;
			bool available;

			if (entry.EntryType == WatchlistEntryType.Realized)
			{
				available = TryGetRealizedGainLossEur(entry, out gainLossEur, out gainLossPct);
			}
			else
			{
				double effectiveFx = entry.EffectiveReferenceFxRate;
				available = entry.Quantity > 0 && entry.ReferencePrice > 0 && entry.LastPriceEur > 0 && effectiveFx > 0;
				double referenceEur = available ? entry.Quantity * entry.ReferencePrice * effectiveFx : 0.0;
				double currentEur = available ? entry.Quantity * entry.LastPriceEur : 0.0;
				gainLossEur = available ? currentEur - referenceEur : 0.0;
				gainLossPct = available && referenceEur > 0.0 ? gainLossEur / referenceEur * 100.0 : 0.0;
			}

			if (!available)
			{
				cell.Text = "–";
				return;
			}

			double value = NormalizeTwoDecimalDisplay(percent ? gainLossPct : gainLossEur);
			cell.Text = percent
				? $"{(value > 0 ? "+" : "")}{value:N2} %"
				: $"{(value > 0 ? "+" : "")}{value:N2} EUR";
			cell.ForeColor = gainLossEur >= 0 ? Color.DarkGreen : Color.Firebrick;
		}

		private static string GetEntryTypeText(WatchlistEntryType type)
		{
			return type == WatchlistEntryType.BuyCandidate
				? L10n.Text("EntryTypeBuyCandidate")
				: type == WatchlistEntryType.Realized
					? L10n.Text("EntryTypeRealized")
					: L10n.Text("EntryTypeHolding");
		}

		private static string GetDisplayedStatusText(
			WatchlistEntry entry,
			bool realizedDetail,
			bool dataRetrievalTimedOut)
		{
			if (realizedDetail)
				return dataRetrievalTimedOut ? L10n.Text("DataRetrievalTimeoutStatus") : entry.StatusText;

			if (entry.EntryType == WatchlistEntryType.Realized)
				return entry.SaleDate != DateTime.MinValue
					? L10n.Format("RealizedStatus", entry.SaleDate)
					: L10n.Text("RealizedStatusNoDate");

			return dataRetrievalTimedOut ? L10n.Text("DataRetrievalTimeoutStatus") : entry.StatusText;
		}

		private static string FormatLimitType(LimitValueType type) =>
			type == LimitValueType.Percent ? L10n.Text("LimitTypePercent") : L10n.Text("LimitTypeAbsolute");

		private static string FormatBool(bool value) => value ? L10n.Text("Yes") : L10n.Text("No");

		private static string EmptyAsDash(string value) =>
			string.IsNullOrWhiteSpace(value) ? "–" : value;

		private static string FormatRawDouble(double value) =>
			value.ToString("0.########", CultureInfo.InvariantCulture);

		private static string FormatDate(DateTime value) =>
			value == DateTime.MinValue ? "–" : value.ToString("dd.MM.yyyy");

		private static string FormatDateTime(DateTime value) =>
			value == DateTime.MinValue ? "–" : value.ToString("dd.MM.yyyy HH:mm:ss");
	}
}
