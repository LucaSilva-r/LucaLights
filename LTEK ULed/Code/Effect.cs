using CommunityToolkit.Mvvm.ComponentModel;
using LTEK_ULed.Validators;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Effect : ObservableObject
    {
        [ObservableProperty]
        [property:JsonPropertyName("name"), NameValidation]
        private string _name = "New Effect";


        [ObservableProperty]
        [property: JsonPropertyName("button")]
        private GameButton _button;

        [ObservableProperty]
        [property: JsonPropertyName("light")]
        private CabinetLight _light;

        [ObservableProperty]
        [property: JsonPropertyName("groupId")]
        private int _groupId;

        [JsonIgnore]
        public ObservableCollection<Segment> segments { get; private set; } = new();
    
        public Effect(string name, GameButton button, CabinetLight light, int groupId)
        {
            this.Name = name;
            this.Button = button;
            this.Light = light;
            this.GroupId = groupId;
        }

        public void Render()
        {

        }
    }
}
