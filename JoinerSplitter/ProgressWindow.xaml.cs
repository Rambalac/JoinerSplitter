namespace JoinerSplitter
{
    using System;
    using System.Threading;
    using System.Windows;

    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private bool wasEnabled;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private ProgressWindow()
        {
            InitializeComponent();
        }

        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        public static ProgressWindow Show(Window owner, double total = 100)
        {
            var dlg = new ProgressWindow();
            dlg.Owner = owner;
            if (owner != null)
            {
                dlg.wasEnabled = owner.IsEnabled;
            }

            dlg.progress.Maximum = total;
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }
    }
}