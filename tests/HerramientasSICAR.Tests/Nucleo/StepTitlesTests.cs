#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Sicar;

namespace HerramientasSICAR.Tests;

public class StepTitlesTests
{
    [Fact]
    public void Lee_un_titulo_por_idioma()
    {
        var titles = StepTitles.Parse("Title_english Lower table \nTitle_deutsch Tisch senken\r\nTitle_espanol Bajar mesa");
        Assert.Equal("Lower table", titles["english"]);
        Assert.Equal("Tisch senken", titles["deutsch"]);
        Assert.Equal("Bajar mesa", titles["espanol"]);
    }

    [Fact]
    public void Ignora_las_lineas_que_no_son_de_titulo_y_los_titulos_vacios()
    {
        var titles = StepTitles.Parse("Comentario libre\nTitle_english \nCategory_7\nTitle_espanol Aviso puerta");
        Assert.Equal(["espanol"], titles.Keys);
    }

    // Como en TIA: la cultura de edición tiene los títulos buenos y las otras, restos de otro paso.
    private static readonly MultilingualText ComentarioConRestos = new(new Dictionary<string, string>
    {
        ["es-ES"] = "Title_english\nTitle_espanol Esperar otra cosa",
        ["en-US"] = "Title_english Lower table\nTitle_espanol Bajar mesa",
        ["de-DE"] = "Title_english\nTitle_espanol Esperar otra cosa",
    });

    [Fact]
    public void Solo_vale_el_idioma_de_edicion_del_proyecto()
    {
        var titles = StepTitles.Parse(ComentarioConRestos, "en-US");
        Assert.Equal("Bajar mesa", titles["espanol"]);
        Assert.Equal("Lower table", titles["english"]);

        Assert.Equal("Esperar otra cosa", StepTitles.Parse(ComentarioConRestos, "es-ES")["espanol"]);
    }

    [Fact]
    public void Sin_idioma_de_edicion_se_usa_la_cultura_con_mas_titulos()
    {
        var titles = StepTitles.Parse(ComentarioConRestos);
        Assert.Equal("Bajar mesa", titles["espanol"]);
        Assert.Equal(2, titles.Count);
    }

    [Fact]
    public void Sin_comentario_no_hay_titulos()
    {
        Assert.Empty(StepTitles.Parse((string?)null));
        Assert.Empty(StepTitles.Parse(MultilingualText.Empty));
    }
}
