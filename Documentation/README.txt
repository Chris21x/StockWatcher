# StockWatcher

**Version 1.1.4** · File version **1.1.4.0**

StockWatcher is a lightweight Windows desktop application for monitoring securities, watchlist candidates and realized positions. It retrieves market data, calculates portfolio and position values, evaluates configurable price limits and can send local and optional ntfy push notifications.

> StockWatcher is a monitoring tool. It does **not** place orders, execute trades, or make investment decisions.

## Highlights

- Windows WinForms application targeting **.NET Framework 4.8**
- Three position types:
  - **Holding**
  - **Buy candidate**
  - **Realized**
- Four views:
  - **Overview**
  - **Holdings**
  - **Buy candidates**
  - **Realized**
- Automatic and manual quote refresh
- Quote lookup by ISIN with persisted market symbol
- Currency conversion to EUR
- Absolute or percentage-based upper/lower limits
- Local alerts:
  - tray balloon
  - alarm dialog
  - tray icon marker
- Optional **ntfy** push notifications
- Per-position price trend indicators
- Portfolio market-value trend
- Open, realized and total P/L summary
- Persisted window position and size
- Start directly in the system tray
- Per-view column layouts:
  - visibility
  - order
  - width
- Field chooser with additional diagnostic/internal fields
- Persisted Overview filters
- Network/timeout robustness for unattended/autostart operation

## Version 1.1.4 column handling

Each view has its own persistent column layout. Default columns and widths remain unchanged until the user customizes them.

Columns can be managed through:

- the small `▾` field-selection button
- right-click on a column header → **Felder auswählen… / Select fields…**
- right-click on a column header → **Spalte ausblenden / Hide column**

Hidden columns keep their absolute slot, width and identity. This makes layout restoration deterministic even if visible columns are reordered while another column is hidden.

The field chooser also exposes diagnostic/runtime fields that are not shown by default.

## Requirements

### Running the application

- Windows
- .NET Framework 4.8 runtime
- Internet access for market-data retrieval
- Optional: an ntfy server/account/topic for push notifications

### Building from source

Recommended:

- Visual Studio 2022
- .NET desktop development workload
- .NET Framework 4.8 Developer Pack

NuGet dependency:

- `Newtonsoft.Json` 13.0.3

The project uses an SDK-style `.csproj`.

## Build

1. Open `StockWatcher.sln` in Visual Studio 2022.
2. Restore NuGet packages if Visual Studio does not do so automatically.
3. Select `Release`.
4. Build the solution.

Typical output:

```text
bin\Release\net48\StockWatcher.exe
```

## First start and data files

StockWatcher stores portfolio/watchlist data in an XML file.

By default:

```text
StockWatcher.xml
```

A local bootstrap file beside the executable stores the selected XML path:

```text
StockWatcher.ini
```

The XML data file may be placed elsewhere, including a synchronized folder.

### Important for public repositories

Do **not** commit personal runtime data.

Keep at least these files local/excluded from Git:

```text
StockWatcher.ini
StockWatcher.xml
```

The XML can contain position data, notes, prices and transaction information. Future/local configuration may also contain secrets.

## Market data

StockWatcher retrieves quotes from external market-data endpoints and uses ISIN-based resolution where necessary. Availability and data quality therefore depend on third-party services and network connectivity.

Version 1.1.3.1 and later include additional protection for unattended startup:

- per-position fetch timeout
- clean abort on transient network/provider failures
- automatic short retry
- no unnecessary symbol re-resolution on connectivity failures
- guaranteed reset of the internal fetch-running state

## Data model

### Holding

An open position with quantity and optional purchase/reference information.

### Buy candidate

A watched security without an open holding. It can use reference values and active limits.

### Realized

A closed position with purchase/reference and sale information. Realized positions continue to receive current quotes for comparison, but they do not trigger price-limit alarms.

## Limits

StockWatcher supports:

- **Absolute limits**
- **Percentage limits**

Percentage limits are relative to the purchase/reference price.

Absolute limits are evaluated:

- in EUR when `ConvertToEur=true`
- otherwise in the quote/listing currency

Upper and lower limits can be enabled independently.

## Notifications

Local notification channels can be enabled independently:

- balloon tip
- alarm dialog
- red tray-icon marker

Optional push notifications use ntfy.

The current public V1.1.4 implementation uses ntfy for **outgoing alarm notifications only**. Android/remote command handling is not part of V1.1.4.

## Documentation

- [German user guide](USER_GUIDE_DE.md)
- [English user guide](USER_GUIDE_EN.md)
- [License](LICENSE.md)

## License

StockWatcher is **source-available**, not OSI Open Source.

Private and commercial **use of the unmodified software is free of charge**. Modification, adaptation, incorporation, merging into another solution, or creation/distribution of derivative versions requires prior permission from the copyright holder.

See [LICENSE.md](LICENSE.md) for the controlling terms.

## Disclaimer

StockWatcher is provided as a technical monitoring utility. Market data can be delayed, incomplete or unavailable. Calculations may depend on user-entered data and external exchange-rate information.

Do not treat StockWatcher output as investment, tax, legal or trading advice. Always verify information independently before making financial decisions.
