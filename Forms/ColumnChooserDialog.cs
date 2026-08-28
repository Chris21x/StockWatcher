using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StockWatcher.Localization;

namespace StockWatcher.Forms
{
	public sealed class ColumnChoice
	{
		public string Id { get; }
		public string DisplayName { get; }
		public bool Visible { get; }

		public ColumnChoice(string id, string displayName, bool visible)
		{
			Id = id ?? "";
			DisplayName = displayName ?? id ?? "";
			Visible = visible;
		}

		public override string ToString() => DisplayName;
	}

	public sealed class ColumnChooserDialog : Form
	{
		private readonly CheckedListBox _list;
		private readonly Button _btnOk;
		private readonly Button _btnCancel;
		private readonly List<ColumnChoice> _choices;

		public HashSet<string> VisibleColumnIds { get; private set; }

		public ColumnChooserDialog(IReadOnlyList<ColumnChoice> choices)
		{
			_choices = new List<ColumnChoice>();
			if (choices != null)
			{
				for (int i = 0; i < choices.Count; i++)
					_choices.Add(choices[i]);
			}

			Text = L10n.Text("ColumnChooserTitle");
			StartPosition = FormStartPosition.CenterParent;
			Size = new Size(430, 590);
			MinimumSize = new Size(360, 420);
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;

			var label = new Label
			{
				Text = L10n.Text("ColumnChooserPrompt"),
				AutoSize = true,
				Location = new Point(12, 12)
			};

			_list = new CheckedListBox
			{
				CheckOnClick = true,
				Location = new Point(12, 34),
				Size = new Size(390, 470),
				Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
				IntegralHeight = false
			};

			for (int i = 0; i < _choices.Count; i++)
				_list.Items.Add(_choices[i], _choices[i].Visible);

			_btnOk = new Button
			{
				Text = L10n.Text("ButtonOk"),
				Size = new Size(90, 28),
				Location = new Point(216, 514),
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right
			};
			_btnOk.Click += BtnOk_Click;

			_btnCancel = new Button
			{
				Text = L10n.Text("ButtonCancel"),
				Size = new Size(90, 28),
				Location = new Point(312, 514),
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
				DialogResult = DialogResult.Cancel
			};

			Controls.Add(label);
			Controls.Add(_list);
			Controls.Add(_btnOk);
			Controls.Add(_btnCancel);
			AcceptButton = _btnOk;
			CancelButton = _btnCancel;
		}

		private void BtnOk_Click(object sender, EventArgs e)
		{
			if (_list.CheckedItems.Count == 0)
			{
				MessageBox.Show(
					L10n.Text("ColumnChooserAtLeastOne"),
					L10n.Text("ColumnChooserTitle"),
					MessageBoxButtons.OK,
					MessageBoxIcon.Information);
				return;
			}

			VisibleColumnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (object item in _list.CheckedItems)
			{
				if (item is ColumnChoice choice)
					VisibleColumnIds.Add(choice.Id);
			}

			DialogResult = DialogResult.OK;
			Close();
		}
	}
}
