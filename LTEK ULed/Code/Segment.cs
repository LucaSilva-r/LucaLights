using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LTEK_ULed.Validators;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Segment : ObservableObject, INotifyPropertyChanged
    {
        [JsonIgnore]
        public Segment Instance;
        [JsonIgnore]
        public Color[] leds { get; private set; } = new Color[0];

        [ObservableProperty]
        [property: JsonPropertyName("name"), NameValidation]
        private string _name = "New Segment";

        [ObservableProperty]
        [property: JsonPropertyName("groupIds"), IpAddressValidation]
        private ObservableCollection<int> _groupIds = new();


        [ObservableProperty]
        [property: JsonPropertyName("length")]
        private int _length = 1;

        public Segment(string name, int length, ObservableCollection<int>? groupIds = null)
        {
            if (groupIds != null)
                this.GroupIds = groupIds;

            Length = length;
            Name = name;
            Instance = this;
            leds = new Color[length];
            base.PropertyChanged += (propertyName, d) =>
            {
                if(d.PropertyName == nameof(Length))
                {
                    leds = new Color[Length];
                }
            };
        }

    }
}
