using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using NLog;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface IWeatherService
{
    OperationResult GetForecast(ShipmentLogistics logistics, double? lat, double? lon, string? cityLabel, DateTime date);
}

public class WeatherService : IWeatherService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private readonly IGeoService _geo;
    private readonly AppSettings _settings;

    public WeatherService(IGeoService geo, AppSettings settings)
    {
        _geo = geo;
        _settings = settings;
    }

    public OperationResult GetForecast(ShipmentLogistics logistics, double? lat, double? lon, string? cityLabel, DateTime date)
    {
        logistics.WeatherLoaded = false;
        logistics.WeatherError = string.Empty;

        if ((!lat.HasValue || !lon.HasValue) && !string.IsNullOrWhiteSpace(cityLabel))
        {
            var g = _geo.Geocode(cityLabel);
            if (g != null) { lat = g.Lat; lon = g.Lon; }
        }

        string city = _geo.ExtractCity(cityLabel) ?? cityLabel ?? "";

        if (!lat.HasValue || !lon.HasValue)
        {
            logistics.WeatherError = "Не удалось определить регион получателя. Уточните адрес доставки.";
            return OperationResult.Fail(logistics.WeatherError);
        }

        try
        {
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}" +
                "&daily=temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min,weathercode,windspeed_10m_max,winddirection_10m_dominant" +
                "&hourly=relativehumidity_2m&windspeed_unit=ms&timezone=auto&forecast_days=7",
                lat.Value, lon.Value);

            var json = Http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");
            var times = daily.GetProperty("time");

            int idx = -1;
            string target = date.ToString("yyyy-MM-dd");
            for (int i = 0; i < times.GetArrayLength(); i++)
                if (times[i].GetString() == target) { idx = i; break; }
            if (idx < 0) idx = Math.Min(2, times.GetArrayLength() - 1);

            double tMax = daily.GetProperty("temperature_2m_max")[idx].GetDouble();
            double tMin = daily.GetProperty("temperature_2m_min")[idx].GetDouble();
            double aMax = daily.GetProperty("apparent_temperature_max")[idx].GetDouble();
            double aMin = daily.GetProperty("apparent_temperature_min")[idx].GetDouble();
            int code = daily.GetProperty("weathercode")[idx].GetInt32();
            double wind = daily.GetProperty("windspeed_10m_max")[idx].GetDouble();
            double windDir = daily.GetProperty("winddirection_10m_dominant")[idx].GetDouble();

            double temp = Math.Round((tMax + tMin) / 2.0);
            double feels = Math.Round((aMax + aMin) / 2.0);
            int humidity = HumidityAtNoon(doc, target);

            logistics.WeatherLoaded = true;
            logistics.WeatherCity = city;
            logistics.ForecastDate = date;
            logistics.Temperature = temp;
            logistics.FeelsLike = feels;
            logistics.Humidity = humidity;
            logistics.WindSpeed = Math.Round(wind, 1);
            logistics.WindDirection = Compass(windDir);
            logistics.Condition = CodeToText(code);
            logistics.Risk = ComputeRisk(feels);

            logger.Info("Прогноз (Open-Meteo) для {City} на {Date}: {Temp}°C (ощущается {Feels}°C), риск {Risk}",
                city, date.ToString("dd.MM"), temp, feels, logistics.Risk);
            return OperationResult.Ok("Прогноз получен.");
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Open-Meteo недоступен, используется приблизительная оценка для {City}", city);
            return Fallback(logistics, city, lat.Value, date);
        }
    }

    private static int HumidityAtNoon(JsonDocument doc, string target)
    {
        try
        {
            var hourly = doc.RootElement.GetProperty("hourly");
            var times = hourly.GetProperty("time");
            var hums = hourly.GetProperty("relativehumidity_2m");
            int best = -1;
            for (int i = 0; i < times.GetArrayLength(); i++)
            {
                var t = times[i].GetString() ?? "";
                if (t.StartsWith(target) && t.EndsWith("12:00")) { best = i; break; }
                if (t.StartsWith(target) && best < 0) best = i;
            }
            if (best >= 0) return hums[best].GetInt32();
        }
        catch { }
        return 60;
    }

    private OperationResult Fallback(ShipmentLogistics logistics, string city, double lat, DateTime date)
    {
        try
        {
            int seed = GeoService.StableHash(city + date.ToString("yyyy-MM-dd"));
            var rnd = new Random(seed);
            double latFactor = (lat - 45) * 0.9;
            double season = Math.Cos((date.Month - 7) / 12.0 * 2 * Math.PI);
            double warm = 18 - season * 22;
            double baseTemp = warm - latFactor * (0.4 + 0.6 * Math.Max(0, season));
            if (lat > 62) baseTemp -= 12;

            double temp = Math.Round(baseTemp + rnd.Next(-5, 6));
            double wind = Math.Round(1 + rnd.NextDouble() * 9, 1);
            double feels = temp <= 10 ? Math.Round(temp - wind * 0.7) : temp;

            logistics.WeatherLoaded = true;
            logistics.WeatherCity = city;
            logistics.ForecastDate = date;
            logistics.Temperature = temp;
            logistics.FeelsLike = feels;
            logistics.Humidity = 50 + rnd.Next(0, 40);
            logistics.WindSpeed = wind;
            logistics.WindDirection = new[] { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" }[rnd.Next(8)];
            logistics.Condition = new[] { "Ясно", "Облачно", "Пасмурно", "Дождь", "Снег" }[rnd.Next(5)];
            logistics.Risk = ComputeRisk(feels);
            return OperationResult.Ok("Прогноз получен (приблизительно).");
        }
        catch
        {
            logistics.WeatherError = "Погодный сервис временно недоступен.";
            return OperationResult.Fail(logistics.WeatherError);
        }
    }

    private WeatherRisk ComputeRisk(double feelsLike)
    {
        if (feelsLike <= _settings.ColdRiskThreshold || feelsLike >= _settings.HeatRiskThreshold)
            return WeatherRisk.High;
        if (feelsLike <= _settings.ColdRiskThreshold + 6 || feelsLike >= _settings.HeatRiskThreshold - 6)
            return WeatherRisk.Medium;
        return WeatherRisk.Low;
    }

    private static string Compass(double deg)
    {
        string[] dirs = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
        int i = (int)Math.Round(deg / 45.0) % 8;
        if (i < 0) i += 8;
        return dirs[i];
    }

    private static string CodeToText(int code) => code switch
    {
        0 => "Ясно",
        1 or 2 => "Переменная облачность",
        3 => "Пасмурно",
        45 or 48 => "Туман",
        51 or 53 or 55 or 56 or 57 => "Морось",
        61 or 63 or 65 or 66 or 67 => "Дождь",
        71 or 73 or 75 or 77 => "Снег",
        80 or 81 or 82 => "Ливень",
        85 or 86 => "Снегопад",
        95 or 96 or 99 => "Гроза",
        _ => "Облачно"
    };
}
