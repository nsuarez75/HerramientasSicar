using HerramientasSICAR.Helpers;
using System.Collections.Generic;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class SincroCheckerTests
    {
        private static ExpresionLogica V(string senal) => ExpresionLogica.Literal(senal);

        private static PasoSecuencia Paso(string nombre, ExpresionLogica ilockAuto) =>
            new PasoSecuencia { Paso = nombre, ExpresionIlockAuto = ilockAuto };

        [Fact]
        public void Las_condiciones_de_PERM_no_cuentan_para_la_sincronizacion()
        {
            // Con PERM (Codigo=1) los pasos con Codigo=2 serían imposibles y no saldría ninguna coincidencia
            var pasos = new List<PasoSecuencia>
            {
                Paso("PERM", V("\"Est-Codigo=1\"")),
                Paso("S001", V("\"Est-Codigo=2\"")),
                Paso("S002", ExpresionLogica.And(V("\"Est-Codigo=2\""), V("\"Est-Pieza\""))),
            };

            var conflictos = new SincroChecker().Comprobar(pasos);

            var conflicto = Assert.Single(conflictos);
            Assert.Equal("S001", conflicto.PasoA.Paso);
            Assert.Equal("S002", conflicto.PasoB.Paso);
            Assert.False(conflicto.Estado.ContainsKey("\"Est-Codigo=1\""));
        }

        [Fact]
        public void La_condicion_de_un_paso_es_solo_la_suya()
        {
            var checker = new SincroChecker();
            checker.Comprobar(new[] { Paso("PERM", V("\"Est-Marcha\"")), Paso("S001", V("\"Est-Pieza\"")) });

            var variables = new HashSet<string>();
            checker.CondicionSincro(Paso("S001", V("\"Est-Pieza\""))).RecogerVariables(variables);

            Assert.Equal(new[] { "\"Est-Pieza\"" }, variables);
        }
    }
}
