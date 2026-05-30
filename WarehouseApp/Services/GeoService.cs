using System.Net.Http;
using System.Text.Json;
using NLog;

namespace WarehouseApp.Services;

public record GeoPoint(string City, double Lat, double Lon);

public record GeoSuggestion(string Display, double Lat, double Lon);

public interface IGeoService
{
    List<GeoSuggestion> Suggest(string query, int limit = 6);
    GeoSuggestion? Geocode(string query);
    int? RoadDistanceKm(double lat1, double lon1, double lat2, double lon2);
    string? ExtractCity(string? address);
    GeoPoint? Resolve(string? city);
    int? DistanceKm(string fromCity, string toCity);
}

public class GeoService : IGeoService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.Add("User-Agent", "WarehouseApp/1.0 (warehouse desktop client)");
        c.DefaultRequestHeaders.Add("Accept-Language", "ru,en");
        return c;
    }

    private static readonly Dictionary<string, GeoPoint> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Москва"] = new("Москва", 55.7558, 37.6173),
        ["Санкт-Петербург"] = new("Санкт-Петербург", 59.9311, 30.3609),
        ["Петербург"] = new("Санкт-Петербург", 59.9311, 30.3609),
        ["Новосибирск"] = new("Новосибирск", 55.0084, 82.9357),
        ["Екатеринбург"] = new("Екатеринбург", 56.8389, 60.6057),
        ["Казань"] = new("Казань", 55.7963, 49.1088),
        ["Нижний Новгород"] = new("Нижний Новгород", 56.2965, 43.9361),
        ["Челябинск"] = new("Челябинск", 55.1644, 61.4368),
        ["Самара"] = new("Самара", 53.1959, 50.1008),
        ["Омск"] = new("Омск", 54.9885, 73.3242),
        ["Ростов-на-Дону"] = new("Ростов-на-Дону", 47.2357, 39.7015),
        ["Уфа"] = new("Уфа", 54.7388, 55.9721),
        ["Красноярск"] = new("Красноярск", 56.0153, 92.8932),
        ["Воронеж"] = new("Воронеж", 51.6720, 39.1843),
        ["Пермь"] = new("Пермь", 58.0105, 56.2502),
        ["Волгоград"] = new("Волгоград", 48.7080, 44.5133),
        ["Краснодар"] = new("Краснодар", 45.0355, 38.9753),
        ["Сочи"] = new("Сочи", 43.5855, 39.7231),
        ["Мурманск"] = new("Мурманск", 68.9585, 33.0827),
        ["Якутск"] = new("Якутск", 62.0355, 129.6755),
        ["Владивосток"] = new("Владивосток", 43.1155, 131.8855),
        ["Калининград"] = new("Калининград", 54.7104, 20.4522),
        ["Тюмень"] = new("Тюмень", 57.1530, 65.5343),
        ["Иркутск"] = new("Иркутск", 52.2870, 104.3050),
    };

    public List<GeoSuggestion> Suggest(string query, int limit = 6)
    {
        var result = new List<GeoSuggestion>();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return result;

        try
        {
            var url = "https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&accept-language=ru"
                + $"&countrycodes=ru&limit={Math.Max(limit, 10)}&q=" + Uri.EscapeDataString(query.Trim());
            var json = Http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!double.TryParse(el.GetProperty("lat").GetString(),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat)) continue;
                if (!double.TryParse(el.GetProperty("lon").GetString(),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon)) continue;

                string label = CityLabel(el);
                if (string.IsNullOrWhiteSpace(label) || !seen.Add(label)) continue;
                result.Add(new GeoSuggestion(label, lat, lon));
            }
        }
        catch (Exception ex)
        {
            logger.Trace(ex, "Подсказки города недоступны для '{Query}'", query);
            foreach (var kv in Cities)
                if (kv.Key.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
                    result.Add(new GeoSuggestion(kv.Value.City, kv.Value.Lat, kv.Value.Lon));
        }
        return result;
    }

    private static string CityLabel(JsonElement el)
    {
        if (!el.TryGetProperty("address", out var addr)) return "";

        string? city = null, region = null;
        foreach (var key in new[] { "city", "town", "village", "municipality", "hamlet" })
            if (addr.TryGetProperty(key, out var v)) { city = v.GetString(); break; }
        if (addr.TryGetProperty("state", out var st)) region = st.GetString();

        if (string.IsNullOrWhiteSpace(city)) return "";
        return !string.IsNullOrWhiteSpace(region) && !region.Equals(city, StringComparison.OrdinalIgnoreCase)
            ? $"{city}, {region}"
            : city;
    }

    public GeoSuggestion? Geocode(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var list = Suggest(query, 1);
        if (list.Count > 0) return list[0];

        var city = ExtractCity(query);
        var p = Resolve(city);
        return p == null ? null : new GeoSuggestion(p.City, p.Lat, p.Lon);
    }

    public int? RoadDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        try
        {
            var url = $"https://router.project-osrm.org/route/v1/driving/"
                + string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1};{2},{3}?overview=false", lon1, lat1, lon2, lat2);
            var json = Http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
            {
                double meters = routes[0].GetProperty("distance").GetDouble();
                return (int)Math.Round(meters / 1000.0);
            }
        }
        catch (Exception ex)
        {
            logger.Trace(ex, "OSRM недоступен, используется геодезическая оценка");
        }
        return (int)Math.Round(Haversine(lat1, lon1, lat2, lon2) * 1.3);
    }

    public string? ExtractCity(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        foreach (var name in Cities.Keys)
            if (address.Contains(name, StringComparison.OrdinalIgnoreCase))
                return Cities[name].City;

        var idx = address.IndexOf("г.", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var tail = address[(idx + 2)..].TrimStart();
            var token = new string(tail.TakeWhile(c => char.IsLetter(c) || c == '-' || c == ' ').ToArray()).Trim();
            var word = token.Split(',', '.', ' ').FirstOrDefault(s => s.Length > 1);
            if (!string.IsNullOrWhiteSpace(word)) return word;
        }

        var first = address.Split(',').FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    public GeoPoint? Resolve(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;
        return Cities.TryGetValue(city.Trim(), out var p) ? p : null;
    }

    public int? DistanceKm(string fromCity, string toCity)
    {
        var a = Resolve(fromCity);
        var b = Resolve(toCity);
        if (a != null && b != null)
            return RoadDistanceKm(a.Lat, a.Lon, b.Lat, b.Lon);

        if (string.IsNullOrWhiteSpace(toCity)) return null;
        var geo = Geocode(toCity);
        if (a != null && geo != null) return RoadDistanceKm(a.Lat, a.Lon, geo.Lat, geo.Lon);
        return null;
    }

    private static string Shorten(string display)
    {
        var parts = display.Split(',').Select(p => p.Trim()).ToList();
        if (parts.Count <= 4) return display;
        return string.Join(", ", parts.Take(4));
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad(lat2 - lat1), dLon = ToRad(lon2 - lon1);
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    internal static int StableHash(string s)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in s) hash = hash * 31 + c;
            return hash;
        }
    }
}
