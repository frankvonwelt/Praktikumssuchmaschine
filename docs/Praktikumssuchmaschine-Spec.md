# Praktikumssuchmaschine – Spec

## Ziel
Betriebe für ein Praktikum finden, nach Branche filtern und nach Entfernung zu einem festen Ausgangspunkt sortiert auflisten.

## Technik
WPF, .NET Framework 4.8.1, nur Code-Behind (kein MVVM). Keine NuGet-Pakete. Ergebnis: eine einzelne .exe.

## Ausgangspunkt
Fest im Code (`OriginName`, `OriginLat`, `OriginLon` in `MainWindow.xaml.cs`). Aktuell: Motterstraße 3, 90451 Nürnberg (49.4025976, 11.0363841).

## Daten
OpenStreetMap Overpass API (`https://overpass-api.de/api/interpreter`, Fallback `https://overpass.openstreetmap.fr/api/interpreter`), POST, JSON. Timeout 45 s pro Server; bei Fehler/Überlastung wird der nächste Server versucht.
Pro gewählter Branche eine Abfrage, die alle Tags der Branche vereinigt: `nwr[<tag>](around:<meter>,<lat>,<lon>);` mit `out center tags;`.
Nur Elemente mit `name`-Tag. Entfernung per Haversine (Luftlinie).

## Branchen → OSM-Tags (anpassbar in `Industries`)
| Branche | Tags |
|---|---|
| Wirtschaft und Verwaltung (Logistik, Lagerei, Verkauf, Büro) | `office`, `shop`, `industrial=warehouse\|logistics` |
| Farbtechnik und Raumgestaltung | `craft=painter\|plasterer\|floorer\|tiler`, `shop=paint` |
| Holztechnik (Tischler, Schreiner, Küchenbau, Zimmerer, Möbelbau) | `craft=carpenter\|joiner\|cabinet_maker\|sawmill`, `shop=kitchen\|furniture` |
| Metalltechnik & Recycling | `craft=metal_construction\|blacksmith\|welder\|locksmith\|toolmaker\|sheet_metal_worker\|car_repair`, `shop=car_repair\|motorcycle\|motorcycle_repair\|bicycle\|car`, `amenity=recycling\|waste_transfer_station`, `industrial=scrap_yard` |
| Hauswirtschaft und Pflege | `amenity=nursing_home\|kindergarten\|childcare`, `social_facility`, `tourism=hotel\|guest_house\|hostel`, `shop=laundry` |
| Gastronomie | `amenity=restaurant\|cafe\|fast_food\|pub\|biergarten`, `craft=caterer` |
| Garten und Landschaftsbau | `craft=gardener\|paver`, `shop=garden_centre\|florist\|agrarian\|farm`, `landuse=plant_nursery\|greenhouse_horticulture\|farmyard` |

Ein Betrieb erscheint nur einmal (bei der ersten passenden gewählten Branche, Reihenfolge wie in der Tabelle).

Hinweise:
- OSM kennt für Tischler, Schreiner und Zimmerer nur `craft=carpenter`; sie sind nicht unterscheidbar.
- `office` und `shop` (Wirtschaft und Verwaltung) sind sehr breit und liefern viele Treffer.
- Betriebe ohne `name`-Tag werden nicht angezeigt.

## UI (Deutsch)
- Radius in km (Standard 10)
- Maximale Anzahl Ergebnisse (Standard 30), nach Sortierung nach Entfernung abgeschnitten; leer = alle
- Branchen als Checkboxen
- Buttons „Suchen“ und „Abbrechen“ (bricht laufende Suche ab)
- DataGrid: Name, Branche, Adresse, Entfernung (km), aufsteigend sortiert
- Button „Export Excel“: speichert die angezeigten Ergebnisse als .xlsx (Spalten wie im Grid; ohne externe Bibliothek, ZIP+XML in `ExcelExport.cs`)
- Statuszeile (Anzahl Treffer / Fehlermeldung)
