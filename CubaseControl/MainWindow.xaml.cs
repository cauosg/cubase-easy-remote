using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Midi;
using Microsoft.Win32;

namespace CubaseRemote
{
    public partial class MainWindow : Window
    {
        private const string LoopMidiExePath = @"C:\Program Files (x86)\Tobias Erichsen\loopMIDI\loopMIDI.exe";
        private const string CubaseProcessName = "Cubase";
        private const string LoopMidiProcessName = "loopMIDI";
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\Tobias Erichsen\loopMIDI\Ports";

        private static readonly string[] RequiredMidiPorts = { "CubaseControl-input", "CubaseControl-feedback" };
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private MidiOut? midiOut;
        private MidiIn? midiIn;
        private readonly Dictionary<int, string> channelMappings;
        private readonly Dictionary<string, int> volumeValues;
        private bool isUpdatingFromCubase = false; // 큐베이스 변경 감지 시 무한 루프 방지

        public MainWindow()
        {
            InitializeComponent();
            channelMappings = new Dictionary<int, string>
            {
                { 7, "Stereo Out" },
                { 20, "Track 1" },
                { 21, "Track 2" },
                { 22, "Track 3" },
                { 23, "Track 4" }
            };
            volumeValues = new Dictionary<string, int>();

            Show(); // 창을 먼저 표시
            InitializeAppAsync();
        }

        private async void InitializeAppAsync()
        {
            //UpdateStatus("Checking Cubase...");
            //await Task.Run(CheckCubaseRunning);

            UpdateStatus("Checking loopMIDI...");
            await Task.Run(EnsureLoopMIDIPorts);

            UpdateStatus("Loading volume settings...");
            await Task.Run(LoadVolumeSettings);

            UpdateStatus("Initializing MIDI...");
            await Task.Run(InitializeMidi);

            UpdateStatus("Ready");
        }

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusBar.Text = message; // UI 요소가 있어야 함 (XAML에서 StatusBar 추가 필요)
            });
        }

        private void CheckCubaseRunning()
        {
            if (Process.GetProcessesByName(CubaseProcessName).Length == 0)
            {
                MessageBox.Show("Cubase가 실행되지 않았습니다. 먼저 실행하세요!", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(0);
            }
        }

        private async Task EnsureLoopMIDIPorts()
        {
            bool portsAdded = false;

            foreach (string port in RequiredMidiPorts)
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Tobias Erichsen\loopMIDI\Ports", true))
                {
                    if (key == null || key.GetValue(port) == null)
                    {
                        Registry.SetValue(RegistryPath, port, 1, RegistryValueKind.DWord);
                        portsAdded = true;
                    }
                }
            }

            if (Process.GetProcessesByName(LoopMidiProcessName).Length == 0 || portsAdded)
            {
                //foreach (var process in Process.GetProcessesByName(LoopMidiProcessName))
                //{
                //    process.Kill();
                //}
                Process.Start(LoopMidiExePath);
            }

            // 500ms 간격으로 최대 10초(20회) 동안 loopMIDI 포트가 활성화되었는지 확인
            bool portsReady = false;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500);

                bool allPortsExist = RequiredMidiPorts.All(port =>
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Tobias Erichsen\loopMIDI\Ports", false))
                    {
                        return key?.GetValue(port) != null;
                    }
                });

                if (allPortsExist)
                {
                    portsReady = true;
                    break;
                }
            }

            if (!portsReady)
            {
                MessageBox.Show("loopMIDI 포트 활성화 실패! CubaseControl-input 및 CubaseControl-feedback이 추가되었는지 확인하세요.",
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeMidi()
        {
            try
            {
                int midiOutIndex = -1;
                int midiInIndex = -1;

                for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                {
                    if (MidiOut.DeviceInfo(i).ProductName.Contains("CubaseControl-input"))
                        midiOutIndex = i;
                }
                for (int i = 0; i < MidiIn.NumberOfDevices; i++)
                {
                    var info = MidiIn.DeviceInfo(i);
                    if (info.ProductName.Contains("CubaseControl-feedback"))
                        midiInIndex = i;
                }

                if (midiOutIndex != -1) midiOut = new MidiOut(midiOutIndex);
                if (midiInIndex != -1)
                {
                    midiIn = new MidiIn(midiInIndex);
                    midiIn.MessageReceived += MidiIn_MessageReceived;
                    midiIn.Start();
                }
                else
                {
                    MessageBox.Show("CubaseControl-feedback MIDI Input 포트를 찾을 수 없습니다!", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MIDI 초기화 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MidiIn_MessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            int status = e.RawMessage & 0xF0;
            int controlNumber = (e.RawMessage >> 8) & 0x7F;
            int value = (e.RawMessage >> 16) & 0x7F;

            if (status == 0xB0 && channelMappings.ContainsKey(controlNumber))
            {
                string channelName = channelMappings[controlNumber];
                volumeValues[channelName] = value;

                Dispatcher.Invoke(() =>
                {
                    isUpdatingFromCubase = true;
                    VolumeSlider.Value = value;
                    VolumeLabel.Text = $"Volume: {value}";
                    isUpdatingFromCubase = false;
                });
            }
        }

        private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
  
            int volume = (int)e.NewValue;
            VolumeLabel.Text = $"Volume: {volume}";

            //string selectedChannel = ChannelList.SelectedItem?.ToString() ?? "";
            int selectedCC = 7;// channelMappings.FirstOrDefault(x => x.Value == selectedChannel).Key;

            SendMidiCC(selectedCC, volume);
        }

        private void SendMidiCC(int controlNumber, int value)
        {
            if (midiOut == null) return;

            int status = 0xB0;
            midiOut.Send(new MidiMessage(status, controlNumber, value).RawData);
        }

        private void SaveVolumeSettings()
        {
            var settings = new Dictionary<string, int>();

            foreach (var channel in channelMappings.Values)
            {
                if (volumeValues.ContainsKey(channel))
                    settings[channel] = volumeValues[channel];
            }

            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));
        }

        private void LoadVolumeSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(SettingsFilePath));
                if (settings != null)
                {
                    foreach (var kvp in settings)
                    {
                        volumeValues[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 설정 파일이 없거나 채널 값이 없는 경우 기본값 50 설정
            foreach (var channel in channelMappings.Values)
            {
                if (!volumeValues.ContainsKey(channel))
                {
                    volumeValues[channel] = 50;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveVolumeSettings();

            //foreach (var process in Process.GetProcessesByName(LoopMidiProcessName))
            //{
            //    process.Kill();
            //}

            base.OnClosed(e);
        }
    }
}
