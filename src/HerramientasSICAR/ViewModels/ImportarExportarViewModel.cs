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
    /// <summary>Saca bloques del proyecto conectado a XML y mete XML de vuelta (en el mismo proyecto o en otro).</summary>
    public partial class ImportarExportarViewModel : Herramienta
    {
        private readonly IProgress<string> _progreso;

        public ImportarExportarViewModel(Contexto contexto) : base(contexto)
        {
            _progreso = new Progress<string>(texto => Contexto.MostrarEstado(texto));
        }

        public override string Nombre => L.T("Importar / Exportar");
        public override string Descripcion => L.T("Exporta bloques del proyecto a XML e importa XML en una carpeta del proyecto.");
        public override string Icono => "replace";
        public override bool UsaTia => true;

        public ObservableCollection<NodoArbol> Plcs { get; } = new ObservableCollection<NodoArbol>();

        [ObservableProperty]
        private string _filtro;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Destino), nameof(HayDestino))]
        [NotifyCanExecuteChangedFor(nameof(ImportarArchivosCommand), nameof(ImportarCarpetaCommand))]
        private NodoArbol _nodoSeleccionado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoMarcados), nameof(HayMarcados))]
        [NotifyCanExecuteChangedFor(nameof(ExportarCommand))]
        private int _marcados;

        public bool HayMarcados => Marcados > 0;
        public bool HayDestino => NodoSeleccionado != null;
        public string TextoMarcados => Marcados == 0 ? L.T("Ningún bloque marcado") : L.T("{0} bloque(s) marcado(s)", Marcados);

        /// <summary>Grupo donde se importará: el seleccionado en el árbol (o el de un bloque seleccionado).</summary>
        public string Destino => NodoSeleccionado == null
            ? L.T("Selecciona en el árbol dónde importar")
            : string.Join(" › ", new[] { NodoSeleccionado.Plc }.Concat(NodoSeleccionado.Grupos));

        /// <summary>Árbol del proyecto recién conectado (o vacío si se desconecta).</summary>
        public void MostrarPlcs(IEnumerable<PlcInfo> plcs)
        {
            Plcs.Clear();
            NodoSeleccionado = null;
            foreach (var plc in plcs)
                Plcs.Add(NodoArbol.DesdePlc(plc, ContarMarcados));
            OnFiltroChanged(Filtro);
            ContarMarcados();
        }

        partial void OnFiltroChanged(string value)
        {
            foreach (var plc in Plcs)
                plc.AplicarFiltro(value);
        }

        [RelayCommand(CanExecute = nameof(PuedeExportar))]
        private async Task Exportar()
        {
            var bloques = Plcs.SelectMany(p => p.BloquesMarcados())
                .Select(n => new BloqueRef { Plc = n.Plc, Grupos = n.Grupos, Nombre = n.Nombre })
                .ToList();

            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta donde guardar los bloques exportados"), Contexto.Ajustes.UltimaCarpeta);
            if (carpeta == null)
                return;
            RecordarCarpeta(carpeta);

            await Contexto.Trabajar(L.T("Exportando…"), async () =>
            {
                var resultado = await Contexto.Tia().ExportarAsync(bloques, carpeta, _progreso);
                MostrarResultado(resultado, L.T("{0} bloque(s) exportado(s) a {1}.", resultado.Correctos, carpeta));
            });
        }

        [RelayCommand(CanExecute = nameof(PuedeImportar))]
        private async Task ImportarArchivos()
        {
            var rutas = await Contexto.Dialogos.ElegirArchivos(L.T("Bloques a importar"), Contexto.Ajustes.UltimaCarpeta, TipoArchivo.Xml);
            if (rutas == null || rutas.Count == 0)
                return;
            RecordarCarpeta(Path.GetDirectoryName(rutas[0]));

            await Importar(ArchivosBloque.PrepararArchivos(rutas, out var descartados), descartados);
        }

        [RelayCommand(CanExecute = nameof(PuedeImportar))]
        private async Task ImportarCarpeta()
        {
            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta con los bloques a importar (las subcarpetas se crean como carpetas)"), Contexto.Ajustes.UltimaCarpeta);
            if (carpeta == null)
                return;
            RecordarCarpeta(carpeta);

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
                var resultado = await Contexto.Tia().ImportarAsync(destino.Plc, destino.Grupos, archivos, _progreso);
                // Se vuelve a leer el proyecto para que el árbol muestre lo importado.
                MostrarPlcs(await Contexto.Tia().LeerArbolAsync());
                resultado.Fallos.InsertRange(0, noBloques);
                MostrarResultado(resultado, L.T("{0} bloque(s) importado(s). Guarda el proyecto en TIA para conservarlos.", resultado.Correctos));
            });
        }

        private void ContarMarcados() => Marcados = Plcs.Sum(p => p.BloquesMarcados().Count());

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

        protected override void ActualizarComandos()
        {
            ExportarCommand.NotifyCanExecuteChanged();
            ImportarArchivosCommand.NotifyCanExecuteChanged();
            ImportarCarpetaCommand.NotifyCanExecuteChanged();
        }
    }
}
