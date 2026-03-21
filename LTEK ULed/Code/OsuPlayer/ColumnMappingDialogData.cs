using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LTEK_ULed.Code.OsuPlayer;

public class ColumnMappingDialogData
{
    public string Title { get; }
    public ObservableCollection<ColumnMapping> Mappings { get; }

    public ColumnMappingDialogData(int keyCount, List<ColumnMapping>? existingMappings)
    {
        Title = $"Column Mappings ({keyCount}K)";
        Mappings = new ObservableCollection<ColumnMapping>();

        for (int i = 0; i < keyCount; i++)
        {
            var existing = existingMappings?.FirstOrDefault(m => m.Column == i);
            Mappings.Add(existing ?? new ColumnMapping { Column = i });
        }
    }
}
