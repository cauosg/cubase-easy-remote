using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CubaseControl
{
    public partial class TrackDialog : Window
    {
        public string TrackName { get; private set; }
        public int TrackNumber { get; private set; }

        private readonly List<string> existingTrackNames;
        private readonly List<int> existingTrackNumbers;

        public TrackDialog(string suggestedName, int suggestedNumber, List<TrackData> existingTracks)
        {
            InitializeComponent();
            existingTrackNames = existingTracks.Select(t => t.Name).ToList();
            existingTrackNumbers = existingTracks.Select(t => t.Number).ToList();

            // 자동 설정된 트랙 이름과 번호
            TrackNameInput.Text = GetNextAvailableTrackName(suggestedName);
            TrackNumberInput.Text = GetNextAvailableTrackNumber(suggestedNumber).ToString();
        }

        private string GetNextAvailableTrackName(string baseName)
        {
            int index = 1;
            string newName = baseName;

            while (existingTrackNames.Contains(newName))
            {
                index++;
                newName = $"Mixer {index}";
            }

            return newName;
        }

        private int GetNextAvailableTrackNumber(int baseNumber)
        {
            int number = baseNumber;

            while (existingTrackNumbers.Contains(number))
            {
                number++;
            }

            return number;
        }

        private void ConfirmAddTrack_Click(object sender, RoutedEventArgs e)
        {
            TrackName = TrackNameInput.Text;
            TrackNumber = int.Parse(TrackNumberInput.Text);

            DialogResult = true;
            Close();
        }

        private void CancelAddTrack_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
