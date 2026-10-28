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
                nodo.Hijos.Add(nodo.NodoDeRama(rama));
            return nodo;
        }

        /// <summary>El PLC o la HMI del que cuelga este nodo.</summary>
        public NodoArbol Raiz => Padre?.Raiz ?? this;

        /// <summary>
        /// Cambia los bloques de este PLC por los recién leídos. Lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReemplazarContenido(GrupoInfo raiz)
        {
            var anteriores = Hijos.ToList();
            Hijos.Clear();
            AñadirContenido(raiz);
            CopiarEstadoHijos(anteriores);
            AjustarMarca();
        }

        /// <summary>
        /// Cambia esas ramas de esta HMI por las recién leídas (<paramref name="leidas"/>: las que el panel tiene).
        /// Las demás ramas no se tocan; lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReemplazarRamas(IEnumerable<RamaHmi> ramas, IReadOnlyList<RamaInfo> leidas)
        {
            foreach (var rama in ramas)
            {
                var anterior = Hijos.FirstOrDefault(h => h.Rama == rama);
                if (anterior != null)
                    Hijos.Remove(anterior);

                var info = leidas.FirstOrDefault(r => r.Rama == rama);
                if (info == null)
                    continue;
                var nuevo = NodoDeRama(info);
                if (anterior != null)
                    CopiarEstado(anterior, nuevo);

                int orden = Orden(rama);
                int posicion = Hijos.Count(h => Orden(h.Rama.Value) < orden);
                Hijos.Insert(posicion, nuevo);
            }
            AjustarMarca();
        }

        /// <summary>El nodo de este árbol que está donde estaba <paramref name="modelo"/> (mismo tipo, rama, carpetas y nombre).</summary>
        public NodoArbol Buscar(NodoArbol modelo)
        {
            if (Tipo == modelo.Tipo && Dispositivo == modelo.Dispositivo && Rama == modelo.Rama && Nombre == modelo.Nombre
                && Grupos.SequenceEqual(modelo.Grupos))
                return this;
            return Hijos.Select(h => h.Buscar(modelo)).FirstOrDefault(n => n != null);
        }

        private static int Orden(RamaHmi rama)
        {
            for (int i = 0; i < ArchivosHmi.OrdenArbol.Count; i++)
                if (ArchivosHmi.OrdenArbol[i] == rama)
                    return i;
            return ArchivosHmi.OrdenArbol.Count;
        }

        private NodoArbol NodoDeRama(RamaInfo rama)
        {
            string nombre = L.T(ArchivosHmi.Nombre(rama.Rama));
            if (ArchivosHmi.EsUnica(rama.Rama))
                return new NodoArbol(TipoNodo.ElementoHmi, Dispositivo, rama.Rama, new string[0], nombre, null, this, _alCambiarMarcas);

            var hijo = new NodoArbol(TipoNodo.Rama, Dispositivo, rama.Rama, new string[0], nombre, null, this, _alCambiarMarcas);
            hijo.AñadirContenido(rama.Raiz);
            return hijo;
        }

        /// <summary>Pasa al nodo recién leído si estaba abierto y marcado, y lo mismo a lo que tiene dentro.</summary>
        private static void CopiarEstado(NodoArbol anterior, NodoArbol nuevo)
        {
            nuevo.IsExpanded = anterior.IsExpanded;
            if (nuevo.EsHoja)
            {
                nuevo._propagando = true;
                nuevo.IsChecked = anterior.IsChecked;
                nuevo._propagando = false;
                return;
            }
            nuevo.CopiarEstadoHijos(anterior.Hijos);
            nuevo.AjustarMarca();
        }

        private void CopiarEstadoHijos(IEnumerable<NodoArbol> anteriores)
        {
            foreach (var hijo in Hijos)
            {
                var anterior = anteriores.FirstOrDefault(a => a.Tipo == hijo.Tipo && a.Nombre == hijo.Nombre);
                if (anterior != null)
                    CopiarEstado(anterior, hijo);
            }
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
            AjustarMarca();
            Padre?.RecalcularMarca();
        }

        /// <summary>Pone la marca de este nodo según la de sus hijos, sin avisar ni tocar a los demás.</summary>
        private void AjustarMarca()
        {
            bool? marca = Hijos.Count == 0 ? IsChecked
                : Hijos.All(h => h.IsChecked == true) ? true
                : Hijos.All(h => h.IsChecked == false) ? false
                : (bool?)null;

            _propagando = true;
            IsChecked = marca;
            _propagando = false;
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
