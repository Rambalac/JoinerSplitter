using System.Windows;

namespace JoinerSplitter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var view = new MainWindow();
            view.DataContext = new AppModel();
            view.Show();
        }
    }
}