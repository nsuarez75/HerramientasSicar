#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Sicar;

namespace HerramientasSICAR.Tests;

public class SicarNumberingTests
{
    [Theory]
    [InlineData(1, 1001)]
    [InlineData(7, 1007)]
    [InlineData(255, 1255)]
    public void El_bloque_de_una_secuencia_es_1000_mas_su_numero(int secuencia, int bloque)
    {
        Assert.Equal(bloque, SicarNumbering.BlockNumber(secuencia));
        Assert.Equal(secuencia, SicarNumbering.SequenceFromBlock(bloque));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void Una_secuencia_fuera_de_rango_no_tiene_bloque(int secuencia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SicarNumbering.BlockNumber(secuencia));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1256)]
    [InlineData(986)]
    public void Un_bloque_fuera_del_rango_SICAR_no_es_una_secuencia(int bloque)
    {
        Assert.Null(SicarNumbering.SequenceFromBlock(bloque));
    }

    [Theory]
    [InlineData(1, "S001")]
    [InlineData(45, "S045")]
    [InlineData(228, "S228")]
    public void La_etiqueta_del_paso_lleva_tres_cifras(int paso, string etiqueta)
    {
        Assert.Equal(etiqueta, SicarNumbering.StepLabel(paso));
        Assert.True(SicarNumbering.TryParseStepLabel(etiqueta, out var leido));
        Assert.Equal(paso, leido);
    }

    [Theory]
    [InlineData("PERM")]
    [InlineData("S000")]
    [InlineData("S229")]
    [InlineData("S01")]
    [InlineData("S-01")]
    [InlineData("X001")]
    [InlineData(null)]
    public void Las_etiquetas_que_no_son_de_paso_se_rechazan(string? etiqueta)
    {
        Assert.False(SicarNumbering.TryParseStepLabel(etiqueta, out _));
    }

    [Fact]
    public void Un_paso_fuera_de_rango_no_tiene_etiqueta()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SicarNumbering.StepLabel(229));
    }
}
