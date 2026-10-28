using HerramientasSICAR.Logica;
using System.Collections.Generic;

namespace HerramientasSICAR.Services
{
    // Copias simples de lo que se lee de TIA. La ventana trabaja con ellas y nunca toca objetos de Openness.

    public class PlcInfo
    {
        public string Nombre { get; set; }
        public GrupoInfo Raiz { get; set; }
    }

    public class GrupoInfo
    {
        public string Nombre { get; set; }
        public List<GrupoInfo> Grupos { get; } = new List<GrupoInfo>();
        public List<BloqueInfo> Bloques { get; } = new List<BloqueInfo>();
    }

    public class BloqueInfo
    {
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public int Numero { get; set; }
        public bool Protegido { get; set; }
    }

    /// <summary>Dónde está un bloque: PLC, grupos desde "Bloques de programa" y nombre.</summary>
    public class BloqueRef
    {
        public string Plc { get; set; }
        public IReadOnlyList<string> Grupos { get; set; }
        public string Nombre { get; set; }
    }

    /// <summary>Un PLC o una HMI del proyecto, sin lo que tiene dentro.</summary>
    public class DispositivoInfo
    {
        public string Nombre { get; set; }
        public bool EsHmi { get; set; }
    }

    /// <summary>PLC y HMI del proyecto conectado.</summary>
    public class ArbolProyecto
    {
        public List<PlcInfo> Plcs { get; } = new List<PlcInfo>();
        public List<HmiInfo> Hmis { get; } = new List<HmiInfo>();
    }

    /// <summary>HMI WinCC clásica con sus ramas exportables, en el orden del árbol.</summary>
    public class HmiInfo
    {
        public string Nombre { get; set; }
        public List<RamaInfo> Ramas { get; } = new List<RamaInfo>();
    }

    /// <summary>Una rama de la HMI. En las ramas únicas (elementos globales, vista general) Raiz no tiene nada dentro.</summary>
    public class RamaInfo
    {
        public RamaHmi Rama { get; set; }
        public CarpetaHmiInfo Raiz { get; set; }
    }

    public class CarpetaHmiInfo
    {
        public string Nombre { get; set; }
        public List<CarpetaHmiInfo> Carpetas { get; } = new List<CarpetaHmiInfo>();
        public List<string> Elementos { get; } = new List<string>();
    }

    /// <summary>Dónde está un elemento de HMI: HMI, rama, carpetas dentro de la rama y nombre.</summary>
    public class ElementoHmiRef
    {
        public string Hmi { get; set; }
        public RamaHmi Rama { get; set; }
        public IReadOnlyList<string> Grupos { get; set; }
        public string Nombre { get; set; }
    }

    public class ResultadoOperacion
    {
        public int Correctos { get; set; }
        public List<string> Fallos { get; } = new List<string>();
    }
}
