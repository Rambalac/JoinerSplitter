using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            DataObject.AddPastingHandler(currentTimeEditBox, timeBoxes_Paste);
            DataObject.AddPastingHandler(startTimeEditBox, timeBoxes_Paste);
            DataObject.AddPastingHandler(endTimeEditBox, timeBoxes_Paste);
        }

        private void timeBoxes_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply)) return;
            string text = e.DataObject.GetData(e.FormatToApply).ToString();
            TimeSpan ts;
            if (TimeSpan.TryParse(text, out ts))
            {
                (sender as TextBox).Text = ts.ToString();
            }
            e.CancelCommand();

        }

        void toolBar_Loaded(object sender, RoutedEventArgs e)
        {
            RemoveOverflow(toolBar);

        }

        void RemoveOverflow(ToolBar toolBar)
        {
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
        }

        void filesToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            RemoveOverflow(filesToolBar);
        }

        async void addButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Video files (mov, mp4, avi, wmv, mkv)|*.mov;*.mp4;*.avi;*.wmv;*.mkv", Multiselect = true };
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

        void Button_SelStart(object sender, RoutedEventArgs e)
        {
            slider.SelectionStart = slider.Value;

        }

        void Button_SelEnd(object sender, RoutedEventArgs e)
        {
            slider.SelectionEnd = slider.Value;
        }

        void Button_Play(object sender, RoutedEventArgs e)
        {
            if (storyboard.GetIsPaused(mainGrid))
                storyboard.Resume(mainGrid);
            else
                storyboard.Pause(mainGrid);
        }

        bool changingSlider = false;
        void storyboard_CurrentTimeInvalidated(object sender, EventArgs e)
        {
            changingSlider = true;
            slider.Value = (sender as ClockGroup).CurrentTime?.TotalSeconds ?? 0;
            changingSlider = false;
        }

        void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!changingSlider)
            {
                Seek(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        void Button_Start(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionStart));
        }

        void Button_End(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionEnd - 0.05));
        }

        void Seek(TimeSpan timeSpan, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            wasPaused = storyboard.GetIsPaused(mainGrid);
            storyboard.SeekAlignedToLastTick(mainGrid, timeSpan, origin);
        }

        bool wasPaused;
        void slider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            wasPaused = storyboard.GetIsPaused(mainGrid);
            storyboard.Pause(mainGrid);
        }

        void slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!wasPaused)
                storyboard.Resume(mainGrid);
        }

        void OpenVideo(VideoFile video)
        {
            storyboard.Stop(mainGrid);
            DataContext.CurrentFile = video;
            storyboard.Begin(mainGrid, true);
        }

        void outputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (filesList.SelectedItem != null)
            {
                var output = filesList.SelectedItem as VideoFile;
                OpenVideo(output);
                storyboard.Seek(mainGrid, output.Start, TimeSeekOrigin.BeginTime);
            }
            else
            {
                DataContext.CurrentFile = null;
                storyboard.Stop(mainGrid);
            }
        }

        async void processButton_Click(object sender, RoutedEventArgs e)
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

        void outputFolderBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            DataContext.CurrentJob.OutputFolder = dlg.SelectedPath;
        }

        readonly static char[] prohibitedFilenameChars = { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };

        void outputFilenameBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (prohibitedFilenameChars.Any(e.Text.Contains)) e.Handled = true;
        }

        void outputFilenameBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply)) return;
            string text = e.DataObject.GetData(e.FormatToApply).ToString();
            if (prohibitedFilenameChars.Any(text.Contains)) e.CancelCommand();
        }

        void splitButton_Click(object sender, RoutedEventArgs e)
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
            RefreshList();
        }

        void Button_Delete(object sender, RoutedEventArgs e)
        {
            var jobFiles = DataContext.CurrentJob.Files;
            foreach (var file in filesList.SelectedItems.Cast<VideoFile>().ToList())
                jobFiles.Remove(file);
            NormalizeGroups();
            RefreshList();
        }

        void NormalizeGroups()
        {
            var jobFiles = DataContext.CurrentJob.Files;
            if (jobFiles.Count == 0) return;
            var curindex = 0;
            var lastIndex = jobFiles[0].GroupIndex;
            foreach (var file in jobFiles)
            {
                if (file.GroupIndex != lastIndex)
                {
                    lastIndex = file.GroupIndex;
                    curindex++;
                }
                file.GroupIndex = curindex;
            }

        }

        void Button_Duplicate(object sender, RoutedEventArgs e)
        {
            var selected = filesList.SelectedItems.Cast<VideoFile>().ToList();
            int insertIndex = selected.Select(v => DataContext.CurrentJob.Files.IndexOf(v)).Max() + 1;
            foreach (var file in selected)
                DataContext.CurrentJob.Files.Insert(insertIndex++, new VideoFile(file));

            RefreshList();
        }

        void Button_MoveUp(object sender, RoutedEventArgs e)
        {
            var jobFiles = DataContext.CurrentJob.Files;
            var selected = filesList.SelectedItems.Cast<VideoFile>().OrderBy(f => jobFiles.IndexOf(f)).ToList();
            for (var i = 0; i < selected.Count; i++)
            {
                var file = selected[i];
                var fileindex = jobFiles.IndexOf(file);
                if (fileindex > 0 && !selected.Contains(jobFiles[fileindex - 1]))
                {
                    if (jobFiles[fileindex - 1].GroupIndex < file.GroupIndex)
                        file.GroupIndex = jobFiles[fileindex - 1].GroupIndex;
                    else
                        jobFiles.Move(fileindex, fileindex - 1);
                }
            }

            NormalizeGroups();
            RefreshList();
        }

        void Button_MoveDown(object sender, RoutedEventArgs e)
        {
            var jobFiles = DataContext.CurrentJob.Files;
            var selected = filesList.SelectedItems.Cast<VideoFile>().OrderBy(f => jobFiles.IndexOf(f)).ToList();
            for (var i = selected.Count - 1; i >= 0; i--)
            {
                var file = selected[i];
                var fileindex = jobFiles.IndexOf(file);
                if (fileindex < jobFiles.Count - 1 && !selected.Contains(jobFiles[fileindex + 1]))
                {
                    if (jobFiles[fileindex + 1].GroupIndex > file.GroupIndex)
                        file.GroupIndex = jobFiles[fileindex + 1].GroupIndex;
                    else
                        jobFiles.Move(fileindex, fileindex + 1);
                }
            }

            NormalizeGroups();
            RefreshList();
        }

        void Button_SplitFiles(object sender, RoutedEventArgs e)
        {
            var currentIndex = DataContext.CurrentJob.Files.IndexOf(DataContext.CurrentFile);
            if (currentIndex == 0) return;

            for (var i = currentIndex; i < DataContext.CurrentJob.Files.Count; i++)
                DataContext.CurrentJob.Files[i].GroupIndex += 1;

            NormalizeGroups();
            RefreshList();
        }

        void RefreshList()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(filesList.ItemsSource);
            view.Refresh();
        }

        static readonly Regex nonTimeSpanChars = new Regex("[^\\d:.]");

        private void TimeBoxes_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (nonTimeSpanChars.IsMatch(e.Text)) e.Handled = true;
        }

    }
}
