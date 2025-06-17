using LTEK_ULed.Validators;
using LTEK_ULed.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    [Serializable]
    public class Effect : ViewModelBase
    {

        private string _name = "New Effect";

        [NameValidation]
        public string name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public GameButton button { get; set; }
        public CabinetLight light { get; set; }
        public readonly int groupId;

        [JsonIgnore]
        public ObservableCollection<Segment> segments { get; private set; } = new();
    
        public Effect(string name, GameButton button, CabinetLight light, int groupId)
        {
            this.name = name;
            this.button = button;
            this.light = light;
            this.groupId = groupId;
        }

        public void Render()
        {

        }
    }
}
