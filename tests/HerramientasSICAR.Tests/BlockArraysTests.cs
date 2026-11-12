using HerramientasSICAR.Logic;
using System;
using System.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class BlockArraysTests
    {
        // Accesos de ejemplo (inventados) con la forma del XML de Openness: dos usos de Flancos con índice fijo, uno con
        // índice variable, y otro array.
        private static string Access(string name, string index) => index.StartsWith("#")
            ? $@"<Access Scope=""LocalVariable""><Symbol><Component Name=""{name}"" AccessModifier=""Array""><Access Scope=""LocalVariable""><Symbol><Component Name=""{index.Substring(1)}"" /></Symbol></Access></Component></Symbol></Access>"
            : $@"<Access Scope=""LocalVariable""><Symbol><Component Name=""{name}"" AccessModifier=""Array""><Access Scope=""LiteralConstant""><Constant><ConstantType>DInt</ConstantType><ConstantValue>{index}</ConstantValue></Constant></Access></Component></Symbol></Access>";

        private static readonly string Fb = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <SW.Blocks.FB ID=""0"">
    <FlgNet xmlns=""http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"">
      {Access("Flancos", "3")}
      {Access("Otro", "7")}
      {Access("Flancos", "#i")}
      {Access("flancos", "3")}
      {Access("Flancos", "9")}
    </FlgNet>
  </SW.Blocks.FB>
</Document>";

        [Fact]
        public void ListArrays_Fb_ReturnsFixedIndexArrays()
        {
            var arrays = BlockArrays.ListArrays(Fb);

            Assert.Equal(new[] { "Flancos", "Otro" }, arrays.Select(a => a.Name));
            Assert.Equal(new long[] { 3, 3, 9 }, arrays[0].Indexes);
            Assert.Equal(3, arrays[0].Uses);
            Assert.Equal(new long[] { 7 }, arrays[1].Indexes);
        }

        [Fact]
        public void Renumber_Array_ChangesOnlyFixedIndexUses()
        {
            string xml = BlockArrays.Renumber(Fb, "FLANCOS", out int changes);

            Assert.Equal(3, changes);
            var arrays = BlockArrays.ListArrays(xml);
            Assert.Equal(new long[] { 1, 2, 3 }, arrays[0].Indexes);
            Assert.Equal(new long[] { 7 }, arrays[1].Indexes);
            Assert.Contains(@"<Component Name=""i"" />", xml);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void Renumber_MissingArray_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => BlockArrays.Renumber(Fb, "NoExiste", out _));
        }
    }
}
