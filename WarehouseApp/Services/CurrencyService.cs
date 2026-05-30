using System.Text.Json;
using NLog;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface ICurrencyService
{
    AppSettings Settings { get; }
    string FormatPrice(decimal rubAmount);
    string FormatPriceAt(decimal rubAmount, decimal? historicalRate);
    void SetCurrency(string currency);
    Task UpdateRatesAsync();
}

public class CurrencyService : ICurrencyService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public AppSettings Settings { get; private set; }

    public CurrencyService()
    {
        Settings = AppSettings.Load();
    }

    public string FormatPrice(decimal rubAmount) => Settings.FormatPrice(rubAmount);

    public string FormatPriceAt(decimal rubAmount, decimal? historicalRate) =>
        Settings.FormatPriceAt(rubAmount, historicalRate);

    public void SetCurrency(string currency)
    {
        logger.Info("Смена валюты отображения: {Old} -> {New}", Settings.Currency, currency);
        Settings.Currency = currency;
        Settings.Save();
    }

    public async Task UpdateRatesAsync()
    {
        logger.Debug("Запрос актуальных курсов валют");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            var response = await client.GetStringAsync(
                "https://api.exchangerate-api.com/v4/latest/RUB");
            using var doc = JsonDocument.Parse(response);
            var rates = doc.RootElement.GetProperty("rates");

            if (rates.TryGetProperty("USD", out var usd))
                Settings.UsdRate = Math.Round(1m / usd.GetDecimal(), 2);
            if (rates.TryGetProperty("EUR", out var eur))
                Settings.EurRate = Math.Round(1m / eur.GetDecimal(), 2);

            Settings.UsdtRate = Settings.UsdRate;

            try
            {
                var cryptoResp = await client.GetStringAsync(
                    "https://api.coingecko.com/api/v3/simple/price?ids=tether&vs_currencies=rub");
                using var cryptoDoc = JsonDocument.Parse(cryptoResp);
                if (cryptoDoc.RootElement.TryGetProperty("tether", out var tether)
                    && tether.TryGetProperty("rub", out var usdtRub))
                {
                    Settings.UsdtRate = usdtRub.GetDecimal();
                }
            }
            catch (Exception cryptoEx)
            {
                logger.Trace(cryptoEx, "Курс USDT не получен, используется USD");
            }

            Settings.RatesUpdatedAt = DateTime.Now;
            Settings.Save();

            logger.Info("Курсы валют обновлены: USD={Usd}, EUR={Eur}, USDT={Usdt}",
                Settings.UsdRate, Settings.EurRate, Settings.UsdtRate);
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Не удалось обновить курсы валют — используются сохранённые значения");
        }
    }
}
