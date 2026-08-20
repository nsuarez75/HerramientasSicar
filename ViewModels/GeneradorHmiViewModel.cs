using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HerramientasSICAR.ViewModels
{
    public class HmiScreenData
    {
        public string Hmi { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string Nombre { get; set; }
        public bool Visible { get; set; }
    }

    // Un subgrupo de pantallas dentro de un grupo (p.ej. "Detail1" -> LayoutDetailed1[32]).
    public class SubgrupoConfig
    {
        public string Clave { get; set; }
        public int Id { get; set; }
        public string Comentario { get; set; }
        public string PlcName { get; set; }
        public int MaxSize { get; set; }
    }

    public class GrupoConfig
    {
        public string Clave { get; set; }
        public int Id { get; set; }
        public string Comentario { get; set; }
        public string PlcName { get; set; }
        public List<SubgrupoConfig> Subgrupos { get; set; }
    }

    public partial class GeneradorHmiViewModel : ObservableObject
    {
        // Columna de Excel, y nombres de subgrupo para Installation/Equipment/Manual Operations,
        // para cada uno de los 8 bloques de pantallas que hay en cada hoja HMI (equivale al
        // "match col" del script Python).
        private static readonly (string Columna, string Inst, string Equip, string Man)[] ColumnasConfig = new[]
        {
            ("D", "Main",         "Robots",         "Manual1"),
            ("F", "General",      "Drives",         "Manual2"),
            ("H", "Detail1",      "Welding",        "UserFunctions"),
            ("J", "Detail2",      "Spare04",        "Poka"),
            ("L", "Fixture",      "Spare05",        "Docking"),
            ("N", "Traceability", "Spare06",        "Spare06"),
            ("P", "Spare07",      "Spare07",        "Spare07"),
            ("R", "Interface",    "Identification", "Spare08"),
        };

        // Equivalente al diccionario "estructura" del script Python: solo los subgrupos
        // listados aquí tienen un bloque IDB real en el PLC; el resto de columnas/subgrupos
        // (p.ej. "Spare07") se leen del Excel pero no generan salida, igual que en Python.
        private static readonly List<GrupoConfig> Estructura = new List<GrupoConfig>
        {
            new GrupoConfig
            {
                Clave = "OEM", Id = 1, Comentario = "OEM", PlcName = "OEM",
                Subgrupos = Enumerable.Range(1, 8).Select(i => new SubgrupoConfig
                {
                    Clave = $"OEM{i}",
                    Id = i,
                    Comentario = $"OEM_{i:D2}",
                    PlcName = $"OEM_{i:D2}",
                    MaxSize = 8
                }).ToList()
            },
            new GrupoConfig
            {
                Clave = "Installation", Id = 2, Comentario = "Installation", PlcName = "Installation",
                Subgrupos = new List<SubgrupoConfig>
                {
                    new SubgrupoConfig { Clave = "General",      Id = 2, Comentario = "Layout general",      PlcName = "LayoutGeneral",     MaxSize = 8  },
                    new SubgrupoConfig { Clave = "Detail1",      Id = 3, Comentario = "Layout detailed 1",    PlcName = "LayoutDetailed1",   MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Detail2",      Id = 4, Comentario = "Layout detailed 2",    PlcName = "LayoutDetailed2",   MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Fixture",      Id = 5, Comentario = "Layout fixture",       PlcName = "LayoutFixture",     MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Traceability", Id = 6, Comentario = "Traceability",         PlcName = "Traceability",      MaxSize = 24 },
                    new SubgrupoConfig { Clave = "Interface",    Id = 8, Comentario = "Interface signals",    PlcName = "InterfaceSignals",  MaxSize = 32 },
                }
            },
            new GrupoConfig
            {
                Clave = "Equipment", Id = 3, Comentario = "Equipments", PlcName = "Equipments",
                Subgrupos = new List<SubgrupoConfig>
                {
                    new SubgrupoConfig { Clave = "Robots",  Id = 1, Comentario = "Robots",  PlcName = "Robots",  MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Drives",  Id = 2, Comentario = "Drives",  PlcName = "Drives",  MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Welding", Id = 3, Comentario = "Welding", PlcName = "Welding", MaxSize = 32 },
                }
            },
            new GrupoConfig
            {
                Clave = "Manual Operations", Id = 4, Comentario = "Manual operation", PlcName = "ManualOperation",
                Subgrupos = new List<SubgrupoConfig>
                {
                    new SubgrupoConfig { Clave = "Manual1",       Id = 1, Comentario = "Manual Movements 1", PlcName = "ManualMovements1", MaxSize = 32 },
                    new SubgrupoConfig { Clave = "Manual2",       Id = 2, Comentario = "Manual Movements 2", PlcName = "ManualMovements2", MaxSize = 32 },
                    new SubgrupoConfig { Clave = "UserFunctions", Id = 3, Comentario = "User functions",     PlcName = "UserFunctions",    MaxSize = 15 },
                    new SubgrupoConfig { Clave = "Poka",          Id = 4, Comentario = "Poka-Yoke test",     PlcName = "PokaYokeTest",     MaxSize = 8  },
                    new SubgrupoConfig { Clave = "Docking",       Id = 5, Comentario = "Docking stations",   PlcName = "DockingStations",  MaxSize = 8  },
                }
            },
        };

        [ObservableProperty]
        private string _rutaOrigen = "";

        [ObservableProperty]
        private string _rutaDestino = "";

        [ObservableProperty]
        private string _estado = "Listo";

        [ObservableProperty]
        private bool _isProcessing = false;

        public GeneradorHmiViewModel()
        {
        }

        [RelayCommand]
        private void BuscarOrigen()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Archivos Excel (*.xlsx)|*.xlsx|Todos los archivos (*.*)|*.*",
                Title = "Seleccionar archivo Excel origen"
            };

            if (ofd.ShowDialog() == true)
            {
                RutaOrigen = ofd.FileName;
                if (string.IsNullOrEmpty(RutaDestino))
                {
                    string dir = Path.GetDirectoryName(RutaOrigen);
                    string filename = Path.GetFileNameWithoutExtension(RutaOrigen);
                    RutaDestino = Path.Combine(dir, $"{filename}_PLC.xlsx");
                }
            }
        }

        [RelayCommand]
        private void BuscarDestino()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Archivos Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                Title = "Guardar nuevo archivo Excel como..."
            };

            if (sfd.ShowDialog() == true)
            {
                RutaDestino = sfd.FileName;
            }
        }

        [RelayCommand]
        private async Task GenerarAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaOrigen) || !File.Exists(RutaOrigen))
            {
                MessageBox.Show("Por favor, selecciona un archivo Excel de origen válido.",
                    "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(RutaDestino))
            {
                MessageBox.Show("Por favor, selecciona una ruta de destino para guardar el archivo Excel.",
                    "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetProcessing(true);
            SetEstado("Iniciando...");

            await Task.Run(() =>
            {
                try
                {
                    SetEstado("Cargando archivo en memoria...");

                    using (var stream = new FileStream(RutaOrigen, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var libroEntrada = new XLWorkbook(stream))
                    {
                        var datos = GenerarDatos(libroEntrada);

                        if (datos.Count == 0)
                        {
                            throw new Exception("No se encontraron pantallas válidas en ninguna hoja del archivo de origen.");
                        }

                        using (var libroSalida = new XLWorkbook())
                        {
                            GenerarConfigPLC(libroSalida, datos);

                            // Los textos se leen siempre de la primera hoja del libro de origen,
                            // igual que en el script Python (todas las hojas HMI comparten la
                            // misma disposición de nombres de pantalla).
                            var hojaTextos = libroEntrada.Worksheets.First();
                            GenerarListasTextos(hojaTextos, libroSalida);

                            SetEstado("Guardando archivo...");
                            libroSalida.SaveAs(RutaDestino);
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"¡Éxito! Archivo generado en:\n{RutaDestino}",
                            "Generación Completada", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error durante el proceso:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    SetEstado("Listo");
                    SetProcessing(false);
                }
            });
        }

        // Las propiedades ObservableProperty están enlazadas a controles WPF (DispatcherObject).
        // GenerarAsync las actualiza desde el hilo de Task.Run, así que hay que reenviarlas
        // siempre al hilo de UI o WPF lanza InvalidOperationException.
        private void SetEstado(string texto)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                Estado = texto;
            else
                dispatcher.Invoke(() => Estado = texto);
        }

        private void SetProcessing(bool valor)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                IsProcessing = valor;
            else
                dispatcher.Invoke(() => IsProcessing = valor);
        }

        private List<HmiScreenData> GenerarDatos(XLWorkbook libro)
        {
            var hmi = new List<HmiScreenData>();

            foreach (var hoja in libro.Worksheets)
            {
                SetEstado($"Leyendo {hoja.Name}...");

                for (int colIdx = 0; colIdx < ColumnasConfig.Length; colIdx++)
                {
                    var (columna, inst, equip, man) = ColumnasConfig[colIdx];
                    int numOem = colIdx + 1;

                    // Pantallas OEM
                    for (int fila = 7; fila <= 14; fila++)
                        AgregarPosicion(hmi, hoja, columna, fila, "OEM", $"OEM{numOem}");

                    // Installation
                    for (int fila = 16; fila <= 47; fila++)
                        AgregarPosicion(hmi, hoja, columna, fila, "Installation", inst);

                    // Equipment
                    for (int fila = 49; fila <= 72; fila++)
                        AgregarPosicion(hmi, hoja, columna, fila, "Equipment", equip);

                    // Manual Operations
                    for (int fila = 74; fila <= 105; fila++)
                        AgregarPosicion(hmi, hoja, columna, fila, "Manual Operations", man);
                }
            }

            return hmi;
        }

        // Se añade siempre una entrada por posición (fila), incluso cuando la celda está vacía,
        // para que el índice dentro de "valores" en GenerarConfigPLC siga correspondiéndose 1:1
        // con la posición del array del PLC. Si se omitieran las celdas vacías, un hueco en medio
        // del rango desplazaría todas las posiciones siguientes y desalinearía la visibilidad.
        private void AgregarPosicion(List<HmiScreenData> hmi, IXLWorksheet hoja, string columna, int fila, string grupo, string subgrupo)
        {
            var cell = hoja.Cell($"{columna}{fila}");
            string nombre = GetVal(cell);

            hmi.Add(new HmiScreenData
            {
                Hmi = hoja.Name,
                Grupo = grupo,
                Subgrupo = subgrupo,
                Nombre = nombre,
                Visible = !string.IsNullOrEmpty(nombre) && EsVisible(cell)
            });
        }

        private bool EsVisible(IXLCell cell)
        {
            var fill = cell.Style.Fill;

            // Sin relleno sólido, la celda se ve con el fondo blanco por defecto de Excel.
            if (fill.PatternType != XLFillPatternValues.Solid)
                return false;

            var color = ResolverColor(cell.Worksheet.Workbook, fill.BackgroundColor);
            return !EsBlancoOTransparente(color);
        }

        // Las plantillas HMI usan colores de tema (Accent1, Background1...) en vez de la
        // paleta indexada clásica de Excel, así que hay que resolverlos contra el tema del
        // libro (y aplicar el tinte) para saber el color real que se ve en la celda.
        private static System.Drawing.Color ResolverColor(XLWorkbook libro, XLColor color)
        {
            switch (color.ColorType)
            {
                case XLColorType.Color:
                    return color.Color;
                case XLColorType.Indexed:
                    return XLColor.FromIndex(color.Indexed).Color;
                case XLColorType.Theme:
                    var baseColor = ResolverColorTema(libro, color.ThemeColor);
                    return AplicarTinte(baseColor, color.ThemeTint);
                default:
                    return System.Drawing.Color.White;
            }
        }

        private static System.Drawing.Color ResolverColorTema(XLWorkbook libro, XLThemeColor tema)
        {
            var t = libro.Theme;
            switch (tema)
            {
                case XLThemeColor.Background1: return t.Background1.Color;
                case XLThemeColor.Text1: return t.Text1.Color;
                case XLThemeColor.Background2: return t.Background2.Color;
                case XLThemeColor.Text2: return t.Text2.Color;
                case XLThemeColor.Accent1: return t.Accent1.Color;
                case XLThemeColor.Accent2: return t.Accent2.Color;
                case XLThemeColor.Accent3: return t.Accent3.Color;
                case XLThemeColor.Accent4: return t.Accent4.Color;
                case XLThemeColor.Accent5: return t.Accent5.Color;
                case XLThemeColor.Accent6: return t.Accent6.Color;
                case XLThemeColor.Hyperlink: return t.Hyperlink.Color;
                case XLThemeColor.FollowedHyperlink: return t.FollowedHyperlink.Color;
                default: return System.Drawing.Color.White;
            }
        }

        // Fórmula de tinte de OOXML: tinte negativo oscurece hacia negro, tinte positivo
        // aclara hacia blanco (p.ej. Accent1 con tinte 0.8 da un azul muy claro, no blanco puro).
        private static System.Drawing.Color AplicarTinte(System.Drawing.Color color, double tinte)
        {
            if (tinte == 0) return color;

            double Ajustar(byte canal) => tinte < 0
                ? canal * (1 + tinte)
                : canal * (1 - tinte) + (255 * tinte);

            int r = (int)Math.Round(Ajustar(color.R));
            int g = (int)Math.Round(Ajustar(color.G));
            int b = (int)Math.Round(Ajustar(color.B));

            return System.Drawing.Color.FromArgb(
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, g)),
                Math.Max(0, Math.Min(255, b)));
        }

        private static bool EsBlancoOTransparente(System.Drawing.Color color)
        {
            const int umbral = 250;
            return color.A == 0 || (color.R >= umbral && color.G >= umbral && color.B >= umbral);
        }

        private string GetVal(IXLCell cell)
        {
            try
            {
                // CachedValue (no Value) replica openpyxl con data_only=True: lee el último
                // resultado que Excel calculó y guardó en el archivo, sin que ClosedXML intente
                // recalcular la fórmula con su propio motor.
                var valor = cell.CachedValue;

                if (valor.IsBlank || valor.IsError) return "";

                return valor.ToString(CultureInfo.InvariantCulture)?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private void GenerarConfigPLC(XLWorkbook libroSalida, List<HmiScreenData> datos)
        {
            SetEstado("Generando configuración PLC...");

            // Agrupamos visibilidad por HMI/Grupo/Subgrupo, preservando el orden de aparición
            // (equivalente a "datos_agrupados" en Python).
            var agrupado = new Dictionary<string, Dictionary<string, Dictionary<string, List<bool>>>>();
            var hmisUnicos = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var item in datos)
            {
                hmisUnicos.Add(item.Hmi);

                if (!agrupado.TryGetValue(item.Hmi, out var porGrupo))
                {
                    porGrupo = new Dictionary<string, Dictionary<string, List<bool>>>();
                    agrupado[item.Hmi] = porGrupo;
                }
                if (!porGrupo.TryGetValue(item.Grupo, out var porSubgrupo))
                {
                    porSubgrupo = new Dictionary<string, List<bool>>();
                    porGrupo[item.Grupo] = porSubgrupo;
                }
                if (!porSubgrupo.TryGetValue(item.Subgrupo, out var visibles))
                {
                    visibles = new List<bool>();
                    porSubgrupo[item.Subgrupo] = visibles;
                }
                visibles.Add(item.Visible);
            }

            var hojaConfig = libroSalida.Worksheets.Add("MenusConfig");

            int idxHmi = 0;
            foreach (var nombreHmi in hmisUnicos)
            {
                idxHmi++;
                string numHmi = idxHmi.ToString("D2");
                string idbName = $"HMIMenusConfig{numHmi}_IDB";

                var lineas = new List<string>
                {
                    $"// --- Configuration for {nombreHmi} ---",
                    "      NOP 0",
                    "",
                    "// The screens accesses is configurated for each HMI depending on the existence of the",
                    "// screen and an additonal user configuration.",
                    "",
                    "// Some screens have a data block associated and the presence of it in the CPU indicates",
                    "// the existence of the screen.  In this case, the no presence of the data block in the",
                    "// CPU hides the access of the screen.  But if it is present, the user can also hide it",
                    "// with an additional configuration due a operational needs.",
                    "",
                    "// Besides, for the screens which do not have a data block associated, their existence and",
                    "// therefore their access must be defined by the user.",
                    "",
                    "// In both cases, that additional configuration must be programmed in this network, before the",
                    "// call to the FB450.",
                    "// For each HMI a call to FB450 must be done and the user additional configuration can also",
                    "// be done.",
                    "",
                    "// Only the necessary screens and their access should be visible.",
                    "",
                    "// Log. 0 = screen visible / Log. 1 = screen invisible and not accessible",
                    ""
                };

                foreach (var grupo in Estructura)
                {
                    lineas.Add($"// Group {grupo.Id} ({grupo.Comentario})");

                    foreach (var sub in grupo.Subgrupos)
                    {
                        lineas.Add($"// Subgroup {sub.Id} ({sub.Comentario})");
                        lineas.Add($"// =     HMIMenusConfig0x_IDB.statCfg.Blanking.{grupo.PlcName}.{sub.PlcName}[y]");
                        lineas.Add("");

                        List<bool> valores = null;
                        if (agrupado.TryGetValue(nombreHmi, out var porGrupo) &&
                            porGrupo.TryGetValue(grupo.Clave, out var porSubgrupo))
                        {
                            porSubgrupo.TryGetValue(sub.Clave, out valores);
                        }
                        valores ??= new List<bool>();

                        var idxSiempreFalse = new List<int>();
                        var idxSiempreTrue = new List<int>();

                        for (int i = 1; i <= sub.MaxSize; i++)
                        {
                            if (i <= valores.Count)
                            {
                                if (valores[i - 1])
                                    idxSiempreFalse.Add(i);
                                else
                                    idxSiempreTrue.Add(i);
                            }
                            else
                            {
                                idxSiempreTrue.Add(i);
                            }
                        }

                        if (idxSiempreFalse.Count > 0)
                        {
                            lineas.Add("      A     alwaysFalse");
                            foreach (var i in idxSiempreFalse)
                                lineas.Add($"      =     {idbName}.statCfg.Blanking.{grupo.PlcName}.{sub.PlcName}[{i}]");
                            lineas.Add("");
                        }

                        if (idxSiempreTrue.Count > 0)
                        {
                            lineas.Add("      A     alwaysTrue");
                            foreach (var i in idxSiempreTrue)
                                lineas.Add($"      =     {idbName}.statCfg.Blanking.{grupo.PlcName}.{sub.PlcName}[{i}]");
                            lineas.Add("");
                        }
                    }
                }

                lineas.AddRange(new[]
                {
                    "      A     alwaysTrue",
                    "",
                    $"      =     {idbName}.statCfg.Blanking.HardwareDiagnostics.ProfinetDiagnostic[8]",
                    "",
                    "// Group 8 (System)",
                    "// Subgroup 5 (Energy)",
                    "",
                    "      A     alwaysTrue",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[2]",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[3]",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[4]",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[5]",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[6]",
                    $"      =     {idbName}.statCfg.Blanking.System.EnergyMeasurement[7]"
                });

                for (int fila = 0; fila < lineas.Count; fila++)
                    hojaConfig.Cell(fila + 1, idxHmi).Value = lineas[fila];

                hojaConfig.Column(idxHmi).Width = 100;
            }
        }

        private void GenerarListasTextos(IXLWorksheet hojaDatos, XLWorkbook libroSalida)
        {
            SetEstado("Generando listas de textos...");

            void CrearHoja(string nombre, List<(int Valor, string Texto)> pares)
            {
                var ws = libroSalida.Worksheets.Add(nombre);
                ws.Cell(1, 1).Value = "Value";
                ws.Cell(1, 2).Value = "Text";

                int fila = 2;
                foreach (var (valor, texto) in pares)
                {
                    ws.Cell(fila, 1).Value = valor;
                    ws.Cell(fila, 2).Value = texto;
                    fila++;
                }
            }

            void AgregarRango(List<(int Valor, string Texto)> lista, string columna, int filaInicio, int filaFin, int offsetIndice)
            {
                for (int fila = filaInicio; fila <= filaFin; fila++)
                {
                    string texto = GetVal(hojaDatos.Cell($"{columna}{fila}"));
                    if (!string.IsNullOrEmpty(texto))
                        lista.Add((fila - filaInicio + 1 + offsetIndice, texto));
                }
            }

            // 1. CO_OEM_Screens_ID
            var datosOem = new List<(int, string)> { (0, "<<OEM screen xx>>") };
            string[] letrasOem = { "D", "F", "H", "J", "L", "N", "P", "R" };
            for (int colIdx = 1; colIdx <= letrasOem.Length; colIdx++)
            {
                string letra = letrasOem[colIdx - 1];
                datosOem.Add((colIdx * 10, $"OEM_0{colIdx}"));

                for (int fila = 7; fila <= 14; fila++)
                {
                    int idxSub = fila - 6;
                    string texto = GetVal(hojaDatos.Cell($"{letra}{fila}"));
                    if (string.IsNullOrEmpty(texto))
                        texto = $"<<OEM 0{colIdx} screen {idxSub:D2}>>";
                    datosOem.Add((colIdx * 10 + idxSub, texto));
                }
            }
            CrearHoja("CO_OEM_Screens_ID", datosOem);

            // 2. CO_Layout_General_ID
            var datosGen = new List<(int, string)> { (0, "<<Layout general level xx>>") };
            AgregarRango(datosGen, "F", 16, 23, 0);
            CrearHoja("CO_Layout_General_ID", datosGen);

            // 3. CO_Layout_Detailed_ID (Nivel 1 y Nivel 2)
            var datosDet = new List<(int, string)> { (0, "<<Layout detailed level xx>>") };
            AgregarRango(datosDet, "H", 16, 47, 0);
            AgregarRango(datosDet, "J", 16, 47, 32);
            CrearHoja("CO_Layout_Detailed_ID", datosDet);

            // 4. CO_Layout_Fixture_ID
            var datosFix = new List<(int, string)> { (0, "<<Layout fixture level xx>>") };
            AgregarRango(datosFix, "L", 16, 47, 0);
            CrearHoja("CO_Layout_Fixture_ID", datosFix);

            // 5. CO_Traceability_ID
            var datosTrac = new List<(int, string)> { (0, "<<Traceability level xx>>") };
            AgregarRango(datosTrac, "N", 16, 39, 0);
            CrearHoja("CO_Traceability_ID", datosTrac);

            // 6. CO_Interface_Signals_ID
            var datosInt = new List<(int, string)> { (0, "<<Interface signals level xx>>") };
            AgregarRango(datosInt, "R", 16, 47, 0);
            CrearHoja("CO_Interface_Signals_ID", datosInt);

            // 7. CO_Rob_ID
            var datosRob = new List<(int, string)> { (0, "<<Robots level xx>>") };
            AgregarRango(datosRob, "D", 49, 72, 0);
            CrearHoja("CO_Rob_ID", datosRob);

            // 8. CO_Manual_Movements_ID (Manual 1 y Manual 2)
            var datosMan = new List<(int, string)> { (0, "<<Manual movements level xx>>") };
            AgregarRango(datosMan, "D", 74, 105, 0);
            AgregarRango(datosMan, "F", 74, 105, 32);
            CrearHoja("CO_Manual_Movements_ID", datosMan);

            // 9. CO_UserFunctions_ID
            var datosUser = new List<(int, string)> { (0, "<<User functions level xx>>") };
            AgregarRango(datosUser, "H", 74, 88, 0);
            CrearHoja("CO_UserFunctions_ID", datosUser);

            // 10. CO_PokaYokeTest_ID
            var datosPoka = new List<(int, string)> { (0, "<<PokaYoke test level xx>>") };
            AgregarRango(datosPoka, "J", 74, 81, 0);
            CrearHoja("CO_PokaYokeTest_ID", datosPoka);

            // 11. CO_DockingStation_ID
            var datosDock = new List<(int, string)> { (0, "<<Docking station level xx>>") };
            AgregarRango(datosDock, "L", 74, 81, 0);
            CrearHoja("CO_DockingStation_ID", datosDock);

            // 12. CO_ProfinetDiagnose_ID
            var datosProfi = new List<(int, string)> { (0, "<<Profinet diagnose level xx>>") };
            AgregarRango(datosProfi, "D", 108, 115, 0);
            CrearHoja("CO_ProfinetDiagnose_ID", datosProfi);

            // 13. CO_WebServer_Device_ID
            var datosWeb = new List<(int, string)> { (0, "<<Web server device level xx>>") };
            AgregarRango(datosWeb, "H", 108, 115, 0);
            CrearHoja("CO_WebServer_Device_ID", datosWeb);
        }
    }
}
