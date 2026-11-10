using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.Logica.MenusHmi
{
    /// <summary>Grupo del menú de la HMI SICAR, con su nombre en la estructura Blanking del IDB.</summary>
    public sealed class GrupoMenu
    {
        public GrupoMenu(int id, string nombre, string comentario, string nombrePlc)
        {
            Id = id;
            Nombre = nombre;
            Comentario = comentario;
            NombrePlc = nombrePlc;
        }

        public int Id { get; }

        /// <summary>Nombre que se ve en la herramienta (el de la plantilla de GESTAMP).</summary>
        public string Nombre { get; }

        /// <summary>Nombre en los comentarios del FC.</summary>
        public string Comentario { get; }

        public string NombrePlc { get; }
    }

    /// <summary>
    /// Lista de textos de la HMI con nombres de pantallas. Las entradas van de la 1 a <see cref="Entradas"/> y la 0
    /// es el texto estándar con «xx»; una entrada sin nombre lleva el texto estándar con su número.
    /// </summary>
    public sealed class ListaMenu
    {
        public ListaMenu(string nombre, int entradas, string estandarEs, string estandarEn, string estandarDe)
        {
            Nombre = nombre;
            Entradas = entradas;
            Estandar = new[] { estandarEs, estandarEn, estandarDe };
        }

        public string Nombre { get; }
        public int Entradas { get; }

        /// <summary>Texto estándar en es-ES, en-US y de-DE, con «xx» donde va el número.</summary>
        public IReadOnlyList<string> Estandar { get; }

        public string TextoEstandar(int idioma, int numero) =>
            numero == 0 ? Estandar[idioma] : Estandar[idioma].Replace("xx", numero.ToString("D2"));
    }

    /// <summary>
    /// Subgrupo del menú: un array de ocultamiento del IDB (si <see cref="Posiciones"/> &gt; 0) y los nombres
    /// de sus pantallas en una lista de textos (si <see cref="Nombres"/> &gt; 0).
    /// </summary>
    public sealed class SubgrupoMenu
    {
        public SubgrupoMenu(GrupoMenu grupo, int id, string nombre, string comentario, string nombrePlc,
            int posiciones, int nombres, ListaMenu lista = null, int desplazamiento = 0)
        {
            Grupo = grupo;
            Id = id;
            Nombre = nombre;
            Comentario = comentario;
            NombrePlc = nombrePlc;
            Posiciones = posiciones;
            Nombres = nombres;
            Lista = lista;
            Desplazamiento = desplazamiento;
        }

        public GrupoMenu Grupo { get; }
        public int Id { get; }
        public string Nombre { get; }
        public string Comentario { get; }
        public string NombrePlc { get; }

        /// <summary>Posiciones del array que escribe el FC (0: el FC no lo toca).</summary>
        public int Posiciones { get; }

        /// <summary>Pantallas con nombre editable (0: no tiene lista de textos).</summary>
        public int Nombres { get; }

        public ListaMenu Lista { get; }

        /// <summary>Valor de la lista que corresponde a la posición 0 del subgrupo (la posición N es Desplazamiento + N).</summary>
        public int Desplazamiento { get; }

        /// <summary>Identifica el subgrupo en el archivo de proyecto, p. ej. «Installation.LayoutGeneral».</summary>
        public string Clave => Grupo.NombrePlc + "." + NombrePlc;

        /// <summary>Cuántas filas tiene en la herramienta.</summary>
        public int Filas => System.Math.Max(Posiciones, Nombres);
    }

    /// <summary>
    /// Menús de la HMI SICAR@GST que configura el Generador HMI: los que tienen ocultamiento en el FC
    /// CallHMIMenusConfig_FC y las listas de textos con los nombres de sus pantallas.
    /// </summary>
    public static class CatalogoMenus
    {
        public const int MaxHmis = 8;
        public const int GruposOem = 8;
        public const int PantallasFunciones = 24;
        public const int FuncionesPorPantalla = 8;

        public static readonly GrupoMenu Oem = new GrupoMenu(1, "OEM", "OEM", "OEM");
        public static readonly GrupoMenu Installation = new GrupoMenu(2, "Installation", "Installation", "Installation");
        public static readonly GrupoMenu Equipment = new GrupoMenu(3, "Equipment", "Equipments", "Equipments");
        public static readonly GrupoMenu ManualOperations = new GrupoMenu(4, "Manual operations", "Manual operation", "ManualOperation");
        public static readonly GrupoMenu HardwareDiagnostics = new GrupoMenu(6, "Hardware diagnostics", "Hardware diagnostics", "HardwareDiagnostics");
        public static readonly GrupoMenu Sistema = new GrupoMenu(8, "System", "System", "System");

        public static readonly ListaMenu ListaOem = new ListaMenu("CO_OEM_Screens_ID", 88, "<<OEM screen xx>>", "<<OEM screen xx>>", "<<OEM screen xx>>");
        public static readonly ListaMenu ListaGeneral = new ListaMenu("CO_Layout_General_ID", 8, "<<Layout nivel general xx>>", "<<Layout general level xx>>", "<<Layout Übersichtsebene xx>>");
        public static readonly ListaMenu ListaDetalle = new ListaMenu("CO_Layout_Detailed_ID", 64, "<<Layout nivel detalle xx>>", "<<Layout detailed level xx>>", "<<Layout Detailebene xx>>");
        public static readonly ListaMenu ListaFixture = new ListaMenu("CO_Layout_Fixture_ID", 32, "<<Fixture xx>>", "<<Fixture xx>>", "<<Fixture xx>>");
        public static readonly ListaMenu ListaTrazabilidad = new ListaMenu("CO_Traceability_ID", 24, "<<Trazabilidad xx>>", "<<Traceability xx>>", "<<Nachvollziehbarkeit xx>>");
        public static readonly ListaMenu ListaInterfaz = new ListaMenu("CO_Interface_Signals_ID", 32, "<<Señales de interfaz xx>>", "<<Interface signals xx>>", "<<Schnittstellensignale xx>>");
        public static readonly ListaMenu ListaRobots = new ListaMenu("CO_Rob_ID", 24, "<<Robot xx>>", "<<Robot xx>>", "<<Robot xx>>");
        public static readonly ListaMenu ListaManual = new ListaMenu("CO_Manual_Movements_ID", 64, "<<Movimientos manuales xx>>", "<<Manual movements xx>>", "<< manuelle Bewegungen xx>>");
        public static readonly ListaMenu ListaFunciones = new ListaMenu("CO_UserFunctions_ID", 32, "<<Funciones usuario xx>>", "<<User functions xx>>", "<<Anwenderfunktionen xx>>");
        public static readonly ListaMenu ListaPokaYoke = new ListaMenu("CO_PokaYokeTest_ID", 8, "<<Poka-Yoke xx>>", "<<Poka-Yoke xx>>", "<<Poka-Yoke xx>>");
        public static readonly ListaMenu ListaDocking = new ListaMenu("CO_DockingStation_ID", 8, "<<Est. de acoplamiento xx>>", "<<Docking station xx>>", "<<Docking-Station xx>>");
        public static readonly ListaMenu ListaProfinet = new ListaMenu("CO_ProfinetDiagnose_ID", 8, "<<PROFINET Diagnóstico xx>>", "<<PROFINET Diagnostic xx>>", "<<PROFINET Diagnose xx>>");
        public static readonly ListaMenu ListaWebServer = new ListaMenu("CO_WebServer_Device_ID", 8, "<<Servidor WEB equipo xx>>", "<<WEB Server Device xx>>", "<<WEB-Server Gerät xx>>");

        /// <summary>Lista con los textos de las funciones de usuario (no es de nombres de pantallas).</summary>
        public const string ListaTextosFunciones = "CO_UserFunctions";

        public static readonly SubgrupoMenu UserFunctions;
        public static readonly SubgrupoMenu Profinet;
        public static readonly SubgrupoMenu Energia;

        /// <summary>Subgrupos en el orden del FC y de la herramienta.</summary>
        public static readonly IReadOnlyList<SubgrupoMenu> Subgrupos;

        /// <summary>Listas de nombres de pantallas, en el orden en que se generan.</summary>
        public static readonly IReadOnlyList<ListaMenu> Listas = new[]
        {
            ListaOem, ListaGeneral, ListaDetalle, ListaFixture, ListaTrazabilidad, ListaInterfaz, ListaRobots,
            ListaManual, ListaFunciones, ListaPokaYoke, ListaDocking, ListaProfinet, ListaWebServer
        };

        static CatalogoMenus()
        {
            var lista = new List<SubgrupoMenu>();
            for (int n = 1; n <= GruposOem; n++)
                lista.Add(new SubgrupoMenu(Oem, n, "OEM" + n, $"OEM_{n:D2}", $"OEM_{n:D2}", 8, 8, ListaOem, n * 10));

            lista.Add(new SubgrupoMenu(Installation, 2, "General level layout", "Layout general", "LayoutGeneral", 8, 8, ListaGeneral));
            lista.Add(new SubgrupoMenu(Installation, 3, "Detailed level 1 layout", "Layout detailed 1", "LayoutDetailed1", 32, 32, ListaDetalle));
            lista.Add(new SubgrupoMenu(Installation, 4, "Detailed level 2 layout", "Layout detailed 2", "LayoutDetailed2", 32, 32, ListaDetalle, 32));
            lista.Add(new SubgrupoMenu(Installation, 5, "Fixture level layout", "Layout fixture", "LayoutFixture", 32, 32, ListaFixture));
            lista.Add(new SubgrupoMenu(Installation, 6, "Traceability", "Traceability", "Traceability", 24, 24, ListaTrazabilidad));
            lista.Add(new SubgrupoMenu(Installation, 8, "Interface signals", "Interface signals", "InterfaceSignals", 32, 32, ListaInterfaz));

            lista.Add(new SubgrupoMenu(Equipment, 1, "Robots", "Robots", "Robots", 32, 24, ListaRobots));
            lista.Add(new SubgrupoMenu(Equipment, 2, "Drives", "Drives", "Drives", 32, 0));
            lista.Add(new SubgrupoMenu(Equipment, 3, "Welding", "Welding", "Welding", 32, 0));

            lista.Add(new SubgrupoMenu(ManualOperations, 1, "Manual movement 1", "Manual Movements 1", "ManualMovements1", 32, 32, ListaManual));
            lista.Add(new SubgrupoMenu(ManualOperations, 2, "Manual movement 2", "Manual Movements 2", "ManualMovements2", 32, 32, ListaManual, 32));
            UserFunctions = new SubgrupoMenu(ManualOperations, 3, "User functions", "User functions", "UserFunctions", PantallasFunciones, PantallasFunciones, ListaFunciones);
            lista.Add(UserFunctions);
            lista.Add(new SubgrupoMenu(ManualOperations, 4, "Poka-Yoke test", "Poka-Yoke test", "PokaYokeTest", 8, 8, ListaPokaYoke));
            lista.Add(new SubgrupoMenu(ManualOperations, 5, "Docking stations", "Docking stations", "DockingStations", 8, 8, ListaDocking));

            Profinet = new SubgrupoMenu(HardwareDiagnostics, 1, "Profinet diagnostics", "Profinet diagnostic", "ProfinetDiagnostic", 8, 8, ListaProfinet);
            lista.Add(Profinet);
            lista.Add(new SubgrupoMenu(HardwareDiagnostics, 3, "Web server", "Web server", "WebServer", 0, 8, ListaWebServer));

            Energia = new SubgrupoMenu(Sistema, 5, "Energy measurement", "Energy", "EnergyMeasurement", 7, 0);
            lista.Add(Energia);

            Subgrupos = lista;
        }

        public static SubgrupoMenu Buscar(string clave) => Subgrupos.FirstOrDefault(s => s.Clave == clave);

        /// <summary>
        /// Si una posición se ve en un HMI nuevo: todas, salvo la 8 de Profinet y la 2 a la 7 de energía, que el FC
        /// de la plantilla ocultaba siempre.
        /// </summary>
        public static bool VisiblePorDefecto(SubgrupoMenu subgrupo, int posicion)
        {
            if (subgrupo == Profinet)
                return posicion != 8;
            if (subgrupo == Energia)
                return posicion == 1;
            return true;
        }
    }
}
