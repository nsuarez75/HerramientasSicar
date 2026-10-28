using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using HerramientasSICAR.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class NodoArbolTests
    {
        private static CarpetaHmiInfo Carpeta(string nombre, params string[] elementos)
        {
            var carpeta = new CarpetaHmiInfo { Nombre = nombre };
            carpeta.Elementos.AddRange(elementos);
            return carpeta;
        }

        private static HmiInfo Hmi()
        {
            var pantallas = Carpeta("Pantallas", "Inicio");
            pantallas.Carpetas.Add(Carpeta("Traceability", "T_01", "T_02"));
            var hmi = new HmiInfo { Nombre = "HMI_1" };
            hmi.Ramas.Add(new RamaInfo { Rama = RamaHmi.Pantallas, Raiz = pantallas });
            hmi.Ramas.Add(new RamaInfo { Rama = RamaHmi.Variables, Raiz = Carpeta("Variables", "Tabla") });
            hmi.Ramas.Add(new RamaInfo { Rama = RamaHmi.Ciclos, Raiz = Carpeta(null, "Default") });
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
            nuevas.Carpetas.Add(Carpeta("Traceability", "T_01", "T_02", "T_03"));
            hmi.ReemplazarRamas(new[] { RamaHmi.Pantallas },
                new List<RamaInfo> { new RamaInfo { Rama = RamaHmi.Pantallas, Raiz = nuevas } });

            Assert.Equal(new RamaHmi?[] { RamaHmi.Pantallas, RamaHmi.Variables, RamaHmi.Ciclos }, hmi.Hijos.Select(h => h.Rama));
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

            hmi.ReemplazarRamas(new[] { RamaHmi.Variables, RamaHmi.Scripts },
                new List<RamaInfo> { new RamaInfo { Rama = RamaHmi.Scripts, Raiz = Carpeta("Scripts", "Script_1") } });

            Assert.Equal(new RamaHmi?[] { RamaHmi.Pantallas, RamaHmi.Ciclos, RamaHmi.Scripts }, hmi.Hijos.Select(h => h.Rama));
        }

        [Fact]
        public void Releer_un_plc_conserva_las_marcas_de_lo_que_sigue()
        {
            var raiz = new GrupoInfo { Nombre = "Bloques de programa" };
            raiz.Bloques.Add(new BloqueInfo { Nombre = "Main", Tipo = "OB", Numero = 1 });
            raiz.Bloques.Add(new BloqueInfo { Nombre = "FB_Viejo", Tipo = "FB", Numero = 10 });
            var plc = NodoArbol.DesdePlc(new PlcInfo { Nombre = "PLC_1", Raiz = raiz }, null);
            Hijo(plc, "Main").IsChecked = true;

            var nueva = new GrupoInfo { Nombre = "Bloques de programa" };
            nueva.Bloques.Add(new BloqueInfo { Nombre = "Main", Tipo = "OB", Numero = 1 });
            nueva.Bloques.Add(new BloqueInfo { Nombre = "FB_Nuevo", Tipo = "FB", Numero = 11 });
            plc.ReemplazarContenido(nueva);

            Assert.Equal(new[] { "Main", "FB_Nuevo" }, plc.Hijos.Select(h => h.Nombre));
            Assert.Equal(new[] { "Main" }, plc.HojasMarcadas().Select(h => h.Nombre));
            Assert.Null(plc.IsChecked);
        }

        [Fact]
        public void Una_hmi_sin_leer_queda_leida_al_traer_sus_ramas()
        {
            var hmi = NodoArbol.SinLeer(new DispositivoInfo { Nombre = "HMI_3", EsHmi = true }, null);
            Assert.Equal(TipoNodo.Hmi, hmi.Tipo);
            Assert.False(hmi.Leido);
            Assert.NotNull(hmi.Detalle);
            Assert.Empty(hmi.Hijos);

            hmi.ReemplazarRamas(ArchivosHmi.OrdenArbol, Hmi().Ramas);

            Assert.True(hmi.Leido);
            Assert.Null(hmi.Detalle);
            Assert.True(hmi.IsExpanded);
            Assert.Equal(new RamaHmi?[] { RamaHmi.Pantallas, RamaHmi.Variables, RamaHmi.Ciclos }, hmi.Hijos.Select(h => h.Rama));
        }

        [Fact]
        public void Un_plc_sin_leer_queda_leido_al_traer_sus_bloques()
        {
            var plc = NodoArbol.SinLeer(new DispositivoInfo { Nombre = "PLC_1" }, null);
            var raiz = new GrupoInfo { Nombre = "Bloques de programa" };
            raiz.Bloques.Add(new BloqueInfo { Nombre = "Main", Tipo = "OB", Numero = 1 });

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

            hmi.ReemplazarRamas(new[] { RamaHmi.Pantallas }, Hmi().Ramas);

            var encontrado = destino.Raiz.Buscar(destino);
            Assert.NotNull(encontrado);
            Assert.NotSame(destino, encontrado);
            Assert.Same(hmi, encontrado.Raiz);
            Assert.Equal(new[] { "Traceability" }, encontrado.Grupos);
        }
    }
}
