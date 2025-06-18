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
        [property: JsonPropertyName("buttonMapping")]
        private GameButton _buttonMapping;

        [ObservableProperty]
        [property: JsonPropertyName("lightMapping")]
        private CabinetLight _lightMapping;

        [ObservableProperty]
        [property: JsonPropertyName("groupId")]
        private int _groupId;

        [ObservableProperty]
        [property: JsonIgnore]
        public ObservableCollection<Segment> segments = new();
    
        public Effect(string name, GameButton buttonMapping, CabinetLight lightMapping, int groupId)
        {
            this.Name = name;
            this.ButtonMapping = buttonMapping;
            this.LightMapping = lightMapping;
            this.GroupId = groupId;
        }

        public void Render()
        {

        }
    }
}
