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
    }

    private void toolBar_Loaded(object sender, RoutedEventArgs e)
    {
      var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
      if (overflowGrid != null)
      {
        overflowGrid.Visibility = Visibility.Collapsed;
      }
    }

    private async void addButton_Click(object sender, RoutedEventArgs e)
    {
      var dlg = new OpenFileDialog { Filter = "Video files (mov, mp4, avi, wmv)|*.mov;*.mp4;*.avi;*.wmv", Multiselect = true };
      var result = dlg.ShowDialog();
      if (result == false) return;

      var ffmpeg = new FFMpeg();
      foreach (var file in dlg.FileNames.Select(p => new VideoFile(p)))
      {
        file.End = file.Duration = await ffmpeg.GetDuration(file.FilePath.LocalPath);
        DataContext.CurrentJob.InputFiles.Add(file);
      }
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
      storyboard.SeekAlignedToLastTick(mainGrid, TimeSpan.Zero, TimeSeekOrigin.BeginTime);
    }

    private void Button_End(object sender, RoutedEventArgs e)
    {
      Seek(TimeSpan.FromMilliseconds(-100), TimeSeekOrigin.Duration);
    }

    private void Seek(TimeSpan timeSpan, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
    {
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

    private void Button_ToOutput(object sender, RoutedEventArgs e)
    {
      var output = new VideoFile
      {
        Start = TimeSpan.FromSeconds(slider.SelectionStart),
        End = TimeSpan.FromSeconds(slider.SelectionEnd),
        GroupIndex = 1,
        FilePath = DataContext.CurrentFile.FilePath,
        ReadOnly = false,
        Duration = DataContext.CurrentFile.Duration
      };

      DataContext.CurrentJob.OutputFiles.Add(output);

    }

    private void inputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (inputList.SelectedItem == null) return;
      outputList.SelectedItem = null;
      var input = inputList.SelectedItem as VideoFile;
      input.Start = TimeSpan.Zero;
      input.End = input.Duration;
      OpenVideo(input);
    }

    private void OpenVideo(VideoFile video)
    {
      storyboard.Stop(mainGrid);
      DataContext.CurrentFile = video;
      storyboard.Begin(mainGrid, true);
    }

    private void outputList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (outputList.SelectedItem == null) return;
      inputList.SelectedItem = null;
      var output = outputList.SelectedItem as VideoFile;
      OpenVideo(output);
      storyboard.Seek(mainGrid, output.Start, TimeSeekOrigin.BeginTime);
    }

    private async void processButton_Click(object sender, RoutedEventArgs e)
    {
      var ffmpeg = new FFMpeg();
      await ffmpeg.DoJob(DataContext.CurrentJob);
    }
  }
}
