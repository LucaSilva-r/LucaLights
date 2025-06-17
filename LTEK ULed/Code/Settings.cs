using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LTEK_ULed.Code
{
    [Serializable]
    internal partial class Settings: ObservableObject
    {
        public static Settings? Instance;

        [ObservableProperty]
        [property: JsonPropertyName("devices")]
        public ObservableCollection<Device> _devices = new();

        [ObservableProperty]
        [property: JsonPropertyName("effects")]
        public ObservableCollection<Effect> _effects = new();

        [JsonIgnore]
        public bool Dirty { get; private set; } = true;


        public Settings()
        {
            if (Settings.Instance == null)
            {
                Settings.Instance = this;
            }
        }

        [JsonConstructor]
        public Settings(ObservableCollection<Device> devices, ObservableCollection<Effect> effects)
        {

            this.Devices = devices;

            if (Settings.Instance == null)
            {
                Settings.Instance = this;
            }
            else
            {
                lock (Settings.Instance!)
                {
                    Settings.Instance.Devices = devices;
                    Settings.Instance.Effects = effects;
                    Settings.Instance.Dirty = true;
                }
            }
        }

        public void RemoveDevice(int index)
        {
            Dirty = true;
            Devices.RemoveAt(index);
        }

        public void AddDevice(Device device)
        {
            Dirty = true;
            Devices.Add(device);
        }

        public void ClearDirty()
        {
            Dirty = false;
        }

        public void MarkDirty()
        {
            Dirty = true;
        }

        public static bool Load()
        {

            var file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
            Debug.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json"))
            {
                try
                {
                    Settings? settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file.FullName));
                    if (settings != null)
                    {
                        Settings.Instance = settings;
                        return true;
                    } else
                    {
                        Settings.Instance = new ();
                        return false;
                    }
                }
                catch
                {
                    Settings.Instance = new();
                    return false;
                }
            }
            else
            {
                file.Directory?.Create();
                Settings.Instance = new();
                return false;
            }
        }

        public static void Save()
        {
            string file = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json";
            try
            {
                string? json = JsonSerializer.Serialize(Settings.Instance, new JsonSerializerOptions() { WriteIndented = true });
                if (json != null)
                {
                    File.WriteAllText(file, json);
                    Debug.WriteLine("Saved");
                    Debug.WriteLine(json);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }
    }
}
