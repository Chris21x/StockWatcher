using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using StockWatcher.Models;
using StockWatcher.Localization;
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
		private TextBox       _txtIncomeEur;
		private TextBox       _txtSalePrice;
		private ComboBox      _cmbSaleCurrency;
		private TextBox       _txtSaleDate;
		private Label         _lblSaleFxRate;
		private Label         _lblStatus;
		private Button        _btnCheck;
		private Button        _btnFetch;
		private Button        _btnOk;
		private Button        _btnCancel;
		private GroupBox      _grpMonitoring;
		private GroupBox      _grpSale;

		private string _resolvedYahooSymbol = "";
		private string _resolvedIsin = "";
		private double _fetchedReferenceFxRate = 0.0;
		private double _fetchedSaleFxRate = 0.0;
		private bool   _lookupRunning = false;
		private bool   _fxLookupRunning = false;
		private bool   _saleFxLookupRunning = false;
		private bool   _isinDirty = false;

		public WatchlistEntry Result { get; private set; }

		public EditEntryDialog(StockFrankfurtClient client, WatchlistEntry existing = null)
		{
			_client = client;
			Text = existing == null ? L10n.Text("EditAddTitle") : L10n.Text("EditTitle");
			ClientSize = new Size(740, 650);
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;

			BuildUi();

			if (existing != null)
			{
				_txtIsin.Text                = existing.Isin;
				_txtName.Text                = existing.Name;
				_cmbEntryType.SelectedIndex  = existing.EntryType == WatchlistEntryType.Realized
					? 2
					: existing.EntryType == WatchlistEntryType.BuyCandidate ? 1 : 0;
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

				if (existing.IncomeEur > 0)
					_txtIncomeEur.Text = existing.IncomeEur.ToString("N2", CultureInfo.InvariantCulture);
				if (existing.SalePrice > 0)
					_txtSalePrice.Text = existing.SalePrice.ToString("N4", CultureInfo.InvariantCulture);

				_cmbSaleCurrency.Text = existing.SaleCurrency ?? "";
				if (existing.SaleDate != DateTime.MinValue)
					_txtSaleDate.Text = existing.SaleDate.ToString("dd.MM.yyyy");

				_fetchedSaleFxRate = existing.SaleFxRate;
				UpdateSaleFxLabel(existing.EffectiveSaleCurrency, existing.SaleFxRate,
					existing.SaleDate != DateTime.MinValue ? existing.SaleDate : (DateTime?)null);

				_isinDirty = false;
			}

			RefreshLimitUnitDisplay();
			RefreshEntryTypeUi();
		}

		private void BuildUi()
		{
			var font = new Font("Segoe UI", 9f);
			var fontSm = new Font("Segoe UI", 8f);

			// -------------------------------------------------------------------
			// Wertpapier / Identität
			// -------------------------------------------------------------------
			var grpIdentity = new GroupBox
			{
				Text = L10n.Text("GroupSecurity"),
				Location = new Point(12, 10),
				Size = new Size(716, 142),
				Font = font
			};
			Controls.Add(grpIdentity);

			grpIdentity.Controls.Add(MakeLbl(L10n.Text("LabelType"), 16, 29, 70, font));
			_cmbEntryType = new ComboBox
			{
				Location = new Point(92, 25),
				Width = 155,
				Font = font,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			_cmbEntryType.Items.AddRange(new object[] { L10n.Text("EntryTypeHolding"), L10n.Text("EntryTypeBuyCandidate"), L10n.Text("EntryTypeRealized") });
			_cmbEntryType.SelectedIndex = 0;
			_cmbEntryType.SelectedIndexChanged += (s, e) => RefreshEntryTypeUi();
			grpIdentity.Controls.Add(_cmbEntryType);

			grpIdentity.Controls.Add(MakeLbl("ISIN:", 270, 29, 45, font));
			_txtIsin = new TextBox
			{
				Location = new Point(318, 25),
				Width = 190,
				Font = font,
				CharacterCasing = CharacterCasing.Upper,
				MaxLength = 12
			};
			_txtIsin.Leave += TxtIsin_Leave;
			_txtIsin.TextChanged += (s, e) => _isinDirty = true;
			grpIdentity.Controls.Add(_txtIsin);

			_btnCheck = new Button
			{
				Text = L10n.Text("ButtonCheck"),
				Location = new Point(516, 23),
				Size = new Size(84, 28),
				Font = font
			};
			_btnCheck.Click += async (s, e) => await RunLookupAsync();
			grpIdentity.Controls.Add(_btnCheck);

			_btnFetch = new Button
			{
				Text = L10n.Text("ButtonFetch"),
				Location = new Point(606, 23),
				Size = new Size(96, 28),
				Font = font
			};
			_btnFetch.Click += async (s, e) => await RunFetchAsync();
			grpIdentity.Controls.Add(_btnFetch);

			grpIdentity.Controls.Add(MakeLbl("Name:", 16, 65, 70, font));
			_txtName = new TextBox
			{
				Location = new Point(92, 61),
				Width = 610,
				Font = font
			};
			grpIdentity.Controls.Add(_txtName);

			grpIdentity.Controls.Add(MakeLbl(L10n.Text("LabelQuoteCurrency"), 16, 101, 95, font));
			_cmbQuoteCurrency = MakeCurrencyCombo(112, 97, font);
			_cmbQuoteCurrency.TextChanged += (s, e) => RefreshLimitUnitDisplay();
			grpIdentity.Controls.Add(_cmbQuoteCurrency);

			_chkConvertToEur = new CheckBox
			{
				Text = L10n.Text("ConvertToEur"),
				Location = new Point(214, 99),
				Width = 420,
				Font = font,
				AutoSize = false
			};
			_chkConvertToEur.CheckedChanged += (s, e) => RefreshLimitUnitDisplay();
			grpIdentity.Controls.Add(_chkConvertToEur);


			// -------------------------------------------------------------------
			// Position / Referenz
			// -------------------------------------------------------------------
			var grpPosition = new GroupBox
			{
				Text = L10n.Text("GroupPositionReference"),
				Location = new Point(12, 160),
				Size = new Size(716, 150),
				Font = font
			};
			Controls.Add(grpPosition);

			grpPosition.Controls.Add(MakeLbl(L10n.Text("LabelQuantity"), 16, 31, 115, font));
			_txtQuantity = new TextBox
			{
				Location = new Point(135, 27),
				Width = 105,
				Font = font
			};
			_txtQuantity.Leave += TxtNumeric_Leave;
			grpPosition.Controls.Add(_txtQuantity);
			grpPosition.Controls.Add(MakeLbl(L10n.Text("NoValueHint"), 248, 31, 150, fontSm, Color.Gray));

			grpPosition.Controls.Add(MakeLbl(L10n.Text("LabelIncome"), 405, 31, 135, font));
			_txtIncomeEur = new TextBox
			{
				Location = new Point(544, 27),
				Width = 100,
				Font = font
			};
			_txtIncomeEur.Leave += TxtNumeric_Leave;
			grpPosition.Controls.Add(_txtIncomeEur);
			grpPosition.Controls.Add(MakeLbl("EUR", 651, 31, 40, font));

			grpPosition.Controls.Add(MakeLbl(L10n.Text("LabelReferencePrice"), 16, 69, 125, font));
			_txtReferencePrice = new TextBox
			{
				Location = new Point(135, 65),
				Width = 105,
				Font = font
			};
			_txtReferencePrice.Leave += TxtNumeric_Leave;
			_txtReferencePrice.TextChanged += (s, e) => RefreshLimitUnitDisplay();
			grpPosition.Controls.Add(_txtReferencePrice);

			grpPosition.Controls.Add(MakeLbl(L10n.Text("LabelCurrency"), 248, 69, 65, font));
			_cmbReferenceCurrency = MakeCurrencyCombo(316, 65, font);
			_cmbReferenceCurrency.Leave += CmbOrDate_Leave;
			_cmbReferenceCurrency.SelectedIndexChanged += async (s, e) => await TriggerFxLookupAsync();
			grpPosition.Controls.Add(_cmbReferenceCurrency);

			grpPosition.Controls.Add(MakeLbl(L10n.Text("LabelReferenceDate"), 405, 69, 135, font));
			_txtReferenceDate = new TextBox
			{
				Location = new Point(544, 65),
				Width = 100,
				Font = font
			};
			_txtReferenceDate.Leave += CmbOrDate_Leave;
			grpPosition.Controls.Add(_txtReferenceDate);
			grpPosition.Controls.Add(MakeLbl("dd.MM.yyyy", 651, 69, 60, fontSm, Color.Gray));

			_lblFxRate = new Label
			{
				Location = new Point(135, 105),
				Width = 260,
				Font = fontSm,
				ForeColor = Color.Gray,
				Text = ""
			};
			grpPosition.Controls.Add(_lblFxRate);
			grpPosition.Controls.Add(MakeLbl(
				L10n.Text("IncomeHint"),
				405, 105, 285, fontSm, Color.Gray));

			// -------------------------------------------------------------------
			// Kursüberwachung (Bestand / Kaufinteresse)
			// -------------------------------------------------------------------
			_grpMonitoring = new GroupBox
			{
				Text = L10n.Text("GroupMonitoring"),
				Location = new Point(12, 318),
				Size = new Size(716, 128),
				Font = font
			};
			Controls.Add(_grpMonitoring);

			_grpMonitoring.Controls.Add(MakeLbl(L10n.Text("LabelLowerLimit"), 16, 31, 115, font));
			_nudLower = MakeNud(135, 27, font);
			_grpMonitoring.Controls.Add(_nudLower);
			_cmbLowerUnit = MakeLimitUnitCombo(263, 27, font);
			_grpMonitoring.Controls.Add(_cmbLowerUnit);
			_chkLowerEnabled = new CheckBox
			{
				Text = L10n.Text("AlarmEnabled"),
				Location = new Point(360, 29),
				Width = 110,
				Font = font
			};
			_grpMonitoring.Controls.Add(_chkLowerEnabled);

			_grpMonitoring.Controls.Add(MakeLbl(L10n.Text("LabelUpperLimit"), 16, 69, 115, font));
			_nudUpper = MakeNud(135, 65, font);
			_grpMonitoring.Controls.Add(_nudUpper);
			_cmbUpperUnit = MakeLimitUnitCombo(263, 65, font);
			_grpMonitoring.Controls.Add(_cmbUpperUnit);
			_chkUpperEnabled = new CheckBox
			{
				Text = L10n.Text("AlarmEnabled"),
				Location = new Point(360, 67),
				Width = 110,
				Font = font
			};
			_grpMonitoring.Controls.Add(_chkUpperEnabled);

			_grpMonitoring.Controls.Add(MakeLbl(
				L10n.Text("PercentLimitHint"),
				488, 49, 210, fontSm, Color.Gray));

			// -------------------------------------------------------------------
			// Verkauf (nur Realisiert) – belegt bewusst denselben Platz wie
			// Kursüberwachung, damit der Dialog je Typ ruhig und kompakt bleibt.
			// -------------------------------------------------------------------
			_grpSale = new GroupBox
			{
				Text = L10n.Text("GroupSale"),
				Location = new Point(12, 318),
				Size = new Size(716, 128),
				Font = font,
				Visible = false
			};
			Controls.Add(_grpSale);

			_grpSale.Controls.Add(MakeLbl(L10n.Text("LabelSalePrice"), 16, 31, 115, font));
			_txtSalePrice = new TextBox
			{
				Location = new Point(135, 27),
				Width = 105,
				Font = font
			};
			_txtSalePrice.Leave += TxtNumeric_Leave;
			_grpSale.Controls.Add(_txtSalePrice);

			_grpSale.Controls.Add(MakeLbl(L10n.Text("LabelCurrency"), 248, 31, 65, font));
			_cmbSaleCurrency = MakeCurrencyCombo(316, 27, font);
			_cmbSaleCurrency.Leave += SaleCmbOrDate_Leave;
			_cmbSaleCurrency.SelectedIndexChanged += async (s, e) => await TriggerSaleFxLookupAsync();
			_grpSale.Controls.Add(_cmbSaleCurrency);

			_grpSale.Controls.Add(MakeLbl(L10n.Text("LabelSaleDate"), 405, 31, 135, font));
			_txtSaleDate = new TextBox
			{
				Location = new Point(544, 27),
				Width = 100,
				Font = font
			};
			_txtSaleDate.Leave += SaleCmbOrDate_Leave;
			_grpSale.Controls.Add(_txtSaleDate);
			_grpSale.Controls.Add(MakeLbl("dd.MM.yyyy", 651, 31, 60, fontSm, Color.Gray));

			_lblSaleFxRate = new Label
			{
				Location = new Point(135, 67),
				Width = 260,
				Font = fontSm,
				ForeColor = Color.Gray,
				Text = ""
			};
			_grpSale.Controls.Add(_lblSaleFxRate);
			_grpSale.Controls.Add(MakeLbl(
				L10n.Text("SaleHint"),
				405, 67, 285, fontSm, Color.Gray));

			// -------------------------------------------------------------------
			// Bemerkung
			// -------------------------------------------------------------------
			var grpNote = new GroupBox
			{
				Text = L10n.Text("GroupNote"),
				Location = new Point(12, 454),
				Size = new Size(716, 112),
				Font = font
			};
			Controls.Add(grpNote);

			_txtNote = new TextBox
			{
				Location = new Point(12, 24),
				Size = new Size(692, 76),
				Font = font,
				Multiline = true,
				AcceptsReturn = true,
				ScrollBars = ScrollBars.Vertical
			};
			grpNote.Controls.Add(_txtNote);

			// -------------------------------------------------------------------
			// Status + Buttons
			// -------------------------------------------------------------------
			_lblStatus = new Label
			{
				Location = new Point(14, 575),
				Size = new Size(714, 22),
				Font = fontSm,
				ForeColor = Color.Gray,
				Text = L10n.Text("EditInitialStatus")
			};
			Controls.Add(_lblStatus);

			_btnOk = new Button
			{
				Text = L10n.Text("ButtonOk"),
				Location = new Point(532, 606),
				Size = new Size(90, 32),
				Font = font,
				DialogResult = DialogResult.OK
			};
			_btnOk.Click += BtnOk_Click;
			Controls.Add(_btnOk);

			_btnCancel = new Button
			{
				Text = L10n.Text("ButtonCancel"),
				Location = new Point(630, 606),
				Size = new Size(98, 32),
				Font = font,
				DialogResult = DialogResult.Cancel
			};
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
				SetStatus(L10n.Text("InvalidIsinFormat"), Color.Red);
				return;
			}
			if (_lookupRunning) return;

			_lookupRunning = true;
			SetLookupButtons(false);
			SetStatus(L10n.Text("LookupRunning"), Color.Gray);

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
				SetStatus(L10n.Text("InvalidIsin"), Color.Red);
				return;
			}
			if (_lookupRunning || _fxLookupRunning || _saleFxLookupRunning) return;

			_lookupRunning = true;
			SetLookupButtons(false);
			SetStatus(L10n.Text("FetchRunning"), Color.Gray);

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
				SetStatus(L10n.Format("QuoteAvailable", _resolvedYahooSymbol, r.Price, r.Currency, r.Timestamp),
					Color.DarkGreen);
			}
			else
			{
				SetStatus(L10n.Format("QuoteUnavailable", r.ErrorMessage), Color.OrangeRed);
			}
		}

		private async Task<IsinListingCandidate> LookupAndSelectCandidateAsync(string isin)
		{
			IsinCandidatesResult lookup = await _client.LookupIsinCandidatesAsync(isin);
			if (lookup.Candidates.Count == 0)
			{
				SetStatus(L10n.Format("LookupSaveAnyway", lookup.ErrorMessage), Color.OrangeRed);
				return null;
			}

			if (lookup.Candidates.Count == 1)
				return lookup.Candidates[0];

			using (var dlg = new SymbolSelectionDialog(isin, lookup.Candidates))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedCandidate != null)
					return dlg.SelectedCandidate;
			}

			SetStatus(L10n.Text("SelectionCancelled"), Color.DarkGoldenrod);
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
				SetStatus(L10n.Format("CandidateQuote", candidate.YahooSymbol, candidate.Exchange, candidate.Country, candidate.LastPrice, candidate.Currency),
					Color.DarkGreen);
			else
				SetStatus(L10n.Format("CandidateNoQuote", candidate.YahooSymbol), Color.DarkGoldenrod);
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
			_lblFxRate.Text      = L10n.Text("FxRetrieving");
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
				_lblFxRate.Text      = L10n.Text("FxUnavailable");
				_lblFxRate.ForeColor = Color.OrangeRed;
			}
			else
			{
				string dateHint = date.HasValue ? $" ({date.Value:dd.MM.yy})" : "";
				_lblFxRate.Text      = L10n.Format("FxValue", dateHint, rate);
				_lblFxRate.ForeColor = Color.DarkGreen;
			}
		}

		private async void SaleCmbOrDate_Leave(object sender, EventArgs e) =>
			await TriggerSaleFxLookupAsync();

		private async Task TriggerSaleFxLookupAsync()
		{
			if (_cmbSaleCurrency == null || _txtSaleDate == null) return;

			string ccy = _cmbSaleCurrency.Text.Trim().ToUpperInvariant();
			if (string.IsNullOrEmpty(ccy) || ccy == "EUR")
			{
				_fetchedSaleFxRate = 1.0;
				UpdateSaleFxLabel(ccy, 1.0, null);
				return;
			}

			if (!TryParseDate(_txtSaleDate.Text, out DateTime date))
			{
				_fetchedSaleFxRate = 0.0;
				_lblSaleFxRate.Text = "";
				return;
			}

			if (_saleFxLookupRunning) return;
			_saleFxLookupRunning     = true;
			_lblSaleFxRate.Text      = L10n.Text("FxRetrieving");
			_lblSaleFxRate.ForeColor = Color.Gray;

			double rate = await _client.GetHistoricalFxRateAsync(ccy, date);

			_saleFxLookupRunning = false;
			_fetchedSaleFxRate = rate;
			UpdateSaleFxLabel(ccy, rate, date);
		}

		private void UpdateSaleFxLabel(string ccy, double rate, DateTime? date)
		{
			if (_lblSaleFxRate == null) return;

			if (string.IsNullOrEmpty(ccy) || ccy == "EUR" || rate == 1.0)
			{
				_lblSaleFxRate.Text = "";
				return;
			}
			if (rate <= 0)
			{
				_lblSaleFxRate.Text      = L10n.Text("FxUnavailable");
				_lblSaleFxRate.ForeColor = Color.OrangeRed;
			}
			else
			{
				string dateHint = date.HasValue ? $" ({date.Value:dd.MM.yy})" : "";
				_lblSaleFxRate.Text      = L10n.Format("FxValue", dateHint, rate);
				_lblSaleFxRate.ForeColor = Color.DarkGreen;
			}
		}

		private void RefreshEntryTypeUi()
		{
			if (_cmbEntryType == null) return;

			bool realized = _cmbEntryType.SelectedIndex == 2;

			if (_grpMonitoring != null) _grpMonitoring.Visible = !realized;
			if (_grpSale != null) _grpSale.Visible = realized;

			if (_txtSalePrice != null) _txtSalePrice.Enabled = realized;
			if (_cmbSaleCurrency != null) _cmbSaleCurrency.Enabled = realized;
			if (_txtSaleDate != null) _txtSaleDate.Enabled = realized;

			if (_nudUpper != null) _nudUpper.Enabled = !realized;
			if (_cmbUpperUnit != null) _cmbUpperUnit.Enabled = !realized;
			if (_chkUpperEnabled != null) _chkUpperEnabled.Enabled = !realized;
			if (_nudLower != null) _nudLower.Enabled = !realized;
			if (_cmbLowerUnit != null) _cmbLowerUnit.Enabled = !realized;
			if (_chkLowerEnabled != null) _chkLowerEnabled.Enabled = !realized;

			if (_chkConvertToEur != null)
			{
				_chkConvertToEur.Enabled = true;
				_chkConvertToEur.Text = realized
					? L10n.Text("ConvertCurrentToEur")
					: L10n.Text("ConvertToEur");
			}

			if (_btnFetch != null)
				_btnFetch.Enabled = !_lookupRunning;
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
			string absoluteUnit = string.IsNullOrEmpty(absoluteCurrency) ? L10n.Text("CurrencyUnknown") : absoluteCurrency;

			ReplaceLimitUnitItems(_cmbUpperUnit, absoluteUnit, upperIndex);
			ReplaceLimitUnitItems(_cmbLowerUnit, absoluteUnit, lowerIndex);

			bool hasReferencePrice = TryGetPositiveReferencePrice(out _);
			bool realized = _cmbEntryType != null && _cmbEntryType.SelectedIndex == 2;
			_cmbUpperUnit.Enabled = !realized && (hasReferencePrice || _cmbUpperUnit.SelectedIndex == 1);
			_cmbLowerUnit.Enabled = !realized && (hasReferencePrice || _cmbLowerUnit.SelectedIndex == 1);
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
			if (_lookupRunning || _fxLookupRunning || _saleFxLookupRunning)
			{
				MessageBox.Show(L10n.Text("WaitForFetch"), L10n.Text("FetchInProgressTitle"),
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				DialogResult = DialogResult.None;
				return;
			}

			string isin = _txtIsin.Text.Trim().ToUpperInvariant();
			if (string.IsNullOrEmpty(isin))
			{
				MessageBox.Show(L10n.Text("EnterIsin"), L10n.Text("InputMissingTitle"),
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			double qty = ParseOptional(_txtQuantity.Text);
			double referencePrice = ParseOptional(_txtReferencePrice.Text);
			double incomeEur = ParseOptional(_txtIncomeEur.Text);
			double salePrice = ParseOptional(_txtSalePrice.Text);
			if (qty < 0 || referencePrice < 0 || incomeEur < 0 || salePrice < 0)
			{
				MessageBox.Show(L10n.Text("PositiveNumbersRequired"),
					L10n.Text("InputErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			WatchlistEntryType entryType = _cmbEntryType.SelectedIndex == 2
				? WatchlistEntryType.Realized
				: _cmbEntryType.SelectedIndex == 1
					? WatchlistEntryType.BuyCandidate
					: WatchlistEntryType.Holding;

			if (entryType == WatchlistEntryType.Realized && (qty <= 0 || referencePrice <= 0 || salePrice <= 0))
			{
				MessageBox.Show(L10n.Text("RealizedDataRequired"),
					L10n.Text("SaleDataMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			LimitValueType upperType = _cmbUpperUnit.SelectedIndex == 1
				? LimitValueType.Percent : LimitValueType.Absolute;
			LimitValueType lowerType = _cmbLowerUnit.SelectedIndex == 1
				? LimitValueType.Percent : LimitValueType.Absolute;

			if (entryType != WatchlistEntryType.Realized &&
				((_chkUpperEnabled.Checked && upperType == LimitValueType.Percent) ||
				 (_chkLowerEnabled.Checked && lowerType == LimitValueType.Percent)) && referencePrice <= 0)
			{
				MessageBox.Show(L10n.Text("PercentNeedsReference"),
					L10n.Text("ReferenceMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			if (entryType != WatchlistEntryType.Realized &&
				((upperType == LimitValueType.Absolute && _nudUpper.Value < 0) ||
				 (lowerType == LimitValueType.Absolute && _nudLower.Value < 0)))
			{
				MessageBox.Show(L10n.Text("AbsoluteLimitNonNegative"),
					L10n.Text("InputErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			string quoteCurrency = NormalizeCurrency(_cmbQuoteCurrency.Text);
			if (entryType != WatchlistEntryType.Realized &&
				!_chkConvertToEur.Checked && string.IsNullOrEmpty(quoteCurrency) &&
				((_chkUpperEnabled.Checked && upperType == LimitValueType.Absolute) ||
				 (_chkLowerEnabled.Checked && lowerType == LimitValueType.Absolute)))
			{
				MessageBox.Show(L10n.Text("QuoteCurrencyRequired"),
					L10n.Text("QuoteCurrencyMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			TryParseDate(_txtReferenceDate.Text, out DateTime referenceDate);

			string referenceCurrency = NormalizeCurrency(_cmbReferenceCurrency.Text);
			if (referencePrice > 0 && string.IsNullOrEmpty(referenceCurrency))
				referenceCurrency = "EUR";

			double fxRate = string.IsNullOrEmpty(referenceCurrency) || referenceCurrency == "EUR"
				? 1.0 : _fetchedReferenceFxRate;

			TryParseDate(_txtSaleDate.Text, out DateTime saleDate);
			string saleCurrency = NormalizeCurrency(_cmbSaleCurrency.Text);
			if (entryType == WatchlistEntryType.Realized && string.IsNullOrEmpty(saleCurrency))
			{
				MessageBox.Show(L10n.Text("SaleCurrencyRequired"),
					L10n.Text("SaleCurrencyMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			if (entryType == WatchlistEntryType.Realized && saleDate == DateTime.MinValue)
			{
				MessageBox.Show(L10n.Text("SaleDateRequired"),
					L10n.Text("SaleDateMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				DialogResult = DialogResult.None;
				return;
			}

			double saleFxRate = entryType == WatchlistEntryType.Realized
				? (saleCurrency == "EUR" ? 1.0 : _fetchedSaleFxRate)
				: _fetchedSaleFxRate;

			string name = _txtName.Text.Trim();
			Result = new WatchlistEntry
			{
				Isin                 = isin,
				Name                 = string.IsNullOrEmpty(name) ? isin : name,
				EntryType            = entryType,
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
				ReferenceFxRate      = fxRate,
				IncomeEur            = incomeEur,
				SalePrice            = salePrice,
				SaleCurrency         = saleCurrency,
				SaleDate             = saleDate,
				SaleFxRate           = saleFxRate
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
			combo.Items.AddRange(new object[] { L10n.Text("CurrencyUnknown"), "%" });
			combo.SelectedIndex = 0;
			return combo;
		}
	}
}
