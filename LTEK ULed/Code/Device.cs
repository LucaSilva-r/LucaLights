using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Threading;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LTEK_ULed.Validators;
using LTEK_ULed.ViewModels;
using System.ComponentModel.DataAnnotations;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;

namespace LTEK_ULed.Code
{
    [Serializable]
    public partial class Device : ViewModelBase
    {
        private string _name = "New Device";

        [NameValidation]
        public string name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        private string _ip = "192.168.1.1";

        [IpAddressValidation]
        public string ip
        {
            get => _ip;
            set => SetProperty(ref _ip, value, true);
        }

        [JsonIgnore]
        public int Nsegments { get; set; } = 0;
        [JsonIgnore]
        public int Nleds { get; set; } = 0;

        public ObservableCollection<Segment> segments { get; private set; } = new ObservableCollection<Segment>();

        private Color[] data = new Color[0];

        DDPSend? dDPsend;

        public Device(string name, string ip, ObservableCollection<Segment> segments)
        {
            this.name = name;

            this.ip = ip;
            this.segments = segments;

            Recalculate();

        }

        [RelayCommand]
        [property: JsonIgnore]
        public void SaveDevice(Window window)
        {
            if (!Settings.Instance!.devices.Contains(this))
            {
                Settings.Instance.devices.Add(this);
            }
            IReadOnlyList<Window> owner = (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.Windows!;

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
        public void EditDevice()
        {
            Window owner = (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow!;
            DeviceSetup deviceSetup = new DeviceSetup()
            {
                DataContext = this,
            };

            deviceSetup.ShowDialog(owner);
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void RemoveSegment(Segment segment)
        {
            Settings.Instance!.MarkDirty();

            segments.Remove(segment);
            Recalculate();
        }

        [RelayCommand]
        [property: JsonIgnore]
        public void AddSegment()
        {
            Settings.Instance!.MarkDirty();

            segments.Add(new Segment("New Segment #" + (segments.Count + 1), 1, 0, 0));

            Recalculate();
        }

        public void Recalculate()
        {
            int counter = 0;

            foreach (Segment item in segments)
            {
                counter += item.leds.Length;
            }

            data = new Color[counter];

            Nleds = counter;
            Nsegments = segments.Count;

            dDPsend?.Dispose();
            dDPsend = new DDPSend(this.ip, data.Length);
        }

        public void Send()
        {
            int counter = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                Array.Copy(segment.leds, 0, data, counter, segment.leds.Length);
                counter += segment.leds.Length;
            }

            dDPsend?.send(data);

        }

        private class DDPSend : IDisposable
        {

            private const int DDP_PORT = 4048;

            private const byte DDP_HEADER_LEN = 10;
            private const int DDP_MAX_DATALEN = (480 * 3);   // fits nicely in an ethernet packet

            private const byte DDP_FLAGS1_VER = 0xc0;   // version mask
            private const byte DDP_FLAGS1_VER1 = 0x40;   // version=1
            private const byte DDP_FLAGS1_PUSH = 0x01;
            private const byte DDP_FLAGS1_QUERY = 0x02;
            private const byte DDP_FLAGS1_REPLY = 0x04;
            private const byte DDP_FLAGS1_STORAGE = 0x08;
            private const byte DDP_FLAGS1_TIME = 0x10;

            private const byte DDP_ID_DISPLAY = 1;
            private const byte DDP_ID_CONFIG = 250;
            private const byte DDP_ID_STATUS = 251;


            struct ddp_hdr_struct
            {
                public short flags;
                public byte type;
                public byte id;
                public int offset;  // MSB
                public short len;     // MSB
            };

            ddp_hdr_struct dh = new ddp_hdr_struct();

            UdpClient client;
            IPEndPoint endPoint;
            byte[] data;

            public DDPSend(string ip, int nLeds)
            {
                dh.flags = DDP_FLAGS1_VER1 | DDP_FLAGS1_PUSH;
                dh.offset = 0;
                dh.type = 1;
                dh.len = BinaryPrimitives.ReverseEndianness((short)(nLeds * 3));
                dh.id = DDP_ID_DISPLAY;

                endPoint = new IPEndPoint(IPAddress.Parse(ip), DDP_PORT);

                client = new UdpClient();
                data = new byte[DDP_HEADER_LEN + nLeds * 3];

                byte[] array = StructureToByteArray(dh);
                for (int i = 0; i < array.Length; i++)
                {
                    data[i] = array[i];
                }
            }

            byte counter = 0;

            public void send(Color[] leds)
            {
                data[1] = (byte)((counter % 15) + 1);
                counter++;
                for (int i = 0; i < leds.Length; i++)
                {
                    data[DDP_HEADER_LEN + i * 3] = leds[i].R;
                    data[DDP_HEADER_LEN + i * 3 + 1] = leds[i].G;
                    data[DDP_HEADER_LEN + i * 3 + 2] = leds[i].B;
                }

                client.SendAsync(data, data.Length, endPoint);
            }

            private static byte[] StructureToByteArray(ddp_hdr_struct obj)
            {

                List<byte> data = new List<byte>();

                data.AddRange(BitConverter.GetBytes(obj.flags));
                data.Add(obj.type);
                data.Add(obj.id);
                data.AddRange(BitConverter.GetBytes(obj.offset));
                data.AddRange(BitConverter.GetBytes(obj.len));
                return data.ToArray();
            }

            public void Dispose()
            {
                client.Dispose();
            }
        }
    }

    [Serializable]
    public class Segment : ViewModelBase
    {
        [JsonIgnore]
        public Segment Instance;
        [JsonIgnore]
        public Color[] leds { get; private set; }

        private string _name = "New Device";

        [NameValidation]
        public string name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        [NumberValidation]
        public int length
        {
            get => _length;
            set
            {
                Settings.Instance?.MarkDirty();
                SetProperty(ref _length, value, true);
                leds = new Color[_length];
            }
        }

        private int _length;

        public GameButton buttonMapping { get; private set; }
        public CabinetLight cabinetMapping { get; private set; }

        public Segment(string name, int length, GameButton buttonMapping, CabinetLight cabinetMapping)
        {
            this.length = length;
            leds = new Color[length];
            this.buttonMapping = buttonMapping;
            this.cabinetMapping = cabinetMapping;
            this.name = name;
            Instance = this;
        }
    }
}
