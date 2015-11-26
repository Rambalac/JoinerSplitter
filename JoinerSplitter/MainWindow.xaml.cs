using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        Storyboard storyboard;

        public MainWindow()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(outputFilenameBox, outputFilenameBox_Paste);
        }

        private void timeBoxes_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply)) return;
            string text = e.DataObject.GetData(e.FormatToApply).ToString();
            TimeSpan ts;
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
            {
                (sender as TextBox).Text = ts.ToString();
            }
            e.CancelCommand();

        }

        void RemoveOverflow(ToolBar toolBar)
        {
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
        }

        readonly static string[] allowedExtensions = { "mov", "mp4", "avi", "wmv", "mkv" };
        readonly static string dialogFilterString = $"Video files ({string.Join(", ", allowedExtensions)})|{string.Join(";", allowedExtensions.Select(s => "*." + s))}";
        readonly static HashSet<string> allowedExtensionsWithDot = new HashSet<string>(allowedExtensions.Select(f => "." + f));
        async void addButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = dialogFilterString, Multiselect = true };
            var result = dlg.ShowDialog();
            if (result == false) return;
            await addFiles(dlg.FileNames);
        }

        async Task addFiles(string[] files, VideoFile before = null, int groupIndex = -1)
        {
            var ffmpeg = new FFMpeg();
            var Error = new List<String>();
            var lastFile = DataContext.CurrentJob.Files.LastOrDefault();
            var beforeIndex = before != null ? DataContext.CurrentJob.Files.IndexOf(before) : -1;
            foreach (var file in files.Select(p => new VideoFile(p)))
            {
                try
                {
                    file.End = file.Duration = await ffmpeg.GetDuration(file.FilePath);
                    file.GroupIndex = (groupIndex >= 0) ? groupIndex : lastFile?.GroupIndex ?? 0;
                    if (before == null)
                        DataContext.CurrentJob.Files.Add(file);
                    else
                    {
                        DataContext.CurrentJob.Files.Insert(beforeIndex, file);
                    }
                }
                catch (Exception)
                {
                    Error.Add(file.FileName);
                }
            }
            if (Error.Any())
            {
                if (files.Length == Error.Count)
                {
                    MessageBox.Show("None of files can be exported by ffmpeg:\r\n" + string.Join("\r\n", Error.Select(s => "  " + s)),
                        "File format error");
                }
                else
                {
                    MessageBox.Show("Some files can not be exported by ffmpeg:\r\n" + string.Join("\r\n", Error.Select(s => "  " + s)), "File format error");
                }
            }

            if (string.IsNullOrEmpty(DataContext.CurrentJob.OutputName) && files.Length > 0)
                DataContext.CurrentJob.OutputName = System.IO.Path.GetFileNameWithoutExtension(files[0]) + ".out" + System.IO.Path.GetExtension(files[0]);
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
            storyboard?.Stop(mainGrid);

            storyboard = new Storyboard();
            var timeline = new MediaTimeline(video.FileUri);
            storyboard.Children.Add(timeline);
            Storyboard.SetTarget(timeline, mediaElement);
            storyboard.CurrentTimeInvalidated += storyboard_CurrentTimeInvalidated;

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
            try
            {
                await ffmpeg.DoJob(job, (p) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.progress.Value = p;
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Processing failed");
            }
            finally
            {
                progress.Close();
            }
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

        private void currentTimeEditBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void filesList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(f => allowedExtensionsWithDot.Contains(System.IO.Path.GetExtension(f)))) e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                if (insertAdorner != null)
                {
                    var control = GetItemAt(filesList, e.GetPosition(filesList));
                    if (control != null)
                    {
                        insertAdorner.Offset = control.TransformToAncestor(filesList).Transform(new Point(0, -4)).Y;
                    }
                    else
                    {
                        if (filesList.Items.Count == 0)
                        {
                            insertAdorner.Offset = GetListViewHeaderHeight(filesList);
                        }
                        else
                        {
                            var item = filesList.ItemContainerGenerator.ContainerFromItem(filesList.Items[filesList.Items.Count - 1]) as ListViewItem;
                            insertAdorner.Offset = item.TransformToAncestor(filesList).Transform(new Point(0, item.ActualHeight-4)).Y;
                        }
                    }
                }
            }
        }

        public static double GetListViewHeaderHeight(ListView view)
        {
            return (VisualTreeHelper.GetChild(
                        VisualTreeHelper.GetChild(
                            VisualTreeHelper.GetChild(
                                VisualTreeHelper.GetChild(
                                    VisualTreeHelper.GetChild(view, 0),
                                    0),
                                0),
                            0),
                        0) as Control).ActualHeight;
        }

        public static ContentControl GetItemAt(ListView listView, Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(listView, point);
            var item = hitTestResult.VisualHit;
            while (item != null)
            {
                if (item is ListViewItem) return (ContentControl)item;
                if (item is GroupItem) return (ContentControl)item;
                item = VisualTreeHelper.GetParent(item);
            }
            return null;
        }

        private async void filesList_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    var result = GetItemAt(filesList, e.GetPosition(filesList));
                    if (result == null)
                    {
                        await addFiles(files);
                        return;
                    }

                    var listItem = result as ListViewItem;
                    if (listItem != null)
                    {
                        var item = listItem.Content as VideoFile;
                        await addFiles(files, item, item.GroupIndex);
                        return;
                    }

                    var group = result as GroupItem;
                    if (group == null) return;

                    var items = (group.Content as CollectionViewGroup).Items;
                    var before = items.FirstOrDefault() as VideoFile;
                    var groupIndex = before.GroupIndex - 1;
                    if (groupIndex < 0) groupIndex = 0;
                    await addFiles(files, before, groupIndex);
                }

            }
            finally
            {
                if (insertAdorner != null)
                {
                    AdornerLayer.GetAdornerLayer(filesList).Remove(insertAdorner);
                    insertAdorner = null;
                }
            }
        }

        ListViewInsertMarkAdorner insertAdorner;
        private void filesList_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                insertAdorner = new ListViewInsertMarkAdorner(filesList);
                AdornerLayer.GetAdornerLayer(filesList).Add(insertAdorner);
            }
        }

        private void filesList_DragLeave(object sender, DragEventArgs e)
        {
            if (insertAdorner != null)
            {
                AdornerLayer.GetAdornerLayer(filesList).Remove(insertAdorner);
                insertAdorner = null;
            }
        }
    }
}
