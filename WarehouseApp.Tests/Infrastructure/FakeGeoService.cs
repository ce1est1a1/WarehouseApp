using WarehouseApp.Services;

namespace WarehouseApp.Tests.Infrastructure;

public sealed class FakeGeoService : IGeoService
{
    private readonly GeoSuggestion? _geocodeResult;
    private readonly string? _extractedCity;

    public FakeGeoService(GeoSuggestion? geocodeResult = null, string? extractedCity = null)
    {
        _geocodeResult = geocodeResult;
        _extractedCity = extractedCity;
    }

    public List<GeoSuggestion> Suggest(string query, int limit = 6) =>
        _geocodeResult is null ? new List<GeoSuggestion>() : new List<GeoSuggestion> { _geocodeResult };

    public GeoSuggestion? Geocode(string query) => _geocodeResult;

    public int? RoadDistanceKm(double lat1, double lon1, double lat2, double lon2) => 650;

    public string? ExtractCity(string? address) => _extractedCity;

    public GeoPoint? Resolve(string? city) => null;

    public int? DistanceKm(string fromCity, string toCity) => null;
}
