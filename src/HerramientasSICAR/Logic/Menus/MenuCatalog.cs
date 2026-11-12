using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.Logic.Menus
{
    /// <summary>Grupo del menú de la HMI SICAR, con su nombre en la estructura Blanking del IDB.</summary>
    public sealed class MenuGroup
    {
        public MenuGroup(int id, string name, string comment, string plcName)
        {
            Id = id;
            Name = name;
            CommentText = comment;
            PlcName = plcName;
        }

        public int Id { get; }

        /// <summary>Nombre que se ve en la herramienta (el de la plantilla de GESTAMP).</summary>
        public string Name { get; }

        /// <summary>Nombre en los comentarios del FC.</summary>
        public string CommentText { get; }

        public string PlcName { get; }
    }

    /// <summary>
    /// Lista de textos de la HMI con nombres de pantallas. Las entradas van de la 1 a <see cref="Entradas"/> y la 0
    /// es el texto estándar con «xx»; una entrada sin nombre lleva el texto estándar con su número.
    /// </summary>
    public sealed class MenuList
    {
        public MenuList(string name, int entries, string standardEs, string standardEn, string standardDe)
        {
            Name = name;
            Entries = entries;
            Standard = new[] { standardEs, standardEn, standardDe };
        }

        public string Name { get; }
        public int Entries { get; }

        /// <summary>Texto estándar en es-ES, en-US y de-DE, con «xx» donde va el número.</summary>
        public IReadOnlyList<string> Standard { get; }

        public string StandardText(int language, int number) =>
            number == 0 ? Standard[language] : Standard[language].Replace("xx", number.ToString("D2"));
    }

    /// <summary>
    /// Subgrupo del menú: un array de ocultamiento del IDB (si <see cref="Posiciones"/> &gt; 0) y los nombres
    /// de sus pantallas en una lista de textos (si <see cref="Nombres"/> &gt; 0).
    /// </summary>
    public sealed class MenuSubgroup
    {
        public MenuSubgroup(MenuGroup group, int id, string name, string comment, string plcName,
            int positions, int names, MenuList list = null, int offset = 0)
        {
            Group = group;
            Id = id;
            Name = name;
            CommentText = comment;
            PlcName = plcName;
            Positions = positions;
            Names = names;
            TargetList = list;
            Offset = offset;
        }

        public MenuGroup Group { get; }
        public int Id { get; }
        public string Name { get; }
        public string CommentText { get; }
        public string PlcName { get; }

        /// <summary>Posiciones del array que escribe el FC (0: el FC no lo toca).</summary>
        public int Positions { get; }

        /// <summary>Pantallas con nombre editable (0: no tiene lista de textos).</summary>
        public int Names { get; }

        public MenuList TargetList { get; }

        /// <summary>Valor de la lista que corresponde a la posición 0 del subgrupo (la posición N es Desplazamiento + N).</summary>
        public int Offset { get; }

        /// <summary>Identifica el subgrupo en el archivo de proyecto, p. ej. «Installation.LayoutGeneral».</summary>
        public string Key => Group.PlcName + "." + PlcName;

        /// <summary>Cuántas filas tiene en la herramienta.</summary>
        public int Rows => System.Math.Max(Positions, Names);
    }

    /// <summary>
    /// Pantallas de Production data de un tipo (contadores, tiempos de ciclo…): cada posición del menú es un dataset,
    /// con su lista SO_ProdData_&lt;Clave&gt;_Dataset_NN de <see cref="Filas"/> textos.
    /// </summary>
    public sealed class ProductionDataKind
    {
        public ProductionDataKind(string key, MenuSubgroup subgroup, int rows, string standardEs, string standardEn, string standardDe)
        {
            Key = key;
            Subgroup = subgroup;
            Rows = rows;
            Standard = new[] { standardEs, standardEn, standardDe };
        }

        public string Key { get; }
        public MenuSubgroup Subgroup { get; }
        public int Rows { get; }

        /// <summary>Texto estándar en es-ES, en-US y de-DE, con «xx» donde va la fila y «DD» donde va el dataset.</summary>
        public IReadOnlyList<string> Standard { get; }

        public string ListName(int dataset) => $"SO_ProdData_{Key}_Dataset_{dataset:D2}";

        public string StandardText(int language, int dataset, int row) =>
            Standard[language].Replace("DD", dataset.ToString("D2")).Replace("xx", row == 0 ? "xx" : row.ToString("D2"));
    }

    /// <summary>
    /// Menús de la HMI SICAR@GST que configura el Generador HMI: los que tienen ocultamiento en el FC
    /// CallHMIMenusConfig_FC y las listas de textos con los nombres de sus pantallas.
    /// </summary>
    public static class MenuCatalog
    {
        public const int MaxHmis = 8;
        public const int OemGroups = 8;
        public const int FunctionScreens = 24;
        public const int FunctionsPerScreen = 8;

        public static readonly MenuGroup Oem = new MenuGroup(1, "OEM", "OEM", "OEM");
        public static readonly MenuGroup Installation = new MenuGroup(2, "Installation", "Installation", "Installation");
        public static readonly MenuGroup Equipment = new MenuGroup(3, "Equipment", "Equipments", "Equipments");
        public static readonly MenuGroup ManualOperations = new MenuGroup(4, "Manual operations", "Manual operation", "ManualOperation");
        public static readonly MenuGroup HardwareDiagnostics = new MenuGroup(6, "Hardware diagnostics", "Hardware diagnostics", "HardwareDiagnostics");
        public static readonly MenuGroup ProductionData = new MenuGroup(7, "Production data", "Production data", "ProductionData");
        public static readonly MenuGroup SystemGroup = new MenuGroup(8, "System", "System", "System");

        public static readonly MenuList OemList = new MenuList("CO_OEM_Screens_ID", 88, "<<OEM screen xx>>", "<<OEM screen xx>>", "<<OEM screen xx>>");
        public static readonly MenuList GeneralList = new MenuList("CO_Layout_General_ID", 8, "<<Layout nivel general xx>>", "<<Layout general level xx>>", "<<Layout Übersichtsebene xx>>");
        public static readonly MenuList DetailList = new MenuList("CO_Layout_Detailed_ID", 64, "<<Layout nivel detalle xx>>", "<<Layout detailed level xx>>", "<<Layout Detailebene xx>>");
        public static readonly MenuList FixtureList = new MenuList("CO_Layout_Fixture_ID", 32, "<<Fixture xx>>", "<<Fixture xx>>", "<<Fixture xx>>");
        public static readonly MenuList TraceabilityList = new MenuList("CO_Traceability_ID", 24, "<<Trazabilidad xx>>", "<<Traceability xx>>", "<<Nachvollziehbarkeit xx>>");
        public static readonly MenuList InterfaceList = new MenuList("CO_Interface_Signals_ID", 32, "<<Señales de interfaz xx>>", "<<Interface signals xx>>", "<<Schnittstellensignale xx>>");
        public static readonly MenuList RobotList = new MenuList("CO_Rob_ID", 24, "<<Robot xx>>", "<<Robot xx>>", "<<Robot xx>>");
        public static readonly MenuList ManualList = new MenuList("CO_Manual_Movements_ID", 64, "<<Movimientos manuales xx>>", "<<Manual movements xx>>", "<< manuelle Bewegungen xx>>");
        public static readonly MenuList FunctionList = new MenuList("CO_UserFunctions_ID", 32, "<<Funciones usuario xx>>", "<<User functions xx>>", "<<Anwenderfunktionen xx>>");
        public static readonly MenuList PokaYokeList = new MenuList("CO_PokaYokeTest_ID", 8, "<<Poka-Yoke xx>>", "<<Poka-Yoke xx>>", "<<Poka-Yoke xx>>");
        public static readonly MenuList DockingList = new MenuList("CO_DockingStation_ID", 8, "<<Est. de acoplamiento xx>>", "<<Docking station xx>>", "<<Docking-Station xx>>");
        public static readonly MenuList ProfinetList = new MenuList("CO_ProfinetDiagnose_ID", 8, "<<PROFINET Diagnóstico xx>>", "<<PROFINET Diagnostic xx>>", "<<PROFINET Diagnose xx>>");
        public static readonly MenuList WebServerList = new MenuList("CO_WebServer_Device_ID", 8, "<<Servidor WEB equipo xx>>", "<<WEB Server Device xx>>", "<<WEB-Server Gerät xx>>");

        /// <summary>Lista con los textos de las funciones de usuario (no es de nombres de pantallas).</summary>
        public const string FunctionTextsList = "CO_UserFunctions";

        public static readonly MenuSubgroup UserFunctions;
        public static readonly MenuSubgroup Profinet;
        public static readonly MenuSubgroup Energy;

        /// <summary>Datasets por tipo de Production data (posiciones 1 a 8 de cada subgrupo).</summary>
        public const int ProductionDatasets = 8;

        /// <summary>Tipos de Production data, en el orden del menú.</summary>
        public static readonly IReadOnlyList<ProductionDataKind> ProductionDataKinds;

        /// <summary>Subgrupos en el orden del FC y de la herramienta.</summary>
        public static readonly IReadOnlyList<MenuSubgroup> Subgroups;

        /// <summary>Listas de nombres de pantallas, en el orden en que se generan.</summary>
        public static readonly IReadOnlyList<MenuList> Lists = new[]
        {
            OemList, GeneralList, DetailList, FixtureList, TraceabilityList, InterfaceList, RobotList,
            ManualList, FunctionList, PokaYokeList, DockingList, ProfinetList, WebServerList
        };

        static MenuCatalog()
        {
            var list = new List<MenuSubgroup>();
            for (int n = 1; n <= OemGroups; n++)
                list.Add(new MenuSubgroup(Oem, n, "OEM" + n, $"OEM_{n:D2}", $"OEM_{n:D2}", 8, 8, OemList, n * 10));

            list.Add(new MenuSubgroup(Installation, 2, "General level layout", "Layout general", "LayoutGeneral", 8, 8, GeneralList));
            list.Add(new MenuSubgroup(Installation, 3, "Detailed level 1 layout", "Layout detailed 1", "LayoutDetailed1", 32, 32, DetailList));
            list.Add(new MenuSubgroup(Installation, 4, "Detailed level 2 layout", "Layout detailed 2", "LayoutDetailed2", 32, 32, DetailList, 32));
            list.Add(new MenuSubgroup(Installation, 5, "Fixture level layout", "Layout fixture", "LayoutFixture", 32, 32, FixtureList));
            list.Add(new MenuSubgroup(Installation, 6, "Traceability", "Traceability", "Traceability", 24, 24, TraceabilityList));
            list.Add(new MenuSubgroup(Installation, 8, "Interface signals", "Interface signals", "InterfaceSignals", 32, 32, InterfaceList));

            list.Add(new MenuSubgroup(Equipment, 1, "Robots", "Robots", "Robots", 32, 24, RobotList));
            list.Add(new MenuSubgroup(Equipment, 2, "Drives", "Drives", "Drives", 32, 0));
            list.Add(new MenuSubgroup(Equipment, 3, "Welding", "Welding", "Welding", 32, 0));

            list.Add(new MenuSubgroup(ManualOperations, 1, "Manual movement 1", "Manual Movements 1", "ManualMovements1", 32, 32, ManualList));
            list.Add(new MenuSubgroup(ManualOperations, 2, "Manual movement 2", "Manual Movements 2", "ManualMovements2", 32, 32, ManualList, 32));
            UserFunctions = new MenuSubgroup(ManualOperations, 3, "User functions", "User functions", "UserFunctions", FunctionScreens, FunctionScreens, FunctionList);
            list.Add(UserFunctions);
            list.Add(new MenuSubgroup(ManualOperations, 4, "Poka-Yoke test", "Poka-Yoke test", "PokaYokeTest", 8, 8, PokaYokeList));
            list.Add(new MenuSubgroup(ManualOperations, 5, "Docking stations", "Docking stations", "DockingStations", 8, 8, DockingList));

            Profinet = new MenuSubgroup(HardwareDiagnostics, 1, "Profinet diagnostics", "Profinet diagnostic", "ProfinetDiagnostic", 8, 8, ProfinetList);
            list.Add(Profinet);
            list.Add(new MenuSubgroup(HardwareDiagnostics, 3, "Web server", "Web server", "WebServer", 0, 8, WebServerList));

            var production = new[]
            {
                new MenuSubgroup(ProductionData, 1, "Process counters", "Process counters", "ProcessCounters", ProductionDatasets, 0),
                new MenuSubgroup(ProductionData, 2, "Shift counters", "Shift counters", "ShiftCounters", ProductionDatasets, 0),
                new MenuSubgroup(ProductionData, 3, "Cycle times", "Cycle times", "CycleTimes", ProductionDatasets, 0),
                new MenuSubgroup(ProductionData, 4, "Limit values", "Limit values", "LimitValues", ProductionDatasets, 0),
            };
            list.AddRange(production);
            ProductionDataKinds = new[]
            {
                new ProductionDataKind("ProcessCounter", production[0], 20,
                    "<<Contadores, imagen xx-datos DD>>", "<<Process counters, screen xx-dataset DD>>", "<<Zähler, Bild xx-Datensatz DD>>"),
                new ProductionDataKind("ShiftCounter", production[1], 10,
                    "<<Contadores de turnos, imagen xx-datos DD>>", "<<Shiftcounters, screen xx-dataset DD>>", "<<Schichtzähler, Bild xx-Datensatz DD>>"),
                new ProductionDataKind("CycleTime", production[2], 20,
                    "<<Tiempos de ciclo, imagen xx-datos DD>>", "<<Cycle times, screen xx-dataset DD>>", "<<Taktzeit, Bild xx-Datensatz DD>>"),
                new ProductionDataKind("LimitValue", production[3], 10,
                    "<<Valores límite, imagen xx-datos DD>>", "<<Limit values, screen xx-dataset DD>>", "<<Grenzwert, Bild xx-Datensatz DD>>"),
            };

            Energy = new MenuSubgroup(SystemGroup, 5, "Energy measurement", "Energy", "EnergyMeasurement", 7, 0);
            list.Add(Energy);

            Subgroups = list;
        }

        public static MenuSubgroup Find(string key) => Subgroups.FirstOrDefault(s => s.Key == key);

        /// <summary>
        /// Si una posición se ve en un HMI nuevo: todas, salvo la 8 de Profinet y la 2 a la 7 de energía, que el FC
        /// de la plantilla ocultaba siempre.
        /// </summary>
        public static bool VisibleByDefault(MenuSubgroup subgroup, int position)
        {
            if (subgroup == Profinet)
                return position != 8;
            if (subgroup == Energy)
                return position == 1;
            return true;
        }
    }
}
