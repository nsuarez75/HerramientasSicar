using HerramientasSICAR.Logica;
using System;
using System.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ArraysBloqueTests
    {
        // Accesos de ejemplo (inventados) con la forma del XML de Openness: dos usos de Flancos con índice fijo, uno con
        // índice variable, y otro array.
        private static string Acceso(string nombre, string indice) => indice.StartsWith("#")
            ? $@"<Access Scope=""LocalVariable""><Symbol><Component Name=""{nombre}"" AccessModifier=""Array""><Access Scope=""LocalVariable""><Symbol><Component Name=""{indice.Substring(1)}"" /></Symbol></Access></Component></Symbol></Access>"
            : $@"<Access Scope=""LocalVariable""><Symbol><Component Name=""{nombre}"" AccessModifier=""Array""><Access Scope=""LiteralConstant""><Constant><ConstantType>DInt</ConstantType><ConstantValue>{indice}</ConstantValue></Constant></Access></Component></Symbol></Access>";

        private static readonly string Fb = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <SW.Blocks.FB ID=""0"">
    <FlgNet xmlns=""http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"">
      {Acceso("Flancos", "3")}
      {Acceso("Otro", "7")}
      {Acceso("Flancos", "#i")}
      {Acceso("flancos", "3")}
      {Acceso("Flancos", "9")}
    </FlgNet>
  </SW.Blocks.FB>
</Document>";

        [Fact]
        public void Lista_los_arrays_con_indice_fijo_y_sus_indices_en_orden()
        {
            var arrays = ArraysBloque.Listar(Fb);

            Assert.Equal(new[] { "Flancos", "Otro" }, arrays.Select(a => a.Nombre));
            Assert.Equal(new long[] { 3, 3, 9 }, arrays[0].Indices);
            Assert.Equal(3, arrays[0].Usos);
            Assert.Equal(new long[] { 7 }, arrays[1].Indices);
        }

        [Fact]
        public void Renumera_solo_los_usos_con_indice_fijo_de_ese_array()
        {
            string xml = ArraysBloque.Renumerar(Fb, "FLANCOS", out int cambios);

            Assert.Equal(3, cambios);
            var arrays = ArraysBloque.Listar(xml);
            Assert.Equal(new long[] { 1, 2, 3 }, arrays[0].Indices);
            Assert.Equal(new long[] { 7 }, arrays[1].Indices);
            Assert.Contains(@"<Component Name=""i"" />", xml);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void Si_el_array_no_esta_avisa()
        {
            Assert.Throws<InvalidOperationException>(() => ArraysBloque.Renumerar(Fb, "NoExiste", out _));
        }
    }
}
