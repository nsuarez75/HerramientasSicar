using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Un PLC, un grupo o un bloque en el árbol. Marcar un PLC o un grupo marca todo lo que tiene dentro.
    /// </summary>
    public partial class NodoArbol : ObservableObject
    {
        private readonly Action _alCambiarMarcas;
        private bool _propagando;

        private NodoArbol(string plc, IReadOnlyList<string> grupos, string nombre, string detalle, NodoArbol padre, Action alCambiarMarcas)
        {
            Plc = plc;
            Grupos = grupos;
            Nombre = nombre;
            Detalle = detalle;
            Padre = padre;
            _alCambiarMarcas = alCambiarMarcas;
        }

        public string Plc { get; }

        /// <summary>Grupos desde "Bloques de programa" hasta este nodo (para un bloque, los de su grupo).</summary>
        public IReadOnlyList<string> Grupos { get; }

        public string Nombre { get; }
        public string Detalle { get; }
        public NodoArbol Padre { get; }
        public bool EsBloque => Detalle != null;
        public string Icono => EsBloque ? "box" : Padre == null ? "cpu" : "folder";
        public ObservableCollection<NodoArbol> Hijos { get; } = new ObservableCollection<NodoArbol>();

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
            var nodo = new NodoArbol(plc.Nombre, new string[0], plc.Nombre, null, null, alCambiarMarcas) { IsExpanded = true };
            nodo.AñadirContenido(plc.Raiz);
            return nodo;
        }

        private void AñadirContenido(GrupoInfo grupo)
        {
            foreach (var subgrupo in grupo.Grupos)
            {
                var hijo = new NodoArbol(Plc, Grupos.Concat(new[] { subgrupo.Nombre }).ToList(), subgrupo.Nombre, null, this, _alCambiarMarcas);
                hijo.AñadirContenido(subgrupo);
                Hijos.Add(hijo);
            }
            foreach (var bloque in grupo.Bloques)
            {
                string detalle = $"{bloque.Tipo}{bloque.Numero}" + (bloque.Protegido ? " · " + L.T("protegido") : "");
                Hijos.Add(new NodoArbol(Plc, Grupos, bloque.Nombre, detalle, this, _alCambiarMarcas));
            }
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

        /// <summary>Bloques marcados de este nodo hacia abajo.</summary>
        public IEnumerable<NodoArbol> BloquesMarcados()
        {
            if (EsBloque)
                return IsChecked == true ? new[] { this } : Enumerable.Empty<NodoArbol>();
            return Hijos.SelectMany(h => h.BloquesMarcados());
        }

        /// <summary>Deja visibles solo los bloques cuyo nombre contiene el texto (y los grupos que los contienen).</summary>
        public bool AplicarFiltro(string texto)
        {
            bool hayTexto = !string.IsNullOrWhiteSpace(texto);
            bool coincide = EsBloque && (!hayTexto || Nombre.IndexOf(texto.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            bool algunHijo = false;
            foreach (var hijo in Hijos)
                algunHijo |= hijo.AplicarFiltro(texto);

            Visible = coincide || algunHijo || (!EsBloque && !hayTexto);
            if (hayTexto && algunHijo)
                IsExpanded = true;
            return Visible;
        }
    }
}
