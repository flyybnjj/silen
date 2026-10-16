using System.Windows;
using Silen.Windows;
using Silen.Core;

namespace Silen.App;

public partial class OptimizationConfirmation : Window
{
    public OptimizationConfirmation() => InitializeComponent();
    private void Accept(object sender, RoutedEventArgs args) => DialogResult = true;
    internal static bool ConfirmAction(string heading, string detail)
    {
        var dialog = new OptimizationConfirmation { Owner = Application.Current.MainWindow, Title = heading };
        dialog.ReviewHeading.Text = heading;
        dialog.ReviewDescription.Text = "Revisa la operación antes de confirmar. Puedes cancelar sin aplicar cambios.";
        dialog.ReviewDetail.Text = detail;
        return dialog.ShowDialog() == true;
    }
    internal static bool Confirm()
    {
        var dialog = new OptimizationConfirmation { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true;
    }
    internal static bool ConfirmPc(IReadOnlyList<PcOptimizationOption> options)
    {
        var dialog = new OptimizationConfirmation { Owner = Application.Current.MainWindow, Title = "Optimizar PC" };
        dialog.ReviewHeading.Text = "¿Aplicar la selección del PC?";
        dialog.ReviewDescription.Text = $"{SpanishText.Form(options.Count, "Se aplicará únicamente el ajuste seleccionado", "Se aplicarán únicamente los ajustes seleccionados")} ({options.Count}). Antes, Silen solicitará un punto de restauración a Windows; Windows pedirá permiso para esta operación. Cada ajuste seguirá guardando su estado anterior.";
        dialog.ReviewDetail.Text = "Revisa los efectos descritos en la selección. Los pasos de energía pueden aumentar consumo y temperatura. Servicios y apps pueden perder funciones hasta restaurarlos. No se borrarán archivos ni se cambiará Fortnite.";
        dialog.SelectedSteps.ItemsSource = options;
        dialog.SelectedStepsPanel.Visibility = Visibility.Visible;
        return dialog.ShowDialog() == true;
    }
    internal static bool ConfirmRestoreFallback(RestorePointResult point)
    {
        var dialog = new OptimizationConfirmation { Owner = Application.Current.MainWindow, Title = "Windows no creó un punto nuevo" };
        dialog.ReviewHeading.Text = "¿Continuar sin un punto nuevo?";
        dialog.ReviewDescription.Text = point.Message;
        dialog.ReviewDetail.Text = "El lote sigue detenido. Puedes cancelar y abrir Protección del sistema desde Resumen, o continuar solo con el respaldo previo de cada ajuste en el journal de Silen. El journal no sustituye un punto de restauración de Windows ni una copia de tus archivos.";
        dialog.AcceptButton.Content = "Continuar solo con journal";
        dialog.RejectButton.Content = "Cancelar lote";
        dialog.Loaded += (_, _) => dialog.RejectButton.Focus();
        return dialog.ShowDialog() == true;
    }
}
