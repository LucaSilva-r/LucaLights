using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using DynamicData;
using LTEK_ULed.Code;
using LTEK_ULed.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Views;

public partial class MainWindow : Window
{
    GameState gameState = GameState.gameState;

    public static MainWindow? Instance { get; private set; }

    SolidColorBrush active = new SolidColorBrush(Color.FromRgb(255, 0, 0));
    SolidColorBrush inactive = new SolidColorBrush(Color.FromRgb(200, 200, 200));

    Dictionary<Rectangle, GameButton> RectToGB = new Dictionary<Rectangle, GameButton>();
    Dictionary<GameButton, Rectangle> GBToRect = new Dictionary<GameButton, Rectangle>();
    List<SegmentView> segmentViews = new List<SegmentView>();

    public MainWindow()
    {


        InitializeComponent();

        Instance = this;

        for (int i = 1; i <= 18; i++)
        {
            GameButton result;
            string text = Convert.ToString(i).PadLeft(2, '0');
            Enum.TryParse<GameButton>("GAME_BUTTON_CUSTOM_" + text, false, out result);
            Rectangle? rect = this.FindControl<Rectangle>("g" + text);

            RectToGB.Add(rect!, result);
            GBToRect.Add(result, rect!);
            rect!.PointerPressed += Rectangle_PointerPressed;
            rect!.PointerReleased += Rectangle_PointerReleased;
            rect!.PointerExited += Rectangle_PointerExited;

        }

        this.GetVisualDescendants().OfType<SegmentView>().ToList();
    }

    public void UpdateUi()
    {

        GameButton gameButton;
        CabinetLight cabinetLight;

        lock (gameState)
        {
            gameButton = gameState.state.gameButton;
            cabinetLight = gameState.state.cabinetLight;
        }

        updatePad(true, gameButton);
        updatePad(false, gameButton);

        updateCabinetLighting(cabinetLight);

    }
    public void UpdateLeds(bool reset = false)
    {
        if (Settings.Instance!.Dirty || segmentViews.Count == 0 || reset)
        {
            segmentViews = this.GetVisualDescendants().OfType<SegmentView>().ToList();
        }

        foreach (SegmentView segmentView in segmentViews)
        {
            segmentView.UpdateLeds();
        }
    }

    private void updateCabinetLighting(CabinetLight cabinetLight)
    {

        SolidColorBrush active = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        SolidColorBrush inactive = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        bassLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_BASS_LEFT) ? active : inactive;
        bassRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_BASS_RIGHT) ? active : inactive;

        mUpLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_UP_LEFT) ? active : inactive;
        mUpRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_UP_RIGHT) ? active : inactive;

        mDownLeft.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_LR_LEFT) ? active : inactive;
        mDownRight.Fill = cabinetLight.HasFlag(CabinetLight.LIGHT_MARQUEE_LR_RIGHT) ? active : inactive;

    }

    private void updatePad(bool player1, GameButton gameButton)
    {
        foreach (GameButton item in Enum.GetValues(typeof(GameButton)))
        {
            if (GBToRect.ContainsKey(item))
            {
                GBToRect[item]!.Fill = gameButton.HasFlag(item) ? active : inactive;
            }
        }
    }


    private void Rectangle_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        (sender as Rectangle)!.Fill = active;

        lock (gameState)
        {
            gameState.state.gameButton |= RectToGB[(sender as Rectangle)!];
            Debug.WriteLine(gameState.state.gameButton);
        }
    }

    private void Rectangle_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        (sender as Rectangle)!.Fill = inactive;

        lock (gameState)
        {
            gameState.state.gameButton &= ~RectToGB[(sender as Rectangle)!];
        }
    }

    private void Rectangle_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        (sender as Rectangle)!.Fill = inactive;

        lock (gameState)
        {
            gameState.state.gameButton &= ~RectToGB[(sender as Rectangle)!];
        }
    }

    private void SaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Settings.Save();
    }

    private void ReloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Settings.Load();
    }

}
