using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace StockWatcher.Services
{
	public class QuoteResult
	{
		public bool Success { get; set; }
		public double Price { get; set; }
		public string Currency { get; set; } = "";
		public DateTime Timestamp { get; set; }
		public string ErrorMessage { get; set; } = "";
		/// <summary>Aufgelöstes Yahoo-Symbol (z.B. "NESN.SW") – zum Persistieren im Eintrag.</summary>
		public string ResolvedSymbol { get; set; } = "";
	}

	public class IsinLookupResult
	{
		public bool Found { get; set; }
		public string Name { get; set; } = "";
		public string YahooSymbol { get; set; } = "";
		public double LastPrice { get; set; }
		public string Currency { get; set; } = "";
		public string ErrorMessage { get; set; } = "";
	}

	public class IsinListingCandidate
	{
		public string YahooSymbol { get; set; } = "";
		public string Country { get; set; } = "";
		public string Exchange { get; set; } = "";
		public string Name { get; set; } = "";
		public double LastPrice { get; set; }
		public string Currency { get; set; } = "";
		public DateTime Timestamp { get; set; } = DateTime.MinValue;
		public bool PriceAvailable { get; set; }
	}

	public class IsinCandidatesResult
	{
		public List<IsinListingCandidate> Candidates { get; } = new List<IsinListingCandidate>();
		public string ErrorMessage { get; set; } = "";
	}

	/// <summary>
	/// Kursdaten via Yahoo Finance (primär) mit Stooq als Fallback.
	/// Yahoo verlangt seit 2024 Crumb-Authentifizierung und zeigt EU-Nutzern
	/// eine GDPR-Consent-Seite. Beides wird automatisch behandelt.
	/// JSON-Parsing via Newtonsoft.Json (kompatibel mit .NET Framework 4.8).
	/// </summary>
	public class StockFrankfurtClient : IDisposable
	{
		// Gemeinsamer Cookie-Speicher für alle Requests (Yahoo setzt Session-Cookies)
		private readonly CookieContainer _cookies = new CookieContainer();
		private readonly HttpClient _http;

		// In-Memory-Cache: ISIN → Yahoo-Ticker (z.B. "NESN.SW", "ADS.DE")
		private readonly Dictionary<string, string> _symbolCache =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// FX-Cache: Währungscode → (Rate nach EUR, Zeitpunkt)
		private readonly Dictionary<string, (double rate, DateTime at)> _fxCache =
			new Dictionary<string, (double, DateTime)>(StringComparer.OrdinalIgnoreCase);
		private const int FxCacheMinutes = 10;

		// Crumb-Authentifizierung
		private string _crumb = null;
		private bool   _crumbAttempted = false;
		private readonly SemaphoreSlim _crumbLock = new SemaphoreSlim(1, 1);

		public StockFrankfurtClient()
		{
			var handler = new HttpClientHandler
			{
				CookieContainer   = _cookies,
				UseCookies        = true,
				AllowAutoRedirect = true
			};
			_http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
			SetBrowserHeaders(_http);
		}

		private static void SetBrowserHeaders(HttpClient client)
		{
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
				"(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
				"application/json, text/plain, */*");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
				"en-US,en;q=0.9,de;q=0.8");
		}

		// -----------------------------------------------------------------------
		// Crumb-Authentifizierung + GDPR-Consent-Handling
		// -----------------------------------------------------------------------

		private async Task EnsureCrumbAsync()
		{
			if (_crumbAttempted) return;
			await _crumbLock.WaitAsync();
			try
			{
				if (_crumbAttempted) return;
				_crumbAttempted = true;

				// Schritt 1: finance.yahoo.com ohne Auto-Redirect aufrufen,
				// damit ein GDPR-Consent-Redirect erkannt werden kann
				try
				{
					var initHandler = new HttpClientHandler
					{
						CookieContainer   = _cookies,
						UseCookies        = true,
						AllowAutoRedirect = false
					};
					using (var initClient = new HttpClient(initHandler) { Timeout = TimeSpan.FromSeconds(15) })
					{
						SetBrowserHeaders(initClient);
						HttpResponseMessage resp = await initClient.GetAsync("https://finance.yahoo.com/");

						int status = (int)resp.StatusCode;
						if (status >= 300 && status < 400)
						{
							Uri location = resp.Headers.Location;
							if (location != null)
							{
								if (!location.IsAbsoluteUri)
									location = new Uri(new Uri("https://finance.yahoo.com/"), location);

								if (location.Host.IndexOf("consent.yahoo.com",
								        StringComparison.OrdinalIgnoreCase) >= 0)
									await HandleConsentAsync(location);
								else
									await _http.GetAsync(location);
							}
						}
					}
				}
				catch { }

				// Schritt 2: Crumb holen
				try
				{
					string crumb = await _http.GetStringAsync(
						"https://query2.finance.yahoo.com/v1/test/getcrumb");
					crumb = crumb?.Trim().Trim('"');
					if (!string.IsNullOrWhiteSpace(crumb) && crumb != "null" && crumb.Length < 50)
						_crumb = crumb;
				}
				catch { }
			}
			finally { _crumbLock.Release(); }
		}

		private async Task HandleConsentAsync(Uri consentUri)
		{
			try
			{
				string html = await _http.GetStringAsync(consentUri);

				string csrf = null;
				var m = Regex.Match(html, @"""csrfToken""\s*:\s*""([^""]+)""");
				if (m.Success) csrf = m.Groups[1].Value;
				if (csrf == null)
				{
					m = Regex.Match(html, @"name=""csrfToken""\s+value=""([^""]+)""");
					if (m.Success) csrf = m.Groups[1].Value;
				}

				string sessionId = GetQueryParam(consentUri.Query, "sessionId");
				if (string.IsNullOrEmpty(sessionId))
				{
					m = Regex.Match(html, @"""sessionId""\s*:\s*""([^""]+)""");
					if (m.Success) sessionId = m.Groups[1].Value;
				}

				if (string.IsNullOrEmpty(csrf) || string.IsNullOrEmpty(sessionId)) return;

				var formData = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string,string>("csrfToken",        csrf),
					new KeyValuePair<string,string>("sessionId",        sessionId),
					new KeyValuePair<string,string>("originalDoneUrl",  "https://finance.yahoo.com/"),
					new KeyValuePair<string,string>("namespace",        "yahoo"),
					new KeyValuePair<string,string>("agree",            "agree")
				});
				await _http.PostAsync("https://consent.yahoo.com/v2/collectConsent", formData);
			}
			catch { }
		}

		private static string GetQueryParam(string query, string key)
		{
			query = query?.TrimStart('?') ?? "";
			foreach (string part in query.Split('&'))
			{
				int eq = part.IndexOf('=');
				if (eq < 0) continue;
				if (string.Equals(part.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase))
					return Uri.UnescapeDataString(part.Substring(eq + 1));
			}
			return null;
		}

		private string CrumbParam =>
			string.IsNullOrEmpty(_crumb) ? "" : $"&crumb={Uri.EscapeDataString(_crumb)}";

		// -----------------------------------------------------------------------
		// Kurs für eine ISIN abrufen (für den periodischen Watchlist-Abruf)
		// -----------------------------------------------------------------------

		public async Task<QuoteResult> GetQuoteAsync(string isin, string knownSymbol = null)
		{
			await EnsureCrumbAsync();

			string isinKey = (isin ?? "").Trim().ToUpperInvariant();
			if (string.IsNullOrEmpty(isinKey))
				return new QuoteResult { Success = false, ErrorMessage = "ISIN fehlt" };

			// Ein vom Nutzer gewähltes/persistiertes Symbol wird nie ungefragt ersetzt,
			// solange es einen Kurs liefert.
			if (!string.IsNullOrWhiteSpace(knownSymbol))
			{
				string symbol = knownSymbol.Trim();
				lock (_symbolCache) { _symbolCache[isinKey] = symbol; }

				QuoteResult known = await FetchPriceAsync(symbol);
				known.ResolvedSymbol = symbol;
				if (known.Success) return known;

				// Nur bei genau einem exakten ISIN-Listing darf ein ungültig gewordenes
				// Symbol automatisch ersetzt werden. Bei Mehrdeutigkeit muss der Nutzer
				// im Edit-Dialog über "Prüfen" auswählen.
				IsinCandidatesResult retry = await LookupIsinCandidatesAsync(isinKey);
				if (retry.Candidates.Count == 1)
				{
					IsinListingCandidate candidate = retry.Candidates[0];
					if (!string.Equals(candidate.YahooSymbol, symbol, StringComparison.OrdinalIgnoreCase))
					{
						QuoteResult fresh = candidate.PriceAvailable
							? QuoteFromCandidate(candidate)
							: await FetchPriceAsync(candidate.YahooSymbol);
						if (fresh.Success)
						{
							lock (_symbolCache) { _symbolCache[isinKey] = candidate.YahooSymbol; }
							fresh.ResolvedSymbol = candidate.YahooSymbol;
							return fresh;
						}
					}
				}

				if (retry.Candidates.Count > 1)
					known.ErrorMessage = "Gespeichertes Symbol ohne Kurs; mehrere Listings gefunden – bitte im Eintrag 'Prüfen' und auswählen";
				return known;
			}

			// Noch kein Symbol gespeichert: nur bei exakt einem Kandidaten automatisch
			// übernehmen. Mehrdeutige Fälle werden bewusst nicht geraten.
			IsinCandidatesResult lookup = await LookupIsinCandidatesAsync(isinKey);
			if (lookup.Candidates.Count == 0)
				return new QuoteResult { Success = false, ErrorMessage = lookup.ErrorMessage };
			if (lookup.Candidates.Count > 1)
				return new QuoteResult
				{
					Success = false,
					ErrorMessage = "Mehrere Listings gefunden – bitte Eintrag bearbeiten, 'Prüfen' und Handelsplatz auswählen"
				};

			IsinListingCandidate single = lookup.Candidates[0];
			lock (_symbolCache) { _symbolCache[isinKey] = single.YahooSymbol; }

			QuoteResult result = single.PriceAvailable
				? QuoteFromCandidate(single)
				: await FetchPriceAsync(single.YahooSymbol);
			result.ResolvedSymbol = single.YahooSymbol;
			return result;
		}

		private static QuoteResult QuoteFromCandidate(IsinListingCandidate candidate)
		{
			return new QuoteResult
			{
				Success = candidate.PriceAvailable,
				Price = candidate.LastPrice,
				Currency = candidate.Currency ?? "",
				Timestamp = candidate.Timestamp,
				ResolvedSymbol = candidate.YahooSymbol ?? "",
				ErrorMessage = candidate.PriceAvailable ? "" : "Kurs nicht verfügbar"
			};
		}

		// -----------------------------------------------------------------------
		// ISIN-Lookup: Ticker + Name + Kurs (für EditEntryDialog)
		// -----------------------------------------------------------------------

		public async Task<IsinLookupResult> LookupIsinAsync(string isin)
		{
			IsinCandidatesResult lookup = await LookupIsinCandidatesAsync(isin);
			if (lookup.Candidates.Count != 1)
			{
				string error = lookup.Candidates.Count > 1
					? "Mehrere Listings gefunden – Auswahl erforderlich"
					: lookup.ErrorMessage;
				return new IsinLookupResult { Found = false, ErrorMessage = error ?? "" };
			}

			IsinListingCandidate candidate = lookup.Candidates[0];
			lock (_symbolCache) { _symbolCache[isin.Trim().ToUpperInvariant()] = candidate.YahooSymbol; }

			return new IsinLookupResult
			{
				Found        = true,
				YahooSymbol  = candidate.YahooSymbol,
				Name         = candidate.Name,
				LastPrice    = candidate.PriceAvailable ? candidate.LastPrice : 0,
				Currency     = candidate.PriceAvailable ? candidate.Currency : "",
				ErrorMessage = candidate.PriceAvailable ? "" : "Kurs nicht verfügbar"
			};
		}

		// -----------------------------------------------------------------------
		// Wechselkurs {fromCurrency} → EUR
		// -----------------------------------------------------------------------

		public async Task<double> GetFxToEurAsync(string fromCurrency)
		{
			if (string.IsNullOrWhiteSpace(fromCurrency)) return 0;
			fromCurrency = fromCurrency.Trim().ToUpperInvariant();
			if (fromCurrency == "EUR") return 1.0;

			lock (_fxCache)
			{
				if (_fxCache.TryGetValue(fromCurrency, out var cached) &&
					(DateTime.Now - cached.at).TotalMinutes < FxCacheMinutes)
					return cached.rate;
			}

			await EnsureCrumbAsync();
			QuoteResult q = await FetchPriceAsync($"{fromCurrency}EUR=X");

			if (q.Success && q.Price > 0)
			{
				lock (_fxCache) { _fxCache[fromCurrency] = (q.Price, DateTime.Now); }
				return q.Price;
			}
			return 0;
		}

		// -----------------------------------------------------------------------
		// Historischer Wechselkurs
		// -----------------------------------------------------------------------

		public async Task<double> GetHistoricalFxRateAsync(string fromCurrency, DateTime date)
		{
			if (string.IsNullOrWhiteSpace(fromCurrency)) return 0;
			fromCurrency = fromCurrency.Trim().ToUpperInvariant();
			if (fromCurrency == "EUR") return 1.0;
			if (date.Date >= DateTime.Today) return await GetFxToEurAsync(fromCurrency);

			await EnsureCrumbAsync();

			long p1 = new DateTimeOffset(date.Date,           TimeSpan.Zero).ToUnixTimeSeconds();
			long p2 = new DateTimeOffset(date.Date.AddDays(3), TimeSpan.Zero).ToUnixTimeSeconds();
			string ticker = $"{fromCurrency}EUR=X";
			string url    = $"https://query1.finance.yahoo.com/v8/finance/chart/" +
			                $"{Uri.EscapeDataString(ticker)}" +
			                $"?interval=1d&period1={p1}&period2={p2}{CrumbParam}";
			try
			{
				string json = await _http.GetStringAsync(url);
				JObject root = JObject.Parse(json);

				JArray results = root["chart"]?["result"] as JArray;
				if (results == null || results.Count == 0) return 0;

				JToken res = results[0];

				// Close-Preise aus indicators.quote[0].close
				JArray closes = res["indicators"]?["quote"]?[0]?["close"] as JArray;
				if (closes != null)
					foreach (JToken c in closes)
						if (c.Type != JTokenType.Null)
							return c.Value<double>();

				// Fallback: meta.regularMarketPrice
				double? fallback = res["meta"]?["regularMarketPrice"]?.Value<double>();
				if (fallback.HasValue && fallback.Value > 0) return fallback.Value;
			}
			catch { }
			return 0;
		}

		// -----------------------------------------------------------------------
		// Exakte ISIN-Auflösung: OpenFIGI liefert alle Listings der ISIN.
		// Nur Listings mit bekannter Yahoo-Abbildung werden als Kandidaten angeboten.
		// -----------------------------------------------------------------------

		public async Task<IsinCandidatesResult> LookupIsinCandidatesAsync(string isin)
		{
			var result = new IsinCandidatesResult();
			if (string.IsNullOrWhiteSpace(isin))
			{
				result.ErrorMessage = "ISIN fehlt";
				return result;
			}

			isin = isin.Trim().ToUpperInvariant();
			await EnsureCrumbAsync();

			List<IsinListingCandidate> candidates = await FigiCandidatesAsync(isin);
			if (candidates.Count == 0)
			{
				result.ErrorMessage = "Keine auf Yahoo Finance abbildbaren Listings für diese ISIN gefunden";
				return result;
			}

			// Kurse parallel, aber begrenzt abrufen. So bleiben auch ISINs mit vielen
			// Handelsplätzen bedienbar, ohne Yahoo mit unbeschränkt vielen Requests zu fluten.
			using (var gate = new SemaphoreSlim(4, 4))
			{
				var tasks = new List<Task>();
				foreach (IsinListingCandidate candidate in candidates)
				{
					tasks.Add(FillCandidateQuoteAsync(candidate, gate));
				}
				await Task.WhenAll(tasks);
			}

			candidates.Sort((a, b) =>
			{
				int c = string.Compare(a.Country, b.Country, StringComparison.CurrentCultureIgnoreCase);
				if (c != 0) return c;
				c = string.Compare(a.Exchange, b.Exchange, StringComparison.CurrentCultureIgnoreCase);
				if (c != 0) return c;
				return string.Compare(a.YahooSymbol, b.YahooSymbol, StringComparison.OrdinalIgnoreCase);
			});

			result.Candidates.AddRange(candidates);
			return result;
		}

		private async Task FillCandidateQuoteAsync(IsinListingCandidate candidate, SemaphoreSlim gate)
		{
			await gate.WaitAsync();
			try
			{
				QuoteResult q = await FetchPriceAsync(candidate.YahooSymbol);
				if (!q.Success) return;

				candidate.PriceAvailable = true;
				candidate.LastPrice = q.Price;
				candidate.Currency = q.Currency;
				candidate.Timestamp = q.Timestamp;
			}
			catch { }
			finally
			{
				gate.Release();
			}
		}

		private async Task<List<IsinListingCandidate>> FigiCandidatesAsync(string isin)
		{
			var candidates = new Dictionary<string, IsinListingCandidate>(StringComparer.OrdinalIgnoreCase);
			try
			{
				string body = $"[{{\"idType\":\"ID_ISIN\",\"idValue\":\"{isin}\"}}]";
				var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

				HttpResponseMessage resp = await _http.PostAsync("https://api.openfigi.com/v3/mapping", content);
				if (!resp.IsSuccessStatusCode) return new List<IsinListingCandidate>();

				string json = await resp.Content.ReadAsStringAsync();
				JArray root = JArray.Parse(json);
				if (root.Count == 0) return new List<IsinListingCandidate>();

				JArray data = root[0]["data"] as JArray;
				if (data == null) return new List<IsinListingCandidate>();

				foreach (JToken item in data)
				{
					string ticker = ((string)item["ticker"] ?? "").Trim();
					string exchCode = ((string)item["exchCode"] ?? "").Trim().ToUpperInvariant();
					if (string.IsNullOrEmpty(ticker) || string.IsNullOrEmpty(exchCode)) continue;

					ExchangeInfo info = GetExchangeInfo(exchCode);
					if (info == null) continue;

					string symbol = BuildYahooSymbol(ticker, info.YahooSuffix);
					if (string.IsNullOrEmpty(symbol)) continue;

					if (!candidates.ContainsKey(symbol))
					{
						candidates[symbol] = new IsinListingCandidate
						{
							YahooSymbol = symbol,
							Country = info.Country,
							Exchange = info.Exchange,
							Name = (string)item["name"] ?? ""
						};
					}
				}
			}
			catch { }

			return new List<IsinListingCandidate>(candidates.Values);
		}

		private static string BuildYahooSymbol(string ticker, string suffix)
		{
			if (string.IsNullOrWhiteSpace(ticker) || suffix == null) return "";
			ticker = ticker.Trim();
			if (suffix.Length == 0 || ticker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				return ticker;
			return ticker + suffix;
		}

		private sealed class ExchangeInfo
		{
			public string YahooSuffix { get; }
			public string Country { get; }
			public string Exchange { get; }

			public ExchangeInfo(string yahooSuffix, string country, string exchange)
			{
				YahooSuffix = yahooSuffix;
				Country = country;
				Exchange = exchange;
			}
		}

		private static ExchangeInfo GetExchangeInfo(string exchCode)
		{
			switch (exchCode)
			{
				// Deutschland
				case "GY": case "GT": case "GQ": return new ExchangeInfo(".DE", "Deutschland", "Xetra");
				case "GF": return new ExchangeInfo(".F",  "Deutschland", "Frankfurt");
				case "GM": return new ExchangeInfo(".MU", "Deutschland", "München");
				case "GS": return new ExchangeInfo(".SG", "Deutschland", "Stuttgart");
				case "GB": return new ExchangeInfo(".BE", "Deutschland", "Berlin");
				case "GH": return new ExchangeInfo(".HM", "Deutschland", "Hamburg");
				case "GI": return new ExchangeInfo(".HA", "Deutschland", "Hannover");
				case "GD": return new ExchangeInfo(".DU", "Deutschland", "Düsseldorf");

				// Europa
				case "SE": case "SW": return new ExchangeInfo(".SW", "Schweiz", "SIX Swiss Exchange");
				case "FP": return new ExchangeInfo(".PA", "Frankreich", "Euronext Paris");
				case "NA": return new ExchangeInfo(".AS", "Niederlande", "Euronext Amsterdam");
				case "LN": case "LC": case "LT": return new ExchangeInfo(".L", "Vereinigtes Königreich", "London Stock Exchange");
				case "IM": return new ExchangeInfo(".MI", "Italien", "Borsa Italiana");
				case "SM": return new ExchangeInfo(".MC", "Spanien", "Bolsa de Madrid");
				case "DC": return new ExchangeInfo(".CO", "Dänemark", "Nasdaq Copenhagen");
				case "SS": return new ExchangeInfo(".ST", "Schweden", "Nasdaq Stockholm");
				case "NO": return new ExchangeInfo(".OL", "Norwegen", "Oslo Børs");
				case "FH": return new ExchangeInfo(".HE", "Finnland", "Nasdaq Helsinki");
				case "BB": return new ExchangeInfo(".BR", "Belgien", "Euronext Brussels");
				case "AV": return new ExchangeInfo(".VI", "Österreich", "Wiener Börse");
				case "PL": return new ExchangeInfo(".LS", "Portugal", "Euronext Lisbon");
				case "ID": return new ExchangeInfo(".IR", "Irland", "Euronext Dublin");
				case "LX": return new ExchangeInfo(".LU", "Luxemburg", "Luxembourg Stock Exchange");

				// Asien/Pazifik
				case "HK": return new ExchangeInfo(".HK", "Hongkong", "Hong Kong Stock Exchange");
				case "JP": case "JT": return new ExchangeInfo(".T", "Japan", "Tokyo Stock Exchange");
				case "AN": case "AT": return new ExchangeInfo(".AX", "Australien", "Australian Securities Exchange");
				case "SP": return new ExchangeInfo(".SI", "Singapur", "Singapore Exchange");
				case "NZ": return new ExchangeInfo(".NZ", "Neuseeland", "New Zealand Exchange");

				// Kanada
				case "CT": case "CN": return new ExchangeInfo(".TO", "Kanada", "Toronto Stock Exchange");
				case "CV": return new ExchangeInfo(".V", "Kanada", "TSX Venture Exchange");

				// USA – Yahoo verwendet für die regulären US-Börsen kein Suffix.
				case "US": case "UN": case "UA": case "UQ": case "UW": case "UR":
				case "UP": case "UB": case "UC": case "UF": case "UM": case "VF":
				case "VG": case "VJ": case "VK": case "VY":
					return new ExchangeInfo("", "USA", "US-Börse");

				default:
					return null;
			}
		}

		// -----------------------------------------------------------------------
		// Kursabruf: Yahoo primär, Stooq als Fallback
		// -----------------------------------------------------------------------

		private async Task<QuoteResult> FetchPriceAsync(string symbol)
		{
			QuoteResult yahoo = NormalizeQuoteUnits(await FetchPriceYahooAsync(symbol));
			if (yahoo.Success) return yahoo;

			// Stooq-Fallback (nicht für FX-Ticker wie CHFEUR=X)
			if (!symbol.Contains("="))
			{
				QuoteResult stooq = NormalizeQuoteUnits(await FetchPriceStooqAsync(symbol));
				if (stooq.Success) return stooq;
			}

			return yahoo;
		}

		/// <summary>
		/// Yahoo kennzeichnet Londoner Pence-Notierungen als "GBp"; GBX ist die
		/// alternative Bezeichnung für Pence Sterling. Intern wird immer auf GBP
		/// normalisiert, bevor FX, P&amp;L oder Limits verarbeitet werden.
		/// Wichtig: "GBp" ist absichtlich case-sensitiv, damit echtes "GBP"
		/// niemals versehentlich durch 100 geteilt wird.
		/// </summary>
		private static QuoteResult NormalizeQuoteUnits(QuoteResult quote)
		{
			if (quote == null || !quote.Success) return quote;

			bool isPence = string.Equals(quote.Currency, "GBp", StringComparison.Ordinal) ||
			               string.Equals(quote.Currency, "GBX", StringComparison.OrdinalIgnoreCase);
			if (isPence)
			{
				quote.Price /= 100.0;
				quote.Currency = "GBP";
			}

			return quote;
		}

		private async Task<QuoteResult> FetchPriceYahooAsync(string symbol)
		{
			try
			{
				string url = $"https://query1.finance.yahoo.com/v8/finance/chart/" +
				             $"{Uri.EscapeDataString(symbol)}" +
				             $"?interval=1d&range=1d{CrumbParam}";

				string json = await _http.GetStringAsync(url);
				JObject root = JObject.Parse(json);

				JArray results = root["chart"]?["result"] as JArray;
				if (results == null || results.Count == 0)
					return new QuoteResult { Success = false, ErrorMessage = "Yahoo: Keine Daten" };

				JToken meta  = results[0]["meta"];
				double price = meta?["regularMarketPrice"]?.Value<double>() ?? 0;
				string ccy   = (string)meta?["currency"] ?? "";
				DateTime ts  = DateTime.Now;
				long? tsRaw  = meta?["regularMarketTime"]?.Value<long?>();
				if (tsRaw.HasValue) ts = DateTimeOffset.FromUnixTimeSeconds(tsRaw.Value).LocalDateTime;

				if (price <= 0)
					return new QuoteResult { Success = false, ErrorMessage = "Yahoo: Kein Kurs verfügbar" };

				return new QuoteResult { Success = true, Price = price, Currency = ccy, Timestamp = ts };
			}
			catch (Exception ex)
			{
				return new QuoteResult { Success = false, ErrorMessage = $"Yahoo: {ex.Message}" };
			}
		}

		/// <summary>
		/// Stooq-Fallback: liefert Close-Preis via CSV.
		/// URL: https://stooq.com/q/l/?s={symbol_lower}&amp;f=sd2t2ohlcv&amp;h&amp;e=csv
		/// </summary>
		private async Task<QuoteResult> FetchPriceStooqAsync(string symbol)
		{
			try
			{
				string url = $"https://stooq.com/q/l/?s={Uri.EscapeDataString(symbol.ToLowerInvariant())}" +
				             $"&f=sd2t2ohlcv&h&e=csv";

				string csv = await _http.GetStringAsync(url);
				string[] lines = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

				if (lines.Length < 2)
					return new QuoteResult { Success = false, ErrorMessage = "Stooq: Keine Daten" };

				string[] parts = lines[1].Split(',');
				// Format: Symbol,Date,Time,Open,High,Low,Close,Volume
				if (parts.Length < 7)
					return new QuoteResult { Success = false, ErrorMessage = "Stooq: Ungültiges Format" };

				string closeRaw = parts[6].Trim();
				if (!double.TryParse(closeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out double close)
				    || close <= 0)
					return new QuoteResult { Success = false, ErrorMessage = "Stooq: Kein gültiger Kurs (N/D?)" };

				string ccy = CurrencyFromSuffix(symbol);
				DateTime ts = DateTime.Now;
				if (parts.Length >= 8 &&
				    DateTime.TryParseExact($"{parts[1].Trim()} {parts[2].Trim()}",
				        "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
				        DateTimeStyles.None, out DateTime stooqTs))
					ts = stooqTs;

				return new QuoteResult { Success = true, Price = close, Currency = ccy, Timestamp = ts };
			}
			catch (Exception ex)
			{
				return new QuoteResult { Success = false, ErrorMessage = $"Stooq: {ex.Message}" };
			}
		}

		private static string CurrencyFromSuffix(string symbol)
		{
			int dot = symbol.LastIndexOf('.');
			if (dot < 0) return "USD";
			switch (symbol.Substring(dot + 1).ToUpperInvariant())
			{
				case "DE": case "F": case "MU": case "SG": case "BE": case "HM": case "HA": case "DU":
				case "PA": case "AS": case "MI": case "MC": case "BR": case "LS": case "VI": case "HE":
				case "IR": case "LU": return "EUR";
				case "SW": return "CHF";
				case "L":  return "GBp";   // Londoner Preise in Pence; wird zentral auf GBP normalisiert
				case "CO": return "DKK";
				case "ST": return "SEK";
				case "OL": return "NOK";
				case "HK": return "HKD";
				case "T":  return "JPY";
				case "TO": case "V": case "NE": return "CAD";
				case "AX": return "AUD";
				case "SI": return "SGD";
				case "NZ": return "NZD";
				default:   return "USD";
			}
		}

		// -----------------------------------------------------------------------
		// Hilfsmethoden
		// -----------------------------------------------------------------------

		private static bool IsKnownType(JToken q)
		{
			string qt = (string)q["quoteType"] ?? "";
			return string.Equals(qt, "EQUITY",    StringComparison.OrdinalIgnoreCase)
			    || string.Equals(qt, "ETF",        StringComparison.OrdinalIgnoreCase)
			    || string.Equals(qt, "MUTUALFUND", StringComparison.OrdinalIgnoreCase);
		}

		private static string GetName(JToken q)
		{
			string n = (string)q["longname"];
			return !string.IsNullOrWhiteSpace(n) ? n : (string)q["shortname"] ?? "";
		}

		public void Dispose()
		{
			_http?.Dispose();
			_crumbLock?.Dispose();
		}
	}
}
