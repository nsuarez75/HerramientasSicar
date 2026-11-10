using HerramientasSICAR.Logica;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ComentariosSecuenciaTests
    {
        // FB de secuencia de ejemplo (inventado), con la forma del XML que exporta TIA V19.
        private const string Fb = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <Engineering version=""V19"" />
  <SW.Blocks.FB ID=""0"">
    <AttributeList>
      <Name>Estacion_Prueba_FB</Name>
      <ProgrammingLanguage>LAD</ProgrammingLanguage>
    </AttributeList>
    <ObjectList>
      <SW.Blocks.CompileUnit ID=""3"" CompositionName=""CompileUnits"">
        <AttributeList>
          <NetworkSource><FlgNet xmlns=""http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5""><Parts /></FlgNet></NetworkSource>
          <ProgrammingLanguage>LAD</ProgrammingLanguage>
        </AttributeList>
        <ObjectList>
          <MultilingualText ID=""4"" CompositionName=""Comment"">
            <ObjectList>
              <MultilingualTextItem ID=""5"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>en-US</Culture>
                  <Text />
                </AttributeList>
              </MultilingualTextItem>
            </ObjectList>
          </MultilingualText>
          <MultilingualText ID=""6"" CompositionName=""Title"">
            <ObjectList>
              <MultilingualTextItem ID=""7"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>en-US</Culture>
                  <Text>============ SEQUENCE ============</Text>
                </AttributeList>
              </MultilingualTextItem>
            </ObjectList>
          </MultilingualText>
        </ObjectList>
      </SW.Blocks.CompileUnit>
      <SW.Blocks.CompileUnit ID=""8"" CompositionName=""CompileUnits"">
        <AttributeList>
          <ProgrammingLanguage>LAD</ProgrammingLanguage>
        </AttributeList>
        <ObjectList>
          <MultilingualText ID=""9"" CompositionName=""Comment"">
            <ObjectList>
              <MultilingualTextItem ID=""A"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>es-ES</Culture>
                  <Text>no se toca</Text>
                </AttributeList>
              </MultilingualTextItem>
              <MultilingualTextItem ID=""B"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>en-US</Culture>
                  <Text>Comentario viejo</Text>
                </AttributeList>
              </MultilingualTextItem>
            </ObjectList>
          </MultilingualText>
          <MultilingualText ID=""C"" CompositionName=""Title"">
            <ObjectList>
              <MultilingualTextItem ID=""D"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>en-US</Culture>
                  <Text>S001 Esperar pieza en la mesa</Text>
                </AttributeList>
              </MultilingualTextItem>
            </ObjectList>
          </MultilingualText>
        </ObjectList>
      </SW.Blocks.CompileUnit>
      <SW.Blocks.CompileUnit ID=""E"" CompositionName=""CompileUnits"">
        <AttributeList>
          <ProgrammingLanguage>LAD</ProgrammingLanguage>
        </AttributeList>
        <ObjectList>
          <MultilingualText ID=""F"" CompositionName=""Title"">
            <ObjectList>
              <MultilingualTextItem ID=""10"" CompositionName=""Items"">
                <AttributeList>
                  <Culture>en-US</Culture>
                  <Text>S002 Cerrar pinza &amp; bajar</Text>
                </AttributeList>
              </MultilingualTextItem>
            </ObjectList>
          </MultilingualText>
        </ObjectList>
      </SW.Blocks.CompileUnit>
    </ObjectList>
  </SW.Blocks.FB>
</Document>";

        private static string ComentarioEnUs(XDocument doc, int segmento) =>
            doc.Descendants("SW.Blocks.CompileUnit").ElementAt(segmento).Elements("ObjectList").Elements("MultilingualText")
                .Single(m => (string)m.Attribute("CompositionName") == "Comment")
                .Descendants("MultilingualTextItem").Single(i => i.Descendants("Culture").Single().Value == "en-US")
                .Descendants("Text").Single().Value;

        [Theory]
        [InlineData("S001 Esperar pieza", "Esperar pieza")]
        [InlineData("  S105   Robot   a casa ", "Robot a casa")]
        [InlineData("S000 Permanent step", "Permanent step")]
        [InlineData("==== SEQUENCE ====", null)]
        [InlineData("S012", null)]
        [InlineData("", null)]
        public void El_titulo_pierde_el_numero_de_paso(string titulo, string esperado)
        {
            Assert.Equal(esperado, ComentariosSecuencia.TituloSinPaso(titulo));
        }

        [Fact]
        public void Lee_los_titulos_distintos_que_hay_que_comentar()
        {
            Assert.Equal(new[] { "Esperar pieza en la mesa", "Cerrar pinza & bajar" }, ComentariosSecuencia.Titulos(Fb));
        }

        [Fact]
        public void Escribe_el_comentario_de_tres_lineas_en_en_US_y_sobrescribe_el_anterior()
        {
            var (xml, comentados) = ComentariosSecuencia.Comentar(Fb, t => new TextosComentario("EN " + t, "DE " + t, t));

            var doc = XDocument.Parse(xml);
            Assert.Equal(2, comentados);
            Assert.Equal("", ComentarioEnUs(doc, 0));
            Assert.Equal("Title_english EN Esperar pieza en la mesa\nTitle_deutsch DE Esperar pieza en la mesa\nTitle_espanol Esperar pieza en la mesa",
                ComentarioEnUs(doc, 1));
            Assert.Equal("no se toca", doc.Descendants("MultilingualTextItem").Single(i => (string)i.Attribute("ID") == "A").Descendants("Text").Single().Value);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void Si_el_segmento_no_tiene_comentario_se_crea_con_IDs_nuevos()
        {
            var (xml, _) = ComentariosSecuencia.Comentar(Fb, t => new TextosComentario(t, t, t));

            var doc = XDocument.Parse(xml);
            Assert.Equal("Title_english Cerrar pinza & bajar\nTitle_deutsch Cerrar pinza & bajar\nTitle_espanol Cerrar pinza & bajar", ComentarioEnUs(doc, 2));
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void La_logica_del_bloque_no_cambia()
        {
            var (xml, _) = ComentariosSecuencia.Comentar(Fb, t => new TextosComentario(t, t, t));

            Assert.Contains(@"<FlgNet xmlns=""http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5""><Parts /></FlgNet>", xml);
        }

        [Theory]
        [InlineData("Estacion_FB", new[] { "10_Sequence & Messageblocks", "OM1", "++0100" }, true)]
        [InlineData("OM1_Summary_FB", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        [InlineData("OM1_Messages_FB", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        [InlineData("Seguridad_FB", new[] { "10_Sequence & Messageblocks", "OM2", "50_Safety" }, false)]
        [InlineData("General_FB", new[] { "10_Sequence & Messageblocks" }, false)]
        [InlineData("Estacion_FC", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        public void Salen_marcados_solo_los_FB_de_secuencia_de_una_estacion(string nombre, string[] grupos, bool marcado)
        {
            Assert.Equal(marcado, ComentariosSecuencia.MarcadoPorDefecto(grupos, nombre));
        }

        [Fact]
        public void Los_nombres_de_equipo_no_se_traducen()
        {
            Assert.True(ComentariosSecuencia.EsNombreDeEquipo("++2040+FIX11 (YA - YH)"));
            Assert.False(ComentariosSecuencia.EsNombreDeEquipo("Desactivar Conveyor"));
        }
    }
}
