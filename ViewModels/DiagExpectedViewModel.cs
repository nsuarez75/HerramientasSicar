using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace HerramientasSICAR.ViewModels
{
    public partial class DiagExpectedViewModel : ObservableObject
    {
        private readonly TiaOpennessService _tiaService;

        [ObservableProperty]
        private string _rutaProyecto = "";

        [ObservableProperty]
        private string _nombreBloque = "";

        [ObservableProperty]
        private string _estado = "Listo";

        [ObservableProperty]
        private bool _isProcessing = false;

        public DiagExpectedViewModel(TiaOpennessService tiaService)
        {
            _tiaService = tiaService;
        }

        [RelayCommand]
        private void BuscarProyecto()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "TIA Portal Project (*.ap15;*.ap15_1;*.ap16;*.ap17;*.ap18;*.ap19)|*.ap15;*.ap15_1;*.ap16;*.ap17;*.ap18;*.ap19|Todos los archivos (*.*)|*.*";
            ofd.Title = "Seleccionar proyecto TIA (.ap19)";

            if (ofd.ShowDialog() == true)
                RutaProyecto = ofd.FileName;
        }

        [RelayCommand]
        private async Task ExportarAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaProyecto) || !File.Exists(RutaProyecto))
            {
                MessageBox.Show("Selecciona un proyecto válido (.apXX)",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(NombreBloque))
            {
                MessageBox.Show("Introduce el nombre del bloque a exportar.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Detect TIA version automatically
            if (!TiaVersionResolver.DetectarDesdeProyecto(RutaProyecto))
            {
                MessageBox.Show(
                    "No se pudo detectar qué versión de TIA Portal corresponde a este proyecto.",
                    "TIA no encontrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsProcessing = true;
            Estado = $"Cargando TIA Portal v{TiaVersionResolver.TiaVersionDetected}…";

            await Task.Run(() =>
            {
                try
                {
                    string pathExportar = Path.GetDirectoryName(RutaProyecto);

                    _tiaService.AbrirProyecto(RutaProyecto);

                    // COMPILE ONLY THE BLOCK
                    Estado = "Compilando bloque...";
                    _tiaService.CompilarBloque(NombreBloque);

                    // EXPORT THE BLOCK TO XML
                    Estado = "Exportando bloque...";
                    _tiaService.ExportarBloque(NombreBloque, pathExportar);

                    // GENERATE XML → EXCEL
                    Estado = "Analizando bloque y generando Excel...";

                    string xmlPath = Path.Combine(pathExportar, NombreBloque + ".xml");
                    string excelPath = Path.Combine(pathExportar, NombreBloque + "_DIAG.xlsx");

                    TIADiagExpected diag = new TIADiagExpected();
                    diag.ProcesarYGenerarExcel(xmlPath, excelPath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Bloque exportado y esperado generado correctamente.\n\nArchivo:\n" + excelPath,
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Error: " + ex.Message, "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    Estado = "Listo";
                    IsProcessing = false;
                }
            });
        }
    }
}
