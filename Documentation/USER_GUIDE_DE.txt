# StockWatcher – Bedienungsanleitung

**Softwareversion:** 1.1.4  
**Dateiversion:** 1.1.4.0

## 1. Zweck

StockWatcher überwacht Wertpapiere, Kaufinteressen und bereits realisierte Positionen. Die Anwendung ruft Kurse ab, rechnet Fremdwährungen bei Bedarf in EUR um, berechnet Positions- und Portfolioinformationen und überwacht konfigurierbare Kurslimits.

StockWatcher ist **kein Handelssystem**. Die Anwendung führt keine Käufe oder Verkäufe aus.

## 2. Hauptfenster

Das Hauptfenster enthält vier Reiter:

- **Übersicht** – kombinierte Ansicht der gewünschten Eintragstypen
- **Bestand** – offene Positionen
- **Kaufinteresse** – beobachtete mögliche Käufe
- **Realisiert** – geschlossene Positionen

Im Reiter **Übersicht** können die drei Typen über Checkboxen ein- und ausgeblendet werden:

- Bestand
- Kaufinteresse
- Realisiert

Die Auswahl wird gespeichert.

## 3. Kursabruf

Ein vollständiger Kursabruf kann ausgelöst werden über:

- **F5**
- Toolbar **Abrufen**
- Menü **Aktion → Jetzt abrufen**
- Tray-Menü **Jetzt abrufen**

Zusätzlich erfolgt der Abruf automatisch im eingestellten Intervall.

Während eines Abrufs zeigt die Statuszeile den aktuellen Fortschritt.

### Netzwerk- und Timeout-Verhalten

Ein einzelner problematischer Abruf darf den gesamten StockWatcher nicht dauerhaft blockieren.

Bei temporären Netzwerk-/Providerproblemen wird der Abruf abgebrochen und nach kurzer Zeit erneut versucht. Danach wird wieder das normale Abrufintervall verwendet.

Der zusätzliche **Daten-Timeout** in den Einstellungen beschreibt, wie lange eine Position ohne erfolgreichen Datenabruf bleiben darf, bevor sie entsprechend als problematisch markiert wird.

## 4. Eintrag hinzufügen

Toolbar:

```text
＋ Hinzufügen
```

Im Dialog zuerst den Typ wählen:

- **Bestand**
- **Kaufinteresse**
- **Realisiert**

Danach die ISIN eingeben.

Mit **Prüfen** wird versucht, das Wertpapier und einen passenden Handelsplatz zu ermitteln. Falls mehrere geeignete Listings gefunden werden, kann ein Handelsplatz ausgewählt werden.

**Abrufen (F5)** aktualisiert den Kurs im Dialog.

### Wichtige Felder

**Name**  
Bezeichnung des Wertpapiers.

**Kurswährung**  
Währung des verwendeten Listings.

**Kurs in EUR umrechnen**  
Der angezeigte bzw. für EUR-Berechnungen verwendete Kurs wird über einen Wechselkurs in EUR umgerechnet. Absolute Limits werden in diesem Fall ebenfalls in EUR bewertet.

**Stückzahl**  
Anzahl der gehaltenen bzw. realisierten Stücke.

**Kauf-/Referenzkurs**  
Kaufpreis oder Vergleichsbasis.

**Währung**  
Währung des Kauf-/Referenzkurses.

**Kauf-/Referenzdatum**  
Optionales Referenzdatum. Format:

```text
dd.MM.yyyy
```

**Erträge/Dividenden**  
Manuell erfasster EUR-Betrag. StockWatcher führt dabei keine Brutto-/Netto- oder Steuerlogik durch.

**Bemerkung**  
Freier Text.

## 5. Eintragstypen

### 5.1 Bestand

Für eine offene Position.

Typischerweise werden gepflegt:

- Stückzahl
- Kauf-/Referenzkurs
- Referenzwährung
- optional Referenzdatum
- Erträge/Dividenden
- Limits

### 5.2 Kaufinteresse

Für ein beobachtetes Wertpapier, das noch nicht als Bestand gehalten wird.

Eine Stückzahl ist nicht erforderlich. Referenzkurs und Limits können genutzt werden, um gewünschte Einstiegsniveaus zu überwachen.

### 5.3 Realisiert

Für eine geschlossene Position.

Zusätzlich werden verwendet:

- Verkaufskurs
- Verkaufswährung
- Verkaufsdatum
- historischer Verkaufs-FX, soweit erforderlich

Realisierte Positionen werden weiterhin kursseitig aktualisiert. Dadurch kann der aktuelle Kurs mit dem damaligen Verkaufskurs verglichen werden.

**Für realisierte Positionen werden keine Kurslimits/Alarme ausgelöst.**

## 6. Limits und Alarme

Für Bestand und Kaufinteresse stehen ein unteres und ein oberes Limit zur Verfügung.

Beide können unabhängig aktiviert werden.

### Absolute Limits

Bei `Kurs in EUR umrechnen`:

```text
aktiv → absolutes Limit in EUR
aus   → absolutes Limit in Kurs-/Listingwährung
```

### Prozent-Limits

Prozent-Limits beziehen sich auf den Kauf-/Referenzkurs.

Beispiel:

```text
Referenzkurs: 100
oberes Limit: +15 %
→ effektives Limit: 115
```

Auch negative Prozentwerte sind möglich.

### Alarmkanäle

Unter **Einstellungen** können unabhängig aktiviert werden:

- Balloon-Tipp
- AlarmDialog
- rote Markierung am Tray-Icon
- ntfy Push-Benachrichtigung

Im AlarmDialog ist **Snooze (1 Zyklus)** möglich. Ist das Limit beim nächsten erfolgreichen Abruf weiterhin verletzt, kann der Alarm erneut ausgelöst werden.

## 7. ntfy Push-Benachrichtigungen

Unter:

```text
Aktion → Einstellungen…
```

kann ntfy aktiviert werden.

Konfigurierbar:

- Aktivierung
- Topic
- Server

Standardserver:

```text
https://ntfy.sh
```

Mit **Testen** kann die Konfiguration geprüft werden.

### Datenschutz-Hinweis

Ein ntfy-Topic sollte ausreichend schwer zu erraten sein. Bei Nutzung eines öffentlichen ntfy-Servers werden Nachrichten über einen externen Dienst übertragen.

**V1.1.4 unterstützt ntfy nur für ausgehende Alarmmeldungen. Remote-Kommandos sind nicht Bestandteil dieser Version.**

## 8. Bearbeiten und Entfernen

Ein Eintrag kann bearbeitet werden über:

- Toolbar **Bearbeiten**
- Doppelklick auf die Zeile

Entfernen erfolgt über:

```text
✕ Entfernen
```

## 9. Kontextmenü eines Eintrags

Rechtsklick auf einen Eintrag:

```text
Neu laden (Refresh / Reload)
Kopiere in neue Bestandsposition
Kopiere in neue Watchlist-Position
Kopiere in neue realisierte Position
```

Beim Kopieren öffnet sich ein neuer Bearbeitungsdialog.

Bereits erfasste Erträge/Dividenden werden bewusst **nicht automatisch kopiert**, damit realisierte Cashflows nicht doppelt gezählt werden.

## 10. Spalten und Felder – V1.1.4

Jeder Reiter besitzt ein **eigenes** gespeichertes Spaltenlayout.

Gespeichert werden:

- Sichtbarkeit
- Reihenfolge
- Breite

Die Default-Spalten und Default-Breiten bleiben zunächst unverändert.

### Felder auswählen

Die Feldauswahl kann auf zwei Wegen geöffnet werden:

1. kleine `▾`-Schaltfläche rechts im Kopfbereich
2. Rechtsklick auf einen Spaltenkopf → **Felder auswählen…**

Dort können zusätzliche Felder eingeblendet werden, darunter auch Diagnose-/interne Felder wie z. B.:

- Yahoo-Symbol
- Referenz-FX
- Verkaufs-FX
- Kurszeitpunkt
- letzter erfolgreicher Abruf
- interner Status
- Alarm-/Limitzustände
- Lookup-Diagnosewerte

### Spalte ausblenden

Rechtsklick auf einen Spaltenkopf:

```text
Spalte ausblenden
```

Mindestens eine Spalte bleibt sichtbar.

### Verhalten ausgeblendeter Spalten

Eine ausgeblendete Spalte behält intern:

- ihre stabile Spalten-ID
- ihren absoluten Slot
- ihre Breite

Werden andere sichtbare Spalten verschoben, während eine Spalte ausgeblendet ist, bleibt deren interner Slot erhalten. Das Verhalten ist bewusst deterministisch und robust.

## 11. Sortieren

Ein Klick auf einen Spaltenkopf sortiert nach der jeweiligen Spalte.

Datumsfelder werden als Datum und nicht lexikografisch als Text sortiert.

## 12. Trendanzeigen

### Kurstrend je Position

Die Trendspalte zeigt die Richtung aufeinanderfolgender erfolgreicher Kursabrufe.

Beispiele:

```text
▲
▲▲
▲▲▲
▲▲▲+
```

Analog für fallende Kurse.

```text
◀▶
```

bedeutet beim Vergleich auf sichtbarer Genauigkeit unverändert.

Der Trend ist Laufzeitinformation und wird nicht dauerhaft als historische Kursserie gespeichert.

### Portfoliotrend

Die Fusszeile enthält zusätzlich einen Trend für den gesamten Marktwert der offenen Bestände.

## 13. Fusszeile

Die Fusszeile zeigt sinngemäss:

```text
Trend | Positionen | Marktwert EUR | offen +/- EUR | realisiert +/- EUR | gesamt +/- EUR
```

**Marktwert**  
Nur offene Bestände.

**offen**  
Unrealisierter Kurs-G/V der offenen Bestände.

**realisiert**  
Realisierter Kurs-G/V der realisierten Positionen plus manuell erfasste Erträge/Dividenden.

**gesamt**  
Offen + realisiert.

## 14. Einstellungen

Öffnen über:

```text
Ctrl+E
```

oder:

```text
Aktion → Einstellungen…
```

Wesentliche Einstellungen:

- Abrufintervall in Minuten
- Daten-Timeout in Minuten
- Starte minimiert
- XML-Datendatei
- lokale Alarmkanäle
- ntfy
- Watchlist-/Limitübersicht

## 15. Start minimiert / Tray

Ist **Starte minimiert** aktiviert, startet StockWatcher direkt im Tray, ohne das Hauptfenster zunächst anzuzeigen.

Kursabrufe und Timer laufen trotzdem.

Tray-Menü:

```text
App anzeigen
Jetzt abrufen
Beenden
```

Doppelklick auf das Tray-Icon öffnet das Hauptfenster.

## 16. Datenspeicherung

Die eigentlichen Daten werden in einer XML-Datei gespeichert.

Standard:

```text
StockWatcher.xml
```

Der Pfad zur Datendatei wird lokal in:

```text
StockWatcher.ini
```

neben der EXE hinterlegt.

Die XML kann auch in einem synchronisierten Verzeichnis liegen.

### Sicherung

Für eine Sicherung ist vor allem die verwendete `StockWatcher.xml` relevant.

Bei einer öffentlichen Source-Code-Ablage sollten `StockWatcher.xml` und `StockWatcher.ini` **nicht eingecheckt** werden.

## 17. Hinweise zur Datenqualität

Kurse, Wechselkurse und Listings stammen aus externen Quellen.

Daher sind möglich:

- verzögerte Kurse
- fehlende Kurse
- temporäre Netzwerkfehler
- Provider-Ausfälle
- geänderte oder nicht mehr verfügbare Symbole

StockWatcher versucht solche Situationen robust zu behandeln, kann aber die Qualität externer Daten nicht garantieren.

## 18. Versionsinformation

Unter Windows:

```text
StockWatcher.exe
→ Eigenschaften
→ Details
```

Für diesen Stand:

```text
Produktversion: 1.1.4
Dateiversion:   1.1.4.0
```

## 19. Wichtiger Hinweis

StockWatcher ist ein persönliches Überwachungs- und Informationswerkzeug. Die Software ersetzt weder Depotdaten noch verbindliche Abrechnungen oder Anlageentscheidungen.

Vor finanziellen Entscheidungen sollten Kurse, Stückzahlen, Währungen und Berechnungsgrundlagen unabhängig geprüft werden.
