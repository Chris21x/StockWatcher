using System.Configuration;

namespace StockWatcher.Properties
{
	internal sealed class Settings : ApplicationSettingsBase
	{
		private static readonly Settings DefaultInstance =
			(Settings)Synchronized(new Settings());

		public static Settings Default => DefaultInstance;

		[UserScopedSetting]
		[DefaultSettingValue("")]
		public string UiCulture
		{
			get => (string)this[nameof(UiCulture)];
			set => this[nameof(UiCulture)] = value ?? "";
		}
	}
}
