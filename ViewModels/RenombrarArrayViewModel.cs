using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace HerramientasSICAR.ViewModels
{
    public partial class RenombrarArrayViewModel : ObservableObject
    {
        private readonly TiaOpennessService _tiaService;

        [ObservableProperty]
        private string _rutaProyecto = "";

        [ObservableProperty]
        private string _nombreBloque = "";

        [ObservableProperty]
        private string _nombreArray = "";

        [ObservableProperty]
        private string _estado = "Listo";

        [ObservableProperty]
        private bool _isProcessing = false;

        public RenombrarArrayViewModel(TiaOpennessService tiaService)
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
        private async Task ReordenarAsync()
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

            if (string.IsNullOrWhiteSpace(NombreArray))
            {
                MessageBox.Show("Introduce el nombre del array de booleanos.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                string xmlPath = null;
                try
                {
                    string pathExportar = Path.GetDirectoryName(RutaProyecto);

                    _tiaService.AbrirProyecto(RutaProyecto);

                    Estado = "Compilando bloque...";
                    _tiaService.CompilarBloque(NombreBloque);

                    Estado = "Exportando bloque...";
                    _tiaService.ExportarBloque(NombreBloque, pathExportar);

                    xmlPath = Path.Combine(pathExportar, NombreBloque + ".xml");
                    if (!File.Exists(xmlPath))
                        throw new Exception("No se pudo exportar el bloque.");

                    Estado = "Reordenando índices del array...";
                    RenombrarIndicesArray(xmlPath, NombreArray);

                    Estado = "Obteniendo ubicación del bloque...";
                    var grupoOriginal = _tiaService.BuscarGrupoBloque(NombreBloque);

                    Estado = "Eliminando bloque original...";
                    _tiaService.EliminarBloque(NombreBloque);

                    Estado = "Importando bloque modificado...";
                    if (grupoOriginal != null)
                        _tiaService.ImportarBloque(xmlPath, grupoOriginal);
                    else
                        _tiaService.ImportarBloque(xmlPath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Array reordenado correctamente.\n\nEl bloque '" + NombreBloque + "' ha sido actualizado con los índices del array '" + NombreArray + "' renormalizados.",
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
                    if (xmlPath != null && File.Exists(xmlPath))
                    {
                        try { File.Delete(xmlPath); }
                        catch { }
                    }

                    Estado = "Listo";
                    IsProcessing = false;
                }
            });
        }

        private void RenombrarIndicesArray(string filePath, string arrayName)
        {
            string contenido = File.ReadAllText(filePath);

            string pattern = $@"(<Component Name=""{arrayName}"" AccessModifier=""Array"">.*?<ConstantValue>)(\d+)(</ConstantValue>)";
            RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
            Regex regex = new Regex(pattern, options);

            MatchCollection matches = regex.Matches(contenido);
            int encontrados = matches.Count;

            if (encontrados == 0)
                throw new Exception($"No se encontró el array '{arrayName}' en el bloque.");

            int contador = 0;
            string resultado = regex.Replace(contenido, delegate (Match match)
            {
                contador++;
                return match.Groups[1].Value + contador + match.Groups[3].Value;
            });

            File.WriteAllText(filePath, resultado);
        }
    }
}