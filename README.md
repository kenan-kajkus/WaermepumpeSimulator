# WP Simulator – Wärmepumpen Jahressimulation & JAZ Rechner

Blazor WebAssembly App zur stündlichen Jahressimulation von Luft-Wasser-Wärmepumpen. Berechnet die Jahresarbeitszahl (JAZ), Taktung, Vereisungsstunden, Heizstabanteil und Betriebskosten auf Basis realer Wetterdaten.

## Features

- **Stündliche Jahressimulation** (8.760 h) mit interpolierten COP-Kennfeldern
- **11 interaktive Diagramme**: Effizienz-Scatter, Dauerlinie, Temperaturverlauf, Leistung & Taktung, Icing Map, Monatsbilanz, Kennlinien-Check (COP, η, Leistung), Härtetest
- **WP-Presets**: Panasonic Aquarea L, Wolf CHA, Vaillant aroTHERM plus, AIRA (IKEA), Lambda – oder eigene Kennfelder eingeben
- **Eigene Modelle**: Benutzerdefinierte Wärmepumpen lokal speichern, umbenennen & löschen
- **Wetterdaten**: Vordefinierte Städte (DE/AT/CH), GPS-Standort via Open-Meteo API, CSV-Upload
- **Mehrjährige Daten**: Jahresauswahl mit vorberechneten Ergebnissen für schnellen Wechsel
- **Vergleichsmodus**: Mehrere Simulationen nebeneinander vergleichen
- **Konfiguration teilen**: Link mit allen Parametern per URL kopieren & versenden
- **Automatisches Update**: Versionsprüfung mit Cache-Clearing bei neuen Deployments
- **Zustandsspeicherung**: Alle Eingaben werden im Browser (localStorage) gespeichert

## Tech Stack

- **Frontend**: Blazor WebAssembly (Standalone, .NET 9)
- **Charts**: [Blazor-ApexCharts](https://github.com/apexcharts/Blazor-ApexCharts) v6.1.0
- **CSS**: Tailwind CSS (CDN), Font Awesome Icons
- **Wetterdaten**: [Open-Meteo API](https://open-meteo.com/)
- **Geocoding**: [Nominatim / OpenStreetMap](https://nominatim.openstreetmap.org/)
- **Hosting**: IIS via GitHub Actions (Self-Hosted Runner)

## Projektstruktur

```
WaermepumpeSimulator/
├── Components/Charts/       # 11 ApexChart-Komponenten
│   ├── EfficiencyChart      # COP vs. Außentemperatur Scatter-Plot
│   ├── DurationChart        # Sortierte Dauerlinie (Load & WP-Leistung)
│   ├── TemperatureChart     # Stündlicher Temperaturverlauf über das Jahr
│   ├── PowerChart           # Thermische Leistung, Heizstab, Taktung
│   ├── DesignChart          # Heizlastkurve, Bivalenzpunkt, WP-Kennlinie
│   ├── IcingMapChart        # Vereisungsrisiko (Temperatur × Feuchte)
│   ├── IcingZoomChart       # Detail-Ansicht kritischer Vereisungsstunden
│   ├── MonthlyChart         # Monatliche Wärme/Strom/Kosten-Balken
│   ├── CheckCopChart        # COP-Kennlinie bei VL35 & VL55 mit Datenpunkten
│   ├── CheckEtaChart        # Carnot-Gütegrad (η) bei VL35 & VL55
│   └── CheckPowerChart      # PMax/PMin-Kurven bei VL35 & VL55
├── Helpers/
│   └── MathHelpers          # Interpolation, Carnot-COP, Taupunkt, Kennfeld-Parser
├── Models/
│   ├── SimulationParameters # Alle Eingabeparameter (Gebäude, Hydraulik, Kennfeld)
│   ├── SimulationResult     # Stündliche Arrays + aggregierte Ergebnisse
│   ├── EvaluationResult     # Ampelbewertung (JAZ, Taktung, Heizstab)
│   ├── ChartData            # Datentypen für die Chart-Komponenten
│   ├── AppState             # Zustandsspeicherung für localStorage
│   └── HeatPumpPreset       # Preset-Modell (Key, Name, Group, Kennfeld)
├── Pages/
│   ├── Home.razor           # Haupt-Simulationsseite mit allen Eingaben & Ergebnissen
│   └── Changelog.razor      # Versionshistorie
├── Services/
│   ├── SimulationEngine     # Kernlogik: Stündliche Simulation (8760h)
│   ├── WeatherDataService   # Open-Meteo API, CSV-Parser, Jahresfilter
│   ├── EvaluationService    # Bewertungslogik & Monatsaggregation
│   └── HeatPumpPresetService # Vordefinierte WP-Kennfelder (15 Modelle)
└── wwwroot/
    ├── index.html           # SPA-Einstiegspunkt mit Versionscheck
    ├── css/app.css          # Eigene Styles
    └── version.txt          # Commit-Hash für automatisches Cache-Update
```

## Simulationsalgorithmus

### Übersicht

Die Simulation berechnet für jede Stunde eines Jahres (8.760 Stunden) die Heizlast, die verfügbare WP-Leistung, den COP und die Energiebilanz. Der Ablauf:

1. **Lookup-Tabelle** – Temperaturraster von -25°C bis +40°C in 0,5°C-Schritten (131 Stützstellen)
2. **Kennfeld-Interpolation** – PMax, PMin und COP werden auf die Lookup-Tabelle interpoliert, jeweils für VL35 und VL55
3. **Lastprofil** – Heizlast pro Kelvin Temperaturdifferenz + Warmwasser-Grundlast
4. **Stündliche Berechnung** – Für jede Stunde: Last, Vorlauftemperatur, verfügbare Leistung, COP, Vereisung, Taktung, Leistungsbilanz
5. **Aggregation** – JAZ, Gesamtstrom, Heizstabanteil, Defizit
6. **Auslegungspunkt** – Bivalenztemperatur, Leistung an NAT

### COP-Berechnung

Der COP wird über den **Carnot-Gütegrad (η)** berechnet:

```
COP = η × COP_Carnot
COP_Carnot = (T_Vorlauf + 273.15) / (T_Vorlauf - T_Quelle)
```

Aus den Herstellerdaten (z.B. COP = 3.41 bei VL35 / AT+2°C) wird η rückgerechnet und als Kurve über die Außentemperatur interpoliert. Zwischen VL35 und VL55 wird linear geblendet basierend auf der aktuellen Vorlauftemperatur.

### Vorlauftemperatur (Heizkurve)

Die Vorlauftemperatur wird linear zwischen `VorlaufMin` (bei Heizgrenze) und `VorlaufMax` (bei Normaußentemperatur) interpoliert:

```
Vorlauf = VorlaufMin + Steigung × (Heizgrenze - Außentemperatur)
Steigung = (VorlaufMax - VorlaufMin) / (Heizgrenze - NAT)
```

Bei aktivierter Nachtabsenkung wird die Vorlauftemperatur und Heizlast in den Nachtstunden reduziert.

### Vereisung

Das Vereisungsmodell berechnet die Verdampfertemperatur unter Berücksichtigung der Lastauslastung:

```
T_Verdampfer = T_Außen - (0.5 + 3.0 × Lastfaktor)
```

Vereisung tritt ein wenn alle Bedingungen erfüllt sind:
- Verdampfertemperatur < -0.5°C
- Verdampfertemperatur < Taupunkt
- Relative Luftfeuchtigkeit > 88%
- Außentemperatur zwischen -4°C und +3°C

Bei Vereisung wird der COP um bis zu 15% reduziert (abhängig vom Lastfaktor).

### Leistungsbilanz

Für jede Stunde wird die thermische Leistungsbilanz aufgestellt:

| Situation | Thermisch | Elektrisch | Heizstab | Defizit |
|---|---|---|---|---|
| Last ≤ WP-Leistung | Last | Last / COP | 0 | 0 |
| Last > WP-Leistung | WP-Max | WP-Max / COP | min(Lücke, HeizstabMax) | Rest |

## Eingabeparameter

### Gebäude & Heizlast

| Parameter | Einheit | Default | Beschreibung |
|---|---|---|---|
| Jahresverbrauch | kWh | 17.000 | Gesamter Heizenergieverbrauch (Heizung + Warmwasser) |
| Wirkungsgrad | – | 0,85 | Bisheriger Wärmeerzeuger (Gas: ~0,85; Öl: ~0,80) |
| Warmwasseranteil | % | 0 | Anteil des Warmwassers am Gesamtverbrauch |
| Heizgrenze | °C | 15 | Außentemperatur ab der nicht mehr geheizt wird |
| Normaußentemperatur (NAT) | °C | -13 | Kältester Auslegungswert für den Standort |
| Raumsolltemperatur | °C | 22 | Gewünschte Raumtemperatur |

### Hydraulik

| Parameter | Einheit | Default | Beschreibung |
|---|---|---|---|
| Vorlauf Max | °C | 34 | Vorlauftemperatur bei NAT (kältester Tag) |
| Vorlauf Min | °C | 30,5 | Vorlauftemperatur bei Heizgrenze |
| Warmwassertemperatur | °C | 50 | Solltemperatur für Warmwasserbereitung |
| Heizstab Max | kW | 9 | Maximale Heizstab-Leistung |

### Nachtabsenkung

| Parameter | Einheit | Default | Beschreibung |
|---|---|---|---|
| Aktiv | – | Nein | Nachtabsenkung ein/ausschalten |
| Start | h | 22:00 | Beginn der Nachtabsenkung |
| Ende | h | 06:00 | Ende der Nachtabsenkung |
| ΔT | K | 5 | Raumtemperatur-Absenkung in der Nacht |

### WP-Kennfeld

Das Kennfeld wird über drei Textfelder definiert:

**PMax** – Maximale thermische Leistung (Außentemperatur, kW):
```
-7, 6.8
2, 7.0
7, 7.0
```

**PMin** – Minimale thermische Leistung (optional, sonst 25% von PMax):
```
-7, 2.4
2, 2.2
7, 2.8
```

**COP** – Leistungszahlen (Vorlauf °C, Außentemperatur °C, COP):
```
35, -7, 2.80
35, 2, 3.41
35, 7, 4.55
55, -7, 2.13
55, 2, 2.41
55, 7, 3.03
```

## Verfügbare WP-Presets

| Hersteller | Modelle |
|---|---|
| Panasonic Aquarea L | 5 kW, 7 kW, 9 kW |
| Wolf | CHA-07, CHA-10 |
| Vaillant aroTHERM plus | VWL35/8.1, VWL55/8.1, VWL75/8.1 |
| AIRA (IKEA) | 6 kW, 8 kW, 12 kW |
| Lambda | EU10L, EU15L, EU20L, EU35L |

Zusätzlich können eigene Wärmepumpen als benutzerdefinierte Presets gespeichert werden. Diese werden im localStorage des Browsers abgelegt und können umbenannt und gelöscht werden.

## Wetterdaten

### Quellen

1. **Vordefinierte Städte**: Hamburg, Berlin, Köln, Frankfurt, München, Hof, Garmisch-Partenkirchen
2. **GPS-Standort**: Browser-Geolocation → Open-Meteo API
3. **CSV-Upload**: Eigene Wetterdaten (Spalten: time/date, temperature/t2m, humidity/rh)

### Open-Meteo API

Stündliche Daten werden über die [Open-Meteo Archive API](https://open-meteo.com/) abgerufen:
- `temperature_2m` – Außentemperatur in 2m Höhe
- `relative_humidity_2m` – Relative Luftfeuchtigkeit in 2m Höhe

### Mehrjährige Daten

Die App lädt standardmäßig mehrere Jahre und bietet:
- Dropdown zur Jahresauswahl
- **"Ø Alle Jahre"** – Stundenmittel über alle verfügbaren Jahre
- Vorberechnung aller Jahre im Hintergrund für sofortigen Wechsel

## Konfiguration teilen

Über den "Teilen"-Button wird die aktuelle Konfiguration als URL-Query-Parameter kodiert. Nur Parameter die vom Default abweichen werden inkludiert. Kurzschlüssel:

| Key | Parameter | Key | Parameter |
|---|---|---|---|
| `jv` | Jahresverbrauch | `vlmax` | Vorlauf Max |
| `wg` | Wirkungsgrad | `vlmin` | Vorlauf Min |
| `wwa` | Warmwasseranteil | `wwt` | Warmwassertemp |
| `hg` | Heizgrenze | `hsm` | Heizstab Max |
| `nat` | Normaußentemperatur | `naa` | Nachtabsenkung aktiv |
| `rst` | Raumsolltemperatur | `ns/ne/ndt` | Nacht Start/Ende/ΔT |
| `ps` | Preis Strom | `pmax` | PMax-Kennfeld |
| `pa` | Preis Alt | `pmin` | PMin-Kennfeld |
| `city` | Standort | `cop` | COP-Kennfeld |
| `preset` | WP-Preset | `gn/glat/glon` | Geo Name/Lat/Lon |

## Diagramme

| # | Diagramm | Beschreibung |
|---|---|---|
| 1 | **Effizienz-Scatter** | COP jeder Stunde vs. Außentemperatur, farbkodiert |
| 2 | **Dauerlinie** | Sortierte Heizlast und WP-Leistung über alle Stunden |
| 3 | **Temperaturverlauf** | Stündliche Außentemperatur über das gesamte Jahr |
| 4 | **Leistung & Taktung** | Thermische Leistung, Heizstab und Taktungsstunden |
| 5 | **Auslegung (Härtetest)** | Heizlastkurve, WP-Leistungskurve, Bivalenzpunkt |
| 6 | **Icing Map** | Vereisungsrisiko als Temperatur × Feuchte-Matrix |
| 7 | **Icing Zoom** | Detailansicht der kritischsten Vereisungsstunden |
| 8 | **Monatsbilanz** | Monatliche Wärme, Strom und Kosten als Balken |
| 9 | **COP-Kennlinie** | Berechnete COP-Kurve bei VL35/VL55 mit Herstellerdaten |
| 10 | **η-Kennlinie** | Carnot-Gütegrad bei VL35/VL55 |
| 11 | **Leistungs-Kennlinie** | PMax/PMin-Kurven bei VL35/VL55 |

## Bewertungslogik

Die Simulationsergebnisse werden automatisch mit einer Ampelbewertung versehen:

**JAZ (Jahresarbeitszahl)**:
- 🔴 < 3,0 – Nicht gut (Vorlauftemperatur prüfen)
- 🟡 3,0–3,5 – In Ordnung, Optimierungspotenzial
- 🟢 3,5–4,0 – Gut, effizient
- 🔵 > 4,0 – Hervorragend

**Taktung**:
- 🟢 < 30% – WP moduliert gut
- 🟠 30–50% – Erhöht, häufiges Takten
- 🔴 > 50% – Überdimensioniert

**Auslegung / Heizstab**:
- 🔴 Bivalenzpunkt > -2°C – Unterdimensioniert
- 🔴 Heizstabanteil > 5% – Zu hoher Heizstab-Verbrauch
- 🟠 Bivalenzpunkt > -5°C – Knapp bemessen
- 🟢 Sonst – Sehr gute Auslegung

## Lokale Entwicklung

```bash
# Voraussetzungen: .NET 9 SDK
dotnet run --project WaermepumpeSimulator
```

Die App startet unter `https://localhost:5001` (oder dem konfigurierten Port).

## Docker

Die App kann als Docker-Container mit nginx als Webserver betrieben werden.

### Build & Run

```bash
# Image bauen (aus dem Projektverzeichnis)
docker build -t wp-simulator ./WaermepumpeSimulator

# Container starten
docker run -d -p 8080:80 --name wp-simulator wp-simulator
```

Die App ist dann unter `http://localhost:8080` erreichbar.

### Architektur

Das Dockerfile nutzt einen **Multi-Stage Build**:

1. **Build-Stage** (`dotnet/sdk:9.0`) – Kompiliert die Blazor WebAssembly App mit `dotnet publish`
2. **Runtime-Stage** (`nginx:alpine`) – Kopiert nur die statischen `wwwroot`-Dateien in den nginx-Container

Das Ergebnis ist ein schlankes Image (~30 MB), da nur nginx und die statischen Dateien (HTML, JS, WASM, DLLs) enthalten sind.

### nginx-Konfiguration

Die mitgelieferte `nginx.conf` konfiguriert:
- **SPA-Routing**: Alle Routen werden auf `index.html` umgeleitet (`try_files $uri $uri/ /index.html`)
- **MIME-Types**: `.wasm` und `.dll` werden korrekt ausgeliefert
- **gzip-Kompression**: Für WASM, JS, JSON, CSS und HTML aktiviert

### docker-compose (optional)

```yaml
services:
  wp-simulator:
    build: ./WaermepumpeSimulator
    ports:
      - "8080:80"
    restart: unless-stopped
```

```bash
docker compose up -d
```

## Deployment

Das Deployment erfolgt automatisch über GitHub Actions bei Push auf `master`:

1. `dotnet publish` erstellt die Release-Artefakte
2. Commit-Hash wird in `version.txt` geschrieben
3. `wwwroot/`-Dateien werden nach `C:\inetpub\wwwroot\waermepumpe` kopiert

### Automatisches Cache-Update

Die App prüft bei jedem Laden `version.txt` gegen den im `localStorage` gespeicherten Hash. Bei Abweichung werden alle Caches gelöscht und die Seite neu geladen. Im Entwicklungsmodus (`version.txt` = `dev`) wird der Cache einmalig pro Tab-Session geleert.

## Lizenz

Privates Projekt.
