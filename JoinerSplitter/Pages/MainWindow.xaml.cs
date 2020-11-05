namespace JoinerSplitter.Pages
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Forms;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Shapes;
    using JetBrains.Annotations;
    using Control = System.Windows.Controls.Control;
    using DataFormats = System.Windows.DataFormats;
    using DataObject = System.Windows.DataObject;
    using DragDropEffects = System.Windows.DragDropEffects;
    using DragEventArgs = System.Windows.DragEventArgs;
    using ListViewItem = System.Windows.Controls.ListViewItem;
    using MessageBox = System.Windows.MessageBox;
    using MouseEventArgs = System.Windows.Input.MouseEventArgs;
    using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
    using Path = System.IO.Path;
    using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    [UsedImplicitly]
    public partial class MainWindow
    {
        private static readonly string[] AllowedExtensions = { "mov", "mp4", "avi", "wmv", "mkv", "mts", "m2ts" };
        private static readonly string DialogFilterString = $"Video files|{string.Join(";", AllowedExtensions.Select(s => "*." + s))}|All files|*.*";
        private static readonly char[] ProhibitedFilenameChars = { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };
        private bool changingSlider;
        private Point? dragStartPoint;
        private ListViewInsertMarkAdorner insertAdorner;
        private Storyboard storyboard;
        private bool wasPaused;

        public MainWindow()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(OutputFilenameBox, OutputFilenameBox_Paste);
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
                switch (item)
                {
                    case ListViewItem listViewItem:
                        return listViewItem;

                    case GroupItem groupItem:
                        return groupItem;
                }

                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        public static double GetListViewHeaderHeight(DependencyObject view)
        {
            return ((Control)VisualTreeHelper.GetChild(
                           VisualTreeHelper.GetChild(
                               VisualTreeHelper.GetChild(
                                   VisualTreeHelper.GetChild(
                                       VisualTreeHelper.GetChild(view, 0),
                                       0),
                                   0),
                               0),
                           0)).ActualHeight;
        }

        private static TItemContainer GetContainerAtPoint<TItemContainer>(ItemsControl control, Point p)
            where TItemContainer : DependencyObject
        {
            var result = VisualTreeHelper.HitTest(control, p);
            var obj = result.VisualHit;
            if (obj != null)
            {
                while ((VisualTreeHelper.GetParent(obj) != null) && !(obj is TItemContainer))
                {
                    obj = VisualTreeHelper.GetParent(obj);
                    if (obj == null)
                    {
                        return null;
                    }
                }
            }

            // Will return null if not found
            return obj as TItemContainer;
        }

        private async Task AddFiles(string[] files, VideoFile before = null, int groupIndex = -1)
        {
            var infoBox = InfoBox.Show(this, Properties.Resources.ReadingFile);

            try
            {
                await Data.AddFiles(files, before, groupIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File format error");
            }
            finally
            {
                infoBox.Close();
            }
        }

        private async void Button_AddVideo(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = DialogFilterString, Multiselect = true };
            var result = dlg.ShowDialog();
            if (result == false)
            {
                return;
            }

            await AddFiles(dlg.FileNames);
        }

        private void Button_Delete(object sender, RoutedEventArgs e)
        {
            Data.DeleteVideos(FilesList.SelectedItems);
            RefreshList();
        }

        private void Button_Duplicate(object sender, RoutedEventArgs e)
        {
            Data.DuplicateVideos(FilesList.SelectedItems);
            RefreshList();
        }

        private void Button_End(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(Slider.SelectionEnd - 0.05));
        }

        private void Button_MoveDown(object sender, RoutedEventArgs e)
        {
            Data.MoveVideosDown(FilesList.SelectedItems);
            RefreshList();
        }

        private void Button_MoveUp(object sender, RoutedEventArgs e)
        {
            Data.MoveVideosUp(FilesList.SelectedItems);
            RefreshList();
        }

        private void Button_OpenPresets(object sender, RoutedEventArgs e)
        {
            EncodingPresetsWindow.Show(this, Data);
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            PlayPause();
        }

        private void Button_SelEnd(object sender, RoutedEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Slider.SelectionEnd = Slider.Value;
            }
            else
            {
                var splitTime = Data.CurrentFile.KeyFrames?.Where(f => f >= Slider.Value).DefaultIfEmpty(Data.CurrentFile.Duration).First() ?? Slider.Value;
                Slider.SelectionEnd = splitTime;
            }
        }

        private void Button_SelStart(object sender, RoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                Slider.SelectionStart = Slider.Value;
            }
            else
            {
                var splitTime = Data.CurrentFile.KeyFrames?.Where(f => f <= Slider.Value).DefaultIfEmpty(0).Last() ?? Slider.Value;
                Slider.SelectionStart = splitTime - 0.1;
            }
        }

        private void Button_SplitFiles(object sender, RoutedEventArgs e)
        {
            Data.SplitGroup();
            RefreshList();
        }

        private void Button_Start(object sender, RoutedEventArgs e)
        {
            Seek(TimeSpan.FromSeconds(Slider.SelectionStart));
        }

        private void FilesList_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(typeof(VideoFile[])))
            {
                insertAdorner = new ListViewInsertMarkAdorner(FilesList);
                AdornerLayer.GetAdornerLayer(FilesList).Add(insertAdorner);
            }
        }

        private void FilesList_DragLeave(object sender, DragEventArgs e)
        {
            if (insertAdorner != null)
            {
                AdornerLayer.GetAdornerLayer(FilesList).Remove(insertAdorner);
                insertAdorner = null;
            }
        }

        private void FilesList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (insertAdorner != null)
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                var control = GetItemAt(FilesList, e.GetPosition(FilesList));
                if (control != null)
                {
                    insertAdorner.Offset = control.TransformToAncestor(FilesList).Transform(new Point(0, -4)).Y;
                }
                else
                {
                    if (FilesList.Items.Count == 0)
                    {
                        insertAdorner.Offset = GetListViewHeaderHeight(FilesList);
                    }
                    else
                    {
                        var item = FilesList.ItemContainerGenerator.ContainerFromItem(FilesList.Items[FilesList.Items.Count - 1]) as ListViewItem;
                        insertAdorner.Offset = item.TransformToAncestor(FilesList).Transform(new Point(0, item.ActualHeight - 4)).Y;
                    }
                }
            }
        }

        private async void FilesList_Drop(object sender, DragEventArgs e)
        {
            if (insertAdorner != null)
            {
                GetBeforeAndGroup(e.GetPosition(FilesList), out var before, out var groupIndex);

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    await AddFiles(files, before, groupIndex);
                }
                else if (e.Data.GetDataPresent(typeof(VideoFile[])))
                {
                    var files = (VideoFile[])e.Data.GetData(typeof(VideoFile[]));
                    MoveFiles(files, before, groupIndex);
                }

                AdornerLayer.GetAdornerLayer(FilesList).Remove(insertAdorner);
                insertAdorner = null;
            }
        }

        private void FilesList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Rectangle || e.OriginalSource is Border)
            {
                return;
            }

            dragStartPoint = e.GetPosition(null);
            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                var result = GetContainerAtPoint<ListViewItem>(FilesList, e.GetPosition(FilesList));
                if ((result != null) && !FilesList.SelectedItems.Contains(result.Content))
                {
                    FilesList.SelectedItem = result.Content;
                }
            }

            e.Handled = true;
        }

        private void FilesList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Rectangle || e.OriginalSource is Border)
            {
                return;
            }

            if ((e.LeftButton == MouseButtonState.Pressed) && (dragStartPoint != null))
            {
                var drag = (Vector)(e.GetPosition(null) - dragStartPoint);
                if ((drag.X > SystemParameters.MinimumHorizontalDragDistance) || (drag.Y > SystemParameters.MinimumVerticalDragDistance))
                {
                    var selected = FilesList.SelectedItems.Cast<VideoFile>().OrderBy(f => Data.CurrentJob.Files.IndexOf(f)).ToArray();
                    if (!selected.Any())
                    {
                        return;
                    }

                    DragDrop.DoDragDrop(FilesList, selected, DragDropEffects.Move);
                    dragStartPoint = null;
                }
            }
        }

        private void FilesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            using (Dispatcher.DisableProcessing())
            {
                if (dragStartPoint != null)
                {
                    var drag = (Vector)(e.GetPosition(null) - dragStartPoint);
                    if ((drag.X <= SystemParameters.MinimumHorizontalDragDistance) && (drag.Y <= SystemParameters.MinimumVerticalDragDistance))
                    {
                        var selected = GetItem(e.GetPosition(FilesList));
                        if (selected != null)
                        {
                            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                            {
                                if (FilesList.SelectedItems.Contains(selected))
                                {
                                    FilesList.SelectedItems.Remove(selected);
                                }
                                else
                                {
                                    FilesList.SelectedItems.Add(selected);
                                }
                            }
                            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                            {
                                var start = Data.CurrentJob.Files.IndexOf((VideoFile)FilesList.SelectedItem);
                                var count = Data.CurrentJob.Files.IndexOf(selected) - start;
                                if (count < 0)
                                {
                                    start += count;
                                    count = -count;
                                }

                                FilesList.SelectedItems.Clear();
                                foreach (var item in Data.CurrentJob.Files.Skip(start).Take(count + 1).ToList())
                                {
                                    FilesList.SelectedItems.Add(item);
                                }
                            }
                            else
                            {
                                if (FilesList.SelectedItems.Count > 1)
                                {
                                    FilesList.SelectedItems.Clear();
                                }

                                FilesList.SelectedItem = selected;
                            }
                        }
                    }

                    dragStartPoint = null;
                }
            }
        }

        private void GetBeforeAndGroup(Point point, out VideoFile before, out int groupIndex)
        {
            var result = GetItemAt(FilesList, point);
            before = null;
            groupIndex = -1;
            switch (result)
            {
                case null:
                    return;

                case ListViewItem listItem:
                    before = (VideoFile)listItem.Content;
                    groupIndex = before.GroupIndex;
                    return;

                case GroupItem group:
                    var items = ((CollectionViewGroup)group.Content).Items;
                    before = (VideoFile)items.FirstOrDefault();
                    groupIndex = before.GroupIndex - 1;
                    if (groupIndex < 0)
                    {
                        groupIndex = 0;
                    }

                    return;
            }
        }

        private VideoFile GetItem(Point point)
        {
            var result = GetItemAt(FilesList, point);
            switch (result)
            {
                case null:
                    return null;

                case ListViewItem listItem:
                    return listItem.Content as VideoFile;
            }

            return null;
        }

        private async void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (Data.CurrentJob.Changed)
            {
                var result = MessageBox.Show(Properties.Resources.UnsavedChanges, Properties.Resources.UnsavedChangesTitle, MessageBoxButton.YesNoCancel);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        await SaveJob();
                        return;

                    case MessageBoxResult.Cancel:
                    case MessageBoxResult.None:
                        e.Cancel = true;
                        return;
                }
            }
        }

        private void MediaElement_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            PlayPause();
        }

        private void MoveFiles(VideoFile[] files, VideoFile before, int groupIndex)
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
            catch (AggregateException ex)
            {
                MessageBox.Show(this, string.Join("\r\n", ex.InnerExceptions.Select(e => e.Message)), "Can not open Job");
            }
            catch (Exception ex)
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
                Multiselect = false,
            };

            var result = dlg.ShowDialog();
            if (result == false)
            {
                return;
            }

            var infoBox = InfoBox.Show(this, Properties.Resources.ReadingFile);
            await OpenJob(dlg.FileName);
            infoBox.Close();
        }

        private void OpenVideo(VideoFile video)
        {
            storyboard?.Stop(MainGrid);

            storyboard = new Storyboard();
            var timeline = new MediaTimeline(video.FileUri);
            storyboard.Children.Add(timeline);
            Storyboard.SetTarget(timeline, MediaElement);
            storyboard.CurrentTimeInvalidated += Storyboard_CurrentTimeInvalidated;

            Data.CurrentFile = video;
            storyboard.Begin(MainGrid, true);
        }

        private void OutputFilenameBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(e.FormatToApply))
            {
                return;
            }

            var text = e.DataObject.GetData(e.FormatToApply).ToString();
            if (ProhibitedFilenameChars.Any(text.Contains))
            {
                e.CancelCommand();
            }
        }

        private void OutputFilenameBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (ProhibitedFilenameChars.Any(e.Text.Contains))
            {
                e.Handled = true;
            }
        }

        private void OutputFolderBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        return;
                    }

                    Data.SelectedOutputFolder = dlg.SelectedPath;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                Data.SelectedOutputFolder = null;
            }
        }

        private void OutputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilesList.SelectedItem != null)
            {
                var output = FilesList.SelectedItem as VideoFile;
                OpenVideo(output);
                storyboard.Seek(MainGrid, TimeSpan.FromSeconds(output.Start), TimeSeekOrigin.BeginTime);
            }
            else
            {
                Data.CurrentFile = null;
                storyboard.Stop(MainGrid);
            }
        }

        private void PlayPause()
        {
            if (storyboard.GetIsPaused(MainGrid))
            {
                storyboard.Resume(MainGrid);
            }
            else
            {
                storyboard.Pause(MainGrid);
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            storyboard?.Pause(MainGrid);
            var job = Data.CurrentJob;
            if (string.IsNullOrWhiteSpace(job.OutputFolder))
            {
                if (string.IsNullOrWhiteSpace(Data.OutputFolder))
                {
                    using (var dlg = new FolderBrowserDialog())
                    {
                        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        {
                            MessageBox.Show(Properties.Resources.NeedOutputFolder, "Folder is required");
                            return;
                        }

                        Data.SelectedOutputFolder = dlg.SelectedPath;
                    }
                }

                job.OutputFolder = Data.OutputFolder;
            }

            var progress = ProgressWindow.Show(this, job.Files.Sum(f => f.CutDuration));
            try
            {
                await FFMpeg.Instance.DoJob(
                    job,
                    cur =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            progress.Progress.Value = cur.Current;
                            if (cur.Estimated.TotalHours >= 1)
                            {
                                progress.EstimatedTime.Text = $"{Math.Floor(cur.Estimated.TotalHours)}:{cur.Estimated.Minutes:00}:{cur.Estimated.Seconds:00}";
                            }
                            else
                            {
                                progress.EstimatedTime.Text = $"{cur.Estimated.Minutes:00}:{cur.Estimated.Seconds:00}";
                            }
                        });
                    },
                    progress.CancellationToken);
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
            var view = CollectionViewSource.GetDefaultView(FilesList.ItemsSource);
            view.Refresh();
        }

        private async void SaveJob(object sender, RoutedEventArgs e)
        {
            await SaveJob();
        }

        private async Task SaveJob()
        {
            if (string.IsNullOrWhiteSpace(Data.CurrentJob.JobFilePath))
            {
                await SaveJobAs();
            }
            else
            {
                await Data.SaveJob(Data.CurrentJob.JobFilePath);
            }
        }

        private async void OnSaveJobAs(object sender = null, RoutedEventArgs e = null)
        {
            await SaveJobAs();
        }

        private async Task SaveJobAs()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JoinerSplitter job file (*.jsj)|*.jsj",
                DefaultExt = ".jsj",
                OverwritePrompt = true,
            };
            if (!string.IsNullOrWhiteSpace(Data.CurrentJob.JobFilePath))
            {
                dlg.FileName = Path.GetFileNameWithoutExtension(Data.CurrentJob.JobFilePath);
                dlg.InitialDirectory = Path.GetDirectoryName(Data.CurrentJob.JobFilePath);
            }
            else
            {
                var firstFile = Data.CurrentJob.Files.FirstOrDefault()?.FilePath;
                if (!string.IsNullOrWhiteSpace(firstFile))
                {
                    var folder = Path.GetDirectoryName(firstFile);
                    if (!string.IsNullOrWhiteSpace(folder))
                    {
                        dlg.FileName = Path.GetFileNameWithoutExtension(Path.GetFileName(folder));
                        dlg.InitialDirectory = folder;
                    }
                }
            }

            var result = dlg.ShowDialog();
            if (result == false)
            {
                return;
            }

            await Data.SaveJob(dlg.FileName);
        }

        private void Seek(TimeSpan timeSpan, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            try
            {
                wasPaused = storyboard.GetIsPaused(MainGrid);
                storyboard.SeekAlignedToLastTick(MainGrid, timeSpan, origin);
            }
            catch (InvalidOperationException)
            {
                // Ignore if file selection happened;
            }
        }

        private void Slider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            wasPaused = storyboard.GetIsPaused(MainGrid);
            storyboard.Pause(MainGrid);
        }

        private void Slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!wasPaused)
            {
                storyboard.Resume(MainGrid);
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!changingSlider)
            {
                Seek(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        private void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            Data.SplitCurrentVideo(Slider.Value);
            RefreshList();
        }

        private void Storyboard_CurrentTimeInvalidated(object sender, EventArgs e)
        {
            changingSlider = true;
            Slider.Value = ((ClockGroup)sender).CurrentTime?.TotalSeconds ?? 0;
            changingSlider = false;
        }
    }
}