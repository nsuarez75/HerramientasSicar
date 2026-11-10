using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Logica.MenusHmi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Idioma que se edita en las cajas de texto del Generador HMI.</summary>
    public enum IdiomaEdicion { Texto, Ingles, Aleman }

    /// <summary>
    /// Caja de texto sobre un <see cref="TextoHmi"/>: según el idioma que se edita, escribe en Texto, Ingles o Aleman.
    /// En inglés y alemán, el texto de marca de agua es el que saldría (el de Texto).
    /// </summary>
    public sealed class TextoEditable : ObservableObject
    {
        private readonly TextoHmi _texto;
        private readonly GeneradorHmiViewModel _vm;
        private readonly Func<string> _estandar;

        public TextoEditable(TextoHmi texto, GeneradorHmiViewModel vm, Func<string> estandar = null)
        {
            _texto = texto;
            _vm = vm;
            _estandar = estandar;
        }

        public string Valor
        {
            get
            {
                switch (_vm.Idioma)
                {
                    case IdiomaEdicion.Ingles: return _texto.Ingles;
                    case IdiomaEdicion.Aleman: return _texto.Aleman;
                    default: return _texto.Texto;
                }
            }
            set
            {
                value = value ?? "";
                if (value == Valor)
                    return;
                switch (_vm.Idioma)
                {
                    case IdiomaEdicion.Ingles: _texto.Ingles = value; break;
                    case IdiomaEdicion.Aleman: _texto.Aleman = value; break;
                    default: _texto.Texto = value; break;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarcaDeAgua));
                _vm.MarcarCambiado();
            }
        }

        /// <summary>Lo que sale si la caja se deja vacía.</summary>
        public string MarcaDeAgua =>
            _vm.Idioma != IdiomaEdicion.Texto && !string.IsNullOrWhiteSpace(_texto.Texto) ? _texto.Texto : _estandar?.Invoke() ?? "";

        public void Refrescar()
        {
            OnPropertyChanged(nameof(Valor));
            OnPropertyChanged(nameof(MarcaDeAgua));
        }

        // Solo la marca de agua: no toca el valor de una caja en la que se está escribiendo.
        public void RefrescarMarca() => OnPropertyChanged(nameof(MarcaDeAgua));
    }

    /// <summary>Una HMI en la lista de HMIs del proyecto.</summary>
    public sealed class HmiEditable : ObservableObject
    {
        private readonly GeneradorHmiViewModel _vm;

        public HmiEditable(HmiMenus hmi, GeneradorHmiViewModel vm)
        {
            Hmi = hmi;
            _vm = vm;
        }

        public HmiMenus Hmi { get; }

        public string Nombre
        {
            get => Hmi.Nombre;
            set
            {
                if (value == Hmi.Nombre)
                    return;
                Hmi.Nombre = value ?? "";
                OnPropertyChanged();
                _vm.MarcarCambiado();
            }
        }

        public decimal? Numero
        {
            get => Hmi.Numero;
            set
            {
                int numero = (int)(value ?? 1);
                if (numero == Hmi.Numero)
                    return;
                Hmi.Numero = numero;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Idb));
                _vm.MarcarCambiado();
            }
        }

        public string Idb => Hmi.NombreIdb;
    }

    /// <summary>Nodo del árbol de «Pantallas»: un grupo del menú (se pliega) o un subgrupo (se elige).</summary>
    public sealed partial class NodoMenu : ObservableObject
    {
        private NodoMenu(string nombre, SubgrupoMenu subgrupo, IReadOnlyList<NodoMenu> hijos)
        {
            Nombre = nombre;
            Subgrupo = subgrupo;
            Hijos = hijos;
        }

        public static NodoMenu DeGrupo(GrupoMenu grupo, IEnumerable<SubgrupoMenu> subgrupos) =>
            new NodoMenu(grupo.Nombre, null, subgrupos.Select(s => new NodoMenu(s.Nombre, s, new NodoMenu[0])).ToList());

        public string Nombre { get; }

        /// <summary>Null en un grupo.</summary>
        public SubgrupoMenu Subgrupo { get; }

        public IReadOnlyList<NodoMenu> Hijos { get; }
        public bool EsGrupo => Subgrupo == null;

        [ObservableProperty]
        private bool _expandido = true;
    }

    /// <summary>Casilla «se ve en esta HMI» de una pantalla.</summary>
    public sealed class CasillaVisible : ObservableObject
    {
        private readonly List<bool> _visibles;
        private readonly int _indice;
        private readonly GeneradorHmiViewModel _vm;

        public CasillaVisible(List<bool> visibles, int indice, GeneradorHmiViewModel vm)
        {
            _visibles = visibles;
            _indice = indice;
            _vm = vm;
        }

        /// <summary>False si el FC no tiene esta posición (la casilla no se ve).</summary>
        public bool Existe => _visibles != null;

        public bool Visible
        {
            get => Existe && _visibles[_indice];
            set
            {
                if (!Existe || value == _visibles[_indice])
                    return;
                _visibles[_indice] = value;
                OnPropertyChanged();
                _vm.MarcarCambiado();
            }
        }

        public void Refrescar() => OnPropertyChanged(nameof(Visible));
    }

    /// <summary>Una pantalla (posición) del subgrupo elegido: su nombre y una casilla por HMI.</summary>
    public sealed class FilaPantalla
    {
        public FilaPantalla(int posicion, TextoEditable nombre, IReadOnlyList<CasillaVisible> casillas)
        {
            Posicion = posicion;
            Nombre = nombre;
            Casillas = casillas;
        }

        public int Posicion { get; }
        public TextoEditable Nombre { get; }
        public bool TieneNombre => Nombre != null;
        public IReadOnlyList<CasillaVisible> Casillas { get; }
    }

    /// <summary>Una de las 24 pantallas de funciones de usuario.</summary>
    public sealed class PantallaFunciones : ObservableObject
    {
        private readonly ProyectoMenus _proyecto;

        public PantallaFunciones(int numero, ProyectoMenus proyecto, IReadOnlyList<FilaFuncion> funciones)
        {
            Numero = numero;
            _proyecto = proyecto;
            Funciones = funciones;
        }

        public int Numero { get; }
        public IReadOnlyList<FilaFuncion> Funciones { get; }

        public string Titulo
        {
            get
            {
                string nombre = _proyecto.Nombre(CatalogoMenus.UserFunctions, Numero).Texto;
                return string.IsNullOrWhiteSpace(nombre) ? Numero.ToString() : Numero + " · " + nombre;
            }
        }

        public int Configuradas => Funciones.Count(f => !f.Funcion.Vacia);

        public void Refrescar()
        {
            OnPropertyChanged(nameof(Titulo));
            OnPropertyChanged(nameof(Configuradas));
        }
    }

    /// <summary>Una función de usuario con sus cinco textos.</summary>
    public sealed class FilaFuncion
    {
        public FilaFuncion(int numero, FuncionUsuario funcion, GeneradorHmiViewModel vm, int pantalla)
        {
            Numero = numero;
            Funcion = funcion;
            Nombre = new TextoEditable(funcion.Nombre, vm, () => Estandar(pantalla, "Name"));
            AccionIzquierda = new TextoEditable(funcion.AccionIzquierda, vm, () => Estandar(pantalla, "Action left"));
            AccionDerecha = new TextoEditable(funcion.AccionDerecha, vm, () => Estandar(pantalla, "Action right"));
            FeedbackIzquierda = new TextoEditable(funcion.FeedbackIzquierda, vm, () => Estandar(pantalla, "Feed back left"));
            FeedbackDerecha = new TextoEditable(funcion.FeedbackDerecha, vm, () => Estandar(pantalla, "Feed back right"));
        }

        // Si la función está entera vacía salen los textos estándar; si no, lo vacío sale vacío.
        private string Estandar(int pantalla, string campo) => Funcion.Vacia ? $"<<S{pantalla} L{Numero} {campo}>>" : "";

        public int Numero { get; }
        public FuncionUsuario Funcion { get; }
        public TextoEditable Nombre { get; }
        public TextoEditable AccionIzquierda { get; }
        public TextoEditable AccionDerecha { get; }
        public TextoEditable FeedbackIzquierda { get; }
        public TextoEditable FeedbackDerecha { get; }

        public IEnumerable<TextoEditable> Textos => new[] { Nombre, AccionIzquierda, AccionDerecha, FeedbackIzquierda, FeedbackDerecha };
    }
}
