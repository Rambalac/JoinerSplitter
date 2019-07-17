using System;

[assembly: CLSCompliant(true)]

namespace JoinerSplitter
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        static App()
        {
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(UIElement), new FrameworkPropertyMetadata(true));
        }
    }
}