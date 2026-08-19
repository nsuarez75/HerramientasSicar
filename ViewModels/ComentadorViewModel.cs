using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace HerramientasSICAR.ViewModels
{
    public partial class ComentadorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _rutaEntrada = "";

        [ObservableProperty]
        private string _nombreSalida = "";

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private int _progresoActual = 0;

        [ObservableProperty]
        private int _progresoTotal = 0;

        [ObservableProperty]
        private string _progresoTexto = "";

        [ObservableProperty]
        private bool _traducirTextos = true;

        public ComentadorViewModel()
        {
        }

        [RelayCommand]
        private void SeleccionarArchivo()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
            
            if (openFileDialog.ShowDialog() == true)
            {
                RutaEntrada = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private async Task EjecutarAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaEntrada))
            {
                MessageBox.Show("Selecciona una lista de textos", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NombreSalida))
            {
                MessageBox.Show("Introduce un nombre para la lista modificada", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outputName = NombreSalida;
            if (!outputName.EndsWith(".xlsx"))
            {
                outputName += ".xlsx";
            }

            IsProcessing = true;
            ProgresoActual = 0;
            ProgresoTotal = 0;
            ProgresoTexto = "Leyendo archivo...";

            try
            {
                string outputPath = Path.Combine(Path.GetDirectoryName(RutaEntrada), outputName);

                // Progress<T> captures the current (UI) SynchronizationContext, so the callback
                // runs on the UI thread even though the translation work happens in background.
                bool traducir = TraducirTextos;
                var progress = new Progress<(int completados, int total)>(p =>
                {
                    ProgresoActual = p.completados;
                    ProgresoTotal = p.total;
                    ProgresoTexto = p.total > 0
                        ? (traducir
                            ? $"Traduciendo títulos: {p.completados}/{p.total} ({(int)(100.0 * p.completados / p.total)}%)"
                            : $"Copiando títulos: {p.completados}/{p.total}")
                        : "Preparando...";
                });

                // Call Processor (translates titles to English/German/Spanish via GTranslate, unless disabled)
                // Runs off the UI thread since it mixes blocking Excel I/O with network calls
                await Task.Run(() => ExcelProcessor.ModificarTodosComentariosAsync(RutaEntrada, outputPath, traducir, progress));

                MessageBox.Show($"Archivo guardado correctamente:\n{outputPath}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
