using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

namespace CubaseControl
{
    public class Preset
    {
        public string Name { get; set; } = "New Preset";
        public List<TrackData> Tracks { get; set; } = new List<TrackData>();
    }

    public class TrackData
    {
        public string Name { get; set; } = "Mixer 0";
        public int Number { get; set; }
        public int Volume { get; set; } = 50; // 범위 0~127
        public bool IsMuted { get; set; } = false;
    }

    public class SettingJson
    {
        // key: preset 이름, value: preset 파일 경로
        public Dictionary<string, string> Presets { get; set; } = new Dictionary<string, string>();
    }

    internal static class FileControl
    {
        // 최근 프리셋 기록은 최대 10개까지 setting.json에 저장 (FILO 방식)
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        // 앱 실행 시 호출: setting.json에 저장된 마지막 사용 프리셋으로 세팅  
        // 만약 최근 프리셋이 없거나 열리지 않으면 자동으로 File/New 동작을 수행
        public static void OpenRecentPresetOnStarted()
        {
            SettingJson setting = LoadSettingJson();
            if (setting.Presets == null || setting.Presets.Count == 0)
            {
                // 최근 프리셋이 없으므로 새 프리셋 생성
                Preset newPreset = CreateNewPreset();
                // ApplyPreset는 CubaseCommunication에서 호출됨
                CubaseCommunication.ApplyPreset(newPreset);
            }
            else
            {
                // 가장 최근 항목부터 순서대로 열어본다.
                foreach (var path in setting.Presets.Values.Reverse())
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            string json = File.ReadAllText(path);
                            Preset preset = JsonConvert.DeserializeObject<Preset>(json);
                            if (preset != null)
                            {
                                CubaseCommunication.ApplyPreset(preset);
                                return;
                            }
                        }
                        catch { }
                    }
                }
                // 모든 최근 프리셋을 열지 못한 경우 새 프리셋 생성
                Preset newPreset = CreateNewPreset();
                CubaseCommunication.ApplyPreset(newPreset);
            }
        }

        // New Preset: 파일 다이얼로그를 이용하여 .preset 파일 생성  
        // (여기서는 간략화를 위해 기본 My Documents 경로에 자동 저장)
        // 믹서 트랙은 모두 리셋되며, 생성 후 recent preset 목록이 업데이트됨
        public static Preset CreateNewPreset()
        {
            // 최근 프리셋 경로를 우선적으로 사용
            SettingJson setting = LoadSettingJson();
            string presetFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (setting.Presets != null && setting.Presets.Count > 0)
            {
                // 가장 최근 프리셋의 경로에서 폴더를 추출
                var lastPresetPath = setting.Presets.Values.Last();
                string? folder = Path.GetDirectoryName(lastPresetPath);
                if (!string.IsNullOrEmpty(folder))
                {
                    presetFolder = folder;
                }
            }

            // 프리셋 파일명 생성
            string fileName = $"Preset_{DateTime.Now:yyyyMMddHHmmss}.preset";
            string filePath = Path.Combine(presetFolder, fileName);

            // 신규 프리셋 생성: 기본 트랙 데이터는 비어있음
            Preset newPreset = new Preset { Name = Path.GetFileNameWithoutExtension(fileName) };
            newPreset.Tracks = new List<TrackData>();

            // 프리셋 저장 (.preset 파일에 JSON 형식으로 저장)
            File.WriteAllText(filePath, JsonConvert.SerializeObject(newPreset, Formatting.Indented));

            // 최근 프리셋 목록 업데이트 (최대 10개)
            UpdateRecentPresetList(newPreset, filePath);

            // 프리셋 생성/변경 시 CubaseCommunication.LoadPresetSetting() 호출
            CubaseCommunication.LoadPresetSetting();

            return newPreset;
        }

        // setting.json 파일을 읽어 최근 프리셋 목록을 반환 (프리셋이 없으면 null)
        public static List<Preset>? ReadRecentPresetList()
        {
            SettingJson current = LoadSettingJson();
            if (current.Presets == null || current.Presets.Count == 0)
                return null;

            List<Preset> presets = new List<Preset>();
            foreach (var kvp in current.Presets)
            {
                if (File.Exists(kvp.Value))
                {
                    string json = File.ReadAllText(kvp.Value);
                    Preset preset = JsonConvert.DeserializeObject<Preset>(json);
                    if (preset != null)
                        presets.Add(preset);
                }
            }
            return presets;
        }

        // 최근 프리셋 목록 업데이트: FILO 방식으로 최대 10개까지 저장  
        // preset 이름이 이미 존재하면 제거한 후 새 항목 추가
        public static SettingJson UpdateRecentPresetList(Preset preset, string presetPath)
        {
            SettingJson current = LoadSettingJson();

            if (current.Presets.ContainsKey(preset.Name))
            {
                current.Presets.Remove(preset.Name);
            }
            current.Presets[preset.Name] = presetPath;

            // 최대 10개를 초과하면 가장 오래된 항목부터 제거
            while (current.Presets.Count > 10)
            {
                string firstKey = current.Presets.Keys.First();
                current.Presets.Remove(firstKey);
            }
            WriteSettingJson(current);
            return current;
        }

        // setting.json을 로드. 존재하지 않거나 유효하지 않은 경로 제거 후 반환.
        public static SettingJson LoadSettingJson()
        {
            CheckOrCreateSettingJson();
            string json = File.ReadAllText(SettingsFilePath);
            SettingJson current = JsonConvert.DeserializeObject<SettingJson>(json) ?? new SettingJson();

            // 존재하지 않는 preset 파일 경로 제거
            var keysToRemove = current.Presets.Where(kvp => !File.Exists(kvp.Value)).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                current.Presets.Remove(key);
            }
            WriteSettingJson(current);
            return current;
        }

        public static void WriteSettingJson(SettingJson settingJson)
        {
            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(settingJson, Formatting.Indented));
        }

        // settings.json 존재 여부를 체크하고 없으면 생성
        public static void CheckOrCreateSettingJson()
        {
            if (!File.Exists(SettingsFilePath))
            {
                SettingJson newSettings = new SettingJson();
                WriteSettingJson(newSettings);
            }
        }
    }
}
