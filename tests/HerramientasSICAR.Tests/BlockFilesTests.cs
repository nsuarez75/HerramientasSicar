using HerramientasSICAR.Logic;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class BlockFilesTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));

        public BlockFilesTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        // Imita la cabecera de una exportación de Openness con el bloque indicado.
        private string CreateXml(string relativePath, string item)
        {
            string path = Path.Combine(_folder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"V19\" />\n" +
                "<DocumentInfo><Created>2026-01-01T00:00:00Z</Created></DocumentInfo>\n" +
                $"<{item} ID=\"0\"><AttributeList><Name>Ejemplo</Name></AttributeList></{item}>\n</Document>");
            return path;
        }

        [Theory]
        [InlineData("SW.Blocks.FB", XmlBlockKind.FB)]
        [InlineData("SW.Blocks.FC", XmlBlockKind.FC)]
        [InlineData("SW.Blocks.OB", XmlBlockKind.OB)]
        [InlineData("SW.Blocks.GlobalDB", XmlBlockKind.GlobalDB)]
        [InlineData("SW.Blocks.InstanceDB", XmlBlockKind.InstanceDB)]
        [InlineData("SW.Blocks.ArrayDB", XmlBlockKind.ArrayDB)]
        [InlineData("SW.Types.PlcStruct", XmlBlockKind.Unknown)]
        public void DetectKind_BlockXml_ReturnsKind(string item, XmlBlockKind expected)
        {
            Assert.Equal(expected, BlockFiles.DetectKind(CreateXml("bloque.xml", item)));
        }

        [Fact]
        public void DetectKind_NotXml_ReturnsUnknown()
        {
            string path = Path.Combine(_folder, "roto.xml");
            File.WriteAllText(path, "esto no es xml");
            Assert.Equal(XmlBlockKind.Unknown, BlockFiles.DetectKind(path));
        }

        [Fact]
        public void PrepareFolder_Blocks_SortsFunctionsDbsThenObs()
        {
            CreateXml("Main.xml", "SW.Blocks.OB");
            CreateXml("Motor_DB.xml", "SW.Blocks.InstanceDB");
            CreateXml("Motor.xml", "SW.Blocks.FB");
            CreateXml("Datos.xml", "SW.Blocks.GlobalDB");
            CreateXml("Escalar.xml", "SW.Blocks.FC");

            var order = BlockFiles.PrepareFolder(_folder, out _).Select(a => Path.GetFileName(a.FilePath));

            Assert.Equal(new[] { "Escalar.xml", "Motor.xml", "Datos.xml", "Motor_DB.xml", "Main.xml" }, order);
        }

        [Fact]
        public void PrepareFolder_Subfolders_BecomeGroups()
        {
            CreateXml(@"Estacion 10\Secuencias\Sec_10.xml", "SW.Blocks.FB");
            CreateXml("Suelto.xml", "SW.Blocks.FC");

            var files = BlockFiles.PrepareFolder(_folder, out _);

            Assert.Equal(new[] { "Estacion 10", "Secuencias" }, files.Single(a => a.FilePath.EndsWith("Sec_10.xml")).Groups);
            Assert.Empty(files.Single(a => a.FilePath.EndsWith("Suelto.xml")).Groups);
        }

        [Fact]
        public void PrepareFolder_NonBlockXml_Discards()
        {
            CreateXml("Bloque.xml", "SW.Blocks.FB");
            string tipo = CreateXml("Tipo.xml", "SW.Types.PlcStruct");

            var files = BlockFiles.PrepareFiles(Directory.GetFiles(_folder), out var discarded);

            Assert.Single(files);
            Assert.Equal(new[] { tipo }, discarded);
        }

        [Fact]
        public void PrepareFiles_LooseFiles_NoGroups()
        {
            string path = CreateXml(@"Sub\Bloque.xml", "SW.Blocks.FB");

            var file = BlockFiles.PrepareFiles(new[] { path }, out _).Single();

            Assert.Empty(file.Groups);
        }
    }
}
