#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Sicar;

namespace HerramientasSICAR.Tests;

public class StepTitlesTests
{
    [Fact]
    public void Parse_Comment_ReadsTitlePerLanguage()
    {
        var titles = StepTitles.Parse("Title_english Lower table \nTitle_deutsch Tisch senken\r\nTitle_espanol Bajar mesa");
        Assert.Equal("Lower table", titles["english"]);
        Assert.Equal("Tisch senken", titles["deutsch"]);
        Assert.Equal("Bajar mesa", titles["espanol"]);
    }

    [Fact]
    public void Parse_OtherLinesAndEmptyTitles_Ignores()
    {
        var titles = StepTitles.Parse("Comentario libre\nTitle_english \nCategory_7\nTitle_espanol Aviso puerta");
        Assert.Equal(["espanol"], titles.Keys);
    }

    // Como en TIA: la cultura de edición tiene los títulos buenos y las otras, restos de otro paso.
    private static readonly MultilingualText CommentWithLeftovers = new(new Dictionary<string, string>
    {
        ["es-ES"] = "Title_english\nTitle_espanol Esperar otra cosa",
        ["en-US"] = "Title_english Lower table\nTitle_espanol Bajar mesa",
        ["de-DE"] = "Title_english\nTitle_espanol Esperar otra cosa",
    });

    [Fact]
    public void Parse_EditingLanguage_UsesOnlyIt()
    {
        var titles = StepTitles.Parse(CommentWithLeftovers, "en-US");
        Assert.Equal("Bajar mesa", titles["espanol"]);
        Assert.Equal("Lower table", titles["english"]);

        Assert.Equal("Esperar otra cosa", StepTitles.Parse(CommentWithLeftovers, "es-ES")["espanol"]);
    }

    [Fact]
    public void Parse_NoEditingLanguage_UsesMostTitled()
    {
        var titles = StepTitles.Parse(CommentWithLeftovers);
        Assert.Equal("Bajar mesa", titles["espanol"]);
        Assert.Equal(2, titles.Count);
    }

    [Fact]
    public void Parse_NoComment_ReturnsEmpty()
    {
        Assert.Empty(StepTitles.Parse((string?)null));
        Assert.Empty(StepTitles.Parse(MultilingualText.Empty));
    }
}
