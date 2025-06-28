using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using LightEffect = LTEK_ULed.Code.LightEffect;

namespace LTEK_ULed.Views;

public partial class MainWindow : Window
{
    GameState gameState = GameState.gameState;

    public static MainWindow? Instance { get; private set; }

    SolidColorBrush active = new SolidColorBrush(Color.FromRgb(255, 0, 0));
    SolidColorBrush inactive = new SolidColorBrush(Color.FromRgb(200, 200, 200));

    BidirectionalDictionary<Rectangle, GameButton> RectGBMap = new();
    BidirectionalDictionary<Rectangle, CabinetLight> RectCBMap = new();
    public MainWindow()
    {
        InitializeComponent();
        Instance = this;


        RectCBMap.Add(mDownLeft, CabinetLight.LIGHT_MARQUEE_LR_LEFT);
        RectCBMap.Add(mUpLeft, CabinetLight.LIGHT_MARQUEE_UP_LEFT);
        RectCBMap.Add(mDownRight, CabinetLight.LIGHT_MARQUEE_LR_RIGHT);
        RectCBMap.Add(mUpRight, CabinetLight.LIGHT_MARQUEE_UP_RIGHT);
        RectCBMap.Add(bassLeft, CabinetLight.LIGHT_BASS_LEFT);
        RectCBMap.Add(bassRight, CabinetLight.LIGHT_BASS_RIGHT);

        foreach (Rectangle rect in RectCBMap.Keys)
        {
            rect!.PointerPressed += Rectangle_PointerPressed;
            rect!.PointerReleased += Rectangle_PointerReleased;
            rect!.PointerExited += Rectangle_PointerExited;
        }

        for (int i = 1; i <= 18; i++)
        {
            GameButton result;
            string text = Convert.ToString(i).PadLeft(2, '0');
            Enum.TryParse<GameButton>("GAME_BUTTON_CUSTOM_" + text, false, out result);
            Rectangle? rect = this.FindControl<Rectangle>("g" + text);

            if (rect != null)
            {
                RectGBMap.Add(rect, result);
            }
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

        foreach (CabinetLight item in RectCBMap.Values)
        {
            RectCBMap.Inverse[item]!.Fill = cabinetLight.HasFlag(item) ? active : inactive;
        }

        foreach (GameButton item in RectGBMap.Values)
        {
            RectGBMap.Inverse[item]!.Fill = gameButton.HasFlag(item) ? active : inactive;
        }
    }

    private SegmentView[][] segmentViewsDict = [];

    //THIS IS FUCKED but works :)
    public void UpdateLeds(bool reset = false)
    {
        if (segmentViewsDict.Length == 0 || reset)
        {
            Array.Clear(segmentViewsDict);
            segmentViewsDict = new SegmentView[Settings.Instance!.Effects.Count][];
            List<SegmentView> segmentViews = this.GetVisualDescendants().OfType<SegmentView>().ToList();

            if (segmentViews.Count > 0 && segmentViews[0].Tag != null)
            {
                for (int i = 0; i < Settings.Instance!.Effects.Count; i++)
                {
                    LightEffect tempEffect = Settings.Instance!.Effects[i];
                    List<SegmentView> segList = new();

                    for (int j = 0; j < tempEffect.Segments.Count; j++)
                    {

                        Segment segment = tempEffect.Segments[j];

                        for (int k = 0; k < segmentViews.Count; k++)
                        {
                            //Debug.WriteLine(segmentViews[k].Tag);
                            if (segmentViews[k].Tag!.ToString() == tempEffect.GroupId.ToString())
                            {
                                Segment tempSegment = (Segment)segmentViews[k].DataContext!;
                                if (tempSegment == segment)
                                {
                                    segList.Add(segmentViews[k]);
                                    break;
                                }
                            }
                        }
                    }
                    segmentViewsDict[i] = segList.ToArray();
                }
            }
        }

        for (int i = 0; i < Settings.Instance!.Effects.Count; i++)
        {
            for (int j = 0; j < Settings.Instance!.Effects[i].leds.Length && j < segmentViewsDict.Length; j++)
            {
                segmentViewsDict[i][j].UpdateLeds(Settings.Instance!.Effects[i].leds[j]);
            }
        }
    }


    private void Rectangle_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        LightEm.IsChecked = false;
        HandleClick((Rectangle)sender!, true);
    }

    private void Rectangle_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (LightEm.IsChecked == false)
            HandleClick((Rectangle)sender!, false);
    }

    private void Rectangle_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (LightEm.IsChecked == false)
            HandleClick((Rectangle)sender!, false);
    }

    private void HandleClick(Rectangle rect, bool pressed)
    {

        lock (gameState)
        {
            if (RectGBMap.ContainsKey(rect))
            {
                gameState.state.gameButton = pressed ? gameState.state.gameButton | RectGBMap[rect] : gameState.state.gameButton & ~RectGBMap[rect];
            }
            else if (RectCBMap.ContainsKey(rect))
            {
                gameState.state.cabinetLight = pressed ? gameState.state.cabinetLight | RectCBMap[rect] : gameState.state.cabinetLight & ~RectCBMap[rect];

            }
        }
        UpdateUi();
    }

    private void LightEmChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            LightEm.IsChecked = false;
            foreach (var item in RectGBMap.Keys)
            {
                item.Fill = inactive;
            }
            foreach (var item in RectCBMap.Keys)
            {
                item.Fill = inactive;
            }

            gameState.state.gameButton = GameButton.NONE;
            gameState.state.cabinetLight = CabinetLight.NONE;

        }
        else if (sender is ToggleButton button)
        {
            if (button.IsChecked == true)
            {
                foreach (var item in RectGBMap.Keys)
                {
                    item.Fill = active;
                }
                foreach (var item in RectCBMap.Keys)
                {
                    item.Fill = active;
                }
                gameState.state.gameButton = ~GameButton.NONE;
                gameState.state.cabinetLight = ~CabinetLight.NONE;

            }
            if (button.IsChecked == false)
            {
                foreach (var item in RectGBMap.Keys)
                {
                    item.Fill = inactive;
                }
                foreach (var item in RectCBMap.Keys)
                {
                    item.Fill = inactive;
                }

                gameState.state.gameButton = GameButton.NONE;
                gameState.state.cabinetLight = CabinetLight.NONE;

            }
        }
    }
}
