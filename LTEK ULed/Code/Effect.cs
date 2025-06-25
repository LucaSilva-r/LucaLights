using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using ColorMine.ColorSpaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using DynamicData;
using LTEK_ULed.Code.Utils;
using LTEK_ULed.Controls;
using LTEK_ULed.Validators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class LightEffect : ObservableObject
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
        private ObservableCollection<Segment> segments = new();

        public Color[][] leds = new Color[0][];

        [ObservableProperty]
        [property: JsonPropertyName("tempColor"), JsonConverter(typeof(ColorJsonConverter))]
        private Color _tempColor = Color.FromRgb(0, 255, 255);

        [ObservableProperty]
        [property: JsonPropertyName("gradient")]
        private LinearGradientBrush _gradient = new LinearGradientBrush()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = {
                new GradientStop(Color.Parse("Red"), 0),
                new GradientStop(Color.Parse("White"), 1)
            }
        };

        [RelayCommand]
        [property: JsonIgnore]
        public async Task DeleteEffect()
        {
            object? result = await DialogHost.Show(new ConfirmationDialog() { Description = "Are you sure you want to delete effect " + Name }, "Dialog");
            if (result != null && (bool)result)
            {
                Settings.Instance!.RemoveEffect(this);
            }
        }

        [RelayCommand]
        [property: JsonIgnore]
        public async Task EditEffect()
        {
            string json = JsonSerializer.Serialize(this, JsonOptions.jsonSerializerOptionsForPropertyModel);
            LightEffect? effect = JsonSerializer.Deserialize<LightEffect>(json, JsonOptions.jsonSerializerOptionsForPropertyModel);

            if (effect == null)
            {
                Debug.WriteLine("Failed to deserialize LightEffect for editing.");
                return;
            }

            object? obj = await DialogHost.Show(new EffectSetup(effect!));

            if (obj != null)
            {
                Name = effect!.Name;
                LightMapping = effect.LightMapping;
                ButtonMapping = effect.ButtonMapping;
                TempColor = effect.TempColor;
                Gradient = effect.Gradient;
                Settings.Save();

            }
        }

        public LightEffect(string name, GameButton buttonMapping, CabinetLight lightMapping, Color tempColor, int groupId, LinearGradientBrush gradient)
        {
            Name = name;
            ButtonMapping = buttonMapping;
            LightMapping = lightMapping;
            GroupId = groupId;
            TempColor = tempColor;

            if (gradient == null)
            {
                gradient = new LinearGradientBrush()
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                        new GradientStop(Color.Parse("Red"), 0),
                        new GradientStop(Color.Parse("White"), 1)
                    }
                };
            }
            gradient.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
            gradient.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
            if (gradient.GradientStops.Count == 0)
            {
                gradient.GradientStops = new()
                {
                    new GradientStop(Color.Parse("Red"), 0),
                    new GradientStop(Color.Parse("White"), 1)
                };
            }
           Gradient = gradient;


            Recalculate();
        }


        public void Recalculate()
        {
            lock (Settings.Lock)
            {
                if (Segments != null)
                {
                    leds = new Color[Segments.Count][];
                    for (int i = 0; i < Segments.Count; i++)
                    {
                        leds[i] = new Color[Segments[i].Length];
                    }
                }
            }
        }

        public void Render(GameButton button, CabinetLight light)
        {
            bool clicked = (((int)button & (int)ButtonMapping) != 0) || (((int)light & (int)LightMapping) != 0);
            for (int i = 0; i < leds.Length; i++)
            {
                if (clicked)
                {
                    FillSegment(leds[i], TempColor);
                }
                else
                {
                    FillSegment(leds[i], Color.FromRgb(0, 0, 0));
                }
            }
        }

        void FillSegment(Color[] leds, Color color)
        {
            for (int i = 0; i < leds.Length; i++)
            {
                leds[i] = color;
            }
        }

        void CollapsingAnimation(Segment segment, int numSegments, float time, Color color)
        {
            time = Math.Clamp(time, 0, 1);
            int length = (int)Math.Ceiling(Extension.Map(time, 0, 1, 0, segment.leds.Length));

            for (int i = 0; i < length; i++)
            {
                segment.leds[i] = color;
            }
        }

        Color FireAnimation(float t)
        {
            Hsv fire = new Hsv();
            fire.S = Extension.Map(Math.Clamp(t, 0, 20), 0, 20, 1, 0);
            fire.V = 1;
            fire.H = 0;
            IRgb c = fire.ToRgb();

            return Color.FromRgb((byte)c.R, (byte)c.G, (byte)c.B);
        }

        Color RainbowAnimation(float t)
        {
            Hsv rainbow = new Hsv();
            rainbow.S = 1;
            rainbow.V = 1;
            rainbow.H = t;
            IRgb c = rainbow.ToRgb();

            return Color.FromRgb((byte)c.R, (byte)c.G, (byte)c.B);
        }

    }
}
