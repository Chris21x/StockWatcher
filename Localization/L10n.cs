using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace StockWatcher.Localization
{
	internal static class L10n
	{
		private static readonly ResourceManager ResourceManager =
			new ResourceManager("StockWatcher.Localization.Strings", Assembly.GetExecutingAssembly());

		public static string Text(string key)
		{
			if (string.IsNullOrEmpty(key)) return "";
			return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
		}

		public static string Format(string key, params object[] args)
		{
			return string.Format(CultureInfo.CurrentCulture, Text(key), args ?? Array.Empty<object>());
		}
	}

	internal static class LanguageManager
	{
		public const string German = "de";
		public const string English = "en";
		public const string French = "fr";
		public const string Italian = "it";
		public const string Spanish = "es";

		public static void Initialize()
		{
			Apply(Properties.Settings.Default.UiCulture);
		}

		public static void Apply(string cultureName)
		{
			CultureInfo culture = ResolveCulture(cultureName);
			if (culture == null) return;

			CultureInfo.DefaultThreadCurrentUICulture = culture;
			Thread.CurrentThread.CurrentUICulture = culture;
		}

		private static CultureInfo ResolveCulture(string cultureName)
		{
			if (string.IsNullOrWhiteSpace(cultureName))
				return CultureInfo.CurrentUICulture;

			try
			{
				return CultureInfo.GetCultureInfo(cultureName.Trim());
			}
			catch (CultureNotFoundException)
			{
				return CultureInfo.CurrentUICulture;
			}
		}
	}
}
