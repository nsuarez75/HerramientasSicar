using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Un array del bloque elegido, con sus usos e índices actuales.</summary>
    public sealed class FilaArray
    {
        public FilaArray(ArrayDelBloque array)
        {
            Array = array;
        }

        public ArrayDelBloque Array { get; }
        public string Nombre => Array.Nombre;
        public string Usos => L.T("{0} usos", Array.Usos);

        /// <summary>Índices actuales, en el orden en que aparecen (los primeros, si son muchos).</summary>
        public string Indices
        {
            get
            {
                const int maximo = 40;
                string lista = string.Join(", ", Array.Indices.Take(maximo));
                return Array.Indices.Count > maximo ? lista + ", …" : lista;
            }
        }
    }

    public partial class RenombrarArrayViewModel : Herramienta
    {
        // Para no leer dos veces el mismo bloque.
        private BloqueRef _leido;

        public RenombrarArrayViewModel(Contexto contexto) : base(contexto)
        {
            Selector = new SelectorBloques(contexto, VistaSelector.Completa,
                (_, b) => (b.Tipo == "FB" || b.Tipo == "FC") && !b.Protegido);
            Selector.Cambiado += AlCambiarBloque;
            _estadoBloque = L.T("Elige en el árbol el FB o FC.");
        }

        public override string Nombre => L.T("Renumerar Array");
        public override string Descripcion => L.T("Renumera de menor a mayor todos los usos de un array dentro de un FB o FC. Útil, por ejemplo, para intercalar un segmento nuevo en un array de flancos sin reordenarlo a mano. No es exclusivo de SICAR.");
        public override string Icono => "list-ordered";
        public override bool UsaTia => true;

        public SelectorBloques Selector { get; }

        public ObservableCollection<FilaArray> Arrays { get; } = new ObservableCollection<FilaArray>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenumerarCommand))]
        private FilaArray _arraySeleccionado;

        [ObservableProperty]
        private string _estadoBloque;

        public string NombreBloque => Selector.BloqueSeleccionado?.Nombre;
        public bool HayBloque => Selector.BloqueSeleccionado != null;
        public bool HayArrays => Arrays.Count > 0;

        private static bool MismoBloque(BloqueRef a, BloqueRef b) =>
            a != null && b != null && a.Plc == b.Plc && a.Nombre == b.Nombre && a.Grupos.SequenceEqual(b.Grupos);

        // Al elegir un bloque se exporta (a una carpeta temporal) y se listan sus arrays con índice fijo.
        private async void AlCambiarBloque()
        {
            OnPropertyChanged(nameof(NombreBloque));
            OnPropertyChanged(nameof(HayBloque));
            RenumerarCommand.NotifyCanExecuteChanged();
            var bloque = Selector.BloqueSeleccionado;
            if (bloque == null)
            {
                _leido = null;
                PonerArrays(null);
                EstadoBloque = L.T("Elige en el árbol el FB o FC.");
                return;
            }
            if (MismoBloque(_leido, bloque) || !PuedeUsarTia)
                return;
            await LeerArrays(bloque);
        }

        private async Task LeerArrays(BloqueRef bloque)
        {
            _leido = bloque;
            PonerArrays(null);
            EstadoBloque = L.T("Leyendo {0}…", bloque.Nombre);
            await Contexto.Trabajar(L.T("Leyendo {0}…", bloque.Nombre), async () =>
            {
                string xml = await Contexto.Tia().EnProyecto(p => p.LeerXml(bloque));
                var arrays = await Task.Run(() => ArraysBloque.Listar(xml));
                if (!MismoBloque(Selector.BloqueSeleccionado, bloque))
                    return;
                PonerArrays(arrays);
                EstadoBloque = arrays.Count == 0
                    ? L.T("{0} no usa ningún array con índice fijo.", bloque.Nombre)
                    : L.T("Elige el array que quieres renumerar.");
                Contexto.MostrarEstado(L.T("{0}: {1} array(s) con índice fijo.", bloque.Nombre, arrays.Count));
            });
            if (Arrays.Count == 0 && MismoBloque(_leido, bloque) && Contexto.EstadoError)
            {
                _leido = null;
                EstadoBloque = Contexto.Estado;
            }
        }

        private void PonerArrays(IReadOnlyList<ArrayDelBloque> arrays)
        {
            Arrays.Clear();
            foreach (var array in arrays ?? new ArrayDelBloque[0])
                Arrays.Add(new FilaArray(array));
            ArraySeleccionado = Arrays.Count == 1 ? Arrays[0] : null;
            OnPropertyChanged(nameof(HayArrays));
        }

        [RelayCommand(CanExecute = nameof(PuedeRenumerar))]
        private async Task Renumerar()
        {
            var bloque = Selector.BloqueSeleccionado;
            string array = ArraySeleccionado.Nombre;
            bool hecho = false;
            int cambios = 0;
            await Contexto.Trabajar(L.T("Exportando {0}…", bloque.Nombre), async () =>
            {
                // Se exporta, se renumera y se importa encima del original, en su mismo grupo.
                var resultado = await Contexto.Tia().EnProyecto(p =>
                    p.ModificarBloques(new[] { bloque }, (_, xml) => ArraysBloque.Renumerar(xml, array, out cambios), compilar: false, ProgresoEnEstado()));
                if (resultado.Fallos.Count > 0)
                {
                    Contexto.PonerFallos(resultado.Fallos);
                    Contexto.MostrarEstado(resultado.Fallos[0], error: true);
                    return;
                }
                hecho = true;
            });
            if (!hecho)
                return;

            // Se vuelve a leer el bloque para ver los índices nuevos.
            await LeerArrays(bloque);
            ArraySeleccionado = Arrays.FirstOrDefault(a => a.Nombre == array) ?? ArraySeleccionado;
            Contexto.MostrarEstado(L.T("{0}: {1} uso(s) de {2} renumerados. Guarda el proyecto en TIA para conservarlos.", bloque.Nombre, cambios, array));
        }

        private bool PuedeRenumerar() => PuedeUsarTia && Selector.BloqueSeleccionado != null && ArraySeleccionado != null;

        protected override void ActualizarComandos() => RenumerarCommand.NotifyCanExecuteChanged();
    }
}
