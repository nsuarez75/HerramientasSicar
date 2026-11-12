using HerramientasSICAR.Logic.Menus;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class HmiMenusTests
    {
        private static readonly XNamespace Stl = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5";
        private static MenuSubgroup Sub(string key) => MenuCatalog.Find(key);

        [Fact]
        public void New_Project_OneHmiWithDefaultVisibility()
        {
            var project = MenuProject.New();

            var hmi = Assert.Single(project.Hmis);
            Assert.Equal(1, hmi.Number);
            Assert.Equal("HMIMenusConfig01_IDB", hmi.IdbName);
            Assert.True(hmi.Visible(Sub("Installation.LayoutDetailed1"), 32));
            Assert.True(hmi.Visible(MenuCatalog.Profinet, 7));
            Assert.False(hmi.Visible(MenuCatalog.Profinet, 8));
            Assert.True(hmi.Visible(MenuCatalog.Energy, 1));
            Assert.All(Enumerable.Range(2, 6), p => Assert.False(hmi.Visible(MenuCatalog.Energy, p)));
        }

        [Fact]
        public void AddHmi_UpToEight_UsesFirstFreeNumber()
        {
            var project = MenuProject.New();
            for (int i = 0; i < 7; i++)
                Assert.NotNull(project.AddHmi());
            Assert.Null(project.AddHmi());

            project.Hmis.RemoveAll(h => h.Number == 3);
            Assert.Equal(3, project.AddHmi().Number);
            Assert.Equal(Enumerable.Range(1, 8), project.Hmis.Select(h => h.Number));
        }

        [Fact]
        public void ToJson_Project_RoundTrips()
        {
            var project = MenuProject.New();
            project.Hmis[0].Name = "Panel A";
            project.Hmis[0].VisibleScreens["Equipments.Robots"][4] = false;
            project.Names["Equipments.Robots"][4] = new HmiText { Text = "Robot ñ", English = "Robot n" };
            project.OemTitles[1] = HmiText.From("Título");
            project.UserFunctionScreens[2][3].RightFeedback = HmiText.From("Activo");

            var read = MenuProject.FromJson(project.ToJson());

            Assert.Equal("Panel A", read.Hmis[0].Name);
            Assert.False(read.Hmis[0].Visible(Sub("Equipments.Robots"), 5));
            Assert.Equal("Robot n", read.Name(Sub("Equipments.Robots"), 5).InLanguage(1));
            Assert.Equal("Robot ñ", read.Name(Sub("Equipments.Robots"), 5).InLanguage(2));
            Assert.Equal("Título", read.OemTitles[1].Text);
            Assert.Equal("Activo", read.UserFunctionScreens[2][3].RightFeedback.Text);
            Assert.DoesNotContain("Vacio", project.ToJson());
        }

        [Fact]
        public void FromJson_IncompleteFile_CompletesIt()
        {
            var read = MenuProject.FromJson("{\"Hmis\":[{\"Nombre\":\"X\",\"Numero\":2}]}");

            Assert.Equal(8, read.OemTitles.Count);
            Assert.Equal(24, read.UserFunctionScreens.Count);
            Assert.All(read.UserFunctionScreens, p => Assert.Equal(8, p.Count));
            Assert.Equal(32, read.Names["Installation.LayoutFixture"].Count);
            Assert.Equal(7, read.Hmis[0].VisibleScreens["System.EnergyMeasurement"].Count);
        }

        [Fact]
        public void Entries_MissingNames_UsesStandardText()
        {
            var project = MenuProject.New();
            project.Names["Installation.LayoutGeneral"][0] = HmiText.From("PLC01");
            project.Hmis[0].VisibleScreens["Installation.LayoutGeneral"][0] = false;   // la visibilidad no cambia las listas

            var entries = MenuGenerator.Entries(project, MenuCatalog.GeneralList);

            Assert.Equal(Enumerable.Range(0, 9), entries.Select(e => e.Value));
            Assert.Equal(new[] { "<<Layout nivel general xx>>", "<<Layout general level xx>>", "<<Layout Übersichtsebene xx>>" }, entries[0].Texts);
            Assert.Equal(new[] { "PLC01", "PLC01", "PLC01" }, entries[1].Texts);
            Assert.Equal(new[] { "<<Layout nivel general 07>>", "<<Layout general level 07>>", "<<Layout Übersichtsebene 07>>" }, entries[7].Texts);
        }

        [Fact]
        public void Entries_Detailed2AndManual2_UsePositions33To64()
        {
            var project = MenuProject.New();
            project.Names["Installation.LayoutDetailed2"][0] = HmiText.From("D2-1");
            project.Names["ManualOperation.ManualMovements2"][31] = HmiText.From("M2-32");

            Assert.Equal("D2-1", MenuGenerator.Entries(project, MenuCatalog.DetailList).Single(e => e.Value == 33).Texts[0]);
            Assert.Equal("M2-32", MenuGenerator.Entries(project, MenuCatalog.ManualList).Single(e => e.Value == 64).Texts[0]);
        }

        [Fact]
        public void Entries_Robots_24NamesAnd32Positions()
        {
            Assert.Equal(25, MenuGenerator.Entries(MenuProject.New(), MenuCatalog.RobotList).Count);
            Assert.Equal(32, Sub("Equipments.Robots").Positions);
        }

        [Fact]
        public void Entries_OemList_GroupTitlesInTens()
        {
            var project = MenuProject.New();
            project.OemTitles[0] = HmiText.From("Lectores");
            project.Names["OEM.OEM_01"][1] = HmiText.From("Lector 2");

            var entries = MenuGenerator.Entries(project, MenuCatalog.OemList);

            Assert.Equal(73, entries.Count);
            Assert.Equal("Lectores", entries.Single(e => e.Value == 10).Texts[1]);
            Assert.Equal("Lector 2", entries.Single(e => e.Value == 12).Texts[0]);
            Assert.Equal("<<OEM 01 screen 03>>", entries.Single(e => e.Value == 13).Texts[2]);
            Assert.Equal("OEM_02", entries.Single(e => e.Value == 20).Texts[0]);
            Assert.Equal(88, entries.Last().Value);
        }

        [Fact]
        public void Entries_UserFunctions_StandardOrPartialTexts()
        {
            var project = MenuProject.New();
            var function = project.UserFunctionScreens[0][0];
            function.Name = HmiText.From("Desactivar");
            function.LeftFeedback = HmiText.From("Activado");

            var entries = MenuGenerator.FunctionEntries(project).ToDictionary(e => e.Value, e => e.Texts[0]);

            Assert.Equal(1 + 24 * 8 * 5, entries.Count);
            Assert.Equal("<<No text>>", entries[0]);
            Assert.Equal("Desactivar", entries[111]);
            Assert.Equal("", entries[112]);
            Assert.Equal("", entries[113]);
            Assert.Equal("Activado", entries[114]);
            Assert.Equal("", entries[115]);
            Assert.Equal("<<S1 L2 Name>>", entries[121]);
            Assert.Equal("<<S24 L8 Feed back right>>", entries[2485]);
        }

        [Fact]
        public void ListXml_TextList_UsesTiaFormat()
        {
            var project = MenuProject.New();
            project.Names["ManualOperation.PokaYokeTest"][0] = HmiText.From("A & <B>");

            var doc = XDocument.Parse(MenuGenerator.TextLists(project)[MenuCatalog.PokaYokeList.Name]);

            var list = doc.Root.Element("Hmi.TextGraphicList.TextList");
            Assert.Equal("CO_PokaYokeTest_ID", list.Element("AttributeList").Element("Name").Value);
            var entries = list.Element("ObjectList").Elements("Hmi.TextGraphicList.TextListEntry").ToList();
            Assert.Equal(9, entries.Count);
            var texts = entries[1].Descendants("MultilingualTextItem").ToList();
            Assert.Equal(new[] { "es-ES", "en-US", "de-DE" }, texts.Select(t => t.Descendants("Culture").Single().Value));
            Assert.Equal("A & <B>", texts[0].Descendants("p").Single().Value);
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Awl_Visibility_WritesAlwaysFalseOrTrue()
        {
            var project = MenuProject.New();
            var hmi = project.Hmis[0];
            var general = hmi.VisibleScreens["Installation.LayoutGeneral"];
            for (int i = 0; i < general.Count; i++)
                general[i] = i < 2;

            var lines = MenuGenerator.Awl(hmi).Select(l => l.ToString()).ToList();

            int start = lines.IndexOf("// Subgroup 2 (Layout general)");
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
            }, lines.Skip(start).Take(9));
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.HardwareDiagnostics.ProfinetDiagnostic[8]", lines);
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.System.EnergyMeasurement[7]", lines);
            Assert.DoesNotContain(lines, l => l.Contains("WebServer"));
        }

        [Fact]
        public void FcXml_Hmis_ThreeNetworksEachInNumberOrder()
        {
            var project = MenuProject.New();
            project.AddHmi();
            project.Hmis[1].Number = 5;
            project.Hmis.Reverse();

            var doc = XDocument.Parse(MenuGenerator.Fc(project));

            var fc = doc.Root.Element("SW.Blocks.FC");
            Assert.Equal("CallHMIMenusConfig_FC", fc.Element("AttributeList").Element("Name").Value);
            Assert.Equal("1450", fc.Element("AttributeList").Element("Number").Value);
            var networks = fc.Element("ObjectList").Elements("SW.Blocks.CompileUnit").ToList();
            Assert.Equal(6, networks.Count);
            Assert.Equal(new[] { "HMIMenusConfig01_IDB", "HMIMenusConfig05_IDB" },
                networks.Descendants().Where(e => e.Name.LocalName == "Instance").Select(e => e.Elements().Single().Attribute("Name").Value));
            int assignments = networks[1].Descendants(Stl + "StlToken").Count(t => (string)t.Attribute("Text") == "Assign");
            Assert.Equal(MenuCatalog.Subgroups.Sum(s => s.Positions), assignments);
            var uids = networks[1].Descendants(Stl + "StlStatement").Select(s => (string)s.Attribute("UId")).ToList();
            Assert.Equal(uids.Count, uids.Distinct().Count());
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Generate_Project_WritesFcAndLists()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GeneradorHmiTests_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var written = MenuGenerator.Generate(MenuProject.New(), folder);

                Assert.True(File.Exists(Path.Combine(folder, "CallHMIMenusConfig_FC.xml")));
                // 13 de nombres de pantallas + CO_UserFunctions + 4 tipos × 8 datasets de Production data
                Assert.Equal(46, Directory.GetFiles(Path.Combine(folder, "Listas de textos")).Length);
                Assert.Equal(47, written.Count);
                Assert.True(File.Exists(Path.Combine(folder, "Listas de textos", "SO_ProdData_LimitValue_Dataset_08.xml")));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Entries_ProductionDatasets_StandardOrPartialTexts()
        {
            var project = MenuProject.New();
            var cycles = MenuCatalog.ProductionDataKinds.Single(t => t.Key == "CycleTime");
            var shifts = MenuCatalog.ProductionDataKinds.Single(t => t.Key == "ShiftCounter");
            project.Dataset(cycles, 1)[0] = HmiText.From("Robot A");

            var full = MenuGenerator.DatasetEntries(project, cycles, 1);
            var empty = MenuGenerator.DatasetEntries(project, shifts, 3);

            Assert.Equal(21, full.Count);
            Assert.Equal(new[] { "<<Tiempos de ciclo, imagen xx-datos 01>>", "<<Cycle times, screen xx-dataset 01>>", "<<Taktzeit, Bild xx-Datensatz 01>>" }, full[0].Texts);
            Assert.Equal("Robot A", full[1].Texts[2]);
            Assert.Equal("", full[2].Texts[0]);
            Assert.Equal(11, empty.Count);
            Assert.Equal(new[] { "<<Contadores de turnos, imagen 10-datos 03>>", "<<Shiftcounters, screen 10-dataset 03>>", "<<Schichtzähler, Bild 10-Datensatz 03>>" }, empty[10].Texts);
        }

        [Fact]
        public void Awl_ProductionData_EightPositionsPerSubgroup()
        {
            var lines = MenuGenerator.Awl(MenuProject.New().Hmis[0]).Select(l => l.ToString()).ToList();

            Assert.Contains("// Group 7 (Production data)", lines);
            Assert.Contains("      =     HMIMenusConfig01_IDB.statCfg.Blanking.ProductionData.LimitValues[8]", lines);
            Assert.DoesNotContain("      =     HMIMenusConfig01_IDB.statCfg.Blanking.ProductionData.LimitValues[9]", lines);
            Assert.True(lines.IndexOf("// Group 7 (Production data)") < lines.IndexOf("// Group 8 (System)"));
        }

        [Fact]
        public void Errors_RepeatedHmiNumbers_ReturnsError()
        {
            var project = MenuProject.New();
            project.AddHmi();
            project.Hmis[1].Number = 1;

            Assert.NotEmpty(project.Errors());
            Assert.Throws<System.InvalidOperationException>(() => MenuGenerator.Generate(project, Path.GetTempPath()));
        }
    }
}
