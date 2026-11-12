using HerramientasSICAR.Logic;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class DeviceNamesTests
    {
        private static DeviceInfo Hmi(string name, string station) => new DeviceInfo { Name = name, Kind = DeviceKind.Hmi, Station = station };

        [Fact]
        public void Visible_IpcRuntime_ReturnsStationName()
        {
            var runtime = Hmi("HMI_RT_1", "IPC_Linea1");
            var panel = Hmi("Panel_10", "Panel_10");

            Assert.Equal("IPC_Linea1", DeviceNames.Visible(runtime, new[] { runtime, panel }));
            Assert.Equal("Panel_10", DeviceNames.Visible(panel, new[] { runtime, panel }));
        }

        [Fact]
        public void Visible_SharedStation_ReturnsBothNames()
        {
            var runtime = Hmi("HMI_RT_1", "IPC_Linea1");
            var plc = new DeviceInfo { Name = "PLC_SW", Kind = DeviceKind.Plc, Station = "IPC_Linea1" };

            Assert.Equal("IPC_Linea1 › HMI_RT_1", DeviceNames.Visible(runtime, new[] { runtime, plc }));
            Assert.Equal("PLC_SW", DeviceNames.Visible(plc, new[] { runtime, plc }));
        }

        [Fact]
        public void Visible_NoStation_ReturnsHmiName()
        {
            var hmi = Hmi("HMI_RT_1", "");

            Assert.Equal("HMI_RT_1", DeviceNames.Visible(hmi, new[] { hmi }));
        }
    }
}
