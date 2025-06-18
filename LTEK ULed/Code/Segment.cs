using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LTEK_ULed.Validators;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Segment : ObservableObject
    {
        [JsonIgnore]
        public Segment Instance;
        [JsonIgnore]
        public Color[] leds { get; private set; }

        [ObservableProperty]
        [property: JsonPropertyName("name"), NameValidation]
        private string _name = "New Segment";

        [ObservableProperty]
        [property: JsonPropertyName("groupIds"), IpAddressValidation]
        private ObservableCollection<int> _groupIds = new();


        [ObservableProperty]
        [property: JsonPropertyName("length")]
        private int _length;

        public Segment(string name, int length, ObservableCollection<int>? groupIds = null)
        {
            if (groupIds != null)
                this.GroupIds = groupIds;
            
            this.Length = length;
            leds = new Color[length];
            this.Name = name;
            Instance = this;
        }
    }
}
