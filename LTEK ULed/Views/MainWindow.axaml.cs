using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LTEK_ULed.Code;
using System;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using System.Security.Cryptography;

namespace LTEK_ULed.Views;

public partial class MainWindow : Window
{
    FileInfo file;
    Settings settings = new Settings();
    GameState gameState = GameState.gameState;

    public static MainWindow? Instance { get; private set; }

    Rectangle[] p1Pad = new Rectangle[9];
    Rectangle[] p2Pad = new Rectangle[9];

    public MainWindow()
    {
        Instance = this;

        InitializeComponent();

        PipeManager.Start();
        LightingManager.Start();

        file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
        Debug.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
        if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json"))
        {
            Settings? temp = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file.FullName));
            if (temp != null)
            {
                settings = temp;
            }
            ipAddressTextBox.Text = settings.ip;
        }
        else
        {
            file.Directory?.Create();
        }

        for (int i = 0; i < 9; i++)
        {
            p1Pad[i] = player1.FindControl<Rectangle>("r1" + i);
        }
    }

    public void UpdateUi()
    {
        updatePad(p1Pad, true, gameState.state.gameButton);

        updateCabinetLighting();

    }

    private void updateCabinetLighting()
    {

        SolidColorBrush active = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        SolidColorBrush inactive = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        CabinetLight cabinetLight = GameState.gameState.state.cabinetLight;
        
        bassLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_BASS_LEFT) ? active : inactive;
        bassRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_BASS_RIGHT) ? active : inactive;
        
        mUpLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_UP_LEFT) ? active : inactive;
        mUpRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_UP_RIGHT) ? active : inactive;

        mDownLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_LR_LEFT) ? active : inactive;
        mDownRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_LR_RIGHT) ? active : inactive;

    }

    private void updatePad(Rectangle[] pad, bool player1, GameButton gameButton)
    {
        SolidColorBrush active = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        SolidColorBrush inactive = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        if (player1)
        {
            //p1Pad[0].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_01) ? active : inactive;
            p1Pad[1].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_03) ? active : inactive;
            //p1Pad[2].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_03) ? active : inactive;
            p1Pad[3].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_01) ? active : inactive;
            //p1Pad[4].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_05) ? active : inactive;
            p1Pad[5].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_02) ? active : inactive;
            //p1Pad[6].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_07) ? active : inactive;
            p1Pad[7].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_04) ? active : inactive;
            //p1Pad[8].Fill = gameButton.HasFlag(GameButton.GAME_BUTTON_CUSTOM_09) ? active : inactive;
        }
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ConnectSerial(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!DDPStreamer.connected)
        {

            if (ipAddressTextBox.Text != null && Regex.IsMatch(ipAddressTextBox.Text, "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                DDPStreamer.Connect(ipAddressTextBox.Text, 60);
                serialConnect.Content = "Disconnect";
                serialConnect.Classes.Add("Danger");


                settings.ip = ipAddressTextBox.Text;
                File.WriteAllText(file.FullName, JsonSerializer.Serialize(settings));

            }
        }
        else
        {
            DDPStreamer.Disconnect();

            serialConnect.Content = "Connect";
            serialConnect.Classes.Remove("Danger");
            serialConnect.IsEnabled = false;

            DispatcherTimer.RunOnce(() =>
            {
                serialConnect.IsEnabled = true;
            }, new TimeSpan(0, 0, 0, 2));
        }
    }

    private async void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        Window settings = new PadSettings();
        await settings.ShowDialog(this);

    }
}
