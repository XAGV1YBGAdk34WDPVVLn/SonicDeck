using System.IO;
using System.Windows;
using SonicDeck.Visuals;

namespace SonicDeck;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            if (e.Args.Contains("--generate-icon"))
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sonicdeck.ico");
                AppIconGenerator.WriteMultiSizeIcoFile(icoPath);
                Shutdown(0);
                return;
            }

            var win = new MainWindow
            {
                Icon = AppIconGenerator.CreateWindowIconSource()
            };
            MainWindow = win;
            win.Show();
        }
        catch (Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                File.WriteAllText(logPath, ex.ToString());
            }
            catch { }
            MessageBox.Show(ex.ToString(), "SonicDeck Startup Error");
            Shutdown(1);
        }
    }
}
