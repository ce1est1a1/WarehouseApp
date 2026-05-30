using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using WarehouseApp.Data;
using WarehouseApp.Models;
using WarehouseApp.Services;

namespace WarehouseApp;

public class AppServices
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly ServiceProvider? _provider;

    public AppDbContext DbContext { get; }
    public IAuthService AuthService { get; }
    public ICategoryService CategoryService { get; }
    public IProductService ProductService { get; }
    public IShipmentService ShipmentService { get; }
    public ISupplyService SupplyService { get; }
    public IReportService ReportService { get; }
    public ICurrencyService CurrencyService { get; }
    public ICounterpartyService CounterpartyService { get; }
    public IGeoService GeoService { get; }
    public IWeatherService WeatherService { get; }
    public ILogisticsService LogisticsService { get; }
    public IHeatmapService HeatmapService { get; }

    public AppServices(string dbPath = "warehouse.db")
    {
        logger.Debug("Инициализация IoC-контейнера, БД: {Path}", dbPath);

        var services = new ServiceCollection();

        services.AddSingleton(_ =>
        {
            var ctx = new AppDbContext($"Data Source={dbPath}");
            DbInitializer.Initialize(ctx);
            return ctx;
        });
        services.AddSingleton(sp => sp.GetRequiredService<ICurrencyService>().Settings);

        RegisterByConvention(services);

        _provider = services.BuildServiceProvider();

        DbContext = _provider.GetRequiredService<AppDbContext>();
        AuthService = _provider.GetRequiredService<IAuthService>();
        CategoryService = _provider.GetRequiredService<ICategoryService>();
        ProductService = _provider.GetRequiredService<IProductService>();
        ShipmentService = _provider.GetRequiredService<IShipmentService>();
        SupplyService = _provider.GetRequiredService<ISupplyService>();
        ReportService = _provider.GetRequiredService<IReportService>();
        CurrencyService = _provider.GetRequiredService<ICurrencyService>();
        CounterpartyService = _provider.GetRequiredService<ICounterpartyService>();
        GeoService = _provider.GetRequiredService<IGeoService>();
        WeatherService = _provider.GetRequiredService<IWeatherService>();
        LogisticsService = _provider.GetRequiredService<ILogisticsService>();
        HeatmapService = _provider.GetRequiredService<IHeatmapService>();

        logger.Info("IoC-контейнер готов");
    }

    /// <summary>
    /// Авторегистрация сервисов через рефлексию: для каждого интерфейса I*
    /// находится реализация по соглашению об именовании (IFoo -> Foo) и
    /// добавляется в контейнер как singleton.
    /// </summary>
    private static void RegisterByConvention(IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        var interfaces = types.Where(t => t.IsInterface && t.Name.StartsWith("I"));
        foreach (var iface in interfaces)
        {
            var implName = iface.Name.Substring(1);
            var impl = types.FirstOrDefault(t =>
                t.IsClass && !t.IsAbstract && t.Name == implName && iface.IsAssignableFrom(t));
            if (impl == null) continue;

            services.AddSingleton(iface, impl);
            logger.Trace("Зарегистрировано через рефлексию: {Interface} -> {Impl}", iface.Name, impl.Name);
        }
    }

    public AppServices(IAuthService auth, ICategoryService cat, IProductService prod, IShipmentService ship,
        ISupplyService? supply = null)
    {
        DbContext = null!;
        AuthService = auth;
        CategoryService = cat;
        ProductService = prod;
        ShipmentService = ship;
        SupplyService = supply!;
        ReportService = null!;
        CurrencyService = new CurrencyService();
        CounterpartyService = new CounterpartyService();
        GeoService = new GeoService();
        WeatherService = new WeatherService(GeoService, CurrencyService.Settings);
        LogisticsService = new LogisticsService();
        HeatmapService = null!;
    }
}
