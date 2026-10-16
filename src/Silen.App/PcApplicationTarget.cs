using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Silen.Windows;
using Silen.Core;

namespace Silen.App;

public sealed partial class PcOptimizationViewModel
{
    private IInstalledProductCatalog productCatalog = null!;
    private List<InstalledProduct> allProducts = [];
    private InstalledProduct? selectedProduct;
    private string? selectedExecutable;
    private string productSearch = "", productStatus = "Busca un producto instalado o elige un .exe. Los ajustes generales siguen siendo de Windows; la preferencia GPU se limita al ejecutable elegido.";
    public ObservableCollection<InstalledProduct> Products { get; } = [];
    public ObservableCollection<string> Executables { get; } = [];
    public ICommand FindProductsCommand { get; private set; } = null!;
    public ICommand LocateProductCommand { get; private set; } = null!;
    public ICommand ChooseExecutableCommand { get; private set; } = null!;
    public string ProductStatus => productStatus;
    public string ProductSearch { get => productSearch; set { if (Set(ref productSearch, value)) FilterProducts(); } }
    public InstalledProduct? SelectedProduct
    {
        get => selectedProduct;
        set { if (Set(ref selectedProduct, value)) { Executables.Clear(); SelectedExecutable = null; CommandManager.InvalidateRequerySuggested(); } }
    }
    public string? SelectedExecutable
    {
        get => selectedExecutable;
        set
        {
            if (value == selectedExecutable || value is not null && !canRun()) return;
            try
            {
                service.SetApplicationTarget(value); selectedExecutable = value;
                hasPreview = false; Options.Clear(); Steps.Clear(); Changed(null); CommandManager.InvalidateRequerySuggested();
                SetProductStatus(value is null ? "Sin producto seleccionado: el optimizador ofrece ajustes generales de Windows." : "Ejecutable elegido: " + value + ". Prepara la selección para revisar temporizador, GPU y controles de entrada de Windows.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { SetProductStatus(ex.Message); }
        }
    }
    private void InitializeProducts(IInstalledProductCatalog catalog, Func<string?> choose)
    {
        productCatalog = catalog;
        FindProductsCommand = new AsyncCommand(_ => FindProductsAsync(), _ => canRun(), ex => SetProductStatus(ex.Message));
        LocateProductCommand = new AsyncCommand(_ => LocateProductAsync(), _ => canRun() && SelectedProduct is not null, ex => SetProductStatus(ex.Message));
        ChooseExecutableCommand = new RelayCommand(_ =>
        {
            var executable = choose(); if (executable is null) return;
            if (!Executables.Contains(executable)) Executables.Add(executable); SelectedExecutable = executable;
        }, _ => canRun());
    }
    internal Task<bool> FindProductsAsync() => run("Buscando juegos y aplicaciones instaladas…", async token =>
    {
        allProducts = (await productCatalog.FindAsync(token)).ToList(); FilterProducts();
        SetProductStatus($"{SpanishText.Count(allProducts.Count, "producto encontrado", "productos encontrados")} en registro, Steam y Epic. Busca por nombre; las apps portables también se pueden elegir manualmente.");
    });
    internal Task<bool> LocateProductAsync()
    {
        var product = SelectedProduct; if (product is null) return Task.FromResult(false);
        return run("Localizando ejecutables de " + product.Name + "…", async token =>
        {
            var paths = await productCatalog.LocateExecutablesAsync(product, token); Executables.Clear();
            foreach (var path in paths) Executables.Add(path);
            SetProductStatus(paths.Count == 0 ? "No se encontró un ejecutable accesible. Puedes elegirlo manualmente; algunas apps Store no exponen un .exe normal."
                : $"{SpanishText.Count(paths.Count, "ejecutable localizado", "ejecutables localizados")}. Elige el principal del producto; ninguno se ha ejecutado.");
        });
    }
    private void FilterProducts()
    {
        var selected = SelectedProduct;
        Products.Clear();
        foreach (var product in allProducts.Where(product => (product.Name + " " + product.Publisher).Contains(ProductSearch.Trim(), StringComparison.OrdinalIgnoreCase)).Take(200)) Products.Add(product);
        if (selected is not null && !Products.Any(product => product.Id == selected.Id)) SelectedProduct = null;
    }
    private void SetProductStatus(string message) { productStatus = message; Changed(nameof(ProductStatus)); }
}
