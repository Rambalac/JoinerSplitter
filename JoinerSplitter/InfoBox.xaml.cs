using System.Windows;

namespace JoinerSplitter
{
    /// <summary>
    /// Interaction logic for InfoBox.xaml
    /// </summary>
    public partial class InfoBox : Window
    {
        InfoBox()
        {
            InitializeComponent();
        }
        public static InfoBox Show(Window owner, string text)
        {
            var dlg = new InfoBox();
            dlg.textBlock.Text = text;
            dlg.Owner = owner;
            dlg.Show();
            return dlg;
        }
    }
}
