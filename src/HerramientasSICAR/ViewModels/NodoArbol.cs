using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TiaConnector.Contracts;

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

        private NodoArbol(TipoNodo tipo, string dispositivo, HmiBranch? rama, IReadOnlyList<string> grupos, string nombre, string detalle,
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
        public HmiBranch? Rama { get; }

        /// <summary>
        /// Carpetas hasta este nodo: en un PLC desde "Bloques de programa"; en una HMI, dentro de su rama.
        /// Para un bloque o un elemento, las de la carpeta donde está.
        /// </summary>
        public IReadOnlyList<string> Grupos { get; }

        public string Nombre { get; }
        public NodoArbol Padre { get; }

        [ObservableProperty]
        private string _detalle;

        /// <summary>False en un PLC o una HMI cuyo contenido no se ha leído de TIA (se eligió no leerlo al conectar).</summary>
        [ObservableProperty]
        private bool _leido = true;
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
                    case TipoNodo.ElementoHmi: return Rama == HmiBranch.Tags ? "list-ordered" : Rama == HmiBranch.Scripts ? "file-code" : "layers";
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

        /// <summary>
        /// Árbol de un PLC. <paramref name="alCambiarMarcas"/> se llama cada vez que el usuario marca o desmarca algo.
        /// Con <paramref name="gruposRaiz"/>, <see cref="PlcTree.Root"/> es esa carpeta del PLC y no la raíz de bloques
        /// (los selectores de las herramientas que empiezan en la carpeta de secuencias).
        /// </summary>
        public static NodoArbol DesdePlc(PlcTree plc, Action alCambiarMarcas, IReadOnlyList<string> gruposRaiz = null)
        {
            var nodo = new NodoArbol(TipoNodo.Plc, plc.Name, null, gruposRaiz ?? new string[0], plc.Name, null, null, alCambiarMarcas) { IsExpanded = true };
            nodo.AñadirContenido(plc.Root);
            return nodo;
        }

        /// <summary>
        /// Árbol de una HMI: sus ramas, con las únicas (elementos globales, vista general) como elementos sueltos.
        /// <paramref name="nombreVisible"/> es el que se enseña (en un IPC, el de la estación); <see cref="Dispositivo"/>
        /// sigue siendo el nombre de la HMI, que es con el que se habla con TIA.
        /// </summary>
        public static NodoArbol DesdeHmi(HmiTree hmi, Action alCambiarMarcas, string nombreVisible = null)
        {
            var nodo = new NodoArbol(TipoNodo.Hmi, hmi.Name, null, new string[0], nombreVisible ?? hmi.Name, null, null, alCambiarMarcas) { IsExpanded = true };
            foreach (var rama in hmi.Branches)
                nodo.Hijos.Add(nodo.NodoDeRama(rama));
            return nodo;
        }

        /// <summary>Un PLC o una HMI sin su contenido, que se lee después con <see cref="ReemplazarContenido"/> o <see cref="ReemplazarRamas"/>.</summary>
        public static NodoArbol SinLeer(DeviceInfo dispositivo, Action alCambiarMarcas, string nombreVisible = null) =>
            new NodoArbol(dispositivo.Kind == DeviceKind.Hmi ? TipoNodo.Hmi : TipoNodo.Plc, dispositivo.Name, null, new string[0],
                nombreVisible ?? dispositivo.Name, L.T("sin leer"), null, alCambiarMarcas) { Leido = false };

        /// <summary>El PLC o la HMI del que cuelga este nodo.</summary>
        public NodoArbol Raiz => Padre?.Raiz ?? this;

        /// <summary>
        /// Cambia los bloques de este PLC por los recién leídos. Lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReemplazarContenido(BlockFolder raiz)
        {
            var anteriores = Hijos.ToList();
            Hijos.Clear();
            AñadirContenido(raiz);
            CopiarEstadoHijos(anteriores);
            AjustarMarca();
            YaLeido();
        }

        /// <summary>
        /// Cambia esas ramas de esta HMI por las recién leídas (<paramref name="leidas"/>: las que el panel tiene).
        /// Las demás ramas no se tocan; lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReemplazarRamas(IEnumerable<HmiBranch> ramas, IReadOnlyList<HmiBranchTree> leidas)
        {
            foreach (var rama in ramas)
            {
                var anterior = Hijos.FirstOrDefault(h => h.Rama == rama);
                if (anterior != null)
                    Hijos.Remove(anterior);

                var info = leidas.FirstOrDefault(r => r.Branch == rama);
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
            YaLeido();
        }

        private void YaLeido()
        {
            if (Leido)
                return;
            Leido = true;
            Detalle = null;
            IsExpanded = true;
        }

        /// <summary>El nodo de este árbol que está donde estaba <paramref name="modelo"/> (mismo tipo, rama, carpetas y nombre).</summary>
        public NodoArbol Buscar(NodoArbol modelo)
        {
            if (Tipo == modelo.Tipo && Dispositivo == modelo.Dispositivo && Rama == modelo.Rama && Nombre == modelo.Nombre
                && Grupos.SequenceEqual(modelo.Grupos))
                return this;
            return Hijos.Select(h => h.Buscar(modelo)).FirstOrDefault(n => n != null);
        }

        private static int Orden(HmiBranch rama)
        {
            for (int i = 0; i < HmiRules.TreeOrder.Count; i++)
                if (HmiRules.TreeOrder[i] == rama)
                    return i;
            return HmiRules.TreeOrder.Count;
        }

        private NodoArbol NodoDeRama(HmiBranchTree rama)
        {
            string nombre = L.T(HmiRules.FolderName(rama.Branch));
            if (HmiRules.IsSingleItem(rama.Branch))
                return new NodoArbol(TipoNodo.ElementoHmi, Dispositivo, rama.Branch, new string[0], nombre, null, this, _alCambiarMarcas);

            var hijo = new NodoArbol(TipoNodo.Rama, Dispositivo, rama.Branch, new string[0], nombre, null, this, _alCambiarMarcas);
            hijo.AñadirContenido(rama.Root);
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

        private void AñadirContenido(BlockFolder grupo)
        {
            foreach (var subgrupo in grupo.Folders)
            {
                var hijo = new NodoArbol(TipoNodo.Carpeta, Dispositivo, null, Grupos.Concat(new[] { subgrupo.Name }).ToList(), subgrupo.Name, null, this, _alCambiarMarcas);
                hijo.AñadirContenido(subgrupo);
                Hijos.Add(hijo);
            }
            foreach (var bloque in grupo.Blocks)
            {
                string detalle = $"{bloque.Type}{bloque.Number}" + (bloque.IsProtected ? " · " + L.T("protegido") : "");
                Hijos.Add(new NodoArbol(TipoNodo.Bloque, Dispositivo, null, Grupos, bloque.Name, detalle, this, _alCambiarMarcas));
            }
        }

        private void AñadirContenido(HmiFolder carpeta)
        {
            foreach (var subcarpeta in carpeta.Folders)
            {
                var hijo = new NodoArbol(TipoNodo.Carpeta, Dispositivo, Rama, Grupos.Concat(new[] { subcarpeta.Name }).ToList(), subcarpeta.Name, null, this, _alCambiarMarcas);
                hijo.AñadirContenido(subcarpeta);
                Hijos.Add(hijo);
            }
            foreach (var elemento in carpeta.Items)
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
