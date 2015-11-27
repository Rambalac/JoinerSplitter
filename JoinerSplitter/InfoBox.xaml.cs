using System;
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

        bool wasEnabled;
        public static InfoBox Show(Window owner, string text)
        {
            var dlg = new InfoBox();
            dlg.textBlock.Text = text;
            dlg.Owner = owner;
            dlg.wasEnabled = owner.IsEnabled;
            owner.IsEnabled = false;
            dlg.Show();
            return dlg;
        }

        protected override void OnClosed(EventArgs e)
        {
            Owner.IsEnabled = wasEnabled;
            base.OnClosed(e);
        }
    }
}
