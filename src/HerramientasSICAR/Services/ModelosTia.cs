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

    public class ResultadoOperacion
    {
        public int Correctos { get; set; }
        public List<string> Fallos { get; } = new List<string>();
    }
}
