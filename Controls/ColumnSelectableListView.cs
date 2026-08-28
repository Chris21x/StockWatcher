using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StockWatcher.Controls
{
	public sealed class ColumnHeaderRightClickEventArgs : EventArgs
	{
		public int ColumnIndex { get; }

		public ColumnHeaderRightClickEventArgs(int columnIndex)
		{
			ColumnIndex = columnIndex;
		}
	}

	public sealed class ColumnSelectableListView : ListView
	{
		private const int LvmFirst = 0x1000;
		private const int LvmGetHeader = LvmFirst + 31;
		private HeaderWindow _headerWindow;

		public event EventHandler<ColumnHeaderRightClickEventArgs> ColumnHeaderRightClicked;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			AttachHeaderWindow();
		}

		protected override void OnHandleDestroyed(EventArgs e)
		{
			DetachHeaderWindow();
			base.OnHandleDestroyed(e);
		}

		private void AttachHeaderWindow()
		{
			DetachHeaderWindow();

			IntPtr headerHandle = SendMessage(Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
			if (headerHandle == IntPtr.Zero)
				return;

			_headerWindow = new HeaderWindow(this);
			_headerWindow.AssignHandle(headerHandle);
		}

		private void DetachHeaderWindow()
		{
			if (_headerWindow == null)
				return;

			try
			{
				_headerWindow.ReleaseHandle();
			}
			catch
			{
				// Beim Handle-Abbau kann der native Header bereits verschwunden sein.
			}

			_headerWindow = null;
		}

		private void RaiseColumnHeaderRightClicked(int columnIndex)
		{
			ColumnHeaderRightClicked?.Invoke(
				this,
				new ColumnHeaderRightClickEventArgs(columnIndex));
		}

		private sealed class HeaderWindow : NativeWindow
		{
			private const int WmRButtonUp = 0x0205;
			private const int HdmFirst = 0x1200;
			private const int HdmHitTest = HdmFirst + 6;
			private readonly ColumnSelectableListView _owner;

			[StructLayout(LayoutKind.Sequential)]
			private struct HdHitTestInfo
			{
				public int X;
				public int Y;
				public uint Flags;
				public int Item;
			}

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			private static extern IntPtr SendMessage(
				IntPtr hWnd,
				int msg,
				IntPtr wParam,
				ref HdHitTestInfo lParam);

			public HeaderWindow(ColumnSelectableListView owner)
			{
				_owner = owner;
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WmRButtonUp)
				{
					long packed = m.LParam.ToInt64();
					var hit = new HdHitTestInfo
					{
						X = unchecked((short)(packed & 0xFFFF)),
						Y = unchecked((short)((packed >> 16) & 0xFFFF))
					};

					int index = unchecked((int)SendMessage(
						Handle,
						HdmHitTest,
						IntPtr.Zero,
						ref hit).ToInt64());

					if (index >= 0 && !_owner.IsDisposed)
					{
						_owner.BeginInvoke(new Action(() =>
							_owner.RaiseColumnHeaderRightClicked(index)));
					}
				}

				base.WndProc(ref m);
			}
		}
	}
}
