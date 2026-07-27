using System.Windows;

namespace SQLGuardian.Ssms;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var options = LaunchOptions.Parse(e.Args);
        var window = new MainWindow(options);
        window.Show();
    }
}
