using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

namespace CubaseControl
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string SettingsFilePath = "tracks.json";
        private Preset preset;
        private string currentPresetName;

        // 메뉴 바인딩용: 현재 프리셋 이름
        public string CurrentPresetName
        {
            get => currentPresetName;
            set
            {
                if (currentPresetName != value)
                {
                    currentPresetName = value;
                    OnPropertyChanged("CurrentPresetName");
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            preset = new Preset() { Tracks = new List<TrackData>() };
            LoadTracks();
            RenderTracks();
            // Preset 이름 표시 업데이트
            CurrentPresetName = preset.Name;
            RefreshRecentPresetMenu();
        }

        #region 옵션바 메뉴 이벤트

        // File > New Preset
        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            // 새 프리셋 생성 시 최근 프리셋 기록에 저장된 폴더를 우선 선택함.
            Preset newPreset = FileControl.CreateNewPreset();
            ApplyPreset(newPreset);
            RefreshRecentPresetMenu();
        }

        // File > Load Preset
        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Preset files (*.preset)|*.preset";
            // 기본 경로는 최근 프리셋 기록이 있으면 해당 폴더, 없으면 내 문서
            SettingJson setting = FileControl.LoadSettingJson();
            if (setting.Presets != null && setting.Presets.Count > 0)
            {
                string lastPresetPath = setting.Presets.Values.Last();
                string folder = Path.GetDirectoryName(lastPresetPath);
                if (!string.IsNullOrEmpty(folder))
                    dlg.InitialDirectory = folder;
            }
            else
            {
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    Preset loadedPreset = JsonSerializer.Deserialize<Preset>(json);
                    if (loadedPreset != null)
                    {
                        ApplyPreset(loadedPreset);
                        FileControl.UpdateRecentPresetList(loadedPreset, dlg.FileName);
                        RefreshRecentPresetMenu();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"프리셋 로드 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // File > Save Preset As
        private void SavePresetAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Preset files (*.preset)|*.preset";
            // 기본 경로: 현재 프리셋 경로(최근 프리셋 기록) 또는 내 문서
            SettingJson setting = FileControl.LoadSettingJson();
            if (setting.Presets != null && setting.Presets.Count > 0)
            {
                string lastPresetPath = setting.Presets.Values.Last();
                string folder = Path.GetDirectoryName(lastPresetPath);
                if (!string.IsNullOrEmpty(folder))
                    dlg.InitialDirectory = folder;
            }
            else
            {
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                    // 최근 프리셋 목록 업데이트
                    FileControl.UpdateRecentPresetList(preset, dlg.FileName);
                    RefreshRecentPresetMenu();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"프리셋 저장 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // File > Quit
        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Add Track 메뉴: 옵션바의 오른쪽 메뉴 항목
        private void AddTrack_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = preset.Tracks.Count + 1;
            string suggestedName = $"Mixer {nextIndex}";
            int suggestedNumber = nextIndex;

            TrackDialog dialog = new TrackDialog(suggestedName, suggestedNumber, preset.Tracks);
            dialog.Owner = this; // 메인 창 중앙 팝업
            if (dialog.ShowDialog() == true)
            {
                preset.Tracks.Add(new TrackData
                {
                    Name = dialog.TrackName,
                    Number = dialog.TrackNumber,
                    Volume = 50,
                    IsMuted = false
                });
                SaveTracks();
                RenderTracks();
            }
        }

        // 최근 프리셋 메뉴 동적으로 구성
        private void RefreshRecentPresetMenu()
        {
            // RecentPresetMenu는 XAML에서 x:Name="RecentPresetMenu"로 지정되어 있어야 합니다.
            if (RecentPresetMenu == null) return;
            RecentPresetMenu.Items.Clear();
            SettingJson setting = FileControl.LoadSettingJson();
            foreach (var kvp in setting.Presets.Reverse()) // 최신순으로
            {
                MenuItem item = new MenuItem { Header = kvp.Key, Tag = kvp.Value };
                item.Click += (s, e) =>
                {
                    string path = (s as MenuItem)?.Tag as string;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            string json = File.ReadAllText(path);
                            Preset presetFromFile = JsonSerializer.Deserialize<Preset>(json);
                            if (presetFromFile != null && presetFromFile.Name != preset.Name)
                            {
                                ApplyPreset(presetFromFile);
                                FileControl.UpdateRecentPresetList(presetFromFile, path);
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        // 해당 프리셋 파일이 없으면 다음 항목을 시도하고, 모두 실패하면 File/New 동작 수행
                        MessageBox.Show("해당 프리셋을 열 수 없습니다. 새 프리셋을 생성합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        Preset newPreset = FileControl.CreateNewPreset();
                        ApplyPreset(newPreset);
                    }
                    RefreshRecentPresetMenu();
                };
                RecentPresetMenu.Items.Add(item);
            }
        }

        #endregion

        #region 믹서 트랙 관련

        // MainWindow에 적용된 프리셋을 UI에 반영합니다.
        public void ApplyPreset(Preset newPreset)
        {
            preset = newPreset;
            CurrentPresetName = preset.Name;
            RenderTracks();
        }

        // MixerPanel (UniformGrid)에 저장된 모든 트랙 UI를 재생성
        public void RenderTracks()
        {
            MixerPanel.Children.Clear();
            MixerPanel.Columns = Math.Max(1, Math.Min(preset.Tracks.Count, 5));
            foreach (var track in preset.Tracks)
            {
                MixerPanel.Children.Add(CreateTrackUI(track));
            }
        }

        // 외부(MIDI 피드백 등)에서 특정 트랙 변경 시 UI 업데이트
        public void UpdateTrackUI(TrackData trackData)
        {
            foreach (var child in MixerPanel.Children)
            {
                if (child is GroupBox group && (group.Header as string) == trackData.Name)
                {
                    if (group.Content is Grid grid)
                    {
                        foreach (UIElement element in grid.Children)
                        {
                            int row = Grid.GetRow(element);
                            if (row == 1 && element is TextBlock tb)
                            {
                                tb.Text = $"{trackData.Volume} dB";
                            }
                            else if (row == 2 && element is Slider slider)
                            {
                                if (!slider.IsFocused)
                                    slider.Value = trackData.Volume;
                            }
                            else if (row == 3 && element is CheckBox cb)
                            {
                                cb.IsChecked = trackData.IsMuted;
                            }
                        }
                    }
                    break;
                }
            }
        }

        // UniformGrid 내 각 트랙 UI 생성
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
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

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

        // Save preset tracks to JSON file
        private void SaveTracks()
        {
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(preset.Tracks, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Load preset tracks from JSON file (if exists)
        private void LoadTracks()
        {
            if (File.Exists(SettingsFilePath))
            {
                var tracks = JsonSerializer.Deserialize<List<TrackData>>(File.ReadAllText(SettingsFilePath));
                if (tracks != null)
                {
                    preset.Tracks = tracks;
                }
            }
        }

        // 헬퍼: 특정 트랙 번호에 해당하는 TrackData 반환
        public TrackData GetTrackByNumber(int trackNumber)
        {
            return preset.Tracks.FirstOrDefault(t => t.Number == trackNumber);
        }

        #endregion

        #region INotifyPropertyChanged 구현

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
