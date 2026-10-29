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
        public void Un_paso_con_transAuto_a_1_no_es_candidato_a_sincronizar()
        {
            // S001 avanza con Servicio; S002 solo sincroniza con Servicio: nunca coinciden
            var espera = Paso("S001", V("\"Est-Codigo=1\""));
            espera.ExpresionTransAuto = V("\"Est-Servicio\"");
            var servicio = Paso("S002", V("\"Est-Servicio\""));

            var conflictos = new SincroChecker().Comprobar(new[] { espera, servicio });

            Assert.Empty(conflictos);
        }

        [Fact]
        public void Con_transAuto_a_0_el_paso_sigue_siendo_candidato()
        {
            var espera = Paso("S001", V("\"Est-Codigo=1\""));
            espera.ExpresionTransAuto = V("\"Est-Marcha\"");
            var servicio = Paso("S002", V("\"Est-Servicio\""));

            var conflicto = Assert.Single(new SincroChecker().Comprobar(new[] { espera, servicio }));

            Assert.False(conflicto.Estado.TryGetValue("\"Est-Marcha\"", out bool marcha) && marcha);
        }

        private static AsignacionLad Salto(int destino, ExpresionLogica condicion) =>
            new AsignacionLad { Destino = "#Sequence.stepNplus1", NombreCorto = "stepNplus1", Tipo = "MOVE", Valor = destino.ToString(), Condicion = condicion };

        private static readonly ExpresionLogica PulsoSincro = ExpresionLogica.Literal("#Sequence.pos_Pulse_OM_Sync");

        [Fact]
        public void Un_paso_con_un_salto_cumplido_no_es_candidato_a_sincronizar()
        {
            var espera = Paso("S001", V("\"Est-Codigo=1\""));
            espera.Saltos.Add(Salto(2, V("\"Est-Servicio\"")));
            var servicio = Paso("S002", V("\"Est-Servicio\""));

            Assert.Empty(new SincroChecker().Comprobar(new[] { espera, servicio }));
        }

        [Fact]
        public void Un_salto_con_el_pulso_de_sincronizacion_negado_no_cuenta()
        {
            var espera = Paso("S001", V("\"Est-Codigo=1\""));
            espera.Saltos.Add(Salto(2, ExpresionLogica.And(ExpresionLogica.Not(PulsoSincro), V("\"Est-Servicio\""))));
            var servicio = Paso("S002", V("\"Est-Servicio\""));

            var conflicto = Assert.Single(new SincroChecker().Comprobar(new[] { espera, servicio }));

            Assert.False(conflicto.Estado.ContainsKey("#Sequence.pos_Pulse_OM_Sync"));
        }

        [Fact]
        public void En_una_secuencia_de_robot_no_se_comprueban_S223_a_S227()
        {
            var pasos = new[]
            {
                Paso("S001", V("\"Est-Codigo=1\"")),
                Paso("S222", V("\"Est-Pieza\"")),
                Paso("S223", V("\"Est-Servicio\"")),
                Paso("S227", V("\"Est-Servicio\"")),
                Paso("S228", V("\"Est-Servicio\"")),
            };

            var utillaje = new SincroChecker().Comprobar(pasos);
            var robot = new SincroChecker { EsRobot = true };
            var conflictosRobot = robot.Comprobar(pasos);

            Assert.Equal(10, utillaje.Count);
            Assert.Equal(new[] { "S001", "S222", "S228" }, robot.PasosAuto.ConvertAll(p => p.Paso));
            Assert.Equal(3, conflictosRobot.Count);
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
