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

            await Task.Run(() =>
            {
                try
                {
                    string outputPath = Path.Combine(Path.GetDirectoryName(RutaEntrada), outputName);

                    // Call Processor
                    ExcelProcessor.ModificarTodosComentarios(RutaEntrada, outputPath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Archivo guardado correctamente:\n{outputPath}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Ocurrió un error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    IsProcessing = false;
                }
            });
        }
    }
}
