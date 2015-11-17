using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JoinerSplitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        new AppModel DataContext => (AppModel)base.DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(outputFilenameBox, outputFilenameBox_Paste);
        }

        private void toolBar_Loaded(object sender, RoutedEventArgs e)
        {
            RemoveOverflow(toolBar);

        }

        private void RemoveOverflow(ToolBar toolBar)
        {
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void filesToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            RemoveOverflow(filesToolBar);
        }

        private async void addButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Video files (mov, mp4, avi, wmv)|*.mov;*.mp4;*.avi;*.wmv", Multiselect = true };
            var result = dlg.ShowDialog();
            if (result == false) return;

            var ffmpeg = new FFMpeg();
            foreach (var file in dlg.FileNames.Select(p => new VideoFile(p)))
            {
                file.End = file.Duration = await ffmpeg.GetDuration(file.FilePath);
                DataContext.CurrentJob.Files.Add(file);
            }
            if (string.IsNullOrEmpty(DataContext.CurrentJob.OutputName) && dlg.FileNames.Length > 0)
                DataContext.CurrentJob.OutputName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileNames[0]) + ".out" + System.IO.Path.GetExtension(dlg.FileNames[0]);

        }

        private void Button_SelStart(object sender, RoutedEventArgs e)
        {
            slider.SelectionStart = slider.Value;

        }

        private void Button_SelEnd(object sender, RoutedEventArgs e)
        {
            slider.SelectionEnd = slider.Value;
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            if (storyboard.GetIsPaused(mainGrid))
                storyboard.Resume(mainGrid);
            else
                storyboard.Pause(mainGrid);
        }

        bool changingSlider = false;
        private void storyboard_CurrentTimeInvalidated(object sender, EventArgs e)
        {
            changingSlider = true;
            slider.Value = (sender as ClockGroup).CurrentTime?.TotalSeconds ?? 0;
            changingSlider = false;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!changingSlider)
            {
                Seek(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        private void Button_Start(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionStart));
        }

        private void Button_End(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionEnd - 0.05));
        }

        private void Seek(TimeSpan timeSpan, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            wasPaused = storyboard.GetIsPaused(mainGrid);
            storyboard.SeekAlignedToLastTick(mainGrid, timeSpan, origin);
        }

        bool wasPaused;
        private void slider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            wasPaused = storyboard.GetIsPaused(mainGrid);
            storyboard.Pause(mainGrid);
        }

        private void slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!wasPaused)
                storyboard.Resume(mainGrid);
        }

        private void OpenVideo(VideoFile video)
        {
            storyboard.Stop(mainGrid);
            DataContext.CurrentFile = video;
            storyboard.Begin(mainGrid, true);
        }

        private void outputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (filesList.SelectedItem == null) return;
            var output = filesList.SelectedItem as VideoFile;
            OpenVideo(output);
            storyboard.Seek(mainGrid, output.Start, TimeSeekOrigin.BeginTime);
        }

        private async void processButton_Click(object sender, RoutedEventArgs e)
        {
            var job = DataContext.CurrentJob;
            var progress = new ProgressWindow();
            progress.Show();
            var ffmpeg = new FFMpeg();
            await ffmpeg.DoJob(job, (p) =>
            {
                Dispatcher.Invoke(() =>
                {
                    progress.progress.Value = p;
                });
            });

            progress.Close();
        }

        private void outputFolderBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            DataContext.CurrentJob.OutputFolder = dlg.SelectedPath;
        }

        readonly static char[] prohibitedFilenameChars = { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };

        private void outputFilenameBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (prohibitedFilenameChars.Any(e.Text.Contains)) e.Handled = true;
        }

        private void outputFilenameBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply)) return;
            string text = e.DataObject.GetData(e.FormatToApply).ToString();
            if (prohibitedFilenameChars.Any(text.Contains)) e.CancelCommand();
        }

        private void splitButton_Click(object sender, RoutedEventArgs e)
        {
            var currentIndex = DataContext.CurrentJob.Files.IndexOf(DataContext.CurrentFile);
            var currentTime = TimeSpan.FromSeconds(slider.Value);
            var newFile = new VideoFile(DataContext.CurrentFile.FilePath)
            {
                Duration = DataContext.CurrentFile.Duration,
                Start = currentTime,
                End = DataContext.CurrentFile.End,
                GroupIndex = DataContext.CurrentFile.GroupIndex + 1
            };
            DataContext.CurrentFile.End = currentTime;
            DataContext.CurrentJob.Files.Insert(currentIndex + 1, newFile);
            for (var i = currentIndex + 2; i < DataContext.CurrentJob.Files.Count; i++)
                DataContext.CurrentJob.Files[i].GroupIndex += 1;
        }
    }
}
