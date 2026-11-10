using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Saca bloques y elementos de HMI del proyecto conectado a XML y mete XML de vuelta (en el mismo proyecto o en otro).
    /// </summary>
    public partial class ImportarExportarViewModel : Herramienta
    {
        private readonly IProgress<string> _progreso;

        public ImportarExportarViewModel(Contexto contexto) : base(contexto)
        {
            _progreso = ProgresoEnEstado();
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
                var partes = new List<string> { nodo.Dispositivo };
                if (nodo.Rama != null)
                    partes.Add(L.T(ArchivosHmi.Nombre(nodo.Rama.Value)));
                partes.AddRange(nodo.Grupos);
                return string.Join(" › ", partes);
            }
        }

        /// <summary>
        /// PLC y HMI del proyecto recién conectado (o ninguno si se desconecta), con el árbol de los que se han leído;
        /// los demás salen «sin leer».
        /// </summary>
        public void MostrarArbol(IReadOnlyList<DispositivoInfo> dispositivos, ArbolProyecto arbol)
        {
            Dispositivos.Clear();
            NodoSeleccionado = null;
            foreach (var dispositivo in dispositivos)
            {
                var plc = dispositivo.EsHmi ? null : arbol.Plcs.FirstOrDefault(p => p.Nombre == dispositivo.Nombre);
                var hmi = dispositivo.EsHmi ? arbol.Hmis.FirstOrDefault(h => h.Nombre == dispositivo.Nombre) : null;
                Dispositivos.Add(plc != null ? NodoArbol.DesdePlc(plc, ContarMarcados)
                    : hmi != null ? NodoArbol.DesdeHmi(hmi, ContarMarcados)
                    : NodoArbol.SinLeer(dispositivo, ContarMarcados));
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
            var info = new DispositivoInfo { Nombre = dispositivo.Nombre, EsHmi = dispositivo.Tipo == TipoNodo.Hmi };

            await Contexto.Trabajar(L.T("Leyendo el árbol del proyecto…"), async () =>
            {
                var arbol = await Contexto.Tia().LeerArbolAsync(new[] { info }, _progreso);
                if (info.EsHmi)
                    dispositivo.ReemplazarRamas(ArchivosHmi.OrdenArbol, arbol.Hmis.Single().Ramas);
                else
                    dispositivo.ReemplazarContenido(arbol.Plcs.Single().Raiz);
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
                .Select(n => new BloqueRef { Plc = n.Dispositivo, Grupos = n.Grupos, Nombre = n.Nombre })
                .ToList();
            var elementosHmi = hojas.Where(n => n.Tipo == TipoNodo.ElementoHmi)
                .Select(n => new ElementoHmiRef { Hmi = n.Dispositivo, Rama = n.Rama.Value, Grupos = n.Grupos, Nombre = n.Nombre })
                .ToList();

            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta donde guardar lo exportado"), Contexto.Ajustes.UltimaCarpeta);
            if (carpeta == null)
                return;
            RecordarCarpeta(carpeta);

            await Contexto.Trabajar(L.T("Exportando…"), async () =>
            {
                var resultado = await Contexto.Tia().ExportarAsync(bloques, elementosHmi, carpeta, _progreso);
                MostrarResultado(resultado, L.T("{0} elemento(s) exportado(s) a {1}.", resultado.Correctos, carpeta));
            });
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
                var resultado = await Contexto.Tia().ImportarAsync(destino.Dispositivo, destino.Grupos, archivos, _progreso);
                // Solo se vuelve a leer el PLC destino (leer el proyecto entero tarda minutos en uno grande),
                // y nada si no se había leído.
                if (destino.Raiz.Leido)
                {
                    _progreso.Report(L.T("Leyendo de nuevo lo importado en {0}…", destino.Dispositivo));
                    var plc = await Contexto.Tia().LeerPlcAsync(destino.Dispositivo);
                    destino.Raiz.ReemplazarContenido(plc.Raiz);
                    ArbolReleido(destino);
                }
                resultado.Fallos.InsertRange(0, noBloques);
                MostrarResultado(resultado, L.T("{0} bloque(s) importado(s). Guarda el proyecto en TIA para conservarlos.", resultado.Correctos));
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
                L.T("Se van a importar {0} elemento(s) en la HMI {1}, cada uno en su rama.\n\nLo que ya exista con el mismo nombre se sobrescribe.", archivos.Count, destino.Dispositivo),
                L.T("Importar"));
            if (!aceptado)
                return;

            await Contexto.Trabajar(L.T("Importando…"), async () =>
            {
                var resultado = await Contexto.Tia().ImportarHmiAsync(destino.Dispositivo, destino.Rama, destino.Grupos, archivos, _progreso);
                // Solo se vuelven a leer las ramas de la HMI donde se ha importado algo, y nada si no se había leído.
                if (destino.Raiz.Leido)
                {
                    _progreso.Report(L.T("Leyendo de nuevo lo importado en {0}…", destino.Dispositivo));
                    var ramas = archivos.Select(a => a.Rama).Distinct().ToList();
                    var leidas = await Contexto.Tia().LeerRamasHmiAsync(destino.Dispositivo, ramas);
                    destino.Raiz.ReemplazarRamas(ramas, leidas);
                    ArbolReleido(destino);
                }
                resultado.Fallos.InsertRange(0, noHmi);
                MostrarResultado(resultado, L.T("{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos.", resultado.Correctos));
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

        private void MostrarResultado(ResultadoOperacion resultado, string mensaje)
        {
            Contexto.PonerFallos(resultado.Fallos);
            Contexto.MostrarEstado(resultado.Fallos.Count == 0 ? mensaje : mensaje + " " + L.T("{0} con error (ver abajo).", resultado.Fallos.Count),
                error: resultado.Fallos.Count > 0);
        }

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
