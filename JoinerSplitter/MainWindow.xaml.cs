using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static System.FormattableString;

namespace JoinerSplitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string[] allowedExtensions = { "mov", "mp4", "avi", "wmv", "mkv" };
        private static readonly string dialogFilterString = Invariant($"Video files ({string.Join(", ", allowedExtensions)})|{string.Join(";", allowedExtensions.Select(s => "*." + s))}");
        private static readonly char[] prohibitedFilenameChars = { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };
        private bool changingSlider = false;
        private Point? dragStartPoint;
        private ListViewInsertMarkAdorner insertAdorner;
        private Storyboard storyboard;
        private bool wasPaused;
        public MainWindow()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(outputFilenameBox, outputFilenameBox_Paste);
            FFMpeg.Instance.FFMpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg\\ffmpeg.exe");
            FFMpeg.Instance.FFProbePath = Path.Combine(Environment.CurrentDirectory, "ffmpeg\\ffprobe.exe");
        }

        private AppModel Data => (AppModel)DataContext;
        public static ContentControl GetItemAt(Visual listView, Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(listView, point);
            var item = hitTestResult.VisualHit;
            while (item != null)
            {
                var listViewItem = item as ListViewItem;
                if (listViewItem != null) return listViewItem;

                var groupItem = item as GroupItem;
                if (groupItem != null) return groupItem;

                item = VisualTreeHelper.GetParent(item);
            }
            return null;
        }

        public static double GetListViewHeaderHeight(DependencyObject view)
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

        private async Task addFiles(string[] files, VideoFile before = null, int groupIndex = -1)
        {
            var infobox = InfoBox.Show(this, "Retrieving video files details...");

            try
            {
                await Data.AddFiles(files, before, groupIndex);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "File format error");
            }

            infobox.Close();
        }

        private async void Button_AddVideo(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = dialogFilterString, Multiselect = true };
            var result = dlg.ShowDialog();
            if (result == false) return;
            await addFiles(dlg.FileNames);
        }

        private void Button_Delete(object sender, RoutedEventArgs e)
        {
            Data.DeleteVideos(filesList.SelectedItems);
            RefreshList();
        }

        private void Button_Duplicate(object sender, RoutedEventArgs e)
        {
            Data.DuplicateVideos(filesList.SelectedItems);
            RefreshList();
        }

        private void Button_End(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionEnd - 0.05));
        }

        private void Button_MoveDown(object sender, RoutedEventArgs e)
        {
            Data.MoveVideosDown(filesList.SelectedItems);
            RefreshList();
        }

        private void Button_MoveUp(object sender, RoutedEventArgs e)
        {
            Data.MoveVideosUp(filesList.SelectedItems);
            RefreshList();
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            if (storyboard.GetIsPaused(mainGrid))
                storyboard.Resume(mainGrid);
            else
                storyboard.Pause(mainGrid);
        }

        private void Button_SelEnd(object sender, RoutedEventArgs e)
        {
            var splitTime = Data.CurrentFile.KeyFrames?.Where(f => f >= slider.Value).DefaultIfEmpty(Data.CurrentFile.Duration).First() ?? slider.Value;
            slider.SelectionEnd = splitTime;
        }

        private void Button_SelStart(object sender, RoutedEventArgs e)
        {
            var splitTime = Data.CurrentFile.KeyFrames?.Where(f => f <= slider.Value).DefaultIfEmpty(0).Last() ?? slider.Value;
            slider.SelectionStart = splitTime - 0.1;
        }

        private void Button_SplitFiles(object sender, RoutedEventArgs e)
        {
            Data.SplitGroup();
            RefreshList();
        }

        private void Button_Start(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(slider.SelectionStart));
        }

        private void filesList_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(typeof(VideoFile[])))
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

        private void filesList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (insertAdorner != null)
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
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
                        insertAdorner.Offset = item.TransformToAncestor(filesList).Transform(new Point(0, item.ActualHeight - 4)).Y;
                    }
                }
            }
        }

        private async void filesList_Drop(object sender, DragEventArgs e)
        {
            if (insertAdorner != null)
            {
                VideoFile before;
                int groupIndex;
                GetBeforeAndGroup(e.GetPosition(filesList), out before, out groupIndex);

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    await addFiles(files, before, groupIndex);
                }
                else if (e.Data.GetDataPresent(typeof(VideoFile[])))
                {
                    var files = (VideoFile[])e.Data.GetData(typeof(VideoFile[]));
                    moveFiles(files, before, groupIndex);
                }

                AdornerLayer.GetAdornerLayer(filesList).Remove(insertAdorner);
                insertAdorner = null;
            }
        }

        private void filesList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);
            e.Handled = true;
        }

        private void filesList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && dragStartPoint != null)
            {
                var drag = (Vector)(e.GetPosition(null) - dragStartPoint);
                if (drag.X > SystemParameters.MinimumHorizontalDragDistance || drag.Y > SystemParameters.MinimumVerticalDragDistance)
                {
                    var selected = filesList.SelectedItems.Cast<VideoFile>().OrderBy(f => Data.CurrentJob.Files.IndexOf(f)).ToArray();
                    DragDrop.DoDragDrop(filesList, selected, DragDropEffects.Move);
                    dragStartPoint = null;
                    selected = null;
                }
            }
        }

        private void filesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            using (var d = Dispatcher.DisableProcessing())
            {
                if (dragStartPoint != null)
                {
                    var drag = (Vector)(e.GetPosition(null) - dragStartPoint);
                    if (drag.X <= SystemParameters.MinimumHorizontalDragDistance && drag.Y <= SystemParameters.MinimumVerticalDragDistance)
                    {
                        var selected = GetItem(e.GetPosition(filesList));
                        if (selected != null)
                        {
                            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                            {
                                if (filesList.SelectedItems.Contains(selected))
                                    filesList.SelectedItems.Remove(selected);
                                else
                                    filesList.SelectedItems.Add(selected);
                            }
                            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                            {
                                var start = Data.CurrentJob.Files.IndexOf((VideoFile)filesList.SelectedItem);
                                var count = Data.CurrentJob.Files.IndexOf(selected) - start;
                                if (count < 0) { start += count; count = -count; }

                                filesList.SelectedItems.Clear();
                                foreach (var item in Data.CurrentJob.Files.Skip(start).Take(count + 1).ToList())
                                    filesList.SelectedItems.Add(item);
                            }
                            else
                            {
                                if (filesList.SelectedItems.Count > 1)
                                    filesList.SelectedItems.Clear();
                                filesList.SelectedItem = selected;
                            }
                        }
                    }
                    dragStartPoint = null;
                }
            }
        }

        private void GetBeforeAndGroup(Point point, out VideoFile before, out int groupIndex)
        {
            var result = GetItemAt(filesList, point);
            before = null;
            groupIndex = -1;
            if (result == null) return;

            var listItem = result as ListViewItem;
            if (listItem != null)
            {
                before = listItem.Content as VideoFile;
                groupIndex = before.GroupIndex;
                return;
            }

            var group = result as GroupItem;
            if (group == null) return;

            var items = (group.Content as CollectionViewGroup).Items;
            before = items.FirstOrDefault() as VideoFile;
            groupIndex = before.GroupIndex - 1;
            if (groupIndex < 0) groupIndex = 0;
        }

        private VideoFile GetItem(Point point)
        {
            var result = GetItemAt(filesList, point);
            if (result == null) return null;

            var listItem = result as ListViewItem;
            if (listItem != null)
                return listItem.Content as VideoFile;
            return null;
        }

        private void moveFiles(VideoFile[] files, VideoFile before, int groupIndex)
        {
            Data.MoveFiles(files, before, groupIndex);
            RefreshList();
        }

        private void NewJob(object sender, RoutedEventArgs e)
        {
            Data.CurrentJob = new Job();
            Data.CurrentFile = null;
        }

        private async Task OpenJob(string path)
        {
            try
            {
                await Data.OpenJob(path);
            }
            catch (SerializationException ex)
            {
                MessageBox.Show(this, ex.Message, "Can not open Job");
            }
        }

        private async void OpenJob(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JoinerSplitter job file (*.jsj)|*.jsj",
                DefaultExt = ".jsj",
                Multiselect = false
            };

            var result = dlg.ShowDialog();
            if (result == false) return;

            var infobox = InfoBox.Show(this, "Retrieving video files details...");
            await OpenJob(dlg.FileName);
            infobox.Close();
        }

        private void OpenVideo(VideoFile video)
        {
            storyboard?.Stop(mainGrid);

            storyboard = new Storyboard();
            var timeline = new MediaTimeline(video.FileUri);
            storyboard.Children.Add(timeline);
            Storyboard.SetTarget(timeline, mediaElement);
            storyboard.CurrentTimeInvalidated += storyboard_CurrentTimeInvalidated;

            Data.CurrentFile = video;
            storyboard.Begin(mainGrid, true);
        }

        private void outputFilenameBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply)) return;
            string text = e.DataObject.GetData(e.FormatToApply).ToString();
            if (prohibitedFilenameChars.Any(text.Contains)) e.CancelCommand();
        }

        private void outputFilenameBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (prohibitedFilenameChars.Any(e.Text.Contains)) e.Handled = true;
        }

        private void outputFolderBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                Data.CurrentJob.OutputFolder = dlg.SelectedPath;
            }
        }

        private void outputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (filesList.SelectedItem != null)
            {
                var output = filesList.SelectedItem as VideoFile;
                OpenVideo(output);
                storyboard.Seek(mainGrid, TimeSpan.FromSeconds(output.Start), TimeSeekOrigin.BeginTime);
            }
            else
            {
                Data.CurrentFile = null;
                storyboard.Stop(mainGrid);
            }
        }

        private async void processButton_Click(object sender, RoutedEventArgs e)
        {
            var job = Data.CurrentJob;
            var progress = ProgressWindow.Show(this, job.Files.Sum(f => f.CutDuration));

            try
            {
                await FFMpeg.Instance.DoJob(job, (cur) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.progress.Value = cur;
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

        private void RefreshList()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(filesList.ItemsSource);
            view.Refresh();
        }

        private async void SaveJob(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Data.CurrentJob.JobFilePath))
                SaveJobAs();
            else
                await Data.SaveJob(Data.CurrentJob.JobFilePath);
        }

        private async void SaveJobAs(object sender = null, RoutedEventArgs e = null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JoinerSplitter job file (*.jsj)|*.jsj",
                DefaultExt = ".jsj",
                OverwritePrompt = true
            };
            if (!string.IsNullOrWhiteSpace(Data.CurrentJob.JobFilePath))
            {
                dlg.FileName = Path.GetFileNameWithoutExtension(Data.CurrentJob.JobFilePath);
                dlg.InitialDirectory = Path.GetDirectoryName(Data.CurrentJob.JobFilePath);
            }
            var result = dlg.ShowDialog();
            if (result == false) return;

            await Data.SaveJob(dlg.FileName);
        }

        private void Seek(TimeSpan timeSpan, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            try
            {
                wasPaused = storyboard.GetIsPaused(mainGrid);
                storyboard.SeekAlignedToLastTick(mainGrid, timeSpan, origin);
            }
            catch (InvalidOperationException)
            {
                //Ignore if file selection happened;
            }
        }

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

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!changingSlider)
            {
                Seek(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        private void splitButton_Click(object sender, RoutedEventArgs e)
        {
            Data.SplitCurrentVideo(slider.Value);
            RefreshList();
        }

        private void storyboard_CurrentTimeInvalidated(object sender, EventArgs e)
        {
            changingSlider = true;
            slider.Value = (sender as ClockGroup).CurrentTime?.TotalSeconds ?? 0;
            changingSlider = false;
        }
    }
}