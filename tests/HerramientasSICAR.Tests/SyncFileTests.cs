using HerramientasSICAR.Logic;
using HerramientasSICAR.Core.Sicar;
using System;
using System.IO;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class SyncFileTests
    {
        [Fact]
        public void InAppData_ProjectAndPlc_UsesSafeNames()
        {
            string path = SyncFile.InAppData("Proyecto", "PLC:1");
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "Sincro");
            Assert.StartsWith(root, path);
            Assert.EndsWith(".json", path);
            Assert.DoesNotContain(":", Path.GetFileName(path));
            Assert.Equal("Proyecto", Path.GetFileName(Path.GetDirectoryName(path)));
        }

        [Fact]
        public void SaveAndLoad_ExclusiveGroups_RoundTrip()
        {
            string folder = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(folder, "sub", "PLC.json");
                var file = new SyncFile { EditingLanguage = "de-DE" };
                file.SetGroups(new[] { new ExclusiveGroup(new[] { "\"A\"", "\"B\"" }) { Note = "nota" } });
                file.Save(path);

                var read = SyncFile.Load(path);
                Assert.Equal("de-DE", read.EditingLanguage);
                var group = Assert.Single(read.Groups());
                Assert.Equal(new[] { "\"A\"", "\"B\"" }, group.Signals);
                Assert.Equal("nota", group.Note);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Load_FileWithSpanishKeys_ReadsGroups()
        {
            string folder = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "PLC.json");
                File.WriteAllText(path, "{\"IdiomaEdicion\":\"es-ES\",\"Excluyentes\":[{\"Senales\":[\"\\\"A\\\"\",\"\\\"B\\\"\"],\"Nota\":\"nota\"}]}");

                var read = SyncFile.Load(path);

                Assert.Equal("es-ES", read.EditingLanguage);
                var group = Assert.Single(read.Groups());
                Assert.Equal(new[] { "\"A\"", "\"B\"" }, group.Signals);
                Assert.Equal("nota", group.Note);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void SaveAndLoad_GroupWithValues_RoundTrip()
        {
            string folder = Path.Combine(Path.GetTempPath(), "HerramientasSICAR.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(folder, "PLC.json");
                var file = new SyncFile();
                file.SetGroups(new[]
                {
                    new ExclusiveGroup(new[] { "\"A\"", "\"B\"" }) { Values = new[] { true, false } },
                    new ExclusiveGroup(new[] { "\"C\"", "\"D\"" }),
                });
                file.Save(path);

                var groups = SyncFile.Load(path).Groups();
                Assert.Equal(new[] { true, false }, groups[0].Values);
                Assert.Null(groups[1].Values);
                // Los grupos con todas las señales a 1 se guardan como antes, sin «Valores».
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "Valores"));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmpty()
        {
            var file = SyncFile.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no.json"));
            Assert.Equal(SyncFile.DefaultLanguage, file.EditingLanguage);
            Assert.Empty(file.Exclusive);
        }
    }
}
