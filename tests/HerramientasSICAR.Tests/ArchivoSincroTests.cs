using HerramientasSICAR.Logica;
using HerramientasSICAR.Nucleo.Sicar;
using System;
using System.IO;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ArchivoSincroTests
    {
        [Fact]
        public void En_AppData_va_por_proyecto_y_PLC_con_nombres_seguros()
        {
            string ruta = ArchivoSincro.EnAppData("Proyecto", "PLC:1");
            string raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "Sincro");
            Assert.StartsWith(raiz, ruta);
            Assert.EndsWith(".json", ruta);
            Assert.DoesNotContain(":", Path.GetFileName(ruta));
            Assert.Equal("Proyecto", Path.GetFileName(Path.GetDirectoryName(ruta)));
        }

        [Fact]
        public void Guardar_crea_la_carpeta_y_Cargar_recupera_las_excluyentes()
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                string ruta = Path.Combine(carpeta, "sub", "PLC.json");
                var archivo = new ArchivoSincro { IdiomaEdicion = "de-DE" };
                archivo.PonerGrupos(new[] { new ExclusiveGroup(new[] { "\"A\"", "\"B\"" }) { Note = "nota" } });
                archivo.Guardar(ruta);

                var leido = ArchivoSincro.Cargar(ruta);
                Assert.Equal("de-DE", leido.IdiomaEdicion);
                var grupo = Assert.Single(leido.Grupos());
                Assert.Equal(new[] { "\"A\"", "\"B\"" }, grupo.Signals);
                Assert.Equal("nota", grupo.Note);
            }
            finally
            {
                if (Directory.Exists(carpeta))
                    Directory.Delete(carpeta, true);
            }
        }

        [Fact]
        public void Sin_archivo_carga_uno_vacio()
        {
            var archivo = ArchivoSincro.Cargar(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no.json"));
            Assert.Equal(ArchivoSincro.IdiomaPorDefecto, archivo.IdiomaEdicion);
            Assert.Empty(archivo.Excluyentes);
        }
    }
}
