using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace Praktikumssuchmaschine
{
    public partial class MainWindow : Window
    {
        // Ausgangspunkt (Koordinaten via OpenStreetMap Nominatim ermittelt)
        private const string OriginName = "Motterstraße 3, 90451 Nürnberg";
        private const double OriginLat = 49.4025976;
        private const double OriginLon = 11.0363841;

        // Öffentliche Overpass-Server; bei Fehler (z.B. 504) wird der nächste versucht
        private static readonly string[] OverpassUrls =
        {
            "https://overpass-api.de/api/interpreter",
            "https://overpass.openstreetmap.fr/api/interpreter",
        };

        // Branche -> OSM-Filter (Tag-Ausdrücke in Overpass-Syntax)
        private static readonly Dictionary<string, string[]> Industries = new Dictionary<string, string[]>
        {
            { "Wirtschaft und Verwaltung", new[] { "\"office\"", "\"shop\"", "\"industrial\"=\"warehouse\"", "\"industrial\"=\"logistics\"", "\"office\"=\"logistics\"" } },
            { "Farbtechnik und Raumgestaltung", new[] { "\"craft\"=\"painter\"", "\"craft\"=\"plasterer\"", "\"craft\"=\"floorer\"", "\"craft\"=\"tiler\"", "\"shop\"=\"paint\"" } },
            { "Holztechnik", new[] { "\"craft\"=\"carpenter\"", "\"craft\"=\"joiner\"", "\"craft\"=\"cabinet_maker\"", "\"craft\"=\"sawmill\"", "\"shop\"=\"kitchen\"", "\"shop\"=\"furniture\"" } },
            { "Metalltechnik & Recycling", new[] { "\"craft\"=\"metal_construction\"", "\"craft\"=\"blacksmith\"", "\"craft\"=\"welder\"", "\"craft\"=\"locksmith\"", "\"craft\"=\"toolmaker\"", "\"craft\"=\"sheet_metal_worker\"", "\"craft\"=\"car_repair\"", "\"shop\"=\"car_repair\"", "\"shop\"=\"motorcycle\"", "\"shop\"=\"motorcycle_repair\"", "\"shop\"=\"bicycle\"", "\"shop\"=\"car\"", "\"amenity\"=\"recycling\"", "\"amenity\"=\"waste_transfer_station\"", "\"industrial\"=\"scrap_yard\"" } },
            { "Hauswirtschaft und Pflege", new[] { "\"amenity\"=\"nursing_home\"", "\"social_facility\"", "\"amenity\"=\"kindergarten\"", "\"amenity\"=\"childcare\"", "\"tourism\"=\"hotel\"", "\"tourism\"=\"guest_house\"", "\"tourism\"=\"hostel\"", "\"shop\"=\"laundry\"" } },
            { "Gastronomie", new[] { "\"amenity\"=\"restaurant\"", "\"amenity\"=\"cafe\"", "\"amenity\"=\"fast_food\"", "\"amenity\"=\"pub\"", "\"amenity\"=\"biergarten\"", "\"craft\"=\"caterer\"" } },
            { "Garten und Landschaftsbau", new[] { "\"craft\"=\"gardener\"", "\"craft\"=\"paver\"", "\"shop\"=\"garden_centre\"", "\"shop\"=\"florist\"", "\"shop\"=\"agrarian\"", "\"landuse\"=\"plant_nursery\"", "\"landuse\"=\"greenhouse_horticulture\"", "\"landuse\"=\"farmyard\"", "\"shop\"=\"farm\"" } },
        };

        private static readonly HttpClient Http = new HttpClient();
        private CancellationTokenSource cts;
        private List<Company> lastResults;

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Http.Timeout = TimeSpan.FromSeconds(45);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Praktikumssuchmaschine/1.0");

            OriginText.Text = "Ausgangspunkt: " + OriginName;
            foreach (var industry in Industries.Keys)
            {
                IndustryPanel.Children.Add(new CheckBox { Content = industry, Margin = new Thickness(0, 0, 16, 0) });
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            double radiusKm;
            if (!double.TryParse(RadiusBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out radiusKm) || radiusKm <= 0)
            {
                StatusText.Text = "Bitte einen gültigen Umkreis in km eingeben.";
                return;
            }

            // Leer = alle Ergebnisse anzeigen
            int maxResults = int.MaxValue;
            if (MaxResultsBox.Text.Trim().Length > 0 && (!int.TryParse(MaxResultsBox.Text, out maxResults) || maxResults <= 0))
            {
                StatusText.Text = "Bitte eine gültige maximale Anzahl eingeben.";
                return;
            }

            var selected = IndustryPanel.Children.OfType<CheckBox>()
                .Where(c => c.IsChecked == true)
                .Select(c => (string)c.Content)
                .ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "Bitte mindestens eine Branche wählen.";
                return;
            }

            SearchButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            StatusText.Text = "Suche läuft...";
            ResultGrid.ItemsSource = null;
            lastResults = null;
            ExportButton.IsEnabled = false;
            cts = new CancellationTokenSource();

            try
            {
                var companies = new List<Company>();
                var seen = new HashSet<string>();
                foreach (var industry in selected)
                {
                    var json = await QueryOverpass(Industries[industry], radiusKm * 1000, cts.Token);
                    ParseResults(json, industry, companies, seen);
                }

                var sorted = companies.OrderBy(c => c.DistanceKm).Take(maxResults).ToList();
                ResultGrid.ItemsSource = sorted;
                lastResults = sorted;
                ExportButton.IsEnabled = sorted.Count > 0;
                StatusText.Text = sorted.Count + " von " + companies.Count + " Betrieben angezeigt.";
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                StatusText.Text = "Suche abgebrochen.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Fehler: " + ex.Message;
            }
            finally
            {
                SearchButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel-Datei (*.xlsx)|*.xlsx",
                FileName = "Praktikumsbetriebe.xlsx"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                ExcelExport.Save(dialog.FileName, lastResults);
                StatusText.Text = "Exportiert nach " + dialog.FileName;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Export fehlgeschlagen: " + ex.Message;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null) cts.Cancel();
        }

        private static async Task<string> QueryOverpass(string[] filters, double radiusMeters, CancellationToken token)
        {
            var around = string.Format(CultureInfo.InvariantCulture, "(around:{0:0},{1},{2})", radiusMeters, OriginLat, OriginLon);
            // Kein ["name"]-Filter in der Abfrage: macht Overpass deutlich langsamer. Namenlose werden beim Auslesen übersprungen.
            var body = "[out:json][timeout:60];(" + string.Concat(filters.Select(f => "nwr[" + f + "]" + around + ";")) + ");out center tags;";
            var content = new Dictionary<string, string> { { "data", body } };

            Exception lastError = null;
            foreach (var url in OverpassUrls)
            {
                try
                {
                    var response = await Http.PostAsync(url, new FormUrlEncodedContent(content), token);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();

                    // Überlasteter Server: Antwort ist leer und enthält nur eine "remark" (z.B. "runtime error")
                    if (json.Contains("\"remark\""))
                    {
                        throw new HttpRequestException("Der Server ist überlastet (Zeitüberschreitung).");
                    }
                    return json;
                }
                // Abbruch durch den Benutzer nicht abfangen, Timeouts schon (nächster Server)
                catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !token.IsCancellationRequested)
                {
                    lastError = ex;
                }
            }
            throw new Exception("Kein Overpass-Server hat geantwortet (überlastet oder Zeitüberschreitung). Bitte später erneut versuchen. " + lastError.Message, lastError);
        }

        private static void ParseResults(string json, string industry, List<Company> companies, HashSet<string> seen)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = (Dictionary<string, object>)serializer.DeserializeObject(json);

            foreach (Dictionary<string, object> element in (object[])root["elements"])
            {
                if (!element.ContainsKey("tags")) continue;
                var tags = (Dictionary<string, object>)element["tags"];
                if (!tags.ContainsKey("name")) continue;

                // Nodes haben lat/lon direkt, Ways/Relations unter "center"
                var pos = element.ContainsKey("center") ? (Dictionary<string, object>)element["center"] : element;
                if (!pos.ContainsKey("lat")) continue;

                // Gleicher Betrieb nicht doppelt (auch über mehrere Branchen hinweg)
                if (!seen.Add(element["type"] + "/" + element["id"])) continue;

                var lat = Convert.ToDouble(pos["lat"], CultureInfo.InvariantCulture);
                var lon = Convert.ToDouble(pos["lon"], CultureInfo.InvariantCulture);

                companies.Add(new Company
                {
                    Name = (string)tags["name"],
                    Industry = industry,
                    Address = BuildAddress(tags),
                    DistanceKm = DistanceKm(OriginLat, OriginLon, lat, lon)
                });
            }
        }

        private static string BuildAddress(Dictionary<string, object> tags)
        {
            Func<string, string> get = key => tags.ContainsKey(key) ? (string)tags[key] : "";
            var street = (get("addr:street") + " " + get("addr:housenumber")).Trim();
            var city = (get("addr:postcode") + " " + get("addr:city")).Trim();
            return string.Join(", ", new[] { street, city }.Where(s => s.Length > 0));
        }

        // Luftlinie (Haversine)
        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            Func<double, double> rad = d => d * Math.PI / 180.0;
            var dLat = rad(lat2 - lat1);
            var dLon = rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(rad(lat1)) * Math.Cos(rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
