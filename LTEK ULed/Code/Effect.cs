using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using DynamicData;
using LTEK_ULed.Controls;
using LTEK_ULed.Validators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Effect : ObservableObject
    {
        [ObservableProperty]
        [property: JsonPropertyName("name"), NameValidation]
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

        [RelayCommand]
        [property: JsonIgnore]
        public async Task DeleteDevice()
        {
            object? result = await DialogHost.Show(new ConfirmationDialog() { Description = "Are you sure you want to delete effect " + Name }, "Dialog");
            if (result != null && (bool)result)
            {
                Settings.Instance!.RemoveEffect(this);
            }
        }

        [RelayCommand]
        [property: JsonIgnore]
        public async Task EditDevice()
        {
            string json = JsonSerializer.Serialize(this);
            Effect effect = JsonSerializer.Deserialize<Effect>(json);
            object? obj = await DialogHost.Show(new EffectSetup(effect!));

            if (obj != null)
            {
                Name = effect!.Name;
                LightMapping = effect.LightMapping;
                ButtonMapping = effect.ButtonMapping;
                Settings.Save();

            }
        }
    }
}
