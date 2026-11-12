using HerramientasSICAR.Logica;
using System;
using System.IO;
using System.Linq;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ArchivosHmiTests : IDisposable
    {
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));

        public ArchivosHmiTests()
        {
            Directory.CreateDirectory(_carpeta);
        }

        public void Dispose()
        {
            Directory.Delete(_carpeta, recursive: true);
        }

        // Imita la cabecera de una exportación de Openness con el elemento indicado.
        private string CrearXml(string rutaRelativa, string elemento)
        {
            string ruta = Path.Combine(_carpeta, rutaRelativa);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"V19\" />\n" +
                $"<{elemento} ID=\"0\"><AttributeList><Name>Ejemplo</Name></AttributeList></{elemento}>\n</Document>");
            return ruta;
        }

        [Fact]
        public void Importa_primero_lo_que_usan_las_pantallas()
        {
            CrearXml("Inicio.xml", "Hmi.Screen.Screen");
            CrearXml("Plantilla.xml", "Hmi.Screen.ScreenTemplate");
            CrearXml("Tabla.xml", "Hmi.Tag.TagTable");
            CrearXml("Lista.xml", "Hmi.TextGraphicList.TextList");
            CrearXml("Rapido.xml", "Hmi.Cycle.Cycle");

            var orden = ArchivosHmi.PrepararCarpeta(_carpeta, out _).Select(a => Path.GetFileName(a.Ruta));

            Assert.Equal(new[] { "Rapido.xml", "Lista.xml", "Tabla.xml", "Plantilla.xml", "Inicio.xml" }, orden);
        }

        [Fact]
        public void En_una_exportacion_cuentan_las_carpetas_que_hay_dentro_de_la_rama()
        {
            // Como lo deja Exportar con varios dispositivos: <HMI>\<rama>\<carpetas>\<elemento>.xml
            CrearXml(@"Panel_1\Pantallas\Estacion 10\Manual\Pinzas.xml", "Hmi.Screen.Screen");
            CrearXml(@"Panel_1\Variables HMI\Tabla_10.xml", "Hmi.Tag.TagTable");

            var archivos = ArchivosHmi.PrepararCarpeta(_carpeta, out _);

            Assert.Equal(new[] { "Estacion 10", "Manual" }, archivos.Single(a => a.Rama == HmiBranch.Screens).Grupos);
            Assert.Empty(archivos.Single(a => a.Rama == HmiBranch.Tags).Grupos);
        }

        [Fact]
        public void Sin_carpeta_de_rama_cuenta_toda_la_ruta()
        {
            CrearXml(@"Estacion 20\Auto.xml", "Hmi.Screen.Screen");

            var archivo = ArchivosHmi.PrepararCarpeta(_carpeta, out _).Single();

            Assert.Equal(new[] { "Estacion 20" }, archivo.Grupos);
        }

        [Fact]
        public void Las_ramas_sin_carpetas_ignoran_la_ruta()
        {
            CrearXml(@"Varios\Rapido.xml", "Hmi.Cycle.Cycle");

            Assert.Empty(ArchivosHmi.PrepararCarpeta(_carpeta, out _).Single().Grupos);
        }

        [Fact]
        public void Los_xml_que_no_son_de_hmi_se_apartan()
        {
            CrearXml("Inicio.xml", "Hmi.Screen.Screen");
            string bloque = CrearXml("Motor.xml", "SW.Blocks.FB");

            var archivos = ArchivosHmi.PrepararArchivos(Directory.GetFiles(_carpeta), out var descartados);

            Assert.Single(archivos);
            Assert.Equal(new[] { bloque }, descartados);
        }

        [Fact]
        public void Si_lo_seleccionado_es_de_su_rama_cuelga_de_ahi()
        {
            var pantalla = new ArchivoHmi("Auto.xml", HmiBranch.Screens, new[] { "Manual" });

            var carpetas = ArchivosHmi.CarpetasDestino(HmiBranch.Screens, new[] { "Estacion 30" }, pantalla);

            Assert.Equal(new[] { "Estacion 30", "Manual" }, carpetas);
        }

        [Fact]
        public void Si_lo_seleccionado_es_de_otra_rama_va_a_la_raiz_de_la_suya()
        {
            var tabla = new ArchivoHmi("Tabla.xml", HmiBranch.Tags, new string[0]);

            Assert.Empty(ArchivosHmi.CarpetasDestino(HmiBranch.Screens, new[] { "Estacion 30" }, tabla));
            Assert.Empty(ArchivosHmi.CarpetasDestino(null, new string[0], tabla));
        }
    }
}
