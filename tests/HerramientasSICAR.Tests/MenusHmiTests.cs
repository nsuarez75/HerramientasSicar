using HerramientasSICAR.Logica.MenusHmi;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class MenusHmiTests
    {
        private static readonly XNamespace Stl = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5";
        private static SubgrupoMenu Sub(string clave) => CatalogoMenus.Buscar(clave);

        [Fact]
        public void Un_proyecto_nuevo_tiene_una_HMI_con_todo_visible_salvo_Profinet_8_y_energia_2_a_7()
        {
            var proyecto = ProyectoMenus.Nuevo();

            var hmi = Assert.Single(proyecto.Hmis);
            Assert.Equal(1, hmi.Numero);
            Assert.Equal("HMIMenusConfig01_IDB", hmi.NombreIdb);
            Assert.True(hmi.Visible(Sub("Installation.LayoutDetailed1"), 32));
            Assert.True(hmi.Visible(CatalogoMenus.Profinet, 7));
            Assert.False(hmi.Visible(CatalogoMenus.Profinet, 8));
            Assert.True(hmi.Visible(CatalogoMenus.Energia, 1));
            Assert.All(Enumerable.Range(2, 6), p => Assert.False(hmi.Visible(CatalogoMenus.Energia, p)));
        }

        [Fact]
        public void Se_anaden_HMI_con_el_primer_numero_libre_hasta_8()
        {
            var proyecto = ProyectoMenus.Nuevo();
            for (int i = 0; i < 7; i++)
                Assert.NotNull(proyecto.AnadirHmi());
            Assert.Null(proyecto.AnadirHmi());

            proyecto.Hmis.RemoveAll(h => h.Numero == 3);
            Assert.Equal(3, proyecto.AnadirHmi().Numero);
            Assert.Equal(Enumerable.Range(1, 8), proyecto.Hmis.Select(h => h.Numero));
        }

        [Fact]
        public void El_proyecto_se_guarda_y_se_abre_igual()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.Hmis[0].Nombre = "Panel A";
            proyecto.Hmis[0].Visibles["Equipments.Robots"][4] = false;
            proyecto.Nombres["Equipments.Robots"][4] = new TextoHmi { Texto = "Robot ñ", Ingles = "Robot n" };
            proyecto.TitulosOem[1] = TextoHmi.De("Título");
            proyecto.FuncionesUsuario[2][3].FeedbackDerecha = TextoHmi.De("Activo");

            var leido = ProyectoMenus.DeJson(proyecto.AJson());

            Assert.Equal("Panel A", leido.Hmis[0].Nombre);
            Assert.False(leido.Hmis[0].Visible(Sub("Equipments.Robots"), 5));
            Assert.Equal("Robot n", leido.Nombre(Sub("Equipments.Robots"), 5).EnIdioma(1));
            Assert.Equal("Robot ñ", leido.Nombre(Sub("Equipments.Robots"), 5).EnIdioma(2));
            Assert.Equal("Título", leido.TitulosOem[1].Texto);
            Assert.Equal("Activo", leido.FuncionesUsuario[2][3].FeedbackDerecha.Texto);
            Assert.DoesNotContain("Vacio", proyecto.AJson());
        }

        [Fact]
        public void Un_archivo_incompleto_se_completa_al_abrir()
        {
            var leido = ProyectoMenus.DeJson("{\"Hmis\":[{\"Nombre\":\"X\",\"Numero\":2}]}");

            Assert.Equal(8, leido.TitulosOem.Count);
            Assert.Equal(24, leido.FuncionesUsuario.Count);
            Assert.All(leido.FuncionesUsuario, p => Assert.Equal(8, p.Count));
            Assert.Equal(32, leido.Nombres["Installation.LayoutFixture"].Count);
            Assert.Equal(7, leido.Hmis[0].Visibles["System.EnergyMeasurement"].Count);
        }

        [Fact]
        public void Las_listas_llevan_todas_las_entradas_con_el_texto_estandar_si_no_hay_nombre()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.Nombres["Installation.LayoutGeneral"][0] = TextoHmi.De("PLC01");
            proyecto.Hmis[0].Visibles["Installation.LayoutGeneral"][0] = false;   // la visibilidad no cambia las listas

            var entradas = GeneradorMenus.Entradas(proyecto, CatalogoMenus.ListaGeneral);

            Assert.Equal(Enumerable.Range(0, 9), entradas.Select(e => e.Valor));
            Assert.Equal(new[] { "<<Layout nivel general xx>>", "<<Layout general level xx>>", "<<Layout Übersichtsebene xx>>" }, entradas[0].Textos);
            Assert.Equal(new[] { "PLC01", "PLC01", "PLC01" }, entradas[1].Textos);
            Assert.Equal(new[] { "<<Layout nivel general 07>>", "<<Layout general level 07>>", "<<Layout Übersichtsebene 07>>" }, entradas[7].Textos);
        }

        [Fact]
        public void Detalle_2_y_manual_2_van_de_la_33_a_la_64_de_su_lista()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.Nombres["Installation.LayoutDetailed2"][0] = TextoHmi.De("D2-1");
            proyecto.Nombres["ManualOperation.ManualMovements2"][31] = TextoHmi.De("M2-32");

            Assert.Equal("D2-1", GeneradorMenus.Entradas(proyecto, CatalogoMenus.ListaDetalle).Single(e => e.Valor == 33).Textos[0]);
            Assert.Equal("M2-32", GeneradorMenus.Entradas(proyecto, CatalogoMenus.ListaManual).Single(e => e.Valor == 64).Textos[0]);
        }

        [Fact]
        public void Los_robots_tienen_24_nombres_y_32_posiciones_en_el_FC()
        {
            Assert.Equal(25, GeneradorMenus.Entradas(ProyectoMenus.Nuevo(), CatalogoMenus.ListaRobots).Count);
            Assert.Equal(32, Sub("Equipments.Robots").Posiciones);
        }

        [Fact]
        public void La_lista_OEM_lleva_el_titulo_de_cada_grupo_en_las_decenas()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.TitulosOem[0] = TextoHmi.De("Lectores");
            proyecto.Nombres["OEM.OEM_01"][1] = TextoHmi.De("Lector 2");

            var entradas = GeneradorMenus.Entradas(proyecto, CatalogoMenus.ListaOem);

            Assert.Equal(73, entradas.Count);
            Assert.Equal("Lectores", entradas.Single(e => e.Valor == 10).Textos[1]);
            Assert.Equal("Lector 2", entradas.Single(e => e.Valor == 12).Textos[0]);
            Assert.Equal("<<OEM 01 screen 03>>", entradas.Single(e => e.Valor == 13).Textos[2]);
            Assert.Equal("OEM_02", entradas.Single(e => e.Valor == 20).Textos[0]);
            Assert.Equal(88, entradas.Last().Valor);
        }

        [Fact]
        public void Una_funcion_vacia_lleva_los_textos_estandar_y_una_a_medias_deja_vacio_lo_que_falta()
        {
            var proyecto = ProyectoMenus.Nuevo();
            var funcion = proyecto.FuncionesUsuario[0][0];
            funcion.Nombre = TextoHmi.De("Desactivar");
            funcion.FeedbackIzquierda = TextoHmi.De("Activado");

            var entradas = GeneradorMenus.EntradasFunciones(proyecto).ToDictionary(e => e.Valor, e => e.Textos[0]);

            Assert.Equal(1 + 24 * 8 * 5, entradas.Count);
            Assert.Equal("<<No text>>", entradas[0]);
            Assert.Equal("Desactivar", entradas[111]);
            Assert.Equal("", entradas[112]);
            Assert.Equal("", entradas[113]);
            Assert.Equal("Activado", entradas[114]);
            Assert.Equal("", entradas[115]);
            Assert.Equal("<<S1 L2 Name>>", entradas[121]);
            Assert.Equal("<<S24 L8 Feed back right>>", entradas[2485]);
        }

        [Fact]
        public void El_XML_de_una_lista_tiene_el_formato_de_TIA()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.Nombres["ManualOperation.PokaYokeTest"][0] = TextoHmi.De("A & <B>");

            var doc = XDocument.Parse(GeneradorMenus.ListasTextos(proyecto)[CatalogoMenus.ListaPokaYoke.Nombre]);

            var lista = doc.Root.Element("Hmi.TextGraphicList.TextList");
            Assert.Equal("CO_PokaYokeTest_ID", lista.Element("AttributeList").Element("Name").Value);
            var entradas = lista.Element("ObjectList").Elements("Hmi.TextGraphicList.TextListEntry").ToList();
            Assert.Equal(9, entradas.Count);
            var textos = entradas[1].Descendants("MultilingualTextItem").ToList();
            Assert.Equal(new[] { "es-ES", "en-US", "de-DE" }, textos.Select(t => t.Descendants("Culture").Single().Value));
            Assert.Equal("A & <B>", textos[0].Descendants("p").Single().Value);
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void El_AWL_pone_las_visibles_a_alwaysFalse_y_las_ocultas_a_alwaysTrue()
        {
            var proyecto = ProyectoMenus.Nuevo();
            var hmi = proyecto.Hmis[0];
            var general = hmi.Visibles["Installation.LayoutGeneral"];
            for (int i = 0; i < general.Count; i++)
                general[i] = i < 2;

            var lineas = GeneradorMenus.Awl(hmi).Select(l => l.ToString()).ToList();

            int inicio = lineas.IndexOf("// Subgroup 2 (Layout general)");
            Assert.Equal(new[]
            {
                "// Subgroup 2 (Layout general)",
                "// =     HMIMenusConfig0x_IDB.statCfg.Blanking.Installation.LayoutGeneral[y]",
                "",
                "      A     alwaysFalse",
                "      =     HMIMenusConfig01_IDB.statCfg.Blanking.Installation.LayoutGeneral[1]",
                "      =     HMIMenusConfig01_IDB.statCfg.Blanking.Installation.LayoutGeneral[2]",
                "",
                "      A     alwaysTrue",
                "      =     HMIMenusConfig01_IDB.statCfg.Blanking.Installation.LayoutGeneral[3]",
            }, lineas.Skip(inicio).Take(9));
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.HardwareDiagnostics.ProfinetDiagnostic[8]", lineas);
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.System.EnergyMeasurement[7]", lineas);
            Assert.DoesNotContain(lineas, l => l.Contains("WebServer"));
        }

        [Fact]
        public void El_FC_tiene_tres_segmentos_por_HMI_en_orden_de_numero()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.AnadirHmi();
            proyecto.Hmis[1].Numero = 5;
            proyecto.Hmis.Reverse();

            var doc = XDocument.Parse(GeneradorMenus.Fc(proyecto));

            var fc = doc.Root.Element("SW.Blocks.FC");
            Assert.Equal("CallHMIMenusConfig_FC", fc.Element("AttributeList").Element("Name").Value);
            Assert.Equal("1450", fc.Element("AttributeList").Element("Number").Value);
            var segmentos = fc.Element("ObjectList").Elements("SW.Blocks.CompileUnit").ToList();
            Assert.Equal(6, segmentos.Count);
            Assert.Equal(new[] { "HMIMenusConfig01_IDB", "HMIMenusConfig05_IDB" },
                segmentos.Descendants().Where(e => e.Name.LocalName == "Instance").Select(e => e.Elements().Single().Attribute("Name").Value));
            int asignaciones = segmentos[1].Descendants(Stl + "StlToken").Count(t => (string)t.Attribute("Text") == "Assign");
            Assert.Equal(CatalogoMenus.Subgrupos.Sum(s => s.Posiciones), asignaciones);
            var uids = segmentos[1].Descendants(Stl + "StlStatement").Select(s => (string)s.Attribute("UId")).ToList();
            Assert.Equal(uids.Count, uids.Distinct().Count());
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Generar_escribe_el_FC_y_las_listas_en_su_carpeta()
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "GeneradorHmiTests_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var escritos = GeneradorMenus.Generar(ProyectoMenus.Nuevo(), carpeta);

                Assert.True(File.Exists(Path.Combine(carpeta, "CallHMIMenusConfig_FC.xml")));
                // 13 de nombres de pantallas + CO_UserFunctions + 4 tipos × 8 datasets de Production data
                Assert.Equal(46, Directory.GetFiles(Path.Combine(carpeta, "Listas de textos")).Length);
                Assert.Equal(47, escritos.Count);
                Assert.True(File.Exists(Path.Combine(carpeta, "Listas de textos", "SO_ProdData_LimitValue_Dataset_08.xml")));
            }
            finally
            {
                if (Directory.Exists(carpeta))
                    Directory.Delete(carpeta, true);
            }
        }

        [Fact]
        public void Un_dataset_vacio_lleva_los_textos_estandar_y_uno_a_medias_deja_vacio_lo_que_falta()
        {
            var proyecto = ProyectoMenus.Nuevo();
            var ciclos = CatalogoMenus.DatosProduccion.Single(t => t.Clave == "CycleTime");
            var turnos = CatalogoMenus.DatosProduccion.Single(t => t.Clave == "ShiftCounter");
            proyecto.Dataset(ciclos, 1)[0] = TextoHmi.De("Robot A");

            var lleno = GeneradorMenus.EntradasDataset(proyecto, ciclos, 1);
            var vacio = GeneradorMenus.EntradasDataset(proyecto, turnos, 3);

            Assert.Equal(21, lleno.Count);
            Assert.Equal(new[] { "<<Tiempos de ciclo, imagen xx-datos 01>>", "<<Cycle times, screen xx-dataset 01>>", "<<Taktzeit, Bild xx-Datensatz 01>>" }, lleno[0].Textos);
            Assert.Equal("Robot A", lleno[1].Textos[2]);
            Assert.Equal("", lleno[2].Textos[0]);
            Assert.Equal(11, vacio.Count);
            Assert.Equal(new[] { "<<Contadores de turnos, imagen 10-datos 03>>", "<<Shiftcounters, screen 10-dataset 03>>", "<<Schichtzähler, Bild 10-Datensatz 03>>" }, vacio[10].Textos);
        }

        [Fact]
        public void Production_data_va_en_el_FC_con_8_posiciones_por_subgrupo()
        {
            var lineas = GeneradorMenus.Awl(ProyectoMenus.Nuevo().Hmis[0]).Select(l => l.ToString()).ToList();

            Assert.Contains("// Group 7 (Production data)", lineas);
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.ProductionData.LimitValues[8]", lineas);
            Assert.DoesNotContain("      =     HMIMenusConfig01_IDB.statCfg.Blanking.ProductionData.LimitValues[9]", lineas);
            Assert.True(lineas.IndexOf("// Group 7 (Production data)") < lineas.IndexOf("// Group 8 (System)"));
        }

        [Fact]
        public void No_se_genera_con_numeros_de_HMI_repetidos()
        {
            var proyecto = ProyectoMenus.Nuevo();
            proyecto.AnadirHmi();
            proyecto.Hmis[1].Numero = 1;

            Assert.NotEmpty(proyecto.Errores());
            Assert.Throws<System.InvalidOperationException>(() => GeneradorMenus.Generar(proyecto, Path.GetTempPath()));
        }
    }
}
