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

namespace CubaseControl
{
    internal class CubaseCommunication
    {
        private const string LoopMidiExePath = @"C:\Program Files (x86)\Tobias Erichsen\loopMIDI\loopMIDI.exe";
        private const string LoopMidiProcessName = "loopMIDI";
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\Tobias Erichsen\loopMIDI\Ports";
        private static readonly string[] RequiredMidiPorts = { "CubaseControl-input", "CubaseControl-feedback" };

        public MainWindow MainWindow { get; }
        public MidiOut? ChannelInput;
        public MidiIn? ChannelFeedback;
        public CubaseCommunication(MainWindow mainWindow) 
        {
            MainWindow = mainWindow;
        }

        //MainWindow init때 실행
        private async Task OpenCommunication()
        {
            await Task.Run(EnsureLoopMIDIPorts);
            await Task.Run(InitializeMidi);
            OpenRecentPreset();
        }

        //앱 실행시 호출됨, 가장 최근 사용한 프리셋으로 세팅
        public void OpenRecentPreset()
        {
            var presets = FileControl.ReadRecentPresetList();
            //최근 사용한 프리셋이 없을 경우 자동으로 File/New 동작 수행
            Preset currentPreset = presets?.Last() ?? FileControl.CreateNewPreset();

            MainWindow.ApplyPreset(currentPreset);
        }

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

            //kill은 하지 않음
            if (Process.GetProcessesByName(LoopMidiProcessName).Length == 0 || portsAdded)
            {
                Process.Start(LoopMidiExePath);
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

        //트랙 번호 감지하여 해당되는 슬라이더의 볼륨 조정
        private void ReceiveFeedback(object? sender, MidiInMessageEventArgs e)
        {
            //받은 신호를 TrackData로 변환하고 UI업데이트하도록 지시
            TrackData next = default;
            MainWindow.UpdateTrackUI(next);
        }

        //볼륨 조정, mute 등 UI에서 수정된 사항 전송
        public void SendMidiInput(TrackData trackData)
        {
            int status;
            ChannelInput.Send(new MidiMessage(status, trackData.Number, trackData.Volume or mute).RawData);
        }
    }
}
