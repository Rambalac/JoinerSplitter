namespace JoinerSplitter
{
    using System;
    using System.Windows;

    /// <summary>
    /// Interaction logic for InfoBox.xaml
    /// </summary>
    public partial class InfoBox
    {
        private bool wasEnabled;

        private InfoBox()
        {
            InitializeComponent();
        }

        public static InfoBox Show(Window owner, string text)
        {
            var dlg = new InfoBox
            {
                TextBlock = { Text = text },
                Owner = owner
            };
            if (owner != null)
            {
                dlg.wasEnabled = owner.IsEnabled;
                owner.IsEnabled = false;
            }

            dlg.Show();
            return dlg;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (Owner != null)
            {
                Owner.IsEnabled = wasEnabled;
            }

            base.OnClosed(e);
        }
    }
}