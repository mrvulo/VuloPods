# VuloPods v1 – Design

Datum: 2026-09-13
Status: vom Auftraggeber freigegeben (Abschnitte 1–3 im Gespräch bestätigt)

## Ziel

VuloPods ist eine Windows-Tray-App für Apple AirPods (alle Modelle bis AirPods Pro 3),
die ohne Kernel-Treiber auskommt:

- Akkustand von linkem Pod, rechtem Pod und Case (10%-Schritte) im Tray-Tooltip
- Popup beim Öffnen des Case-Deckels
- Ohr-Erkennung mit Auto-Pause / Auto-Play der Windows-Medienwiedergabe

Der Geräuschmodus (ANC / Transparenz / Adaptiv) ist **nicht** Teil von v1. Er
braucht eine L2CAP-Verbindung (Apple Accessory Protocol, PSM 0x1001), die Windows
nur Kernel-Treibern gibt. v1 bereitet dafür lediglich eine Schnittstelle vor
(siehe `IAapTransport`); der Treiber ist Teilprojekt 2 mit eigener Spezifikation.

## Nicht-Ziele (v1)

- Geräuschmodus umschalten, 1%-Akkuwerte, AAP-Protokollcode
- Einstellungsfenster, Autostart, Akku-Warnungen
- Mehrere Geräte gleichzeitig anzeigen (es wird immer genau eines gezeigt)
- Installer / Signierung

## Tech-Stack

- .NET 8, C#, Zielframework `net8.0-windows10.0.19041.0` (für WinRT-APIs)
- WPF für Tray und Popup, Tray-Icon über das NuGet-Paket `H.NotifyIcon.Wpf`
- WinRT `Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher`
  für den LE-Scan
- WinRT `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`
  für Pause/Play
- xUnit für Tests

## Solution-Struktur

```
VuloPods.sln
  src/VuloPods.Core/      Klassenbibliothek, keine UI-Abhängigkeit
  src/VuloPods.App/       WPF-App (Tray, Popup, Verdrahtung)
  tests/VuloPods.Tests/   xUnit
```

### VuloPods.Core

| Baustein | Aufgabe | Abhängigkeiten |
|---|---|---|
| `ProximityPairingParser` | Bytes des Apple-Herstellerpakets (Company 0x004C, Typ 0x07, 27 Byte) → `AirPodsAdvertisement` | keine (reine Funktion) |
| `AirPodsAdvertisement` | Record: `Model`, `LeftBattery`, `RightBattery`, `CaseBattery` (0–100 oder null), `LeftCharging`, `RightCharging`, `CaseCharging`, `LeftInEar`, `RightInEar`, `LidOpen`, `LidOpenCounter`, `ModelId` (ushort) | keine |
| `AirPodsModel` | Enum + Zuordnung `ModelId → Name` (AirPods 1/2/3/4, Pro 1/2/3, Max, Unknown) | keine |
| `AirPodsScanner` | Startet/stoppt den `BluetoothLEAdvertisementWatcher` (ActiveScanning), filtert Company 0x004C, ruft Parser, meldet `(AirPodsAdvertisement, rssi, timestamp)` | WinRT, Parser |
| `AirPodsStateStore` | Hält `AirPodsState` (aktuelles Gerät). Nimmt Scanner-Meldungen entgegen, wendet Geräteauswahl und Timeout an, feuert Ereignisse | Scanner (per Interface `IAirPodsSource`) |
| `MediaController` | `PauseIfPlayingAsync()` / `ResumeIfWePausedAsync()` über die Windows-Mediensitzung | WinRT |
| `AutoPausePolicy` | Reine Logik: aus Ohr-Übergängen Pause/Resume-Entscheidungen ableiten, mit 1 s Entprellung | `IMediaController` |
| `IAapTransport`, `NoiseControlMode` | Vorbereitete Schnittstelle für Teilprojekt 2. v1: `UnavailableAapTransport` meldet `IsAvailable = false` | keine |
| `Log` | Datei-Logger `%LocalAppData%\VuloPods\log.txt`, max. 1 MB, eine Rotation (`log.1.txt`) | keine |

Ereignisse des `AirPodsStateStore`:

- `DeviceAppeared(AirPodsState)` – erstes Paket eines gültigen Geräts
- `StateChanged(AirPodsState)` – jede Änderung von Akku/Laden/Ohr/Deckel
- `LidOpened(AirPodsState)` – Deckelbit wechselt von zu auf offen **und** der Lid-Counter ist neu
- `EarStateChanged(AirPodsState, previous)` – linker oder rechter In-Ear-Status wechselt
- `DeviceLost()` – 30 s ohne Paket

### VuloPods.App

- `App.xaml.cs`: Startup ohne Hauptfenster, erstellt Core-Objekte, verdrahtet Ereignisse, Single-Instance über Mutex
- `TrayIcon` (H.NotifyIcon): Tooltip `"<Modell>  L 80%  R 90%  Case 60%"` bzw. `"VuloPods – kein Gerät"`; Kontextmenü: *Anzeigen*, *Beenden*; Linksklick zeigt Popup
- `PopupWindow`: rahmenlos, `ShowInTaskbar=false`, `Topmost`, Position unten rechts über der Taskleiste (Arbeitsbereich des primären Monitors), dunkles Design, Modellname, drei Akkubalken mit Ladeblitz-Symbol, Ohr-Markierung. Auto-Hide nach 5 s, Ausblenden per Fade; Klick außerhalb (Deactivated) schließt sofort
- Kein Settings-UI, keine Persistenz

### VuloPods.Tests

- Parser: bekannte Pakete aus librepods-/Continuity-Doku, plus von den eigenen Geräten aufgezeichnete Pakete; ungültige Längen/Typen → null
- StateStore: LidOpened feuert genau einmal pro Counter; Gerätewechsel nur bei stärkerem Signal (siehe Regel); DeviceLost nach 30 s (Zeit injizierbar)
- AutoPausePolicy mit Fake-`IMediaController`: pausiert nur wenn Wiedergabe lief; setzt nur fort wenn selbst pausiert; Entprellung

## Bluetooth-Details

### Paketformat (Proximity Pairing, Typ 0x07)

Herstellerdaten: `07 19 <payload 25 Byte>`. Relevante Offsets (ab Beginn der Herstellerdaten, Byte 0 = 0x07):

| Byte | Inhalt |
|---|---|
| 3–4 | Modell-ID (Big Endian), z. B. 0x0E20 = AirPods Pro 1, 0x1420 = AirPods Pro 2, 0x2420 = AirPods Pro 2 USB-C, 0x0220 = AirPods 1, 0x0F20 = AirPods 2, 0x1320 = AirPods 3, 0x1920 = AirPods 4, 0x1B20 = AirPods 4 ANC, 0x0A20 = AirPods Max, 0x2720 = AirPods Pro 3. Die Tabelle wird beim Aufzeichnen der eigenen Geräte verifiziert und ergänzt |
| 5 | Status-Bits: Bit 5 = „dieser Pod ist primär = links“, In-Ear-Bits für primär/sekundär, „Pod im Case“ |
| 6 | Akku: oberes Nibble rechter Pod, unteres Nibble linker Pod (aus Sicht der Primär-Seite; wird per Bit 5 gespiegelt). 0–9 → 0–90 %, 0xA–0xE → 100 %, 0xF → unbekannt |
| 7 | oberes Nibble: Lade-Flags (Bit 4 rechts, Bit 5 links, Bit 6 Case); unteres Nibble: Case-Akku wie oben |
| 8 | Deckel: Bit 3 = offen, Bits 0–2 = Lid-Open-Counter; gültig nur, wenn Pods im Case |
| 11–26 | verschlüsselt, wird ignoriert |

Die genaue Bit-Zuordnung von Byte 5 wird bei der Implementierung gegen die librepods-Doku
und aufgezeichnete Pakete abgeglichen; Tests fixieren das Ergebnis.

### Geräteauswahl

- Kandidat = jedes gültige Paket mit Modell-ID, RSSI ≥ −75 dBm
- Solange kein aktives Gerät: erster Kandidat wird aktiv
- Anderes Gerät übernimmt nur, wenn es in 5 aufeinanderfolgenden Paketen jeweils
  mindestens 10 dBm stärker ist als das zuletzt vom aktiven Gerät gemessene RSSI
- Pakete mit demselben Modell wie das aktive Gerät gelten als das aktive Gerät
  (Adressrotation wird so überbrückt)
- 30 s ohne Paket vom aktiven Gerät → `DeviceLost`, Auswahl beginnt von vorn

### Deckel-Ereignis

`LidOpened` feuert, wenn `LidOpen` von false auf true wechselt **oder** der
`LidOpenCounter` sich bei offenem Deckel ändert. Identische Pakete lösen nichts aus.

### Auto-Pause / Auto-Play

- Übergang „ein Pod im Ohr → dieser Pod nicht im Ohr“ → nach 1 s Entprellung
  `PauseIfPlayingAsync()`; merkt `wePaused = true`, wenn tatsächlich pausiert wurde
- Übergang „Pod zurück ins Ohr“ (mindestens ein Pod im Ohr) → nach 1 s
  `ResumeIfWePausedAsync()`; setzt `wePaused = false`
- Wechselt der Status innerhalb der Entprellzeit zurück, passiert nichts
- Läuft keine Mediensitzung, passiert nichts

## Fehlerbehandlung

- Kein Adapter / Bluetooth aus (`Watcher.Status == Aborted` oder Fehler beim Start):
  Tooltip „VuloPods – Bluetooth nicht verfügbar“, Neuversuch alle 10 s
- Unbekannte Modell-ID: Anzeige als „AirPods (unbekannt)“ mit allen Werten,
  Modell-ID einmalig ins Log
- Parser gibt bei falscher Länge/Typ `null` zurück; Scanner fängt alle Ausnahmen
  pro Paket ab und loggt sie
- Unbehandelte Ausnahmen im UI-Thread werden geloggt, die App läuft weiter
  (`DispatcherUnhandledException`)
- Log: `%LocalAppData%\VuloPods\log.txt`, Rotation bei 1 MB

## Offene Punkte für Teilprojekt 2 (nicht v1)

- KMDF-L2CAP-Profiltreiber (Referenzen: Microsoft „Bluetooth Echo L2CAP“-Sample,
  WinPods `WinPodsAAP.c`), Signierung (EV-Zertifikat + Attestierung) oder Testsigning
- `IAapTransport`-Implementierung über den Treiber, AAP-Handshake, Noise-Control-Pakete,
  1%-Akku, AAP-Ohr-Erkennung
