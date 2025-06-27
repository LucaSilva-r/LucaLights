using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LTEK_ULed.Code;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace LTEK_ULed.Controls;

public partial class GradientPicker : UserControl
{

    Border? container;
    public GradientPicker()
    {
        InitializeComponent();

        GradientPresets = new ObservableCollection<GradientPreset>(GradientPreset.GetPresets());

    }
    public static readonly StyledProperty<ObservableCollection<GradientPreset>> GradientPresetsProperty =
        AvaloniaProperty.Register<GradientPicker, ObservableCollection<GradientPreset>>(nameof(Gradient), defaultBindingMode: Avalonia.Data.BindingMode.OneTime);

    private ObservableCollection<GradientPreset> GradientPresets
    {
        get => GetValue(GradientPresetsProperty);
        set => SetValue(GradientPresetsProperty, value);
    }

    public static readonly StyledProperty<LinearGradientBrush> GradientProperty =
    AvaloniaProperty.Register<GradientPicker, LinearGradientBrush>(
        nameof(Gradient),
        defaultValue: new LinearGradientBrush()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = {
                new GradientStop(Color.Parse("Red"), 0),
                new GradientStop(Color.Parse("White"), 1)
            }
        },
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
        );

    public LinearGradientBrush Gradient
    {
        get => GetValue(GradientProperty);
        set => SetValue(GradientProperty, value);
    }

    Border? selectedGradientHandle = null;




    private void AddGradientHandle(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Only handle left button presses
        if (e.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
        {
            // Get the position of the click relative to the container
            var position = e.GetPosition(container);
            // Calculate the offset based on the width of the container
            double offset = Math.Clamp(position.X / container.Bounds.Width, 0, 1);
            // Create a new GradientStop at the clicked position

            var newStop = new GradientStop(Color.Parse("White"), offset);
            // Add the new stop to the gradient

            for (int i = 0; i < Gradient.GradientStops.Count; i++)
            {
                if (Gradient.GradientStops[i].Offset > offset)
                {
                    if (i - 1 < 0)
                    {
                        newStop.Color = Gradient.GradientStops[i].Color;
                        Gradient.GradientStops.Insert(0, newStop);
                        break;
                    }
                    Color rMax = Gradient.GradientStops[i].Color;
                    Color rMin = Gradient.GradientStops[i - 1].Color;

                    double difference = Gradient.GradientStops[i].Offset - Gradient.GradientStops[i - 1].Offset;
                    double pos = (offset - Gradient.GradientStops[i - 1].Offset);

                    double pos1 = pos / difference;
                    double pos2 = 1 - (pos / difference);

                    // Interpolate the color between the two stops
                    newStop.Color = Color.FromArgb(
                        (byte)(rMax.A * pos1 + rMin.A * pos2),
                        (byte)(rMax.R * pos1 + rMin.R * pos2),
                        (byte)(rMax.G * pos1 + rMin.G * pos2),
                        (byte)(rMax.B * pos1 + rMin.B * pos2));
                    Gradient.GradientStops.Insert(i, newStop);
                    break;
                }
            }

            if (!Gradient.GradientStops.Contains(newStop))
            {
                newStop.Color = Gradient.GradientStops.Last().Color;
                Gradient.GradientStops.Add(newStop);

            }
            e.Handled = true;
        }
    }

    bool clicked = false;
    bool moved = false;

    private void GradientHandlePressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border border)
        {
            clicked = true;
            moved = false;
            if (e.GetCurrentPoint(border).Properties.IsRightButtonPressed)
            {
                if (border.DataContext is GradientStop gradientStop && Gradient.GradientStops.Count > 1)
                {
                    Gradient.GradientStops.Remove(gradientStop);
                }

                e.Handled = true;
                return; // Only handle left button presses
            }
            else
            {
                selectedGradientHandle = border;
            }
            e.Handled = true;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        container = e.NameScope.Find<Border>("GradientContainer");
    }

    private void GradientHandleMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        moved = true;
        // Make sure we are dragging a handle
        if (selectedGradientHandle == null || selectedGradientHandle.DataContext is not GradientStop selectedStop)
        {
            return;
        }

        // This is the container the handles live in
        var container = sender as Control;
        if (container == null) return;

        // 1. Update the Offset
        // Calculate the new offset based on mouse position, clamped between 0 and 1
        double position = e.GetPosition(container).X;
        double width = container.Bounds.Width;
        double newOffset = Math.Clamp(position / width, 0, 1);

        selectedStop.Offset = newOffset;

        // 2. Re-sort the Collection
        // Get the observable collection of stops
        var stops = Gradient.GradientStops;

        // Create a new list that is explicitly sorted by the updated offsets
        var sortedStops = stops.OrderBy(s => s.Offset).ToList();

        // Now, apply this new order to the original collection by moving items.
        // This is the most efficient way to sort an ObservableCollection without clearing it.
        for (int i = 0; i < sortedStops.Count; i++)
        {
            var itemToSort = sortedStops[i];
            int currentIndex = stops.IndexOf(itemToSort);

            // If the item is not already in its sorted position, move it.
            if (currentIndex != i)
            {
                stops.Move(currentIndex, i);
            }
        }

        e.Handled = true;
    }


    private void GradientHandleReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (sender is Border border)
        {
            selectedGradientHandle = null;
            e.Handled = true;

            if (!moved && clicked)
            {
                FlyoutBase.ShowAttachedFlyout(border);

            }
            moved = false;
            clicked = false;
        }
    }

    private void GradientPresetPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {

        if (sender is Border border && border.DataContext is GradientPreset preset)
        {
            // Clear the current gradient stops
            Gradient.GradientStops.Clear();
            // Add the stops from the selected preset
            foreach (var stop in preset.gradientBrush.GradientStops)
            {
                Gradient.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
            }
            e.Handled = true;
        }
    }
}