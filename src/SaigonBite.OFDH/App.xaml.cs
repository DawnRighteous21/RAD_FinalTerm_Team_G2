using System.Windows;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var db = new DatabaseConnection();
        db.InitializeSchema();
    }
}
