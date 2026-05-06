using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.IO;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Centralized service for all TIA Portal Openness interactions
    /// </summary>
    public class TiaOpennessService : IDisposable
    {
        private TiaPortal _tiaPortal;
        private Project _project;
        private bool _initialized = false;

        public TiaOpennessService()
        {
            // Don't connect to TIA Portal in constructor - do it lazily when needed
        }

        private void EnsureConnected()
        {
            if (_initialized)
                return;

            // Try to attach to an existing TIA Portal instance
            try
            {
                foreach (var proc in TiaPortal.GetProcesses())
                {
                    try
                    {
                        _tiaPortal = proc.Attach();
                        if (_tiaPortal != null)
                        {
                            Console.WriteLine("Adjuntado a instancia existente de TIA Portal.");
                            break;
                        }
                    }
                    catch { }
                }

                // If no instance is open, create a new one
                if (_tiaPortal == null)
                {
                    _tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
                    Console.WriteLine("Nueva instancia de TIA Portal creada.");
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al conectar con TIA Portal: " + ex.Message);
            }
        }

        public void AbrirProyecto(string pathProyecto)
        {
            EnsureConnected(); // Connect only when needed

            if (!File.Exists(pathProyecto))
                throw new FileNotFoundException("No se encontró el archivo del proyecto.", pathProyecto);

            // If the project is already open, use it directly
            foreach (var proj in _tiaPortal.Projects)
            {
                if (proj.Path.FullName.Equals(Path.GetFullPath(pathProyecto), StringComparison.OrdinalIgnoreCase))
                {
                    _project = proj;
                    Console.WriteLine("Proyecto ya estaba abierto, se reutiliza.");
                    return;
                }
            }

            // If not open, open it
            _project = _tiaPortal.Projects.Open(new FileInfo(pathProyecto));
            Console.WriteLine("Proyecto abierto correctamente.");
        }

        public void CompilarTodo()
        {
            if (_project == null)
                throw new InvalidOperationException("El proyecto no está abierto.");

            PlcSoftware plc = ObtenerPLCSoftware();
            if (plc == null)
                throw new Exception("No se encontró ningún PLC con software en el proyecto.");

            Console.WriteLine("Compilando todos los bloques del PLC...");
            var compilable = plc.BlockGroup.GetService<ICompilable>();
            if (compilable != null)
                compilable.Compile();
            else
                throw new Exception("El grupo de bloques no es compilable.");

            Console.WriteLine("Compilación completa.");
        }

        public void ExportarBloque(string nombreBloque, string rutaExportacion)
        {
            if (_project == null)
                throw new InvalidOperationException("El proyecto no está abierto.");

            Directory.CreateDirectory(rutaExportacion);

            PlcSoftware plc = ObtenerPLCSoftware();
            if (plc == null)
                throw new Exception("No se encontró ningún PLC con software en el proyecto.");

            PlcBlock bloque = BuscarBloque(plc.BlockGroup, nombreBloque);
            if (bloque == null)
                throw new Exception("No se encontró el bloque '" + nombreBloque + "' en el proyecto.");

            // Overwrite the file if it already exists
            string destino = Path.Combine(rutaExportacion, bloque.Name + ".xml");
            if (File.Exists(destino))
                File.Delete(destino);

            bloque.Export(new FileInfo(destino), ExportOptions.WithDefaults);
        }

        public void EliminarBloque(string nombreBloque)
        {
            if (_project == null)
                throw new InvalidOperationException("El proyecto no está abierto.");

            PlcSoftware plc = ObtenerPLCSoftware();
            if (plc == null)
                throw new Exception("No se encontró ningún PLC con software en el proyecto.");

            PlcBlock bloque = BuscarBloque(plc.BlockGroup, nombreBloque);
            if (bloque != null)
                bloque.Delete();
        }

        public void ImportarBloque(string rutaImportacion)
        {
            if (_project == null)
                throw new InvalidOperationException("El proyecto no está abierto.");

            FileInfo archivo = new FileInfo(rutaImportacion);
            if (!archivo.Exists)
                throw new FileNotFoundException("Archivo XML de importación no encontrado.", rutaImportacion);

            PlcSoftware plc = ObtenerPLCSoftware();
            if (plc == null)
                throw new Exception("No se encontró ningún PLC con software en el proyecto.");

            // Search for the main folder 10_Sequence & Messageblocks
            PlcBlockGroup raiz = BuscarGrupo(plc.BlockGroup, "10_Sequence & Messageblocks");
            if (raiz == null)
                throw new Exception("No se encontró la carpeta '10_Sequence & Messageblocks'.");

            // Subfolder (OM1, OM2, General…)
            string carpetaDestino = archivo.Directory.Name;
            PlcBlockGroup destino = BuscarOCrearGrupo(raiz, carpetaDestino);

            destino.Blocks.Import(archivo, ImportOptions.Override);
        }

        public void CompilarBloque(string nombreBloque)
        {
            if (_project == null)
                throw new InvalidOperationException("El proyecto no está abierto.");

            PlcSoftware plc = ObtenerPLCSoftware();
            if (plc == null)
                throw new Exception("No se encontró ningún PLC con software en el proyecto.");

            PlcBlock bloque = BuscarBloque(plc.BlockGroup, nombreBloque);
            if (bloque == null)
                throw new Exception("No se encontró el bloque '" + nombreBloque + "'.");

            var compilable = bloque.GetService<ICompilable>();
            if (compilable == null)
                throw new Exception("El bloque no es compilable.");

            compilable.Compile();
        }

        private PlcSoftware ObtenerPLCSoftware()
        {
            foreach (Device device in _project.Devices)
            {
                foreach (DeviceItem item in device.DeviceItems)
                {
                    SoftwareContainer container = item.GetService<SoftwareContainer>();
                    if (container != null && container.Software is PlcSoftware)
                        return (PlcSoftware)container.Software;
                }
            }
            return null;
        }

        private PlcBlock BuscarBloque(PlcBlockGroup grupo, string nombreBloque)
        {
            foreach (PlcBlock bloque in grupo.Blocks)
                if (bloque.Name.Equals(nombreBloque, StringComparison.OrdinalIgnoreCase))
                    return bloque;

            foreach (PlcBlockGroup subgrupo in grupo.Groups)
            {
                PlcBlock encontrado = BuscarBloque(subgrupo, nombreBloque);
                if (encontrado != null)
                    return encontrado;
            }

            return null;
        }

        private PlcBlockGroup BuscarGrupo(PlcBlockGroup grupo, string nombre)
        {
            if (grupo.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase))
                return grupo;

            foreach (PlcBlockGroup sub in grupo.Groups)
            {
                PlcBlockGroup encontrado = BuscarGrupo(sub, nombre);
                if (encontrado != null)
                    return encontrado;
            }

            return null;
        }

        private PlcBlockGroup BuscarOCrearGrupo(PlcBlockGroup grupoRaiz, string nombreGrupo)
        {
            foreach (PlcBlockGroup subgrupo in grupoRaiz.Groups)
            {
                if (subgrupo.Name.Equals(nombreGrupo, StringComparison.OrdinalIgnoreCase))
                    return subgrupo;
            }

            return grupoRaiz.Groups.Create(nombreGrupo);
        }

        public void CerrarProyecto()
        {
            if (_project != null)
            {
                _project.Close();
                _project = null;
            }
        }

        public void Dispose()
        {
            //CerrarProyecto();
            if (_tiaPortal != null)
                _tiaPortal.Dispose();
        }
    }
}
