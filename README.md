<p align="center"><img src="docs/assets/logo.png" alt="VuloPods" width="160"></p>

# VuloPods

Windows-Tray-App für Apple AirPods: Akkustand, Deckel-Popup, Ohr-Erkennung mit Auto-Pause. Ohne Kernel-Treiber.

## Funktionen

- Akkustand von linkem Pod, rechtem Pod und Case im Tray-Tooltip (10%-Schritte)
- Popup beim Öffnen des Case-Deckels, Linksklick aufs Tray-Icon zeigt es jederzeit
- Medien pausieren, wenn ein Pod aus dem Ohr genommen wird; weiterspielen beim Einsetzen
- Autostart-Schalter im Tray-Menü („Mit Windows starten“)
- Akku-Warnung unter 20 % als Windows-Toast (nicht im Vollbild)
- Popup ohne Fokusklau; ein Klick daneben schließt es
- Helles/dunkles Design folgt der Windows-Einstellung
- Diagnose-Export als ZIP (Tray-Menü „Diagnose exportieren…“): Logs, letzte Rohpakete, Systeminfo
- Erkennung von AirPods 5, AirPods Max (USB-C) und Beats Fit Pro

Nicht enthalten: Geräuschmodus (ANC/Transparenz). Der braucht unter Windows einen Kernel-Treiber und ist als Teilprojekt 2 geplant.

## Installieren

Voraussetzungen: Windows 10 2004 oder neuer, .NET 8 SDK, Bluetooth-LE-Adapter.

```bash
powershell -ExecutionPolicy Bypass -File scripts/install.ps1
```

Das Skript baut die Release-Version, installiert sie nach `%LocalAppData%\Programs\VuloPods`, legt Verknüpfungen im Startmenü und auf dem Desktop an und startet VuloPods. Erneut ausführen aktualisiert die Installation. Entfernen:

```bash
powershell -ExecutionPolicy Bypass -File scripts/uninstall.ps1
```

## Bauen und starten (Entwicklung)

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/VuloPods.App
```

Die AirPods müssen nicht gekoppelt sein; sie müssen nur in Reichweite sein.

## Diagnose

Log: `%LocalAppData%\VuloPods\log.txt`. Mit `--dump` schreibt die App jedes empfangene AirPods-Paket als Hex ins Log:

```bash
dotnet run --project src/VuloPods.App -- --dump
```

## Projektstruktur

- `src/VuloPods.Core` – Parser, Zustandslogik, Auto-Pause, WinRT-Anbindung
- `src/VuloPods.App` – WPF: Tray-Icon, Popup
- `tests/VuloPods.Tests` – xUnit
- `docs/superpowers/specs` – Design, `docs/superpowers/plans` – Implementierungsplan

## Lizenz

Lizenz: MIT (siehe `LICENSE`)
