using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    [Serializable]
    internal class Settings
    {
        public static Settings? Instance;

        public ObservableCollection<Device> devices { get; set; } = new();


        [JsonIgnore]
        public bool Dirty { get; private set; } = true;

        public Settings(ObservableCollection<Device> devices)
        {
            this.devices = devices;

            if(Settings.Instance == null)
            {
                Settings.Instance = this;
            } else
            {
                lock (Settings.Instance!)
                {
                    Settings.Instance.devices.Clear();
                    Settings.Instance.Dirty = true;
                    foreach (Device device in this.devices)
                    {
                        Settings.Instance.devices.Add(device);
                    }
                }
            }
        }

        public void RemoveDevice(int index)
        {
            Dirty = true;
            devices.RemoveAt(index);
        }

        public void AddDevice(Device device)
        {
            Dirty = true;
            devices.Add(device);
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
                    if (JsonSerializer.Deserialize<Settings>(File.ReadAllText(file.FullName)) != null)
                    {
                        return true;
                    }
                    else
                    {
                        new Settings(new ObservableCollection<Device>());
                        return false;
                    }
                }
                catch
                {
                    new Settings(new ObservableCollection<Device>());
                    return false;
                }

            }
            else
            {
                file.Directory?.Create();
                new Settings(new ObservableCollection<Device>());
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
