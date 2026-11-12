using System.Collections.Generic;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logica
{
    /// <summary>
    /// Nombre con el que se enseña una HMI. El runtime de un IPC (estación PC) suele llamarse HMI_RT_1, así que se enseña
    /// el nombre de la estación, que es el que se ve arriba del todo en el árbol de TIA. Con TIA se sigue hablando con el
    /// nombre del runtime.
    /// </summary>
    public static class NombresDispositivo
    {
        /// <summary>
        /// La estación si la HMI está dentro de un dispositivo con otro nombre; «estación › HMI» si esa estación tiene
        /// más de un PLC o HMI (no se distinguirían); el nombre de la HMI en los demás casos. Los PLC salen por su nombre.
        /// </summary>
        public static string Visible(DeviceInfo dispositivo, IEnumerable<DeviceInfo> todos)
        {
            if (dispositivo.Kind != DeviceKind.Hmi || string.IsNullOrWhiteSpace(dispositivo.Station) || dispositivo.Station == dispositivo.Name)
                return dispositivo.Name;
            bool compartida = todos.Count(d => d.Station == dispositivo.Station) > 1;
            return compartida ? dispositivo.Station + " › " + dispositivo.Name : dispositivo.Station;
        }
    }
}
