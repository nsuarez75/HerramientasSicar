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
    public partial class NumeradorViewModel : ObservableObject
    {
        private readonly TiaOpennessService _tiaService;

        private readonly string[] bloques = new string[]
        {
            "GeneralMessages_FB","OM1_Messages_FB","OM2_Messages_FB","OM3_Messages_FB",
            "OM4_Messages_FB","OM5_Messages_FB","OM6_Messages_FB","OM7_Messages_FB",
            "OM8_Messages_FB"
        };

        [ObservableProperty]
        private string _rutaProyecto = "";

        [ObservableProperty]
        private string _estado = "Listo";

        [ObservableProperty]
        private bool _isProcessing = false;

        public NumeradorViewModel(TiaOpennessService tiaService)
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
        private async Task NumerarAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaProyecto) || !File.Exists(RutaProyecto))
            {
                MessageBox.Show("Selecciona un proyecto válido (.apxx)",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Detect TIA version from selected project
            if (!TiaVersionResolver.DetectarDesdeProyecto(RutaProyecto))
            {
                MessageBox.Show("No se encontró la versión TIA correspondiente para este proyecto.",
                    "TIA no encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    // Compile all blocks once
                    Estado = "Compilando proyecto...";
                    _tiaService.CompilarTodo();

                    // Process each block
                    foreach (string bloque in bloques)
                    {
                        try
                        {
                            Estado = "Procesando " + bloque + "...";

                            string carpetaDestino;
                            if (bloque.StartsWith("OM"))
                                carpetaDestino = Path.Combine(pathExportar, @"ProgramBlocks\10_Sequence & Messageblocks", bloque.Substring(0, 3));
                            else
                                carpetaDestino = Path.Combine(pathExportar, @"ProgramBlocks\10_Sequence & Messageblocks", "General");

                            Directory.CreateDirectory(carpetaDestino);

                            string rutaXml = Path.Combine(carpetaDestino, bloque + ".xml");

                            // Export block
                            _tiaService.ExportarBloque(bloque, carpetaDestino);

                            // Modify XML
                            if (File.Exists(rutaXml))
                            {
                                string contenido = File.ReadAllText(rutaXml);
                                Regex patron = new Regex(@"(<Component[^>]*Name=\""no_)\d+(?=\"")", RegexOptions.IgnoreCase);
                                int contador = 0;

                                string nuevo = patron.Replace(contenido, delegate (Match match)
                                {
                                    contador++;
                                    return match.Groups[1].Value + contador;
                                });

                                File.WriteAllText(rutaXml, nuevo);
                            }

                            // Delete existing block and import
                            //tiaService.EliminarBloque(bloque);
                            _tiaService.ImportarBloque(rutaXml);
                        }
                        catch (Exception ex)
                        {
                            // If a block fails, show warning and continue with others
                            Estado = "Error en " + bloque + ": " + ex.Message;
                            Console.WriteLine("⚠️ Error en bloque " + bloque + ": " + ex.Message);
                            continue;
                        }
                    }

                    // Delete temporary folder
                    string rutaProgramBlocks = Path.Combine(Path.GetDirectoryName(RutaProyecto), "ProgramBlocks");
                    if (Directory.Exists(rutaProgramBlocks))
                        Directory.Delete(rutaProgramBlocks, true);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Bloques exportados, numerados e importados correctamente.",
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
