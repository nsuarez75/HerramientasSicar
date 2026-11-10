using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Cómo enseña los bloques un selector.</summary>
    public enum VistaSelector
    {
        /// <summary>Todo el árbol del PLC, como en Importar / Exportar.</summary>
        Completa,

        /// <summary>Desde la carpeta 10_Sequence &amp; Messageblocks, plegable por OM.</summary>
        Secuencias
    }

    /// <summary>
    /// Selector de bloques común a las herramientas: el árbol del proyecto que se lee al conectar (el mismo que el de
    /// Importar / Exportar), con solo los bloques que le valen a la herramienta, buscador y botón para leer un PLC sin
    /// leer. Se elige un bloque (<see cref="BloqueSeleccionado"/>) o varios con casillas (<see cref="Marcados"/>).
    /// </summary>
    public partial class SelectorBloques : ObservableObject
    {
        private readonly Contexto _contexto;
        private readonly VistaSelector _vista;
        private readonly Func<IReadOnlyList<string>, BloqueInfo, bool> _filtro;
        private readonly Func<IReadOnlyList<string>, string, bool> _marcadoInicial;
        private readonly Dictionary<string, bool> _marcas = new Dictionary<string, bool>();
        private bool _reconstruyendo;

        /// <param name="filtro">Bloques que salen: carpetas desde la raíz del PLC y el bloque.</param>
        /// <param name="conCasillas">Elegir varios con casillas (si no, se elige uno seleccionándolo).</param>
        /// <param name="marcadoInicial">Con casillas, si un bloque sale marcado la primera vez que aparece.</param>
        public SelectorBloques(Contexto contexto, VistaSelector vista, Func<IReadOnlyList<string>, BloqueInfo, bool> filtro,
            bool conCasillas = false, Func<IReadOnlyList<string>, string, bool> marcadoInicial = null)
        {
            _contexto = contexto;
            _vista = vista;
            _filtro = filtro;
            ConCasillas = conCasillas;
            _marcadoInicial = marcadoInicial;
            _contexto.ArbolCambiado += _ => Reconstruir();
            _contexto.PropertyChanged += AlCambiarContexto;
            _contexto.OcupadoCambiado += () => LeerCommand.NotifyCanExecuteChanged();
            Reconstruir();
        }

        public bool ConCasillas { get; }

        /// <summary>Un nodo por PLC del proyecto, con los bloques que valen.</summary>
        public ObservableCollection<NodoArbol> Raices { get; } = new ObservableCollection<NodoArbol>();

        [ObservableProperty]
        private string _filtroTexto;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BloqueSeleccionado), nameof(TextoLeer))]
        [NotifyCanExecuteChangedFor(nameof(LeerCommand))]
        private NodoArbol _seleccionado;

        /// <summary>Se elige otro bloque o cambian las marcas.</summary>
        public event Action Cambiado;

        /// <summary>El bloque seleccionado, o null si lo seleccionado no es un bloque.</summary>
        public BloqueRef BloqueSeleccionado => Seleccionado?.Tipo == TipoNodo.Bloque ? Referencia(Seleccionado) : null;

        public List<BloqueRef> Marcados() => Raices.SelectMany(r => r.HojasMarcadas()).Select(Referencia).ToList();

        public int NumeroMarcados => Raices.Sum(r => r.HojasMarcadas().Count());

        public bool HayPlcs => Raices.Count > 0;

        public string TextoLeer => Seleccionado == null ? L.T("Selecciona un PLC para leerlo")
            : Seleccionado.Raiz.Leido ? L.T("Leer de nuevo {0}", Seleccionado.Raiz.Nombre)
            : L.T("Leer {0}", Seleccionado.Raiz.Nombre);

        /// <summary>Lee de TIA el PLC de lo seleccionado; lo nuevo lo ven todas las herramientas.</summary>
        [RelayCommand(CanExecute = nameof(PuedeLeer))]
        private async Task Leer()
        {
            string plc = Seleccionado.Raiz.Nombre;
            await _contexto.Trabajar(L.T("Leyendo el árbol del proyecto…"), async () =>
            {
                await _contexto.LeerPlcAsync(plc);
                _contexto.MostrarEstado(L.T("Árbol de {0} leído.", plc));
            });
        }

        private bool PuedeLeer() => _contexto.Conectado && !_contexto.Ocupado && Seleccionado != null;

        partial void OnFiltroTextoChanged(string value)
        {
            foreach (var raiz in Raices)
                raiz.AplicarFiltro(value);
        }

        partial void OnSeleccionadoChanged(NodoArbol value)
        {
            if (!_reconstruyendo)
                Cambiado?.Invoke();
        }

        private void AlCambiarContexto(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Contexto.Conectado) || e.PropertyName == nameof(Contexto.ProyectoConectado))
                LeerCommand.NotifyCanExecuteChanged();
        }

        // Rehace el árbol desde el común; conserva la selección y las marcas de lo que ya estaba.
        private void Reconstruir()
        {
            _reconstruyendo = true;
            var seleccionado = Seleccionado;
            Raices.Clear();
            foreach (var dispositivo in _contexto.Dispositivos.Where(d => !d.EsHmi))
            {
                var plc = _contexto.PlcLeido(dispositivo.Nombre);
                if (plc == null)
                {
                    Raices.Add(NodoArbol.SinLeer(dispositivo, AlMarcar));
                    continue;
                }

                var raiz = plc.Raiz;
                var ruta = new List<string>();
                if (_vista == VistaSelector.Secuencias)
                    raiz = BuscarCarpeta(plc.Raiz, ComentariosSecuencia.CarpetaSecuencias, ruta);
                var podado = raiz == null ? new GrupoInfo { Nombre = plc.Raiz.Nombre } : Podar(raiz, ruta.ToList());
                var nodo = NodoArbol.DesdePlc(new PlcInfo { Nombre = plc.Nombre, Raiz = podado }, AlMarcar, ruta);
                if (raiz == null)
                    nodo.Detalle = L.T("sin la carpeta {0}", ComentariosSecuencia.CarpetaSecuencias);
                else if (podado.Grupos.Count == 0 && podado.Bloques.Count == 0)
                    nodo.Detalle = L.T("ningún bloque");
                if (ConCasillas)
                    PonerMarcas(nodo);
                Raices.Add(nodo);
            }
            OnFiltroTextoChanged(FiltroTexto);
            Seleccionado = seleccionado == null ? null : Raices.Select(r => r.Buscar(seleccionado)).FirstOrDefault(n => n != null);
            _reconstruyendo = false;
            OnPropertyChanged(nameof(HayPlcs));
            OnPropertyChanged(nameof(NumeroMarcados));
            Cambiado?.Invoke();
        }

        // Solo los bloques que valen, y las carpetas que tienen alguno.
        private GrupoInfo Podar(GrupoInfo grupo, List<string> ruta)
        {
            var podado = new GrupoInfo { Nombre = grupo.Nombre };
            foreach (var sub in grupo.Grupos)
            {
                ruta.Add(sub.Nombre);
                var hijo = Podar(sub, ruta);
                ruta.RemoveAt(ruta.Count - 1);
                if (hijo.Grupos.Count > 0 || hijo.Bloques.Count > 0)
                    podado.Grupos.Add(hijo);
            }
            foreach (var bloque in grupo.Bloques.Where(b => _filtro(ruta, b)))
                podado.Bloques.Add(bloque);
            return podado;
        }

        // La carpeta con ese nombre a cualquier profundidad; deja en «ruta» las carpetas hasta ella (incluida).
        private static GrupoInfo BuscarCarpeta(GrupoInfo grupo, string nombre, List<string> ruta)
        {
            foreach (var sub in grupo.Grupos)
            {
                ruta.Add(sub.Nombre);
                if (sub.Nombre == nombre)
                    return sub;
                var encontrada = BuscarCarpeta(sub, nombre, ruta);
                if (encontrada != null)
                    return encontrada;
                ruta.RemoveAt(ruta.Count - 1);
            }
            return null;
        }

        // Cada bloque conserva su marca; uno que no estaba sale como diga marcadoInicial.
        private void PonerMarcas(NodoArbol nodo)
        {
            if (nodo.EsHoja)
            {
                string clave = Clave(Referencia(nodo));
                if (!_marcas.TryGetValue(clave, out bool marcado))
                    _marcas[clave] = marcado = _marcadoInicial?.Invoke(nodo.Grupos, nodo.Nombre) ?? false;
                if (marcado)
                    nodo.IsChecked = true;
                return;
            }
            foreach (var hijo in nodo.Hijos)
                PonerMarcas(hijo);
        }

        private void AlMarcar()
        {
            if (_reconstruyendo)
                return;
            foreach (var hoja in Raices.SelectMany(TodasLasHojas))
                _marcas[Clave(Referencia(hoja))] = hoja.IsChecked == true;
            OnPropertyChanged(nameof(NumeroMarcados));
            Cambiado?.Invoke();
        }

        private static IEnumerable<NodoArbol> TodasLasHojas(NodoArbol nodo) =>
            nodo.EsHoja ? new[] { nodo } : nodo.Hijos.SelectMany(TodasLasHojas);

        private static BloqueRef Referencia(NodoArbol nodo) =>
            new BloqueRef { Plc = nodo.Dispositivo, Grupos = nodo.Grupos, Nombre = nodo.Nombre };

        private static string Clave(BloqueRef b) => b.Plc + "|" + string.Join("/", b.Grupos) + "|" + b.Nombre;
    }
}
