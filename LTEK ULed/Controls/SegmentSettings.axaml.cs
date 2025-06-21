using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using LTEK_ULed.Code;
using Semi.Avalonia;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Xml.Linq;

namespace LTEK_ULed.Controls;

public partial class SegmentSettings : UserControl
{
    // 1. Renamed the static AvaloniaProperty field to follow the "PropertyNameProperty" convention.
    public static readonly StyledProperty<ObservableCollection<int>> GroupIdsProperty =
        AvaloniaProperty.Register<SegmentSettings, ObservableCollection<int>>(nameof(GroupIds),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public ObservableCollection<int> GroupIds
    {
        get => GetValue(GroupIdsProperty);
        set => SetValue(GroupIdsProperty, value);
    }

    public static readonly StyledProperty<string> SegmentNameProperty =
        AvaloniaProperty.Register<SegmentSettings, string>(nameof(SegmentName), defaultValue: "Segment Name",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string SegmentName
    {
        get => GetValue(SegmentNameProperty);
        set => SetValue(SegmentNameProperty, value);
    }

    public static readonly StyledProperty<int> SegmentLengthProperty =
        AvaloniaProperty.Register<SegmentSettings, int>(nameof(SegmentLength),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int SegmentLength
    {
        get => GetValue(SegmentLengthProperty);
        set => SetValue(SegmentLengthProperty, value);
    }

    static SegmentSettings()
    {
        GroupIdsProperty.Changed.AddClassHandler<SegmentSettings>((sender, args) => sender.OnGroupIdsChanged(args));
    }

    public SegmentSettings()
    {
        InitializeComponent();
        // The logic for creating MenuItems has been moved out of the constructor.
    }

    /// <summary>
    /// This method is called whenever the GroupIds property changes.
    /// </summary>
    private void OnGroupIdsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        // When the collection is changed (e.g., bound from the viewmodel),
        // we need to stop listening to the old collection (if any).
        if (e.OldValue is ObservableCollection<int> oldCollection)
        {
            oldCollection.CollectionChanged -= OnGroupIdsCollectionChanged;
        }

        // ...and start listening to the new collection for additions/removals.
        if (e.NewValue is ObservableCollection<int> newCollection)
        {
            newCollection.CollectionChanged += OnGroupIdsCollectionChanged;
        }

        // Re-create the menu items to reflect the new state.
        RebuildMenuItems();
    }

    /// <summary>
    /// This method is called when items are added or removed from the GroupIds collection itself.
    /// </summary>
    private void OnGroupIdsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Instead of rebuilding the whole menu, just update the check marks for efficiency.
        UpdateMenuItemChecks();
    }

    /// <summary>
    /// Populates the MenuItem list based on the current GroupIds collection.
    /// </summary>
    private void RebuildMenuItems()
    {
        EffectMapping.Items.Clear();
        if (Settings.Instance == null || GroupIds == null)
        {
            return;
        }


        object? iconGeometry;

        IResourceDictionary? _resources = new Icons();
        _resources.TryGetResource("SemiIconCheckBoxTick", null, out iconGeometry);

        System.Collections.Generic.IList<IResourceProvider> t = _resources.MergedDictionaries;

        foreach (LightEffect effect in Settings.Instance.Effects)
        {
            var menuItem = new MenuItem()
            {
                Header = effect.Name,
                IsEnabled = true,
                StaysOpenOnClick = true,
                Tag = effect.GroupId,
                IsChecked = GroupIds.Contains(effect.GroupId)
            };



            PathIcon icon = new PathIcon();
            if (iconGeometry != null)
            {
                icon.Data = (Avalonia.Media.Geometry)iconGeometry;
            }

            icon.Bind(PathIcon.IsVisibleProperty, menuItem.GetObservable(MenuItem.IsCheckedProperty));

            menuItem.Icon = icon;

            menuItem.Click += OnMenuItemClick;
            EffectMapping.Items.Add(menuItem);
        }
    }

    /// <summary>
    /// Updates the IsChecked state of each menu item.
    /// </summary>
    private void UpdateMenuItemChecks()
    {
        if (GroupIds == null) return;

        foreach (var item in EffectMapping.Items)
        {
            if (item is MenuItem menuItem && menuItem.Tag is int groupId)
            {
                menuItem.IsChecked = GroupIds.Contains(groupId);
            }
        }
    }

    /// <summary>
    /// Handles clicks on menu items to add/remove the effect's GroupId from our source collection.
    /// </summary>
    private void OnMenuItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        if (sender is MenuItem { Tag: int groupId } menuItem && GroupIds != null)
        {
            // The IsChecked state is toggled automatically. We just need to sync the collection.
            menuItem.IsChecked = !menuItem.IsChecked;
            if (menuItem.IsChecked)
            {
                if (!GroupIds.Contains(groupId))
                {
                    GroupIds.Add(groupId);
                }
            }
            else
            {
                GroupIds.Remove(groupId);
            }
        }
    }

    private void MappingClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!EffectMapping.IsOpen)
        {

            RebuildMenuItems();
            EffectMapping.Open();
        }
    }
}
