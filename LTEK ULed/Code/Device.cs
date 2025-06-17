using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Code.Utils;
using LTEK_ULed.Controls;
using LTEK_ULed.Validators;
using LTEK_ULed.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        [property: JsonPropertyName("name"), NameValidation]
        private string _name = "New Device";

        [ObservableProperty]
        [property: JsonPropertyName("ip"), IpAddressValidation]
        private string _ip = "192.168.1.1";

        [JsonIgnore]
        public int Nsegments { get; set; } = 0;
        [JsonIgnore]
        public int Nleds { get; set; } = 0;

        [ObservableProperty]
        [property: JsonPropertyName("segments")]
        private ObservableCollection<Segment> _segments = new ObservableCollection<Segment>();

        private Color[] data = new Color[0];

        DDPSend? dDPsend;

        public Device(string name, string ip, ObservableCollection<Segment> segments)
        {
            this.Name = name;

            this.Ip = ip;
            this.Segments = segments;

            Recalculate();

        }

        [RelayCommand]
        [property: JsonIgnore]
        public void SaveDevice(Window window)
        {
            if (!Settings.Instance!.Devices.Contains(this))
            {
                Settings.Instance.Devices.Add(this);
            }
            IReadOnlyList<Window> owner = (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.Windows!;

            Settings.Save();

            for (int i = 0; i < owner.Count; i++)
            {
                Window w = owner[i];
                if (w.Name == "deviceSetup")
                {
                    w.Close();
                }
            }
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void DeleteDevice()
        {
            DialogHost.Show(new ConfirmationDialog() { Command = DeletionConfirmedCommand, Description="Are you sure you want to delete device " + Name},"Dialog");
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void DeletionConfirmed()
        {
            bool debug = Settings.Instance!.Devices.Remove(this);

            Settings.Instance.MarkDirty();
            Settings.Save();
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void EditDevice()
        {
            MainWindow.Instance!.EditDevice(this);
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void RemoveSegment(Segment segment)
        {
            Settings.Instance!.MarkDirty();

            Segments.Remove(segment);
            Recalculate();
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void AddSegment()
        {
            Settings.Instance!.MarkDirty();

            Segments.Add(new Segment("New Segment #" + (Segments.Count + 1), 1, 0, 0));

            Recalculate();
        }

        public void Recalculate()
        {
            int counter = 0;

            foreach (Segment item in Segments)
            {
                counter += item.leds.Length;
            }

            data = new Color[counter];

            Nleds = counter;
            Nsegments = Segments.Count;

            dDPsend?.Dispose();
            dDPsend = new DDPSend(this.Ip, data.Length);
        }

        public void Send()
        {
            int counter = 0;

            for (int i = 0; i < Segments.Count; i++)
            {
                Segment segment = Segments[i];
                Array.Copy(segment.leds, 0, data, counter, segment.leds.Length);
                counter += segment.leds.Length;
            }

            dDPsend?.send(data);

        }
    }
}
