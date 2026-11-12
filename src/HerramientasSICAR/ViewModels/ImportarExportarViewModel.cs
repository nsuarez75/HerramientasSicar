using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Saca bloques y elementos de HMI del proyecto conectado a XML y mete XML de vuelta (en el mismo proyecto o en otro).
    /// </summary>
    public partial class ImportarExportarViewModel : Herramienta
    {
        public ImportarExportarViewModel(Contexto contexto) : base(contexto)
        {
            Contexto.ArbolCambiado += AlCambiarArbol;
        }

        // El árbol es común a todas las herramientas: se rehace al conectar y se actualiza el PLC que se vuelve a leer.
        private void AlCambiarArbol(CambioArbol cambio)
        {
            if (cambio.Completo)
            {
                MostrarArbol(cambio.Dispositivos, cambio.Arbol);
                return;
            }
            var raiz = Dispositivos.FirstOrDefault(d => d.Tipo == TipoNodo.Plc && d.Nombre == cambio.Plc.Name);
            if (raiz == null)
                return;
            raiz.ReemplazarContenido(cambio.Plc.Root);
            ArbolReleido(NodoSeleccionado != null && NodoSeleccionado.Raiz == raiz ? NodoSeleccionado : raiz);
        }

        public override string Nombre => L.T("Importar / Exportar");
        public override string Descripcion => L.T("Exporta bloques y elementos de HMI del proyecto a XML e importa XML en el proyecto.");
        public override string Icono => "replace";
        public override bool UsaTia => true;

        /// <summary>PLC y HMI del proyecto conectado, cada uno con su árbol.</summary>
        public ObservableCollection<NodoArbol> Dispositivos { get; } = new ObservableCollection<NodoArbol>();

        [ObservableProperty]
        private string _filtro;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Destino), nameof(HayDestino), nameof(TextoLeer))]
        [NotifyCanExecuteChangedFor(nameof(ImportarArchivosCommand), nameof(ImportarCarpetaCommand), nameof(LeerCommand))]
        private NodoArbol _nodoSeleccionado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoMarcados), nameof(HayMarcados))]
        [NotifyCanExecuteChangedFor(nameof(ExportarCommand))]
        private int _marcados;

        public bool HayMarcados => Marcados > 0;
        public bool HayDestino => NodoSeleccionado != null;
        public string TextoMarcados => Marcados == 0 ? L.T("Ningún elemento marcado") : L.T("{0} elemento(s) marcado(s)", Marcados);

        /// <summary>Botón para leer (o volver a leer) de TIA el PLC o la HMI de lo seleccionado.</summary>
        public string TextoLeer => NodoSeleccionado == null ? L.T("Selecciona un PLC o una HMI para leerlo")
            : NodoSeleccionado.Raiz.Leido ? L.T("Leer de nuevo {0}", NodoSeleccionado.Raiz.Nombre)
            : L.T("Leer {0}", NodoSeleccionado.Raiz.Nombre);

        /// <summary>
        /// Dónde se importará: la carpeta seleccionada en el árbol (o la de un bloque seleccionado).
        /// En una HMI, cada archivo va a su rama; lo seleccionado solo cuenta para los de esa misma rama.
        /// </summary>
        public string Destino
        {
            get
            {
                var nodo = NodoSeleccionado;
                if (nodo == null)
                    return L.T("Selecciona en el árbol dónde importar");
                var partes = new List<string> { nodo.Raiz.Nombre };
                if (nodo.Rama != null)
                    partes.Add(L.T(HmiRules.FolderName(nodo.Rama.Value)));
                partes.AddRange(nodo.Grupos);
                return string.Join(" › ", partes);
            }
        }

        /// <summary>
        /// PLC y HMI del proyecto recién conectado (o ninguno si se desconecta), con el árbol de los que se han leído;
        /// los demás salen «sin leer».
        /// </summary>
        private void MostrarArbol(IReadOnlyList<DeviceInfo> dispositivos, ProjectTree arbol)
        {
            Dispositivos.Clear();
            NodoSeleccionado = null;
            foreach (var dispositivo in dispositivos)
            {
                bool esHmi = dispositivo.Kind == DeviceKind.Hmi;
                var plc = esHmi ? null : arbol.Plcs.FirstOrDefault(p => p.Name == dispositivo.Name);
                var hmi = esHmi ? arbol.Hmis.FirstOrDefault(h => h.Name == dispositivo.Name) : null;
                string nombre = NombresDispositivo.Visible(dispositivo, dispositivos);
                Dispositivos.Add(plc != null ? NodoArbol.DesdePlc(plc, ContarMarcados)
                    : hmi != null ? NodoArbol.DesdeHmi(hmi, ContarMarcados, nombre)
                    : NodoArbol.SinLeer(dispositivo, ContarMarcados, nombre));
            }
            OnFiltroChanged(Filtro);
            ContarMarcados();
        }

        /// <summary>Lee de TIA el PLC o la HMI de lo seleccionado: si no se leyó al conectar, o para ver cambios hechos en TIA.</summary>
        [RelayCommand(CanExecute = nameof(PuedeLeer))]
        private async Task Leer()
        {
            var destino = NodoSeleccionado;
            var dispositivo = destino.Raiz;

            await Contexto.Trabajar(L.T("Leyendo el árbol del proyecto…"), async () =>
            {
                if (dispositivo.Tipo == TipoNodo.Hmi)
                {
                    var tia = await Contexto.TiaAsync();
                    var arbol = await tia.ReadTreeAsync(new string[0], new[] { dispositivo.Dispositivo }, Contexto.ProgresoTia());
                    dispositivo.ReemplazarRamas(HmiRules.TreeOrder, arbol.Hmis.Single().Branches);
                }
                else
                    await Contexto.LeerPlcAsync(dispositivo.Dispositivo);   // lo pone en el árbol común: lo ven todas las herramientas
                ArbolReleido(destino);
                Contexto.MostrarEstado(L.T("Árbol de {0} leído.", dispositivo.Nombre));
            });
        }

        partial void OnFiltroChanged(string value)
        {
            foreach (var dispositivo in Dispositivos)
                dispositivo.AplicarFiltro(value);
        }

        [RelayCommand(CanExecute = nameof(PuedeExportar))]
        private async Task Exportar()
        {
            var hojas = Dispositivos.SelectMany(d => d.HojasMarcadas()).ToList();
            var bloques = hojas.Where(n => n.Tipo == TipoNodo.Bloque)
                .Select(n => new BlockRef { Plc = n.Dispositivo, Folders = n.Grupos.ToList(), Name = n.Nombre })
                .ToList();
            var elementosHmi = hojas.Where(n => n.Tipo == TipoNodo.ElementoHmi)
                .Select(n => new HmiItemRef { Hmi = n.Dispositivo, Branch = n.Rama.Value, Folders = n.Grupos.ToList(), Name = n.Nombre })
                .ToList();

            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta donde guardar lo exportado"), Contexto.Ajustes.UltimaCarpeta);
            if (carpeta == null)
                return;
            RecordarCarpeta(carpeta);

            await Contexto.Trabajar(L.T("Exportando…"), async () =>
            {
                var tia = await Contexto.TiaAsync();
                var progreso = Contexto.ProgresoTia();
                var resultado = new BatchResult();
                if (bloques.Count > 0)
                    Sumar(resultado, await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = bloques, Folder = CarpetaExportacion(carpeta, bloques.Select(b => b.Plc), elementosHmi.Count > 0) },
                        progreso));
                if (elementosHmi.Count > 0 && !resultado.Stopped)
                    Sumar(resultado, await tia.ExportHmiAsync(
                        new ExportHmiArgs { Items = elementosHmi, Folder = CarpetaExportacion(carpeta, elementosHmi.Select(e => e.Hmi), bloques.Count > 0) },
                        progreso));
                Contexto.MostrarResultado(resultado, L.T("{0} elemento(s) exportado(s) a {1}.", resultado.Succeeded, carpeta));
            });
        }

        /// <summary>
        /// Carpeta donde exportar los bloques (o los elementos de HMI). Si se exporta de más de un PLC o HMI, cada uno va
        /// en su subcarpeta: TIA-Connector la crea cuando hay varios del mismo tipo; si hay uno solo pero también se
        /// exporta del otro tipo, se pone aquí.
        /// </summary>
        private static string CarpetaExportacion(string carpeta, IEnumerable<string> dispositivos, bool tambienDelOtroTipo)
        {
            var distintos = dispositivos.Distinct().ToList();
            return tambienDelOtroTipo && distintos.Count == 1 ? Path.Combine(carpeta, FileRules.SafeName(distintos[0])) : carpeta;
        }

        private static void Sumar(BatchResult total, BatchResult parte)
        {
            total.Succeeded += parte.Succeeded;
            total.Failures.AddRange(parte.Failures);
            total.Files.AddRange(parte.Files);
            total.Stopped = parte.Stopped;
            total.Remaining = parte.Remaining;
        }

        [RelayCommand(CanExecute = nameof(PuedeImportar))]
        private async Task ImportarArchivos()
        {
            var rutas = await Contexto.Dialogos.ElegirArchivos(L.T("Archivos a importar"), Contexto.Ajustes.UltimaCarpeta, TipoArchivo.Xml);
            if (rutas == null || rutas.Count == 0)
                return;
            RecordarCarpeta(Path.GetDirectoryName(rutas[0]));

            if (NodoSeleccionado.EsDeHmi)
                await ImportarEnHmi(ArchivosHmi.PrepararArchivos(rutas, out var descartadosHmi), descartadosHmi);
            else
                await Importar(ArchivosBloque.PrepararArchivos(rutas, out var descartados), descartados);
        }

        [RelayCommand(CanExecute = nameof(PuedeImportar))]
        private async Task ImportarCarpeta()
        {
            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta con lo que se va a importar (las subcarpetas se crean como carpetas)"), Contexto.Ajustes.UltimaCarpeta);
            if (carpeta == null)
                return;
            RecordarCarpeta(carpeta);

            if (NodoSeleccionado.EsDeHmi)
                await ImportarEnHmi(ArchivosHmi.PrepararCarpeta(carpeta, out var descartadosHmi), descartadosHmi);
            else
                await Importar(ArchivosBloque.PrepararCarpeta(carpeta, out var descartados), descartados);
        }

        private async Task Importar(List<ArchivoImportar> archivos, List<string> descartados)
        {
            var noBloques = descartados.Select(r => L.T("{0}: no es un bloque exportado de TIA, no se importa", Path.GetFileName(r))).ToList();
            if (archivos.Count == 0)
            {
                Contexto.PonerFallos(noBloques);
                Contexto.MostrarEstado(L.T("No hay bloques que importar."), error: true);
                return;
            }

            var destino = NodoSeleccionado;
            bool aceptado = await Contexto.Dialogos.Confirmar(L.T("Importar bloques"),
                L.T("Se van a importar {0} bloque(s) en {1}.\n\nLos bloques que ya existan con el mismo nombre se sobrescriben.", archivos.Count, Destino),
                L.T("Importar"));
            if (!aceptado)
                return;

            await Contexto.Trabajar(L.T("Importando…"), async () =>
            {
                // Los bloques que ya existen con el mismo nombre en la carpeta destino se sobrescriben.
                var tia = await Contexto.TiaAsync();
                var resultado = await tia.ImportBlocksAsync(new ImportBlocksArgs
                {
                    Files = archivos.Select(a => new ImportFile
                    {
                        Path = a.Ruta, Plc = destino.Dispositivo, Folders = destino.Grupos.Concat(a.Grupos).ToList()
                    }).ToList()
                }, Contexto.ProgresoTia());
                // Solo se vuelve a leer el PLC destino (leer el proyecto entero tarda minutos en uno grande),
                // y nada si no se había leído.
                if (destino.Raiz.Leido)
                {
                    Contexto.MostrarEstado(L.T("Leyendo de nuevo lo importado en {0}…", destino.Dispositivo));
                    await Contexto.LeerPlcAsync(destino.Dispositivo);
                    ArbolReleido(destino);
                }
                Contexto.MostrarResultado(resultado, L.T("{0} bloque(s) importado(s). Guarda el proyecto en TIA para conservarlos.", resultado.Succeeded),
                    importando: true, previos: noBloques);
            });
        }

        private async Task ImportarEnHmi(List<ArchivoHmi> archivos, List<string> descartados)
        {
            var noHmi = descartados.Select(r => L.T("{0}: no es un elemento de HMI exportado de TIA, no se importa", Path.GetFileName(r))).ToList();
            if (archivos.Count == 0)
            {
                Contexto.PonerFallos(noHmi);
                Contexto.MostrarEstado(L.T("No hay elementos de HMI que importar."), error: true);
                return;
            }

            var destino = NodoSeleccionado;
            bool aceptado = await Contexto.Dialogos.Confirmar(L.T("Importar en la HMI"),
                L.T("Se van a importar {0} elemento(s) en la HMI {1}, cada uno en su rama.\n\nLo que ya exista con el mismo nombre se sobrescribe.", archivos.Count, destino.Raiz.Nombre),
                L.T("Importar"));
            if (!aceptado)
                return;

            await Contexto.Trabajar(L.T("Importando…"), async () =>
            {
                // Lo que ya existe con el mismo nombre se sobrescribe; TIA-Connector importa en el orden de las ramas.
                var tia = await Contexto.TiaAsync();
                var resultado = await tia.ImportHmiAsync(new ImportHmiArgs
                {
                    Hmi = destino.Dispositivo,
                    Files = archivos.Select(a => new HmiImportFile
                    {
                        Path = a.Ruta, Branch = a.Rama, Folders = ArchivosHmi.CarpetasDestino(destino.Rama, destino.Grupos, a).ToList()
                    }).ToList()
                }, Contexto.ProgresoTia());
                // Solo se vuelven a leer las ramas de la HMI donde se ha importado algo, y nada si no se había leído.
                if (destino.Raiz.Leido)
                {
                    Contexto.MostrarEstado(L.T("Leyendo de nuevo lo importado en {0}…", destino.Raiz.Nombre));
                    var ramas = archivos.Select(a => a.Rama).Distinct().ToList();
                    var leidas = await tia.ReadHmiBranchesAsync(destino.Dispositivo, ramas);
                    destino.Raiz.ReemplazarRamas(ramas, leidas);
                    ArbolReleido(destino);
                }
                Contexto.MostrarResultado(resultado, L.T("{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos.", resultado.Succeeded),
                    previos: noHmi);
            });
        }

        /// <summary>
        /// Tras releer parte del árbol: vuelve a seleccionar el destino (sus nodos pueden ser otros) y
        /// aplica el filtro y la cuenta de marcados a lo nuevo.
        /// </summary>
        private void ArbolReleido(NodoArbol destino)
        {
            NodoSeleccionado = destino.Raiz.Buscar(destino);
            OnPropertyChanged(nameof(TextoLeer));
            OnFiltroChanged(Filtro);
            ContarMarcados();
        }

        private void ContarMarcados() => Marcados = Dispositivos.Sum(d => d.HojasMarcadas().Count());

        private void RecordarCarpeta(string carpeta)
        {
            Contexto.Ajustes.UltimaCarpeta = carpeta;
            Contexto.Ajustes.Guardar();
        }

        private bool PuedeExportar() => PuedeUsarTia && Marcados > 0;
        private bool PuedeImportar() => PuedeUsarTia && NodoSeleccionado != null;
        private bool PuedeLeer() => PuedeUsarTia && NodoSeleccionado != null;

        protected override void ActualizarComandos()
        {
            ExportarCommand.NotifyCanExecuteChanged();
            ImportarArchivosCommand.NotifyCanExecuteChanged();
            ImportarCarpetaCommand.NotifyCanExecuteChanged();
            LeerCommand.NotifyCanExecuteChanged();
        }
    }
}
