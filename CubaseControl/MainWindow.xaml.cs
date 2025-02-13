using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;

namespace CubaseControl
{
    public partial class MainWindow : Window
    {
        private const string SettingsFilePath = "tracks.json";
        private List<TrackData> Tracks = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadTracks();
            RenderTracks();
        }

        private void AddTrack_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = Tracks.Count + 1;
            string suggestedName = $"Mixer {nextIndex}";
            int suggestedNumber = nextIndex;

            TrackDialog dialog = new TrackDialog(suggestedName, suggestedNumber, Tracks);
            dialog.Owner = this; // 메인 창의 가운데에서 팝업
            if (dialog.ShowDialog() == true)
            {
                Tracks.Add(new TrackData { Name = dialog.TrackName, Number = dialog.TrackNumber, Volume = 50, IsMuted = false });
                SaveTracks();
                RenderTracks();
            }
        }

        private void RenderTracks()
        {
            MixerPanel.Children.Clear();
            MixerPanel.Columns = Math.Max(1, Math.Min(Tracks.Count, 5)); // 1/n 비율 유지 (최소 1:5, 최대 1:2)
            foreach (var track in Tracks)
            {
                MixerPanel.Children.Add(CreateTrackUI(track));
            }
        }

        private UIElement CreateTrackUI(TrackData track)
        {
            GroupBox trackGroup = new GroupBox
            {
                Header = track.Name,
                MinWidth = 100,
                MaxWidth = 200,
                Margin = new Thickness(5)
            };

            Grid panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 트랙명
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 볼륨 dB
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6, GridUnitType.Star) }); // 슬라이더
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }); // 뮤트 버튼

            TextBlock volumeLabel = new TextBlock
            {
                Text = $"{track.Volume} dB",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(volumeLabel, 1);

            Slider volumeSlider = new Slider
            {
                Orientation = Orientation.Vertical,
                Minimum = 0,
                Maximum = 127,
                Value = track.Volume,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            volumeSlider.ValueChanged += (s, e) =>
            {
                track.Volume = (int)e.NewValue;
                volumeLabel.Text = $"{track.Volume} dB";
                SaveTracks();
            };
            Grid.SetRow(volumeSlider, 2);

            CheckBox muteCheckbox = new CheckBox
            {
                Content = "Mute",
                IsChecked = track.IsMuted,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            muteCheckbox.Checked += (s, e) =>
            {
                track.IsMuted = true;
                SaveTracks();
            };
            muteCheckbox.Unchecked += (s, e) =>
            {
                track.IsMuted = false;
                SaveTracks();
            };
            Grid.SetRow(muteCheckbox, 3);

            panel.Children.Add(volumeLabel);
            panel.Children.Add(volumeSlider);
            panel.Children.Add(muteCheckbox);
            trackGroup.Content = panel;

            return trackGroup;
        }

        private void SaveTracks()
        {
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(Tracks));
        }

        private void LoadTracks()
        {
            if (File.Exists(SettingsFilePath))
            {
                Tracks = JsonSerializer.Deserialize<List<TrackData>>(File.ReadAllText(SettingsFilePath)) ?? new List<TrackData>();
            }
        }
    }

    public class TrackData
    {
        public string Name { get; set; }
        public int Number { get; set; }
        public int Volume { get; set; }
        public bool IsMuted { get; set; }
    }
}
