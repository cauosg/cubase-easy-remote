using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Midi;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace CubaseControl
{
    internal class CubaseCommunication
    {
        private const string LoopMidiExePath = @"C:\Program Files (x86)\Tobias Erichsen\loopMIDI\loopMIDI.exe";
        private const string LoopMidiProcessName = "loopMIDI";
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\Tobias Erichsen\loopMIDI\Ports";
        private static readonly string[] RequiredMidiPorts = { "CubaseControl-input", "CubaseControl-feedback" };
        private const int MuteControlOffset = 50;
        public MainWindow MainWindow { get; }
        public MidiOut? ChannelInput;
        public MidiIn? ChannelFeedback;

        // 전역 인스턴스로 설정하여 LoadPresetSetting 등에서 참조
        public static CubaseCommunication Instance { get; private set; }

        public CubaseCommunication(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            Instance = this;
        }

        // MainWindow 초기화 시 실행: LoopMIDI 포트 보장, MIDI 초기화, 프리셋 로드 후 최근 프리셋 적용
        public async Task OpenCommunication()
        {
            await Task.Run(() => EnsureLoopMIDIPorts());
            await Task.Run(() => InitializeMidi());
            await Task.Run(() => LoadPresetSetting());
            OpenRecentPreset();
        }

        // 최근 프리셋 적용
        public void OpenRecentPreset()
        {
            var presets = FileControl.ReadRecentPresetList();
            Preset currentPreset;
            if (presets == null || presets.Count == 0)
            {
                currentPreset = FileControl.CreateNewPreset();
            }
            else
            {
                currentPreset = presets.Last(); // 가장 최근 프리셋 선택
            }
            ApplyPreset(currentPreset);
        }

        // LoopMIDI 포트 보장: 없으면 레지스트리에서 추가 후 실행
        private void EnsureLoopMIDIPorts()
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
                Process.Start(LoopMidiExePath);
            }
        }

        // MIDI 초기화: CubaseControl-input (출력)과 CubaseControl-feedback (입력) 포트 사용
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
                    if (MidiIn.DeviceInfo(i).ProductName.Contains("CubaseControl-feedback"))
                        midiInIndex = i;
                }
                if (midiOutIndex != -1) ChannelInput = new MidiOut(midiOutIndex);
                if (midiInIndex != -1)
                {
                    ChannelFeedback = new MidiIn(midiInIndex);
                    ChannelFeedback.MessageReceived += ReceiveFeedback;
                    ChannelFeedback.Start();
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

        // MIDI 피드백 수신: 수신된 메시지를 해석하여 해당 트랙의 볼륨 등을 업데이트
        private void ReceiveFeedback(object? sender, MidiInMessageEventArgs e)
        {
            int status = e.RawMessage & 0xF0;
            if (status == 0xB0) // Control Change 메시지
            {
                int controlNumber = (e.RawMessage >> 8) & 0x7F;
                int value = (e.RawMessage >> 16) & 0x7F;

                // mute 메시지 여부 체크: mute 메시지는 (트랙번호 + MuteControlOffset)를 사용
                if (controlNumber >= MuteControlOffset)
                {
                    int trackNumber = controlNumber - MuteControlOffset;
                    var track = MainWindow.GetTrackByNumber(trackNumber);
                    if (track != null)
                    {
                        // value가 127이면 mute on, 0이면 mute off (임계치는 필요에 따라 조정)
                        track.IsMuted = (value >= 64);
                        MainWindow.UpdateTrackUI(track);
                        return;
                    }
                }

                // 그렇지 않으면 볼륨 메시지로 처리 (controlNumber를 트랙 번호로 사용)
                var volTrack = MainWindow.GetTrackByNumber(controlNumber);
                if (volTrack != null)
                {
                    volTrack.Volume = value;
                    MainWindow.UpdateTrackUI(volTrack);
                }
            }
        }

        public void SendMidiInput(TrackData trackData)
        {
            if (ChannelInput == null) return;

            int status = 0xB0; // Control Change 메시지

            if (trackData.IsMuted)
            {
                // mute 상태: mute 제어 전용 메시지 전송 (값 127)
                int muteCC = trackData.Number + MuteControlOffset;
                int muteValue = 127;
                ChannelInput.Send(new MidiMessage(status, muteCC, muteValue).RawData);
            }
            else
            {
                // unmute 상태: 먼저 mute 해제 메시지 전송 (값 0)
                int muteCC = trackData.Number + MuteControlOffset;
                int unmuteValue = 0;
                ChannelInput.Send(new MidiMessage(status, muteCC, unmuteValue).RawData);
                // 그리고 볼륨 메시지 전송
                ChannelInput.Send(new MidiMessage(status, trackData.Number, trackData.Volume).RawData);
            }
        }

        // 프리셋 설정을 로드: 최근 프리셋 파일에 저장된 설정을 읽어 Cubase에 전송
        public static void LoadPresetSetting()
        {
            // LoadPresetSetting은 최근 preset 파일을 읽어 각 트랙의 설정을 적용하는 기능입니다.
            SettingJson settings = FileControl.LoadSettingJson();
            if (settings.Presets.Count > 0)
            {
                var lastPresetPath = settings.Presets.Values.Last();
                if (File.Exists(lastPresetPath))
                {
                    try
                    {
                        string json = File.ReadAllText(lastPresetPath);
                        Preset preset = JsonConvert.DeserializeObject<Preset>(json);
                        if (preset != null && Instance != null)
                        {
                            // 각 트랙에 대해 MIDI 메시지 전송
                            foreach (var track in preset.Tracks)
                            {
                                Instance.SendMidiInput(track);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"프리셋 로드 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // 프리셋을 적용: MainWindow의 믹서 UI 업데이트 및 MIDI 전송 수행
        public static void ApplyPreset(Preset preset)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.ApplyPreset(preset);
                }
            });
        }
    }
}
