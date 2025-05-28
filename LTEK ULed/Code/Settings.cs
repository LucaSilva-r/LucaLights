using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public List<Device> devices { get; set; } = new List<Device>();


        [JsonIgnore]
        public bool Dirty { get; private set; } = true;

        public Settings(List<Device> devices)
        {
            this.devices = devices;

            Settings.Instance = this;
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
                Settings? temp = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file.FullName));
                if (temp != null)
                {
                   return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                file.Directory?.Create();
                return false;
            }
        }

        public static bool Save()
        {
            string file = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json";
            try
            {
                File.WriteAllText(file, JsonSerializer.Serialize(Settings.Instance, new JsonSerializerOptions() { WriteIndented = true}));
            }
            catch
            {
                return false;
            }
            return true;

        }
    }
}
