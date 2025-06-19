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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Device : ObservableObject, IDisposable
    {
        [ObservableProperty]
        [property: JsonPropertyName("name"), NameValidation]
        private string _name = "New Device";

        [ObservableProperty]
        [property: JsonPropertyName("ip"), IpAddressValidation]
        private string _ip = "192.168.1.1";


        [ObservableProperty]
        [property: JsonIgnore]
        public int _nsegments = 0;

        [ObservableProperty]
        [property: JsonIgnore]
        public int _nleds = 0;

        [ObservableProperty]
        [property: JsonPropertyName("segments")]
        private ObservableCollection<Segment> _segments = new ObservableCollection<Segment>();

        private Color[] data = new Color[0];

        DDPSend? dDPsend;
        private bool _disposed = false; // To detect redundant calls

        public Device(string name, string ip, ObservableCollection<Segment> segments)
        {
            this.Name = name;

            this.Ip = ip;
            this.Segments = segments;

            Recalculate();

        }

        public void Recalculate()
        {
            lock (Settings.Lock)
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

        [RelayCommand]
        [property: JsonIgnore]
        public async Task DeleteDevice()
        {
            object? result = await DialogHost.Show(new ConfirmationDialog() { Description = "Are you sure you want to delete device " + Name }, "Dialog");
            if (result != null && (bool)result)
            {
                Settings.Instance!.RemoveDevice(this);
            }
        }

        [RelayCommand]
        [property: JsonIgnore]
        public async Task EditDevice()
        {

            string json = JsonSerializer.Serialize(this);
            using (Device dev = JsonSerializer.Deserialize<Device>(json))
            {
                object? obj = await DialogHost.Show(new DeviceSetup(dev!));

                if (obj != null)
                {
                    lock (Settings.Lock)
                    {

                        foreach (Segment segment in Segments)
                        {
                            foreach (Effect effect in Settings.Instance!.Effects)
                            {
                                effect.Segments.Remove(segment);
                            }
                        }

                        Name = dev!.Name;
                        Ip = dev!.Ip;
                        Segments.Clear();

                        foreach (Segment segment in dev.Segments)
                        {
                            Segments.Add(segment);
                            foreach (int groupId in segment.GroupIds)
                            {
                                Effect? effect = Settings.Instance!.Effects.FirstOrDefault(x => x.GroupId == groupId);
                                if(effect != null)
                                {
                                    effect.Segments.Add(segment);
                                    effect.Recalculate();
                                }
                            }
                        }
                        Recalculate();
                        Settings.Instance!.MarkDirty();
                        Settings.Save();
                    }
                }
            }
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

            Segments.Add(new Segment("New Segment #" + (Segments.Count + 1), new()));

            Recalculate();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                dDPsend?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.
            _disposed = true;
        }

        // Add a finalizer in case Dispose() is not called
        ~Device()
        {
            Dispose(false);
        }
    }
}
