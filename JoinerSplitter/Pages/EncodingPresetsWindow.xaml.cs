namespace JoinerSplitter.Pages
{
    using System.Windows;

    /// <summary>
    /// Interaction logic for EncodingPresetsWindow.xaml.
    /// </summary>
    public partial class EncodingPresetsWindow
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
