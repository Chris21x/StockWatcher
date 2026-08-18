using System;

namespace StockWatcher.Models
{
	public enum WatchlistEntryType
	{
		Holding = 0,
		BuyCandidate = 1
	}

	public enum LimitValueType
	{
		Absolute = 0,
		Percent = 1
	}

	public class WatchlistEntry
	{
		// ---- Persistiert ----
		public string Isin { get; set; } = "";
		public string Name { get; set; } = "";
		public WatchlistEntryType EntryType { get; set; } = WatchlistEntryType.Holding;
		public string Note { get; set; } = "";
		public string YahooSymbol { get; set; } = "";       // z.B. "NESN.SW" – beschleunigt Abruf, manuell korrigierbar
		public string QuoteCurrency { get; set; } = "";     // Kurs-/Listingwährung, z.B. "CHF", "USD", "EUR"

		public double LimitUpper { get; set; } = 0.0;
		public LimitValueType LimitUpperType { get; set; } = LimitValueType.Absolute;
		public bool LimitUpperEnabled { get; set; } = false;
		public double LimitLower { get; set; } = 0.0;
		public LimitValueType LimitLowerType { get; set; } = LimitValueType.Absolute;
		public bool LimitLowerEnabled { get; set; } = false;

		public bool ConvertToEur { get; set; } = false;      // Kurs in EUR umrechnen? Absolute Limits dann ebenfalls in EUR.
		public double Quantity { get; set; } = 0.0;          // Stückzahl
		public double ReferencePrice { get; set; } = 0.0;    // Kauf-/Referenzkurs in ReferenceCurrency
		public string ReferenceCurrency { get; set; } = ""; // Kauf-/Referenzwährung, z.B. "EUR", "CHF"
		public DateTime ReferenceDate { get; set; } = DateTime.MinValue;
		public double ReferenceFxRate { get; set; } = 0.0;   // historischer Kurs ReferenceCurrency→EUR am Referenzdatum

		// ---- Laufzeitdaten (werden zur Diagnose mitpersistiert) ----
		public double LastPrice { get; set; } = 0.0;         // Kurs in QuoteCurrency
		public double LastPriceEur { get; set; } = 0.0;      // Kurs in EUR
		public double FxRate { get; set; } = 1.0;            // verwendeter Wechselkurs QuoteCurrency→EUR
		public DateTime LastUpdate { get; set; } = DateTime.MinValue;
		public string StatusText { get; set; } = "–";
		public bool AlarmUpperFired { get; set; } = false;
		public bool AlarmLowerFired { get; set; } = false;
		public bool UpperLimitReached { get; set; } = false;
		public bool LowerLimitReached { get; set; } = false;

		// ---- Lookup-Throttling (nicht persistiert) ----
		public int LookupFailCount { get; set; } = 0;
		public DateTime NextLookupAttempt { get; set; } = DateTime.MinValue;

		/// <summary>Vergleichspreis für absolute Limits: EUR wenn ConvertToEur, sonst Kurs in QuoteCurrency.</summary>
		public double ComparePrice => ConvertToEur ? LastPriceEur : LastPrice;

		/// <summary>Währung für absolute Limits.</summary>
		public string AbsoluteLimitCurrency => ConvertToEur ? "EUR" : (QuoteCurrency ?? "").Trim().ToUpperInvariant();

		/// <summary>
		/// Effektive Kauf-/Referenzwährung. Historisch bedeutete eine leere Kaufwährung EUR;
		/// diese Semantik bleibt für bestehende Daten erhalten.
		/// </summary>
		public string EffectiveReferenceCurrency =>
			string.IsNullOrWhiteSpace(ReferenceCurrency)
				? "EUR"
				: ReferenceCurrency.Trim().ToUpperInvariant();

		/// <summary>
		/// Effektiver FX-Faktor Kauf-/Referenzwährung→EUR für die P&amp;L-Berechnung.
		/// EUR oder leer → 1.0; andere Währung → ReferenceFxRate (0 = noch nicht ermittelt).
		/// </summary>
		public double EffectiveReferenceFxRate =>
			string.Equals(EffectiveReferenceCurrency, "EUR", StringComparison.OrdinalIgnoreCase)
				? 1.0 : ReferenceFxRate;
	}
}
