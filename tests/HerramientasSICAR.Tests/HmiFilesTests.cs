using HerramientasSICAR.Logic;
using System;
using System.IO;
using System.Linq;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class HmiFilesTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));

        public HmiFilesTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        // Imita la cabecera de una exportación de Openness con el elemento indicado.
        private string CreateXml(string relativePath, string item)
        {
            string path = Path.Combine(_folder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"V19\" />\n" +
                $"<{item} ID=\"0\"><AttributeList><Name>Ejemplo</Name></AttributeList></{item}>\n</Document>");
            return path;
        }

        [Fact]
        public void PrepareFolder_HmiItems_SortsDependenciesFirst()
        {
            CreateXml("Inicio.xml", "Hmi.Screen.Screen");
            CreateXml("Plantilla.xml", "Hmi.Screen.ScreenTemplate");
            CreateXml("Tabla.xml", "Hmi.Tag.TagTable");
            CreateXml("Lista.xml", "Hmi.TextGraphicList.TextList");
            CreateXml("Rapido.xml", "Hmi.Cycle.Cycle");

            var order = HmiFiles.PrepareFolder(_folder, out _).Select(a => Path.GetFileName(a.FilePath));

            Assert.Equal(new[] { "Rapido.xml", "Lista.xml", "Tabla.xml", "Plantilla.xml", "Inicio.xml" }, order);
        }

        [Fact]
        public void PrepareFolder_ExportLayout_KeepsFoldersInsideBranch()
        {
            // Como lo deja Exportar con varios dispositivos: <HMI>\<rama>\<carpetas>\<elemento>.xml
            CreateXml(@"Panel_1\Pantallas\Estacion 10\Manual\Pinzas.xml", "Hmi.Screen.Screen");
            CreateXml(@"Panel_1\Variables HMI\Tabla_10.xml", "Hmi.Tag.TagTable");

            var files = HmiFiles.PrepareFolder(_folder, out _);

            Assert.Equal(new[] { "Estacion 10", "Manual" }, files.Single(a => a.Branch == HmiBranch.Screens).Groups);
            Assert.Empty(files.Single(a => a.Branch == HmiBranch.Tags).Groups);
        }

        [Fact]
        public void PrepareFolder_NoBranchFolder_KeepsWholePath()
        {
            CreateXml(@"Estacion 20\Auto.xml", "Hmi.Screen.Screen");

            var file = HmiFiles.PrepareFolder(_folder, out _).Single();

            Assert.Equal(new[] { "Estacion 20" }, file.Groups);
        }

        [Fact]
        public void PrepareFolder_BranchWithoutFolders_IgnoresPath()
        {
            CreateXml(@"Varios\Rapido.xml", "Hmi.Cycle.Cycle");

            Assert.Empty(HmiFiles.PrepareFolder(_folder, out _).Single().Groups);
        }

        [Fact]
        public void PrepareFiles_NonHmiXml_Discards()
        {
            CreateXml("Inicio.xml", "Hmi.Screen.Screen");
            string block = CreateXml("Motor.xml", "SW.Blocks.FB");

            var files = HmiFiles.PrepareFiles(Directory.GetFiles(_folder), out var discarded);

            Assert.Single(files);
            Assert.Equal(new[] { block }, discarded);
        }

        [Fact]
        public void TargetFolders_SelectionInSameBranch_NestsUnderIt()
        {
            var screen = new HmiFile("Auto.xml", HmiBranch.Screens, new[] { "Manual" });

            var folders = HmiFiles.TargetFolders(HmiBranch.Screens, new[] { "Estacion 30" }, screen);

            Assert.Equal(new[] { "Estacion 30", "Manual" }, folders);
        }

        [Fact]
        public void TargetFolders_SelectionInOtherBranch_UsesBranchRoot()
        {
            var table = new HmiFile("Tabla.xml", HmiBranch.Tags, new string[0]);

            Assert.Empty(HmiFiles.TargetFolders(HmiBranch.Screens, new[] { "Estacion 30" }, table));
            Assert.Empty(HmiFiles.TargetFolders(null, new string[0], table));
        }
    }
}
