using NLog;
using WarehouseApp.Forms;

namespace WarehouseApp;

static class Program
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    [STAThread]
    static void Main()
    {
        Application.ThreadException += (_, e) =>
            logger.Error(e.Exception, "Необработанное исключение в UI-потоке");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Fatal(e.ExceptionObject as Exception, "Критическое необработанное исключение");

        try
        {
            logger.Info("Запуск приложения WarehouseApp");

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var services = new AppServices("warehouse.db");
            Application.Run(new LoginForm(services));

            logger.Info("Приложение WarehouseApp завершило работу штатно");
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Не удалось запустить приложение");
            throw;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
