using HerramientasSICAR.Logica;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ArchivosBloqueTests : IDisposable
    {
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));

        public ArchivosBloqueTests()
        {
            Directory.CreateDirectory(_carpeta);
        }

        public void Dispose()
        {
            Directory.Delete(_carpeta, recursive: true);
        }

        // Imita la cabecera de una exportación de Openness con el bloque indicado.
        private string CrearXml(string rutaRelativa, string elemento)
        {
            string ruta = Path.Combine(_carpeta, rutaRelativa);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"V19\" />\n" +
                "<DocumentInfo><Created>2026-01-01T00:00:00Z</Created></DocumentInfo>\n" +
                $"<{elemento} ID=\"0\"><AttributeList><Name>Ejemplo</Name></AttributeList></{elemento}>\n</Document>");
            return ruta;
        }

        [Theory]
        [InlineData("SW.Blocks.FB", TipoBloqueXml.FB)]
        [InlineData("SW.Blocks.FC", TipoBloqueXml.FC)]
        [InlineData("SW.Blocks.OB", TipoBloqueXml.OB)]
        [InlineData("SW.Blocks.GlobalDB", TipoBloqueXml.GlobalDB)]
        [InlineData("SW.Blocks.InstanceDB", TipoBloqueXml.InstanceDB)]
        [InlineData("SW.Blocks.ArrayDB", TipoBloqueXml.ArrayDB)]
        [InlineData("SW.Types.PlcStruct", TipoBloqueXml.Desconocido)]
        public void Detecta_el_tipo_de_bloque_del_xml(string elemento, TipoBloqueXml esperado)
        {
            Assert.Equal(esperado, ArchivosBloque.DetectarTipo(CrearXml("bloque.xml", elemento)));
        }

        [Fact]
        public void Un_archivo_que_no_es_xml_se_considera_desconocido()
        {
            string ruta = Path.Combine(_carpeta, "roto.xml");
            File.WriteAllText(ruta, "esto no es xml");
            Assert.Equal(TipoBloqueXml.Desconocido, ArchivosBloque.DetectarTipo(ruta));
        }

        [Fact]
        public void Importa_primero_funciones_luego_DB_y_al_final_OB()
        {
            CrearXml("Main.xml", "SW.Blocks.OB");
            CrearXml("Motor_DB.xml", "SW.Blocks.InstanceDB");
            CrearXml("Motor.xml", "SW.Blocks.FB");
            CrearXml("Datos.xml", "SW.Blocks.GlobalDB");
            CrearXml("Escalar.xml", "SW.Blocks.FC");

            var orden = ArchivosBloque.PrepararCarpeta(_carpeta, out _).Select(a => Path.GetFileName(a.Ruta));

            Assert.Equal(new[] { "Escalar.xml", "Motor.xml", "Datos.xml", "Motor_DB.xml", "Main.xml" }, orden);
        }

        [Fact]
        public void Las_subcarpetas_se_convierten_en_grupos()
        {
            CrearXml(@"Estacion 10\Secuencias\Sec_10.xml", "SW.Blocks.FB");
            CrearXml("Suelto.xml", "SW.Blocks.FC");

            var archivos = ArchivosBloque.PrepararCarpeta(_carpeta, out _);

            Assert.Equal(new[] { "Estacion 10", "Secuencias" }, archivos.Single(a => a.Ruta.EndsWith("Sec_10.xml")).Grupos);
            Assert.Empty(archivos.Single(a => a.Ruta.EndsWith("Suelto.xml")).Grupos);
        }

        [Fact]
        public void Los_xml_que_no_son_bloques_se_apartan()
        {
            CrearXml("Bloque.xml", "SW.Blocks.FB");
            string tipo = CrearXml("Tipo.xml", "SW.Types.PlcStruct");

            var archivos = ArchivosBloque.PrepararArchivos(Directory.GetFiles(_carpeta), out var descartados);

            Assert.Single(archivos);
            Assert.Equal(new[] { tipo }, descartados);
        }

        [Fact]
        public void Archivos_sueltos_van_directos_al_grupo_destino()
        {
            string ruta = CrearXml(@"Sub\Bloque.xml", "SW.Blocks.FB");

            var archivo = ArchivosBloque.PrepararArchivos(new[] { ruta }, out _).Single();

            Assert.Empty(archivo.Grupos);
        }

        [Theory]
        [InlineData("+A1+FB_Pinza", "+A1+FB_Pinza")]
        [InlineData("Datos/Motor", "Datos_Motor")]
        [InlineData("A:B*C?", "A_B_C_")]
        [InlineData("  ", "_")]
        public void Nombre_seguro_para_windows(string nombre, string esperado)
        {
            Assert.Equal(esperado, ArchivosBloque.NombreSeguro(nombre));
        }
    }
}
