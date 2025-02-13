using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace CubaseControl
{
    public class Preset
    {
        public string Name { get; set; } = "New Preset";
        public List<TrackData> Tracks { get; set; }
    }

    public class TrackData
    {
        public string Name { get; set; } = "Mixer 0";
        public int Number { get; set; }
        public int Volume { get; set; } = 50;//범위 0~127
        public bool IsMuted { get; set; } = false;
    }

    public class SettingJson
    {
        //key: 프리셋 이름, value: 프리셋 경로
        public Dictionary<string, string> Presets { get; set; }
    }

    internal static class FileControl
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        //앱 실행시 호출됨, 가장 최근 사용한 프리셋으로 세팅
        //d. 최근 사용한 프리셋이 없을 경우 자동으로 File/New 동작 수행
        public static void OpenRecentPresetOnStarted()
        {

        }

        //a. New Preset: 윈도우 filedialog이용, 현재 프리셋의 경로 기준으로 open, 완전 신규일 경우 내 문서 경로로, preset 형식의 파일 생성, 믹서 트랙 모두 리셋, setting.json에 최근 프리셋 기록 업데이트
        public static Preset CreateNewPreset()
        {
            return default;
        }

        //SettingsFilePath의 settings.json 읽어서 목록 리턴
        public static List<Preset>? ReadRecentPresetList()
        {
            SettingJson current = LoadSettingJson();

            //목록 없음: null
            return null;
        }

        //b. setting.json에 설정 대신 최근 사용한 프리셋 경로 최대 10개까지 저장, FILO 방식으로
        public static SettingJson UpdateRecentPresetList(Preset preset)
        {
            //setting.json을 확인
            SettingJson current = LoadSettingJson();
            var last = current.Presets.Last();

            bool isChanged = false;

            //1. key가 일치하는 항목이 있는지 체크
            if (current.Presets.ContainsKey(preset.Name))
            {
                if (preset.Name == last.Key)
                {
                    //해당 항목을 맨 뒤로 이동하고
                    isChanged = true;
                }
            }
            else
            {
                //현재 preset kvp를 Last에 추가, dictionary 크기 10개넘을 경우 first를 pop
                isChanged = true;
            }
            
            //setting.json 저장 및 클래스 형태 반환
            if (isChanged)
            {
                WriteSettingJson(current);
            }
        }

        //b. setting.json에 설정 대신 최근 사용한 프리셋 경로 최대 10개까지 저장, FILO 방식으로
        public static SettingJson LoadSettingJson()
        {
            CheckOrCreateSettingJson();
            //setting.json을 확인
            SettingJson current = JsonConvert.DeserializeObject<SettingJson>();

            bool isChanged = false;
            //setting.json에 저장된 각 경로들을 확인, file.exists false경우 제거
            foreach (string path in current.Presets.Values)
            { }

            if (isChanged)
            {
                WriteSettingJson(current);
            }

            if (current.Presets == null)
                current.Presets = new Dictionary<string, string>();

            return current;
        }

        public static void WriteSettingJson(SettingJson settingJson)
        {

        }

        //settings.json 존재하는지 체크 하고 없을 경우 생성
        public static void CheckOrCreateSettingJson()
        {
        }
    }
}
