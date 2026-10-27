using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HerramientasSICAR.ViewModels
{
    public enum TipoNodo
    {
        Plc,
        Hmi,
        Rama,
        Carpeta,
        Bloque,
        ElementoHmi
    }

    /// <summary>
    /// Un nodo del árbol: PLC o HMI, rama de HMI, carpeta, bloque o elemento de HMI.
    /// Marcar un nodo marca todo lo que tiene dentro.
    /// </summary>
    public partial class NodoArbol : ObservableObject
    {
        private readonly Action _alCambiarMarcas;
        private bool _propagando;

        private NodoArbol(TipoNodo tipo, string dispositivo, RamaHmi? rama, IReadOnlyList<string> grupos, string nombre, string detalle,
            NodoArbol padre, Action alCambiarMarcas)
        {
            Tipo = tipo;
            Dispositivo = dispositivo;
            Rama = rama;
            Grupos = grupos;
            Nombre = nombre;
            Detalle = detalle;
            Padre = padre;
            _alCambiarMarcas = alCambiarMarcas;
        }

        public TipoNodo Tipo { get; }

        /// <summary>Nombre del PLC o de la HMI al que pertenece el nodo.</summary>
        public string Dispositivo { get; }

        /// <summary>Rama de la HMI (null en los PLC y en el propio nodo de la HMI).</summary>
        public RamaHmi? Rama { get; }

        /// <summary>
        /// Carpetas hasta este nodo: en un PLC desde "Bloques de programa"; en una HMI, dentro de su rama.
        /// Para un bloque o un elemento, las de la carpeta donde está.
        /// </summary>
        public IReadOnlyList<string> Grupos { get; }

        public string Nombre { get; }
        public string Detalle { get; }
        public NodoArbol Padre { get; }
        public bool EsHoja => Tipo == TipoNodo.Bloque || Tipo == TipoNodo.ElementoHmi;
        public bool EsDeHmi => Tipo == TipoNodo.Hmi || Rama != null;
        public ObservableCollection<NodoArbol> Hijos { get; } = new ObservableCollection<NodoArbol>();

        public string Icono
        {
            get
            {
                switch (Tipo)
                {
                    case TipoNodo.Plc: return "cpu";
                    case TipoNodo.Hmi: return "monitor";
                    case TipoNodo.Bloque: return "box";
                    case TipoNodo.ElementoHmi: return Rama == RamaHmi.Variables ? "list-ordered" : Rama == RamaHmi.Scripts ? "file-code" : "layers";
                    default: return "folder";
                }
            }
        }

        [ObservableProperty]
        private bool? _isChecked = false;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _visible = true;

        /// <summary>Árbol de un PLC. <paramref name="alCambiarMarcas"/> se llama cada vez que el usuario marca o desmarca algo.</summary>
        public static NodoArbol DesdePlc(PlcInfo plc, Action alCambiarMarcas)
        {
            var nodo = new NodoArbol(TipoNodo.Plc, plc.Nombre, null, new string[0], plc.Nombre, null, null, alCambiarMarcas) { IsExpanded = true };
            nodo.AñadirContenido(plc.Raiz);
            return nodo;
        }

        /// <summary>Árbol de una HMI: sus ramas, con las únicas (elementos globales, vista general) como elementos sueltos.</summary>
        public static NodoArbol DesdeHmi(HmiInfo hmi, Action alCambiarMarcas)
        {
            var nodo = new NodoArbol(TipoNodo.Hmi, hmi.Nombre, null, new string[0], hmi.Nombre, null, null, alCambiarMarcas) { IsExpanded = true };
            foreach (var rama in hmi.Ramas)
            {
                string nombre = L.T(ArchivosHmi.Nombre(rama.Rama));
                if (ArchivosHmi.EsUnica(rama.Rama))
                {
                    nodo.Hijos.Add(new NodoArbol(TipoNodo.ElementoHmi, hmi.Nombre, rama.Rama, new string[0], nombre, null, nodo, alCambiarMarcas));
                    continue;
                }
                var hijo = new NodoArbol(TipoNodo.Rama, hmi.Nombre, rama.Rama, new string[0], nombre, null, nodo, alCambiarMarcas);
                hijo.AñadirContenido(rama.Raiz);
                nodo.Hijos.Add(hijo);
            }
            return nodo;
        }

        private void AñadirContenido(GrupoInfo grupo)
        {
            foreach (var subgrupo in grupo.Grupos)
            {
                var hijo = new NodoArbol(TipoNodo.Carpeta, Dispositivo, null, Grupos.Concat(new[] { subgrupo.Nombre }).ToList(), subgrupo.Nombre, null, this, _alCambiarMarcas);
                hijo.AñadirContenido(subgrupo);
                Hijos.Add(hijo);
            }
            foreach (var bloque in grupo.Bloques)
            {
                string detalle = $"{bloque.Tipo}{bloque.Numero}" + (bloque.Protegido ? " · " + L.T("protegido") : "");
                Hijos.Add(new NodoArbol(TipoNodo.Bloque, Dispositivo, null, Grupos, bloque.Nombre, detalle, this, _alCambiarMarcas));
            }
        }

        private void AñadirContenido(CarpetaHmiInfo carpeta)
        {
            foreach (var subcarpeta in carpeta.Carpetas)
            {
                var hijo = new NodoArbol(TipoNodo.Carpeta, Dispositivo, Rama, Grupos.Concat(new[] { subcarpeta.Nombre }).ToList(), subcarpeta.Nombre, null, this, _alCambiarMarcas);
                hijo.AñadirContenido(subcarpeta);
                Hijos.Add(hijo);
            }
            foreach (var elemento in carpeta.Elementos)
                Hijos.Add(new NodoArbol(TipoNodo.ElementoHmi, Dispositivo, Rama, Grupos, elemento, null, this, _alCambiarMarcas));
        }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (_propagando || value == null)
                return;

            MarcarHijos(value);
            Padre?.RecalcularMarca();
            _alCambiarMarcas?.Invoke();
        }

        private void MarcarHijos(bool? value)
        {
            // Con el filtro puesto solo se marca lo que se ve.
            foreach (var hijo in Hijos.Where(h => h.Visible))
            {
                hijo._propagando = true;
                hijo.IsChecked = value;
                hijo._propagando = false;
                hijo.MarcarHijos(value);
            }
        }

        private void RecalcularMarca()
        {
            bool? marca = Hijos.Count == 0 ? IsChecked
                : Hijos.All(h => h.IsChecked == true) ? true
                : Hijos.All(h => h.IsChecked == false) ? false
                : (bool?)null;

            _propagando = true;
            IsChecked = marca;
            _propagando = false;
            Padre?.RecalcularMarca();
        }

        /// <summary>Bloques y elementos de HMI marcados de este nodo hacia abajo.</summary>
        public IEnumerable<NodoArbol> HojasMarcadas()
        {
            if (EsHoja)
                return IsChecked == true ? new[] { this } : Enumerable.Empty<NodoArbol>();
            return Hijos.SelectMany(h => h.HojasMarcadas());
        }

        /// <summary>Deja visibles solo los bloques y elementos cuyo nombre contiene el texto (y las carpetas que los contienen).</summary>
        public bool AplicarFiltro(string texto)
        {
            bool hayTexto = !string.IsNullOrWhiteSpace(texto);
            bool coincide = EsHoja && (!hayTexto || Nombre.IndexOf(texto.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            bool algunHijo = false;
            foreach (var hijo in Hijos)
                algunHijo |= hijo.AplicarFiltro(texto);

            Visible = coincide || algunHijo || (!EsHoja && !hayTexto);
            if (hayTexto && algunHijo)
                IsExpanded = true;
            return Visible;
        }
    }
}
