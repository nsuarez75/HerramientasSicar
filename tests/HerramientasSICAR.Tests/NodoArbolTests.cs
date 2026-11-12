using HerramientasSICAR.ViewModels;
using System.Collections.Generic;
using System.Linq;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class NodoArbolTests
    {
        private static HmiFolder Carpeta(string nombre, params string[] elementos)
        {
            var carpeta = new HmiFolder { Name = nombre };
            carpeta.Items.AddRange(elementos);
            return carpeta;
        }

        private static HmiTree Hmi()
        {
            var pantallas = Carpeta("Pantallas", "Inicio");
            pantallas.Folders.Add(Carpeta("Traceability", "T_01", "T_02"));
            var hmi = new HmiTree { Name = "HMI_1" };
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Screens, Root = pantallas });
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Tags, Root = Carpeta("Variables", "Tabla") });
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Cycles, Root = Carpeta(null, "Default") });
            return hmi;
        }

        private static NodoArbol Hijo(NodoArbol nodo, string nombre) => nodo.Hijos.Single(h => h.Nombre == nombre);

        [Fact]
        public void Releer_una_rama_cambia_solo_esa_y_conserva_marcas_y_carpetas_abiertas()
        {
            var hmi = NodoArbol.DesdeHmi(Hmi(), null);
            var pantallas = hmi.Hijos[0];
            var variables = hmi.Hijos[1];
            var traceability = Hijo(pantallas, "Traceability");
            traceability.IsExpanded = true;
            Hijo(traceability, "T_01").IsChecked = true;

            var nuevas = Carpeta("Pantallas", "Inicio");
            nuevas.Folders.Add(Carpeta("Traceability", "T_01", "T_02", "T_03"));
            hmi.ReemplazarRamas(new[] { HmiBranch.Screens },
                new List<HmiBranchTree> { new HmiBranchTree { Branch = HmiBranch.Screens, Root = nuevas } });

            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Tags, HmiBranch.Cycles }, hmi.Hijos.Select(h => h.Rama));
            Assert.Same(variables, hmi.Hijos[1]);
            var nueva = Hijo(hmi.Hijos[0], "Traceability");
            Assert.NotSame(traceability, nueva);
            Assert.True(nueva.IsExpanded);
            Assert.Equal(new[] { "T_01", "T_02", "T_03" }, nueva.Hijos.Select(h => h.Nombre));
            Assert.Equal(new[] { "T_01" }, hmi.HojasMarcadas().Select(h => h.Nombre));
            Assert.Null(nueva.IsChecked);
            Assert.Null(hmi.IsChecked);
        }

        [Fact]
        public void Una_rama_que_ya_no_esta_se_quita_y_una_nueva_entra_en_su_orden()
        {
            var hmi = NodoArbol.DesdeHmi(Hmi(), null);

            hmi.ReemplazarRamas(new[] { HmiBranch.Tags, HmiBranch.Scripts },
                new List<HmiBranchTree> { new HmiBranchTree { Branch = HmiBranch.Scripts, Root = Carpeta("Scripts", "Script_1") } });

            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Cycles, HmiBranch.Scripts }, hmi.Hijos.Select(h => h.Rama));
        }

        [Fact]
        public void Releer_un_plc_conserva_las_marcas_de_lo_que_sigue()
        {
            var raiz = new BlockFolder { Name = "Bloques de programa" };
            raiz.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });
            raiz.Blocks.Add(new BlockInfo { Name = "FB_Viejo", Type = "FB", Number = 10 });
            var plc = NodoArbol.DesdePlc(new PlcTree { Name = "PLC_1", Root = raiz }, null);
            Hijo(plc, "Main").IsChecked = true;

            var nueva = new BlockFolder { Name = "Bloques de programa" };
            nueva.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });
            nueva.Blocks.Add(new BlockInfo { Name = "FB_Nuevo", Type = "FB", Number = 11 });
            plc.ReemplazarContenido(nueva);

            Assert.Equal(new[] { "Main", "FB_Nuevo" }, plc.Hijos.Select(h => h.Nombre));
            Assert.Equal(new[] { "Main" }, plc.HojasMarcadas().Select(h => h.Nombre));
            Assert.Null(plc.IsChecked);
        }

        [Fact]
        public void Una_hmi_sin_leer_queda_leida_al_traer_sus_ramas()
        {
            var hmi = NodoArbol.SinLeer(new DeviceInfo { Name = "HMI_3", Kind = DeviceKind.Hmi }, null);
            Assert.Equal(TipoNodo.Hmi, hmi.Tipo);
            Assert.False(hmi.Leido);
            Assert.NotNull(hmi.Detalle);
            Assert.Empty(hmi.Hijos);

            hmi.ReemplazarRamas(HmiRules.TreeOrder, Hmi().Branches);

            Assert.True(hmi.Leido);
            Assert.Null(hmi.Detalle);
            Assert.True(hmi.IsExpanded);
            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Tags, HmiBranch.Cycles }, hmi.Hijos.Select(h => h.Rama));
        }

        [Fact]
        public void Un_plc_sin_leer_queda_leido_al_traer_sus_bloques()
        {
            var plc = NodoArbol.SinLeer(new DeviceInfo { Name = "PLC_1" }, null);
            var raiz = new BlockFolder { Name = "Bloques de programa" };
            raiz.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });

            plc.ReemplazarContenido(raiz);

            Assert.Equal(TipoNodo.Plc, plc.Tipo);
            Assert.True(plc.Leido);
            Assert.Equal(new[] { "Main" }, plc.Hijos.Select(h => h.Nombre));
        }

        [Fact]
        public void Buscar_encuentra_el_destino_en_el_arbol_releido()
        {
            var hmi = NodoArbol.DesdeHmi(Hmi(), null);
            var destino = Hijo(hmi.Hijos[0], "Traceability");

            hmi.ReemplazarRamas(new[] { HmiBranch.Screens }, Hmi().Branches);

            var encontrado = destino.Raiz.Buscar(destino);
            Assert.NotNull(encontrado);
            Assert.NotSame(destino, encontrado);
            Assert.Same(hmi, encontrado.Raiz);
            Assert.Equal(new[] { "Traceability" }, encontrado.Grupos);
        }
    }
}
