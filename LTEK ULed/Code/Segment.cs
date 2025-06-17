using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Avalonia.Media;
using LTEK_ULed.Validators;
using LTEK_ULed.ViewModels;

namespace LTEK_ULed.Code
{
    [Serializable]
    public class Segment : ViewModelBase
    {
        [JsonIgnore]
        public Segment Instance;
        [JsonIgnore]
        public Color[] leds { get; private set; }

        private string _name = "New Device";
        public ObservableCollection<int> groupIds { get; private set; } = new();

        [NameValidation]
        public string name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        [NumberValidation]
        public int length
        {
            get => _length;
            set
            {
                Settings.Instance?.MarkDirty();
                SetProperty(ref _length, value, true);
                leds = new Color[_length];
            }
        }

        private int _length;

        public GameButton buttonMapping { get; set; }
        public CabinetLight cabinetMapping { get; set; }

        public Segment(string name, int length, GameButton buttonMapping, CabinetLight cabinetMapping, ObservableCollection<int>? groupIds = null)
        {
            if (groupIds != null)
                this.groupIds = groupIds;
            
            this.length = length;
            leds = new Color[length];
            this.buttonMapping = buttonMapping;
            this.cabinetMapping = cabinetMapping;
            this.name = name;
            Instance = this;
        }
    }
}
