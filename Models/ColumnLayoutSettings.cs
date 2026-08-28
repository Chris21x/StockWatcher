namespace StockWatcher.Models
{
	public sealed class ColumnLayoutItem
	{
		public string Id { get; set; } = "";
		public bool Visible { get; set; } = false;
		public int Width { get; set; } = 100;
		public int Order { get; set; } = 0;
	}
}
