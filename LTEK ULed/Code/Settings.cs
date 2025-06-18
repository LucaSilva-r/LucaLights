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
    public partial class Settings : ObservableObject
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
            if (Instance == null)
            {
                Instance = this;
            }
        }

        [JsonConstructor]
        public Settings(ObservableCollection<Device> devices, ObservableCollection<Effect> effects)
        {

            this.Devices = devices;
            this.Effects = effects;
        }

        //Devices
        public void RemoveDevice(Device device)
        {
            Dirty = true;
            Devices.Remove(device);
            Save();
        }

        public void RemoveDeviceAt(int index)
        {
            Dirty = true;
            Devices.RemoveAt(index);
            Save();
        }

        public void AddDevice(Device device)
        {
            Dirty = true;
            Devices.Add(device);
            Save();
        }

        //Effects
        public void RemoveEffect(Effect effect)
        {
            Dirty = true;
            Effects.Remove(effect);
            Save();
        }

        public void RemoveEffectAt(int index)
        {
            Dirty = true;
            Effects.RemoveAt(index);
            Save();
        }

        public void AddEffect(Effect effect)
        {
            Dirty = true;
            Effects.Add(effect);
            Save();
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
                        Instance = settings;
                        Debug.WriteLine("Settings Loaded Succesfully");

                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("Settings Loaded Unsuccesfully");
                        Instance = new();
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Instance = new();
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("Created new settings file");

                file.Directory?.Create();
                Instance = new();
                return false;
            }
        }

        public static void Save()
        {
            string file = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json";
            try
            {
                string? json = JsonSerializer.Serialize(Instance, new JsonSerializerOptions() { WriteIndented = true });
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
