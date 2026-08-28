# StockWatcher – User Guide

**Software version:** 1.1.4  
**File version:** 1.1.4.0

## 1. Purpose

StockWatcher monitors securities, buy candidates and realized positions. It retrieves quotes, converts foreign currencies to EUR when required, calculates position and portfolio information, and evaluates configurable price limits.

StockWatcher is **not a trading system**. It does not place or execute orders.

## 2. Main window

The main window contains four tabs:

- **Overview** – combined view of selected entry types
- **Holdings** – open positions
- **Buy candidates** – securities being watched for a possible purchase
- **Realized** – closed positions

In the **Overview** tab, the three entry types can be shown or hidden using checkboxes:

- Holdings
- Buy candidates
- Realized

The filter selection is persisted.

## 3. Quote refresh

A complete quote refresh can be triggered using:

- **F5**
- toolbar **Refresh**
- menu **Action → Refresh now**
- tray menu **Refresh now**

Quotes are also refreshed automatically according to the configured interval.

During a refresh, the status bar shows the current progress.

### Network and timeout behavior

A single problematic request should not permanently block StockWatcher.

On transient network/provider failures, the current refresh cycle is aborted and retried after a short interval. Normal periodic scheduling resumes afterwards.

The additional **Data timeout** setting defines how long a position may remain without a successful data retrieval before StockWatcher marks it accordingly.

## 4. Adding an entry

Use the toolbar:

```text
＋ Add
```

First choose the entry type:

- **Holding**
- **Buy candidate**
- **Realized**

Then enter the ISIN.

Use **Check** to resolve the security and an appropriate listing. If more than one suitable listing is found, StockWatcher can ask you to select a market.

**Fetch (F5)** updates the quote inside the dialog.

### Important fields

**Name**  
Security name.

**Quote currency**  
Currency of the selected listing.

**Convert quote to EUR**  
The quote is converted to EUR for the relevant display/calculation paths. Absolute limits are also evaluated in EUR when this option is enabled.

**Quantity**  
Number of units held or realized.

**Purchase/reference price**  
Purchase price or comparison basis.

**Currency**  
Currency of the purchase/reference price.

**Purchase/reference date**  
Optional reference date. Format:

```text
dd.MM.yyyy
```

**Income/dividends**  
A manually entered EUR amount. StockWatcher intentionally applies no gross/net or tax logic to this value.

**Note**  
Free text.

## 5. Entry types

### 5.1 Holding

Used for an open position.

Typical data:

- quantity
- purchase/reference price
- reference currency
- optional reference date
- income/dividends
- limits

### 5.2 Buy candidate

Used for a security being monitored before purchase.

A quantity is not required. A reference price and limits can be used to monitor preferred entry levels.

### 5.3 Realized

Used for a closed position.

Additional fields include:

- sale price
- sale currency
- sale date
- historical sale FX rate where required

Realized positions continue to receive current quotes so that the current market price can be compared with the historical sale price.

**Realized positions do not trigger price-limit alarms.**

## 6. Limits and alarms

Holdings and buy candidates can have an independent lower and upper limit.

Each limit can be enabled separately.

### Absolute limits

When `Convert quote to EUR` is:

```text
enabled  → absolute limit is evaluated in EUR
disabled → absolute limit is evaluated in the quote/listing currency
```

### Percentage limits

Percentage limits are relative to the purchase/reference price.

Example:

```text
Reference price: 100
Upper limit: +15%
→ effective limit: 115
```

Negative percentage values are also supported.

### Alarm channels

The following channels can be enabled independently in **Settings**:

- balloon tip
- alarm dialog
- red tray-icon marker
- ntfy push notification

The alarm dialog provides **Snooze (1 cycle)**. If the limit is still reached after the next successful refresh, the alarm may fire again.

## 7. ntfy push notifications

Open:

```text
Action → Settings…
```

and enable ntfy.

Configurable values:

- enabled/disabled
- topic
- server

Default server:

```text
https://ntfy.sh
```

Use **Test** to verify the configuration.

### Privacy note

Use a sufficiently hard-to-guess ntfy topic. When using a public ntfy server, notifications are transmitted through an external service.

**V1.1.4 uses ntfy for outgoing alarm notifications only. Remote command handling is not part of this version.**

## 8. Editing and removing entries

Edit an entry using:

- toolbar **Edit**
- double-click on the row

Remove using:

```text
✕ Remove
```

## 9. Entry context menu

Right-click an entry:

```text
Reload (Refresh)
Copy into new holding
Copy into new watchlist position
Copy into new realized position
```

A new edit dialog opens for the copied entry.

Previously recorded income/dividends are intentionally **not copied automatically**, preventing already realized cash flows from being counted twice.

## 10. Columns and fields – V1.1.4

Each tab has its **own persistent column layout**.

Persisted properties:

- visibility
- order
- width

The default columns and default widths remain unchanged until customized.

### Select fields

Open the field chooser using either:

1. the small `▾` button at the right side of the header area
2. right-click a column header → **Select fields…**

Additional fields can be enabled, including diagnostic/internal information such as:

- Yahoo symbol
- reference FX
- sale FX
- quote timestamp
- last successful fetch
- internal status
- alarm/limit states
- lookup diagnostics

### Hide a column

Right-click a column header:

```text
Hide column
```

At least one column remains visible.

### Hidden-column behavior

A hidden column keeps:

- its stable column ID
- its absolute slot
- its width

If other visible columns are reordered while a column is hidden, the hidden column retains its internal slot. This behavior is intentionally deterministic and robust.

## 11. Sorting

Click a column header to sort by that column.

Date columns are sorted as actual dates rather than lexicographically as text.

## 12. Trend indicators

### Per-security price trend

The trend column shows the direction of consecutive successful quote updates.

Examples:

```text
▲
▲▲
▲▲▲
▲▲▲+
```

The same pattern is used for falling prices.

```text
◀▶
```

means unchanged at the displayed comparison precision.

Trend state is runtime information and is not stored as a historical price series.

### Portfolio trend

The footer also contains a trend indicator for the total market value of open holdings.

## 13. Footer

The footer shows information equivalent to:

```text
Trend | positions | market value EUR | open P/L | realized P/L | total P/L
```

**Market value**  
Open holdings only.

**Open P/L**  
Unrealized price P/L of open holdings.

**Realized P/L**  
Price P/L of realized positions plus manually entered income/dividends.

**Total P/L**  
Open + realized.

## 14. Settings

Open with:

```text
Ctrl+E
```

or:

```text
Action → Settings…
```

Main settings include:

- quote interval in minutes
- data timeout in minutes
- start minimized
- XML data file
- local alarm channels
- ntfy
- watchlist/limit overview

## 15. Start minimized / tray operation

When **Start minimized** is enabled, StockWatcher starts directly in the system tray without initially showing the main window.

Timers and quote refresh continue to run.

Tray menu:

```text
Show app
Refresh now
Exit
```

Double-click the tray icon to show the main window.

## 16. Data storage

Portfolio/watchlist data is stored in XML.

Default:

```text
StockWatcher.xml
```

The selected data-file path is stored locally in:

```text
StockWatcher.ini
```

beside the executable.

The XML file may also be stored in a synchronized folder.

### Backup

The active `StockWatcher.xml` is the primary file to back up.

When the source repository is public, `StockWatcher.xml` and `StockWatcher.ini` should **not** be committed.

## 17. Data-quality considerations

Quotes, FX rates and listing information come from external sources.

Possible issues include:

- delayed quotes
- missing quotes
- temporary network failures
- provider outages
- changed or unavailable symbols

StockWatcher attempts to handle these cases robustly, but it cannot guarantee third-party data quality.

## 18. Version information

On Windows:

```text
StockWatcher.exe
→ Properties
→ Details
```

For this release:

```text
Product version: 1.1.4
File version:    1.1.4.0
```

## 19. Important notice

StockWatcher is a monitoring and information utility. It does not replace broker/depot records, binding transaction statements or independent verification.

Before making financial decisions, independently verify prices, quantities, currencies and calculation inputs.
