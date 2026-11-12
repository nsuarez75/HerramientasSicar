using HerramientasSICAR.Logic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class SequenceCommentsTests
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

        private static string CommentEnUs(XDocument doc, int network) =>
            doc.Descendants("SW.Blocks.CompileUnit").ElementAt(network).Elements("ObjectList").Elements("MultilingualText")
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
        public void TitleWithoutStep_StepTitle_RemovesStepNumber(string title, string expected)
        {
            Assert.Equal(expected, SequenceComments.TitleWithoutStep(title));
        }

        [Fact]
        public void Titles_Fb_ReturnsDistinctTitles()
        {
            Assert.Equal(new[] { "Esperar pieza en la mesa", "Cerrar pinza & bajar" }, SequenceComments.Titles(Fb));
        }

        [Fact]
        public void Comment_NetworkWithComment_WritesThreeLinesInEnUs()
        {
            var (xml, commented) = SequenceComments.Comment(Fb, t => new CommentTexts("EN " + t, "DE " + t, t));

            var doc = XDocument.Parse(xml);
            Assert.Equal(2, commented);
            Assert.Equal("", CommentEnUs(doc, 0));
            Assert.Equal("Title_english EN Esperar pieza en la mesa\nTitle_deutsch DE Esperar pieza en la mesa\nTitle_espanol Esperar pieza en la mesa",
                CommentEnUs(doc, 1));
            Assert.Equal("no se toca", doc.Descendants("MultilingualTextItem").Single(i => (string)i.Attribute("ID") == "A").Descendants("Text").Single().Value);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void CommentText_AllLanguages_SicarOrder()
        {
            var texts = new CommentTexts(new System.Collections.Generic.Dictionary<TitleLanguage, string>
            {
                [TitleLanguage.French] = "FR",
                [TitleLanguage.Castellano] = "ES",
                [TitleLanguage.Polish] = "PL",
                [TitleLanguage.English] = "EN",
                [TitleLanguage.Turkish] = "TR",
                [TitleLanguage.German] = "DE",
            });

            Assert.Equal("Title_english EN\nTitle_deutsch DE\nTitle_espanol ES\nTitle_polski PL\nTitle_tuerk TR\nTitle_francais FR",
                texts.CommentText());
        }

        [Theory]
        [InlineData(TitleLanguage.English, true, "en de es pl tr fr")]
        [InlineData(TitleLanguage.Castellano, false, "en de es")]
        [InlineData(TitleLanguage.Polish, false, "en de es pl")]
        public void Languages_SourceAndTranslate_ReturnsLines(TitleLanguage source, bool translate, string codes)
        {
            Assert.Equal(codes, string.Join(" ", SequenceComments.Languages(source, translate).Select(SequenceComments.Code)));
        }

        [Fact]
        public void Comment_NetworkWithoutComment_CreatesWithNewIds()
        {
            var (xml, _) = SequenceComments.Comment(Fb, t => new CommentTexts(t, t, t));

            var doc = XDocument.Parse(xml);
            Assert.Equal("Title_english Cerrar pinza & bajar\nTitle_deutsch Cerrar pinza & bajar\nTitle_espanol Cerrar pinza & bajar", CommentEnUs(doc, 2));
            var ids = doc.Descendants().Select(e => (string)e.Attribute("ID")).Where(i => i != null).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Comment_Block_KeepsLogic()
        {
            var (xml, _) = SequenceComments.Comment(Fb, t => new CommentTexts(t, t, t));

            Assert.Contains(@"<FlgNet xmlns=""http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5""><Parts /></FlgNet>", xml);
        }

        [Theory]
        [InlineData("Estacion_FB", new[] { "10_Sequence & Messageblocks", "OM1", "++0100" }, true)]
        [InlineData("OM1_Summary_FB", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        [InlineData("OM1_Messages_FB", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        [InlineData("Seguridad_FB", new[] { "10_Sequence & Messageblocks", "OM2", "50_Safety" }, false)]
        [InlineData("General_FB", new[] { "10_Sequence & Messageblocks" }, false)]
        [InlineData("Estacion_FC", new[] { "10_Sequence & Messageblocks", "OM1" }, false)]
        public void CheckedByDefault_Block_OnlyStationSequences(string name, string[] groups, bool isChecked)
        {
            Assert.Equal(isChecked, SequenceComments.CheckedByDefault(groups, name));
        }

        [Fact]
        public void IsEquipmentName_Text_DetectsEquipmentNames()
        {
            Assert.True(SequenceComments.IsEquipmentName("++2040+FIX11 (YA - YH)"));
            Assert.False(SequenceComments.IsEquipmentName("Desactivar Conveyor"));
        }
    }
}
