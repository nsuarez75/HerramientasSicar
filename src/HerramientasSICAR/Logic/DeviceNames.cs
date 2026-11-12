using System.Collections.Generic;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logic
{
    /// <summary>
    /// Nombre con el que se enseña una HMI. El runtime de un IPC (estación PC) suele llamarse HMI_RT_1, así que se enseña
    /// el nombre de la estación, que es el que se ve arriba del todo en el árbol de TIA. Con TIA se sigue hablando con el
    /// nombre del runtime.
    /// </summary>
    public static class DeviceNames
    {
        /// <summary>
        /// La estación si la HMI está dentro de un dispositivo con otro nombre; «estación › HMI» si esa estación tiene
        /// más de un PLC o HMI (no se distinguirían); el nombre de la HMI en los demás casos. Los PLC salen por su nombre.
        /// </summary>
        public static string Visible(DeviceInfo device, IEnumerable<DeviceInfo> all)
        {
            if (device.Kind != DeviceKind.Hmi || string.IsNullOrWhiteSpace(device.Station) || device.Station == device.Name)
                return device.Name;
            bool shared = all.Count(d => d.Station == device.Station) > 1;
            return shared ? device.Station + " › " + device.Name : device.Station;
        }
    }
}
