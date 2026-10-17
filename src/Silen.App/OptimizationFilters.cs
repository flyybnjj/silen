using Silen.Windows;

namespace Silen.App;

public sealed partial class ToolsViewModel
{
    private string manualSearch = "", manualCategory = "Todas";
    public string ManualSearch { get => manualSearch; set { if (Set(ref manualSearch, value)) { Changed(nameof(VisibleManualCategories)); Changed(nameof(HasVisibleManualCategories)); } } }
    public string ManualCategory { get => manualCategory; set { value = string.IsNullOrWhiteSpace(value) ? "Todas" : value; if (Set(ref manualCategory, value)) { Changed(nameof(VisibleManualCategories)); Changed(nameof(HasVisibleManualCategories)); } } }
    public bool HasVisibleManualCategories => VisibleManualCategories.Any();
    public IEnumerable<string> ManualCategoryNames => new[] { "Todas" }.Concat(OptimizationCategories.Select(group => group.Name).Distinct());
    public IEnumerable<OptimizationCategory> VisibleManualCategories => OptimizationCategories
        .Where(group => manualCategory == "Todas" || group.Name == manualCategory)
        .Select(group => new OptimizationCategory(group.Name, group.Toggles.Where(row => string.IsNullOrWhiteSpace(manualSearch) ||
            $"{group.Name} {row.Setting.Name} {row.Setting.Description}".Contains(manualSearch.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray()))
        .Where(group => group.Toggles.Count > 0);
}

public sealed partial class PcOptimizationViewModel
{
    private string optionSearch = "", optionCategory = "Todas";
    public string OptionSearch { get => optionSearch; set { if (Set(ref optionSearch, value)) { Changed(nameof(VisibleOptions)); Changed(nameof(HasVisibleOptions)); } } }
    public string OptionCategory { get => optionCategory; set { value = string.IsNullOrWhiteSpace(value) ? "Todas" : value; if (Set(ref optionCategory, value)) { Changed(nameof(VisibleOptions)); Changed(nameof(HasVisibleOptions)); } } }
    public bool HasVisibleOptions => VisibleOptions.Any();
    public IEnumerable<string> OptionCategories => new[] { "Todas" }.Concat(Options.Select(row => row.Option.Category).Distinct());
    public IEnumerable<PcOptimizationChoice> VisibleOptions => Options.Where(row =>
        (optionCategory == "Todas" || row.Option.Category == optionCategory) &&
        (string.IsNullOrWhiteSpace(optionSearch) || $"{row.Option.Title} {row.Option.Description}".Contains(optionSearch.Trim(), StringComparison.OrdinalIgnoreCase)));
}
