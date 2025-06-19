using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Settings : ObservableObject
    {
        public static readonly object Lock = new object();

        public static Settings? Instance;

        [ObservableProperty]
        [property: JsonPropertyName("devices")]
        public ObservableCollection<Device> _devices = new();

        [ObservableProperty]
        [property: JsonPropertyName("effects")]
        public ObservableCollection<Effect> _effects = new();

        [JsonIgnore]
        public bool Dirty { get; private set; } = true;


        public Settings(bool editMode = false)
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        [JsonConstructor]
        public Settings(ObservableCollection<Device> devices, ObservableCollection<Effect> effects)
        {
            lock (Lock)
            {
                this.Devices = devices;
                this.Effects = effects;
            }
        }

        //Devices
        public void RemoveDevice(Device device)
        {
            lock (Lock)
            {
                Dirty = true;

                foreach (Segment segment in device.Segments)
                {
                    foreach (int groupId in segment.GroupIds)
                    {
                        Effect? effect = Settings.Instance.Effects.FirstOrDefault(e => e.GroupId == groupId, null);
                        if (effect != null)
                        {
                            effect.Segments.Remove(segment);
                            effect.Recalculate();
                        }
                    }
                }

                Devices.Remove(device);
                Save();
            }
        }

        public void AddDevice(Device device)
        {
            lock (Lock)
            {
                Dirty = true;

                foreach (Segment segment in device.Segments)
                {
                    foreach (int groupId in segment.GroupIds)
                    {
                        Effect? effect = Settings.Instance.Effects.FirstOrDefault(e => e.GroupId == groupId, null);
                        if (effect != null && !effect.Segments.Contains(segment))
                        {
                            effect.Segments.Add(segment);
                            effect.Recalculate();
                        }
                    }
                }
                device.Recalculate();

                Devices.Add(device);
                Save();
            }
        }

        //Effects
        public void RemoveEffect(Effect effect)
        {
            lock (Lock)
            {
                Dirty = true;
                Effects.Remove(effect);
                Save();
            }
        }


        public void AddEffect(Effect effect)
        {
            lock (Lock)
            {
                Dirty = true;
                Effects.Add(effect);
                Save();
            }
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
            lock (Lock)
            {
                bool loaded = false;
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

                            loaded = true;
                        }
                        else
                        {
                            Debug.WriteLine("Settings Loaded Unsuccesfully");
                            Instance = new();
                            loaded = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Instance = new();
                        loaded = false;
                    }
                }
                else
                {
                    Debug.WriteLine("Created new settings file");

                    file.Directory?.Create();
                    Instance = new();
                    loaded = false;
                }


                foreach (Device device in Settings.Instance.Devices)
                {
                    foreach (Segment segment in device.Segments)
                    {
                        foreach (int groupId in segment.GroupIds)
                        {
                            Effect? effect = Settings.Instance.Effects.FirstOrDefault(e => e.GroupId == groupId, null);
                            if (effect != null)
                            {
                                effect.Segments.Add(segment);
                            }
                        }
                    }
                }

                return loaded;
            }

        }

        public static void Save()
        {
            lock (Lock)
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
}
