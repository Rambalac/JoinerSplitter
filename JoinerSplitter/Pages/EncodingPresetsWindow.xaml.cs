namespace JoinerSplitter.Pages
{
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using Properties;

    /// <summary>
    /// Interaction logic for EncodingPresetsWindow.xaml
    /// </summary>
    public partial class EncodingPresetsWindow : Window
    {
        public EncodingPresetsWindow()
        {
            InitializeComponent();
        }

        private AppModel Data => (AppModel)DataContext;

        public static void Show(Window owner, AppModel model)
        {
            var dlg = new EncodingPresetsWindow
            {
                Owner = owner,
                DataContext = model
            };
            dlg.ShowDialog();
            model.SaveEncoders();
        }
    }
}
