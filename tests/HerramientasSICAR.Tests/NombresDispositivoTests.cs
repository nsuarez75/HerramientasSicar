using HerramientasSICAR.Logica;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class NombresDispositivoTests
    {
        private static DeviceInfo Hmi(string nombre, string estacion) => new DeviceInfo { Name = nombre, Kind = DeviceKind.Hmi, Station = estacion };

        [Fact]
        public void El_runtime_de_un_IPC_se_ve_con_el_nombre_del_IPC()
        {
            var runtime = Hmi("HMI_RT_1", "IPC_Linea1");
            var panel = Hmi("Panel_10", "Panel_10");

            Assert.Equal("IPC_Linea1", NombresDispositivo.Visible(runtime, new[] { runtime, panel }));
            Assert.Equal("Panel_10", NombresDispositivo.Visible(panel, new[] { runtime, panel }));
        }

        [Fact]
        public void Si_el_IPC_tiene_varios_dispositivos_se_ven_los_dos_nombres()
        {
            var runtime = Hmi("HMI_RT_1", "IPC_Linea1");
            var plc = new DeviceInfo { Name = "PLC_SW", Kind = DeviceKind.Plc, Station = "IPC_Linea1" };

            Assert.Equal("IPC_Linea1 › HMI_RT_1", NombresDispositivo.Visible(runtime, new[] { runtime, plc }));
            Assert.Equal("PLC_SW", NombresDispositivo.Visible(plc, new[] { runtime, plc }));
        }

        [Fact]
        public void Sin_estacion_se_ve_el_nombre_de_la_HMI()
        {
            var hmi = Hmi("HMI_RT_1", "");

            Assert.Equal("HMI_RT_1", NombresDispositivo.Visible(hmi, new[] { hmi }));
        }
    }
}
