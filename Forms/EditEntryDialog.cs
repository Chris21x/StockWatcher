using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using StockWatcher.Models;
using StockWatcher.Services;

namespace StockWatcher.Forms
{
	public class EditEntryDialog : Form
	{
		private readonly StockFrankfurtClient _client;

		private TextBox       _txtIsin;
		private TextBox       _txtName;
		private ComboBox      _cmbEntryType;
		private TextBox       _txtNote;
		private ComboBox      _cmbQuoteCurrency;
		private NumericUpDown _nudUpper;
		private ComboBox      _cmbUpperUnit;
		private CheckBox      _chkUpperEnabled;
		private NumericUpDown _nudLower;
		private ComboBox      _cmbLowerUnit;
		private CheckBox      _chkLowerEnabled;
		private CheckBox      _chkConvertToEur;
		private TextBox       _txtQuantity;
		private TextBox       _txtReferencePrice;
		private ComboBox      _cmbReferenceCurrency;
		private TextBox       _txtReferenceDate;
		private Label         _lblFxRate;
		private Label         _lblStatus;
		private Button        _btnCheck;
		private Button        _btnFetch;
		private Button        _btnOk;
		private Button        _btnCancel;

		private string _resolvedYahooSymbol = "";
		private string _resolvedIsin = "";
		private double _fetchedReferenceFxRate = 0.0;
		private bool   _lookupRunning = false;
		private bool   _fxLookupRunning = false;
		private bool   _isinDirty = false;

		public WatchlistEntry Result { get; private set; }

		public EditEntryDialog(StockFrankfurtClient client, WatchlistEntry existing = null)
		{
			_client = client;
			Text = existing == null ? "Wertpapier hinzufügen" : "Eintrag bearbeiten";
			Size = new Size(610, 690);
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;

			BuildUi();

			if (existing != null)
			{
				_txtIsin.Text                = existing.Isin;
				_txtName.Text                = existing.Name;
				_cmbEntryType.SelectedIndex  = existing.EntryType == WatchlistEntryType.BuyCandidate ? 1 : 0;
				_txtNote.Text                = existing.Note ?? "";
				_resolvedYahooSymbol         = existing.YahooSymbol ?? "";
				_resolvedIsin                = existing.Isin ?? "";
				_cmbQuoteCurrency.Text       = existing.QuoteCurrency ?? "";
				_nudUpper.Value              = ClampToNud(existing.LimitUpper);
				_cmbUpperUnit.SelectedIndex  = existing.LimitUpperType == LimitValueType.Percent ? 1 : 0;
				_chkUpperEnabled.Checked     = existing.LimitUpperEnabled;
				_nudLower.Value              = ClampToNud(existing.LimitLower);
				_cmbLowerUnit.SelectedIndex  = existing.LimitLowerType == LimitValueType.Percent ? 1 : 0;
				_chkLowerEnabled.Checked     = existing.LimitLowerEnabled;
				_chkConvertToEur.Checked     = existing.ConvertToEur;

				if (existing.Quantity > 0)
					_txtQuantity.Text = existing.Quantity.ToString("G", CultureInfo.InvariantCulture);
				if (existing.ReferencePrice > 0)
					_txtReferencePrice.Text = existing.ReferencePrice.ToString("N4", CultureInfo.InvariantCulture);

				_cmbReferenceCurrency.Text = existing.ReferenceCurrency ?? "";

				if (existing.ReferenceDate != DateTime.MinValue)
					_txtReferenceDate.Text = existing.ReferenceDate.ToString("dd.MM.yyyy");

				_fetchedReferenceFxRate = existing.ReferenceFxRate;
				UpdateFxLabel(existing.EffectiveReferenceCurrency, existing.ReferenceFxRate,
					existing.ReferenceDate != DateTime.MinValue ? existing.ReferenceDate : (DateTime?)null);

				_isinDirty = false;
			}

			RefreshLimitUnitDisplay();
		}

		private void BuildUi()
		{
			const int LabelW    = 145;
			const int FieldLeft = 158;
			const int FieldW    = 220;
			int       y         = 18;
			const int RowH      = 36;
			var font   = new Font("Segoe UI", 9f);
			var fontSm = new Font("Segoe UI", 8f);

			// ---- ISIN ----
			Controls.Add(MakeLbl("ISIN:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtIsin = new TextBox { Location = new Point(FieldLeft, y), Width = FieldW,
				Font = font, CharacterCasing = CharacterCasing.Upper, MaxLength = 12 };
			_txtIsin.Leave       += TxtIsin_Leave;
			_txtIsin.TextChanged += (s, e) => _isinDirty = true;
			Controls.Add(_txtIsin);
			_btnCheck = new Button { Text = "Prüfen",
				Location = new Point(FieldLeft + FieldW + 8, y - 1), Size = new Size(80, 26), Font = font };
			_btnCheck.Click += async (s, e) => await RunLookupAsync();
			Controls.Add(_btnCheck);
			y += RowH;

			// ---- Name ----
			Controls.Add(MakeLbl("Name:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtName = new TextBox { Location = new Point(FieldLeft, y), Width = FieldW + 88, Font = font };
			Controls.Add(_txtName);
			y += RowH;

			// ---- Typ ----
			Controls.Add(MakeLbl("Typ:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_cmbEntryType = new ComboBox
			{
				Location      = new Point(FieldLeft, y),
				Width         = 160,
				Font          = font,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			_cmbEntryType.Items.AddRange(new object[] { "Bestand", "Kaufkandidat" });
			_cmbEntryType.SelectedIndex = 0;
			Controls.Add(_cmbEntryType);
			y += RowH;

			// ---- Kurs-/Listingwährung ----
			Controls.Add(MakeLbl("Kurs-/Listingwährung:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_cmbQuoteCurrency = MakeCurrencyCombo(FieldLeft, y, font);
			_cmbQuoteCurrency.TextChanged += (s, e) => RefreshLimitUnitDisplay();
			Controls.Add(_cmbQuoteCurrency);
			Controls.Add(MakeLbl("(wird bei Kursabruf automatisch aktualisiert)", FieldLeft + 86, y + 4, 290, fontSm, Color.Gray));
			y += RowH;

			// ---- Unteres Limit ----
			Controls.Add(MakeLbl("Unteres Limit:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_nudLower = MakeNud(FieldLeft, y, font);
			Controls.Add(_nudLower);
			_cmbLowerUnit = MakeLimitUnitCombo(FieldLeft + 128, y, font);
			Controls.Add(_cmbLowerUnit);
			_chkLowerEnabled = new CheckBox
			{
				Text = "Alarm aktiv",
				Location = new Point(FieldLeft + 220, y + 2),
				Width = 105,
				Font = font,
				Checked = false
			};
			Controls.Add(_chkLowerEnabled);
			y += RowH;

			// ---- Oberes Limit ----
			Controls.Add(MakeLbl("Oberes Limit:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_nudUpper = MakeNud(FieldLeft, y, font);
			Controls.Add(_nudUpper);
			_cmbUpperUnit = MakeLimitUnitCombo(FieldLeft + 128, y, font);
			Controls.Add(_cmbUpperUnit);
			_chkUpperEnabled = new CheckBox
			{
				Text = "Alarm aktiv",
				Location = new Point(FieldLeft + 220, y + 2),
				Width = 105,
				Font = font,
				Checked = false
			};
			Controls.Add(_chkUpperEnabled);
			y += RowH;

			// ---- In EUR umrechnen ----
			_chkConvertToEur = new CheckBox
			{
				Text     = "Kurs in EUR umrechnen  (absolute Limits ebenfalls in EUR)",
				Location = new Point(FieldLeft - LabelW - 4, y + 2),
				Width    = FieldW + LabelW + 140,
				Font     = font
			};
			_chkConvertToEur.CheckedChanged += (s, e) => RefreshLimitUnitDisplay();
			Controls.Add(_chkConvertToEur);
			y += RowH;

			// ---- Stückzahl ----
			Controls.Add(MakeLbl("Stückzahl:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtQuantity = new TextBox { Location = new Point(FieldLeft, y), Width = 120, Font = font };
			_txtQuantity.Leave += TxtNumeric_Leave;
			Controls.Add(_txtQuantity);
			Controls.Add(MakeLbl("(leer = keine Angabe)", FieldLeft + 128, y + 4, 160, fontSm, Color.Gray));
			y += RowH;

			// ---- Kauf-/Referenzkurs + Währung ----
			Controls.Add(MakeLbl("Kauf-/Referenzkurs:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtReferencePrice = new TextBox { Location = new Point(FieldLeft, y), Width = 100, Font = font };
			_txtReferencePrice.Leave += TxtNumeric_Leave;
			_txtReferencePrice.TextChanged += (s, e) => RefreshLimitUnitDisplay();
			Controls.Add(_txtReferencePrice);

			Controls.Add(MakeLbl("Währung:", FieldLeft + 108, y + 3, 60, font));
			_cmbReferenceCurrency = MakeCurrencyCombo(FieldLeft + 168, y, font);
			_cmbReferenceCurrency.Leave += CmbOrDate_Leave;
			_cmbReferenceCurrency.SelectedIndexChanged += async (s, e) => await TriggerFxLookupAsync();
			Controls.Add(_cmbReferenceCurrency);
			y += RowH;

			// ---- Kauf-/Referenzdatum + FX-Rate ----
			Controls.Add(MakeLbl("Kauf-/Referenzdatum:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtReferenceDate = new TextBox { Location = new Point(FieldLeft, y), Width = 100, Font = font };
			_txtReferenceDate.Leave += CmbOrDate_Leave;
			Controls.Add(_txtReferenceDate);
			Controls.Add(MakeLbl("(dd.MM.yyyy, optional)", FieldLeft + 108, y + 4, 130, fontSm, Color.Gray));

			_lblFxRate = new Label
			{
				Location  = new Point(FieldLeft + 242, y + 3),
				Width     = 170,
				Font      = fontSm,
				ForeColor = Color.Gray,
				Text      = ""
			};
			Controls.Add(_lblFxRate);
			y += RowH;

			// ---- Bemerkung ----
			Controls.Add(MakeLbl("Bemerkung:", FieldLeft - LabelW - 4, y + 3, LabelW, font));
			_txtNote = new TextBox
			{
				Location      = new Point(FieldLeft, y),
				Size          = new Size(FieldW + 160, 88),
				Font          = font,
				Multiline     = true,
				AcceptsReturn = true,
				ScrollBars    = ScrollBars.Vertical
			};
			Controls.Add(_txtNote);
			y += 96;

			// ---- Status ----
			_lblStatus = new Label
			{
				Location  = new Point(9, y),
				Size      = new Size(576, 20),
				Font      = fontSm,
				ForeColor = Color.Gray,
				Text      = "ISIN eingeben, dann Tab oder 'Prüfen' klicken  (Identität: OpenFIGI, Kurs: Yahoo/Stooq)"
			};
			Controls.Add(_lblStatus);
			y += 30;

			// ---- Buttons ----
			_btnFetch = new Button { Text = "Abrufen (F5)", Location = new Point(9, y),
				Size = new Size(120, 30), Font = font };
			_btnFetch.Click += async (s, e) => await RunFetchAsync();
			Controls.Add(_btnFetch);

			_btnOk = new Button { Text = "OK", Location = new Point(400, y),
				Size = new Size(80, 30), Font = font, DialogResult = DialogResult.OK };
			_btnOk.Click += BtnOk_Click;
			_btnCancel = new Button { Text = "Abbrechen", Location = new Point(488, y),
				Size = new Size(96, 30), Font = font, DialogResult = DialogResult.Cancel };
			Controls.Add(_btnOk);
			Controls.Add(_btnCancel);
			AcceptButton = _btnOk;
			CancelButton = _btnCancel;
		}

		// -----------------------------------------------------------------------
		// ISIN-Lookup
		// -----------------------------------------------------------------------

		private async void TxtIsin_Leave(object sender, EventArgs e)
		{
			if (_isinDirty && IsValidFormat(_txtIsin.Text.Trim()))
				await RunLookupAsync();
		}

		private async Task RunLookupAsync()
		{
			string isin = _txtIsin.Text.Trim().ToUpperInvariant();
			if (!IsValidFormat(isin))
			{
				SetStatus("✗  Ungültiges Format  (2 Buchstaben + 10 Zeichen, z.B. DE000A1EWWW0)", Color.Red);
				return;
			}
			if (_lookupRunning) return;

			_lookupRunning = true;
			SetLookupButtons(false);
			SetStatus("Listings und Kurse werden ermittelt…", Color.Gray);

			IsinListingCandidate candidate = await LookupAndSelectCandidateAsync(isin);

			SetLookupButtons(true);
			_lookupRunning = false;

			if (candidate == null) return;
			ApplyCandidate(isin, candidate);
			ShowCandidateStatus(candidate);
		}

		private async Task RunFetchAsync()
		{
			string isin = _txtIsin.Text.Trim().ToUpperInvariant();
			if (!IsValidFormat(isin))
			{
				SetStatus("✗  Ungültige ISIN", Color.Red);
				return;
			}
			if (_lookupRunning || _fxLookupRunning) return;

			_lookupRunning = true;
			SetLookupButtons(false);
			SetStatus("Abruf läuft…", Color.Gray);

			QuoteResult r = null;
			if (HasResolvedSymbolFor(isin))
			{
				r = await _client.GetQuoteAsync(isin, _resolvedYahooSymbol);
				if (r.Success && !string.IsNullOrEmpty(r.ResolvedSymbol))
				{
					_resolvedYahooSymbol = r.ResolvedSymbol;
					_resolvedIsin = isin;
				}
			}
			else
			{
				IsinListingCandidate candidate = await LookupAndSelectCandidateAsync(isin);
				if (candidate != null)
				{
					ApplyCandidate(isin, candidate);
					if (candidate.PriceAvailable)
					{
						r = new QuoteResult
						{
							Success = true,
							Price = candidate.LastPrice,
							Currency = candidate.Currency,
							Timestamp = candidate.Timestamp,
							ResolvedSymbol = candidate.YahooSymbol
						};
					}
					else
					{
						r = await _client.GetQuoteAsync(isin, candidate.YahooSymbol);
					}
				}
			}

			SetLookupButtons(true);
			_lookupRunning = false;

			if (r == null) return;
			if (r.Success)
			{
				ApplyQuoteCurrencyFromQuery(r.Currency);
				SetStatus($"✓  {_resolvedYahooSymbol}  –  Kurs: {r.Price:N2} {r.Currency}  ({r.Timestamp:HH:mm})",
					Color.DarkGreen);
			}
			else
			{
				SetStatus($"✗  Kurs nicht verfügbar  –  {r.ErrorMessage}", Color.OrangeRed);
			}
		}

		private async Task<IsinListingCandidate> LookupAndSelectCandidateAsync(string isin)
		{
			IsinCandidatesResult lookup = await _client.LookupIsinCandidatesAsync(isin);
			if (lookup.Candidates.Count == 0)
			{
				SetStatus($"✗  {lookup.ErrorMessage} – Eintrag kann trotzdem gespeichert werden.", Color.OrangeRed);
				return null;
			}

			if (lookup.Candidates.Count == 1)
				return lookup.Candidates[0];

			using (var dlg = new SymbolSelectionDialog(isin, lookup.Candidates))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedCandidate != null)
					return dlg.SelectedCandidate;
			}

			SetStatus("Auswahl abgebrochen – bisherige Symbolzuordnung bleibt unverändert.", Color.DarkGoldenrod);
			return null;
		}

		private void ApplyCandidate(string isin, IsinListingCandidate candidate)
		{
			_resolvedYahooSymbol = candidate.YahooSymbol ?? "";
			_resolvedIsin = isin;
			_isinDirty = false;

			if (!string.IsNullOrWhiteSpace(candidate.Name) && string.IsNullOrWhiteSpace(_txtName.Text))
				_txtName.Text = candidate.Name;

			ApplyQuoteCurrencyFromQuery(candidate.Currency);
		}

		private void ApplyQuoteCurrencyFromQuery(string currency)
		{
			if (string.IsNullOrWhiteSpace(currency)) return;

			string ccy = currency.Trim().ToUpperInvariant();
			_cmbQuoteCurrency.Text = ccy;

			// Bei einem neuen Referenzwert darf die Listingwährung als sinnvolle Vorbelegung dienen.
			// Bereits vorhandene/manuell gepflegte Referenzdaten werden nie überschrieben.
			if (string.IsNullOrWhiteSpace(_txtReferencePrice.Text) &&
				string.IsNullOrWhiteSpace(_cmbReferenceCurrency.Text))
				_cmbReferenceCurrency.Text = ccy;

			RefreshLimitUnitDisplay();
		}

		private void ShowCandidateStatus(IsinListingCandidate candidate)
		{
			if (candidate.PriceAvailable)
				SetStatus($"✓  {candidate.YahooSymbol}  –  {candidate.Exchange}, {candidate.Country}  –  Kurs: {candidate.LastPrice:N2} {candidate.Currency}",
					Color.DarkGreen);
			else
				SetStatus($"✓  Symbol gefunden: {candidate.YahooSymbol}  –  Kurs nicht verfügbar", Color.DarkGoldenrod);
		}

		private bool HasResolvedSymbolFor(string isin)
		{
			return !string.IsNullOrWhiteSpace(_resolvedYahooSymbol) &&
			       string.Equals(_resolvedIsin, isin, StringComparison.OrdinalIgnoreCase);
		}

		private void SetLookupButtons(bool enabled)
		{
			_btnCheck.Enabled = enabled;
			_btnFetch.Enabled = enabled;
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F5)
			{
				_ = RunFetchAsync();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void SetStatus(string text, Color color)
		{
			_lblStatus.Text      = text;
			_lblStatus.ForeColor = color;
		}

		// -----------------------------------------------------------------------
		// Historischer FX-Abruf (ausgelöst bei Änderung Referenzdatum/-währung)
		// -----------------------------------------------------------------------

		private async void CmbOrDate_Leave(object sender, EventArgs e) =>
			await TriggerFxLookupAsync();

		private async Task TriggerFxLookupAsync()
		{
			string ccy = _cmbReferenceCurrency.Text.Trim().ToUpperInvariant();

			if (string.IsNullOrEmpty(ccy) || ccy == "EUR")
			{
				_fetchedReferenceFxRate = 1.0;
				UpdateFxLabel(ccy, 1.0, null);
				return;
			}

			if (!TryParseDate(_txtReferenceDate.Text, out DateTime date))
			{
				_fetchedReferenceFxRate = 0.0;
				_lblFxRate.Text = "";
				return;
			}

			if (_fxLookupRunning) return;
			_fxLookupRunning     = true;
			_lblFxRate.Text      = "FX wird ermittelt…";
			_lblFxRate.ForeColor = Color.Gray;

			double rate = await _client.GetHistoricalFxRateAsync(ccy, date);

			_fxLookupRunning       = false;
			_fetchedReferenceFxRate = rate;
			UpdateFxLabel(ccy, rate, date);
		}

		private void UpdateFxLabel(string ccy, double rate, DateTime? date)
		{
			if (string.IsNullOrEmpty(ccy) || ccy == "EUR" || rate == 1.0)
			{
				_lblFxRate.Text = "";
				return;
			}
			if (rate <= 0)
			{
				_lblFxRate.Text      = "FX nicht verfügbar";
				_lblFxRate.ForeColor = Color.OrangeRed;
			}
			else
			{
				string dateHint = date.HasValue ? $" ({date.Value:dd.MM.yy})" : "";
				_lblFxRate.Text      = $"FX{dateHint}: {rate:N4}";
				_lblFxRate.ForeColor = Color.DarkGreen;
			}
		}

		// -----------------------------------------------------------------------
		// Limit-Einheiten
		// -----------------------------------------------------------------------

		private void RefreshLimitUnitDisplay()
		{
			if (_cmbUpperUnit == null || _cmbLowerUnit == null) return;

			int upperIndex = _cmbUpperUnit.SelectedIndex < 0 ? 0 : _cmbUpperUnit.SelectedIndex;
			int lowerIndex = _cmbLowerUnit.SelectedIndex < 0 ? 0 : _cmbLowerUnit.SelectedIndex;

			string absoluteCurrency = _chkConvertToEur != null && _chkConvertToEur.Checked
				? "EUR"
				: NormalizeCurrency(_cmbQuoteCurrency?.Text);
			string absoluteUnit = string.IsNullOrEmpty(absoluteCurrency) ? "Währung?" : absoluteCurrency;

			ReplaceLimitUnitItems(_cmbUpperUnit, absoluteUnit, upperIndex);
			ReplaceLimitUnitItems(_cmbLowerUnit, absoluteUnit, lowerIndex);

			bool hasReferencePrice = TryGetPositiveReferencePrice(out _);
			_cmbUpperUnit.Enabled = hasReferencePrice || _cmbUpperUnit.SelectedIndex == 1;
			_cmbLowerUnit.Enabled = hasReferencePrice || _cmbLowerUnit.SelectedIndex == 1;
		}

		private static void ReplaceLimitUnitItems(ComboBox combo, string absoluteUnit, int selectedIndex)
		{
			combo.BeginUpdate();
			combo.Items.Clear();
			combo.Items.Add(absoluteUnit);
			combo.Items.Add("%");
			combo.SelectedIndex = selectedIndex == 1 ? 1 : 0;
			combo.EndUpdate();
		}

		private bool TryGetPositiveReferencePrice(out double value)
		{
			value = ParseOptional(_txtReferencePrice?.Text ?? "");
			return value > 0;
		}

		// -----------------------------------------------------------------------
		// Validierung numerischer Textboxen
		// -----------------------------------------------------------------------

		private void TxtNumeric_Leave(object sender, EventArgs e)
		{
			var tb = (TextBox)sender;
			if (string.IsNullOrWhiteSpace(tb.Text)) return;

			if (ParseOptional(tb.Text) >= 0)
				tb.ForeColor = SystemColors.WindowText;
			else
			{
				tb.ForeColor = Color.Red;
				tb.Focus();
			}
		}

		// -----------------------------------------------------------------------
		// OK
		// -----------------------------------------------------------------------

		private void BtnOk_Click(object sender, EventArgs e)
		{
			if (_lookupRunning || _fxLookupRunning)
			{
				MessageBox.Show("Bitte den laufenden Abruf abwarten.", "Abruf läuft",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				DialogResult = DialogResult.None;
				return;
			}

			string isin = _txtIsin.Text.Trim().ToUpperInvariant();
			if (string.IsNullOrEmpty(isin))
			{
				MessageBox.Show("Bitte eine ISIN eingeben.", "Eingabe fehlt",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			double qty = ParseOptional(_txtQuantity.Text);
			double referencePrice = ParseOptional(_txtReferencePrice.Text);
			if (qty < 0 || referencePrice < 0)
			{
				MessageBox.Show("Stückzahl und Kauf-/Referenzkurs müssen positive Zahlen sein.",
					"Eingabefehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			LimitValueType upperType = _cmbUpperUnit.SelectedIndex == 1
				? LimitValueType.Percent : LimitValueType.Absolute;
			LimitValueType lowerType = _cmbLowerUnit.SelectedIndex == 1
				? LimitValueType.Percent : LimitValueType.Absolute;

			if (((_chkUpperEnabled.Checked && upperType == LimitValueType.Percent) ||
			     (_chkLowerEnabled.Checked && lowerType == LimitValueType.Percent)) && referencePrice <= 0)
			{
				MessageBox.Show("Prozentuale Limits benötigen einen Kauf-/Referenzkurs.",
					"Referenzkurs fehlt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			if ((upperType == LimitValueType.Absolute && _nudUpper.Value < 0) ||
				(lowerType == LimitValueType.Absolute && _nudLower.Value < 0))
			{
				MessageBox.Show("Absolute Limits dürfen nicht negativ sein. Negative Werte sind nur bei Prozent-Limits zulässig.",
					"Eingabefehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			string quoteCurrency = NormalizeCurrency(_cmbQuoteCurrency.Text);
			if (!_chkConvertToEur.Checked && string.IsNullOrEmpty(quoteCurrency) &&
				((_chkUpperEnabled.Checked && upperType == LimitValueType.Absolute) ||
				 (_chkLowerEnabled.Checked && lowerType == LimitValueType.Absolute)))
			{
				MessageBox.Show("Für ein aktives absolutes Limit ohne EUR-Umrechnung muss die Kurs-/Listingwährung angegeben sein.",
					"Kurswährung fehlt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			TryParseDate(_txtReferenceDate.Text, out DateTime referenceDate);

			string referenceCurrency = NormalizeCurrency(_cmbReferenceCurrency.Text);
			if (referencePrice > 0 && string.IsNullOrEmpty(referenceCurrency))
				referenceCurrency = "EUR";

			double fxRate = string.IsNullOrEmpty(referenceCurrency) || referenceCurrency == "EUR"
				? 1.0 : _fetchedReferenceFxRate;

			string name = _txtName.Text.Trim();
			Result = new WatchlistEntry
			{
				Isin                 = isin,
				Name                 = string.IsNullOrEmpty(name) ? isin : name,
				EntryType            = _cmbEntryType.SelectedIndex == 1
				                           ? WatchlistEntryType.BuyCandidate
				                           : WatchlistEntryType.Holding,
				Note                 = _txtNote.Text,
				YahooSymbol          = HasResolvedSymbolFor(isin) ? _resolvedYahooSymbol : "",
				QuoteCurrency        = quoteCurrency,
				LimitUpper           = (double)_nudUpper.Value,
				LimitUpperType       = upperType,
				LimitUpperEnabled    = _chkUpperEnabled.Checked,
				LimitLower           = (double)_nudLower.Value,
				LimitLowerType       = lowerType,
				LimitLowerEnabled    = _chkLowerEnabled.Checked,
				ConvertToEur         = _chkConvertToEur.Checked,
				Quantity             = qty,
				ReferencePrice       = referencePrice,
				ReferenceCurrency    = referenceCurrency,
				ReferenceDate        = referenceDate,
				ReferenceFxRate      = fxRate
			};
		}

		// -----------------------------------------------------------------------
		// Hilfsmethoden
		// -----------------------------------------------------------------------

		private static double ParseOptional(string raw)
		{
			raw = raw.Trim();
			if (string.IsNullOrEmpty(raw)) return 0;

			if (raw.Contains(",") && raw.Contains("."))
			{
				if (raw.LastIndexOf(',') > raw.LastIndexOf('.'))
					raw = raw.Replace(".", "").Replace(",", ".");
				else
					raw = raw.Replace(",", "");
			}
			else
			{
				raw = raw.Replace(",", ".");
			}

			if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
				return v >= 0 ? v : -1;
			return -1;
		}

		private static bool TryParseDate(string s, out DateTime date)
		{
			date = DateTime.MinValue;
			if (string.IsNullOrWhiteSpace(s)) return false;
			string[] fmts = { "dd.MM.yyyy", "d.M.yyyy", "d.MM.yyyy", "dd.M.yyyy", "yyyy-MM-dd" };
			return DateTime.TryParseExact(s.Trim(), fmts,
				       CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
			    || DateTime.TryParse(s.Trim(), out date);
		}

		private static bool IsValidFormat(string isin) =>
			!string.IsNullOrEmpty(isin) && isin.Length == 12 &&
			Regex.IsMatch(isin, @"^[A-Z]{2}[A-Z0-9]{10}$");

		private static string NormalizeCurrency(string raw) =>
			(raw ?? "").Trim().ToUpperInvariant();

		private static decimal ClampToNud(double value)
		{
			if (value > 1000000.0) return 1000000m;
			if (value < -1000000.0) return -1000000m;
			return (decimal)value;
		}

		private static Label MakeLbl(string text, int x, int y, int w, Font font, Color? color = null)
		{
			var l = new Label { Text = text, Location = new Point(x, y), Width = w, Font = font };
			if (color.HasValue) l.ForeColor = color.Value;
			return l;
		}

		private static NumericUpDown MakeNud(int x, int y, Font font) => new NumericUpDown
		{
			Location = new Point(x, y), Width = 120, DecimalPlaces = 2,
			Minimum = -1000000m, Maximum = 1000000m, Increment = 1m, Font = font
		};

		private static ComboBox MakeCurrencyCombo(int x, int y, Font font)
		{
			var combo = new ComboBox
			{
				Location      = new Point(x, y),
				Width         = 78,
				Font          = font,
				DropDownStyle = ComboBoxStyle.DropDown
			};
			combo.Items.AddRange(new object[] { "", "EUR", "CHF", "USD", "CAD", "GBP", "HKD", "DKK", "SEK", "NOK", "JPY", "AUD" });
			return combo;
		}

		private static ComboBox MakeLimitUnitCombo(int x, int y, Font font)
		{
			var combo = new ComboBox
			{
				Location      = new Point(x, y),
				Width         = 84,
				Font          = font,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			combo.Items.AddRange(new object[] { "Währung?", "%" });
			combo.SelectedIndex = 0;
			return combo;
		}
	}
}
