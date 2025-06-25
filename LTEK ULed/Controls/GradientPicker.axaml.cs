using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Linq;

namespace LTEK_ULed.Controls;

public partial class GradientPicker : UserControl
{

    Border? container;
    public GradientPicker()
    {
        InitializeComponent();
    }


    public static readonly StyledProperty<LinearGradientBrush> GradientProperty =
    AvaloniaProperty.Register<SegmentView, LinearGradientBrush>(
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

    private void GradientHandlePressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border border)
        {
            selectedGradientHandle = border;
           
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
        }
    }
}