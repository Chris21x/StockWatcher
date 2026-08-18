using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StockWatcher.Services;

namespace StockWatcher.Forms
{
	public class SymbolSelectionDialog : Form
	{
		private readonly ListView _listView;
		private readonly Button _btnAccept;
		private readonly CandidateComparer _sorter = new CandidateComparer();
		private int _sortColumn = -1;
		private SortOrder _sortOrder = SortOrder.None;

		public IsinListingCandidate SelectedCandidate { get; private set; }

		public SymbolSelectionDialog(string isin, IList<IsinListingCandidate> candidates)
		{
			Text = "Handelsplatz auswählen";
			Size = new Size(1040, 520);
			MinimumSize = new Size(760, 380);
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.Sizable;
			MaximizeBox = true;
			MinimizeBox = false;

			var font = new Font("Segoe UI", 9f);
			var lbl = new Label
			{
				Text = $"Für {isin} wurden mehrere Listings gefunden. Bitte gewünschte Kursquelle auswählen:",
				Location = new Point(12, 12),
				Size = new Size(1000, 24),
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
				Font = font
			};
			Controls.Add(lbl);

			_listView = new ListView
			{
				Location = new Point(12, 42),
				Size = new Size(1000, 390),
				Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
				View = View.Details,
				FullRowSelect = true,
				GridLines = true,
				HideSelection = false,
				MultiSelect = false,
				AllowColumnReorder = false,
				Font = font,
				ListViewItemSorter = _sorter
			};
			_listView.Columns.Add("Symbol", 105);
			_listView.Columns.Add("Land", 125);
			_listView.Columns.Add("Handelsplatz", 165);
			_listView.Columns.Add("Bezeichnung", 355);
			_listView.Columns.Add("Aktueller/letzter Preis", 150, HorizontalAlignment.Right);
			_listView.Columns.Add("Währung", 80);
			_listView.SelectedIndexChanged += (s, e) => _btnAccept.Enabled = _listView.SelectedItems.Count == 1;
			_listView.ColumnClick += ListView_ColumnClick;
			Controls.Add(_listView);

			if (candidates != null)
			{
				foreach (IsinListingCandidate candidate in candidates)
				{
					string price = candidate.PriceAvailable ? FormatPrice(candidate.LastPrice) : "–";
					var item = new ListViewItem(candidate.YahooSymbol ?? "");
					item.SubItems.Add(candidate.Country ?? "");
					item.SubItems.Add(candidate.Exchange ?? "");
					item.SubItems.Add(candidate.Name ?? "");
					item.SubItems.Add(price);
					item.SubItems.Add(candidate.PriceAvailable ? candidate.Currency ?? "" : "");
					item.Tag = candidate;
					_listView.Items.Add(item);
				}
			}

			_btnAccept = new Button
			{
				Text = "Übernehmen",
				Size = new Size(110, 30),
				Location = new Point(784, 442),
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
				Enabled = false,
				Font = font,
				DialogResult = DialogResult.OK
			};
			_btnAccept.Click += BtnAccept_Click;
			Controls.Add(_btnAccept);

			var btnCancel = new Button
			{
				Text = "Abbrechen",
				Size = new Size(110, 30),
				Location = new Point(902, 442),
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
				Font = font,
				DialogResult = DialogResult.Cancel
			};
			Controls.Add(btnCancel);

			AcceptButton = _btnAccept;
			CancelButton = btnCancel;
		}

		private void BtnAccept_Click(object sender, EventArgs e)
		{
			if (_listView.SelectedItems.Count != 1)
			{
				DialogResult = DialogResult.None;
				return;
			}

			SelectedCandidate = _listView.SelectedItems[0].Tag as IsinListingCandidate;
			if (SelectedCandidate == null)
				DialogResult = DialogResult.None;
		}

		private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (_sortColumn == e.Column)
				_sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
			else
			{
				_sortColumn = e.Column;
				_sortOrder = SortOrder.Ascending;
			}

			_sorter.Column = _sortColumn;
			_sorter.Order = _sortOrder;
			_listView.Sort();
		}

		private static string FormatPrice(double price)
		{
			if (price <= 0) return "–";
			return Math.Abs(price) < 10.0 ? price.ToString("N4") : price.ToString("N2");
		}

		private sealed class CandidateComparer : IComparer
		{
			public int Column { get; set; }
			public SortOrder Order { get; set; } = SortOrder.Ascending;

			public int Compare(object x, object y)
			{
				var leftItem = x as ListViewItem;
				var rightItem = y as ListViewItem;
				if (leftItem == null || rightItem == null) return 0;

				int result;
				if (Column == 4)
				{
					var left = leftItem.Tag as IsinListingCandidate;
					var right = rightItem.Tag as IsinListingCandidate;
					if (left == null || right == null) result = 0;
					else if (left.PriceAvailable != right.PriceAvailable)
						result = left.PriceAvailable ? -1 : 1;
					else
						result = left.LastPrice.CompareTo(right.LastPrice);
				}
				else
				{
					string left = Column < leftItem.SubItems.Count ? leftItem.SubItems[Column].Text : "";
					string right = Column < rightItem.SubItems.Count ? rightItem.SubItems[Column].Text : "";
					result = string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
				}

				return Order == SortOrder.Descending ? -result : result;
			}
		}
	}
}
