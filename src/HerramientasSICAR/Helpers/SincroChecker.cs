using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HerramientasSICAR.Helpers
{
    public class ConflictoSincro
    {
        public PasoSecuencia PasoA { get; set; }
        public PasoSecuencia PasoB { get; set; }

        /// <summary>Signal values that make both steps synchronizable in the same scan.</summary>
        public Dictionary<string, bool> Estado { get; set; }

        public string Descripcion => PasoA.Paso + " ↔ " + PasoB.Paso;

        public string EstadoTexto => string.Join(", ", Estado.OrderBy(e => e.Key).Select(e => e.Key + "=" + (e.Value ? "1" : "0")));
    }

    /// <summary>
    /// A signal one step always requires with a value, while the other step can synchronize with the
    /// opposite value. It is a fact about both ilocks, not a proposal: only the process tells whether
    /// the signal makes sense to separate the steps.
    /// </summary>
    public class DiferenciaSincro
    {
        /// <summary>Step that always requires the value.</summary>
        public PasoSecuencia PasoQueExige { get; set; }

        public PasoSecuencia OtroPaso { get; set; }

        public string Senal { get; set; }

        /// <summary>Value always required by PasoQueExige.</summary>
        public bool Valor { get; set; }

        /// <summary>The other step also uses the signal in its ilocks, but admits the opposite value.</summary>
        public bool OtroLaComprueba { get; set; }
    }

    /// <summary>
    /// When switching from manual to automatic, the SICAR sequence engine jumps to the step whose
    /// ilocks are fulfilled and faults if several steps are fulfilled at once. A step whose transAuto
    /// is true is not proposed, because it can advance. The conditions of PERM do not take part.
    /// This checker looks, for every pair of steps, for a signal state that fulfills both.
    /// </summary>
    public class SincroChecker
    {
        // "...-YA-H" / "...-YA-W": home and work sensors of the same actuator
        private static readonly Regex _regSensorActuador = new Regex(@"^(.*-Y[A-Z0-9]+)-([HW])""?$", RegexOptions.IgnoreCase);

        // "...-ProcessCode=651": bits decoded from one value, or "x == 5" comparisons
        private static readonly Regex _regValorDecodificado = new Regex(@"^(.+?)\s*==?\s*([^=]+?)""?$");

        // Only real steps can be a sync target (PERM, BEA... are not)
        private static readonly Regex _regPaso = new Regex(@"^S\d+$", RegexOptions.IgnoreCase);

        /// <summary>An actuator cannot be in home and work position at the same time.</summary>
        public bool ExcluirHomeWork { get; set; } = true;

        /// <summary>Only one of "Tag=1", "Tag=2"... (or "x == 1", "x == 2"...) can be true at the same time.</summary>
        public bool ExcluirValoresDecodificados { get; set; } = true;

        /// <summary>Number of the first manual step; this step and all the following ones are manual. Null = none.</summary>
        public int? PrimerPasoManual { get; set; }

        public List<PasoSecuencia> PasosAuto { get; private set; } = new List<PasoSecuencia>();

        public List<ConflictoSincro> Comprobar(IEnumerable<PasoSecuencia> pasos)
        {
            PasosAuto = pasos
                .Where(p => _regPaso.IsMatch(p.Paso) &&
                            (PrimerPasoManual == null || NumeroPaso(p) < PrimerPasoManual))
                .ToList();

            var conflictos = new List<ConflictoSincro>();

            for (int i = 0; i < PasosAuto.Count; i++)
            {
                for (int j = i + 1; j < PasosAuto.Count; j++)
                {
                    var a = PasosAuto[i];
                    var b = PasosAuto[j];

                    var ambos = FijarConstantes(ExpresionLogica.And(CondicionSincro(a), CondicionSincro(b)));
                    var estado = ExpresionLogica.And(ambos, Restricciones(ambos)).BuscarSolucion();
                    if (estado != null)
                        conflictos.Add(new ConflictoSincro { PasoA = a, PasoB = b, Estado = estado });
                }
            }

            return conflictos;
        }

        /// <summary>
        /// step.ilockAuto AND step.ilockExtSync AND NOT step.transAuto (PERM does not count).
        /// A network that does not write an ilock leaves it at 1 (engine default); one that does not
        /// write transAuto never advances.
        /// </summary>
        public ExpresionLogica CondicionSincro(PasoSecuencia paso)
        {
            return ExpresionLogica.And(
                paso.ExpresionIlockAuto ?? ExpresionLogica.True,
                paso.ExpresionIlockExtSync ?? ExpresionLogica.True,
                ExpresionLogica.Not(paso.ExpresionTransAuto ?? ExpresionLogica.False));
        }

        /// <summary>First step recognised as manual, or null when none can be detected.</summary>
        public static int? DetectarPrimerPasoManual(IEnumerable<PasoSecuencia> pasos)
        {
            var manuales = pasos.Where(p => _regPaso.IsMatch(p.Paso) && EsManual(p)).Select(NumeroPaso).ToList();
            return manuales.Count > 0 ? manuales.Min() : (int?)null;
        }

        public static int NumeroPaso(PasoSecuencia paso) => int.Parse(paso.Paso.Substring(1));

        /// <summary>
        /// Manual steps write ilockManual / limitManual and nothing of the automatic mode.
        /// Neither the title nor the step number are reliable (MANMOV steps, robot steps above S100).
        /// </summary>
        public static bool EsManual(PasoSecuencia paso)
        {
            var escritas = new HashSet<string>(
                paso.Asignaciones.Where(a => a.Tipo != "MOVE").Select(a => a.NombreCorto),
                StringComparer.OrdinalIgnoreCase);

            bool manual = escritas.Contains("ilockManual") || escritas.Contains("limitManual");
            bool auto = escritas.Contains("ilockAuto") || escritas.Contains("ilockExtSync") || escritas.Contains("transAuto");
            return manual && !auto;
        }

        /// <summary>
        /// Signals that one step always requires and the other step does not (it can synchronize
        /// with the opposite value). Only signals each step checks in its own ilocks are listed:
        /// values deduced from the physical rules (-H/-W) would repeat them.
        /// </summary>
        public List<DiferenciaSincro> Diferencias(ConflictoSincro conflicto)
        {
            var a = CondicionSincro(conflicto.PasoA);
            var b = CondicionSincro(conflicto.PasoB);
            var propiasA = SenalesPropias(conflicto.PasoA);
            var propiasB = SenalesPropias(conflicto.PasoB);

            var diferencias = new List<DiferenciaSincro>();
            diferencias.AddRange(DiferenciasDe(conflicto.PasoA, a, propiasA, conflicto.PasoB, b, propiasB));
            diferencias.AddRange(DiferenciasDe(conflicto.PasoB, b, propiasB, conflicto.PasoA, a, propiasA));
            return diferencias;
        }

        private IEnumerable<DiferenciaSincro> DiferenciasDe(PasoSecuencia paso, ExpresionLogica condicion, HashSet<string> propias,
                                                            PasoSecuencia otro, ExpresionLogica condicionOtro, HashSet<string> propiasOtro)
        {
            foreach (var x in propias.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                foreach (bool valor in new[] { true, false })
                {
                    if (Implica(condicion, x, valor) && EsPosible(ExpresionLogica.And(condicionOtro, Literal(x, !valor))))
                        yield return new DiferenciaSincro
                        {
                            PasoQueExige = paso,
                            OtroPaso = otro,
                            Senal = x,
                            Valor = valor,
                            OtroLaComprueba = propiasOtro.Contains(x)
                        };
                }
            }
        }

        /// <summary>The condition can only be true with signal = valor.</summary>
        private bool Implica(ExpresionLogica condicion, string senal, bool valor) =>
            !EsPosible(ExpresionLogica.And(condicion, Literal(senal, !valor)));

        private bool EsPosible(ExpresionLogica condicion)
        {
            var e = FijarConstantes(condicion);
            return ExpresionLogica.And(e, Restricciones(e)).BuscarSolucion() != null;
        }

        private static ExpresionLogica Literal(string senal, bool valor) =>
            valor ? ExpresionLogica.Literal(senal) : ExpresionLogica.Not(ExpresionLogica.Literal(senal));

        private static HashSet<string> SenalesPropias(PasoSecuencia paso)
        {
            var senales = new HashSet<string>();
            paso.ExpresionIlockAuto?.RecogerVariables(senales);
            paso.ExpresionIlockExtSync?.RecogerVariables(senales);
            paso.ExpresionTransAuto?.RecogerVariables(senales);
            return senales;
        }

        /// <summary>alwaysTrue / alwaysFalse tags are constants, not free signals.</summary>
        private static ExpresionLogica FijarConstantes(ExpresionLogica e)
        {
            var variables = new HashSet<string>();
            e.RecogerVariables(variables);
            foreach (var v in variables)
            {
                string nombre = v.Trim('"', '#');
                if (nombre.Equals("alwaysTrue", StringComparison.OrdinalIgnoreCase))
                    e = e.Asignar(v, true);
                else if (nombre.Equals("alwaysFalse", StringComparison.OrdinalIgnoreCase))
                    e = e.Asignar(v, false);
            }
            return e;
        }

        private ExpresionLogica Restricciones(ExpresionLogica condicion)
        {
            var variables = new HashSet<string>();
            condicion.RecogerVariables(variables);

            var restricciones = new List<ExpresionLogica>();
            if (ExcluirHomeWork)
                restricciones.AddRange(ExclusionesHomeWork(variables));
            if (ExcluirValoresDecodificados)
                restricciones.AddRange(ExclusionesValores(variables));
            return ExpresionLogica.And(restricciones);
        }

        private static IEnumerable<ExpresionLogica> ExclusionesValores(HashSet<string> variables)
        {
            var grupos = variables
                .Select(v => new { Variable = v, Match = _regValorDecodificado.Match(v) })
                .Where(x => x.Match.Success)
                .GroupBy(x => x.Match.Groups[1].Value.Trim().ToUpperInvariant())
                .Select(g => g.GroupBy(x => x.Match.Groups[2].Value.Trim()).Select(v => v.First().Variable).ToList())
                .Where(g => g.Count > 1);

            foreach (var grupo in grupos)
                for (int i = 0; i < grupo.Count; i++)
                    for (int j = i + 1; j < grupo.Count; j++)
                        yield return NoAmbas(grupo[i], grupo[j]);
        }

        private static IEnumerable<ExpresionLogica> ExclusionesHomeWork(HashSet<string> variables)
        {
            return variables
                .Select(v => new { Variable = v, Match = _regSensorActuador.Match(v) })
                .Where(x => x.Match.Success)
                .GroupBy(x => x.Match.Groups[1].Value.ToUpperInvariant())
                .Select(g => new
                {
                    Home = g.FirstOrDefault(x => x.Match.Groups[2].Value.ToUpperInvariant() == "H")?.Variable,
                    Work = g.FirstOrDefault(x => x.Match.Groups[2].Value.ToUpperInvariant() == "W")?.Variable
                })
                .Where(x => x.Home != null && x.Work != null)
                .Select(x => NoAmbas(x.Home, x.Work));
        }

        private static ExpresionLogica NoAmbas(string a, string b) =>
            ExpresionLogica.Not(ExpresionLogica.And(ExpresionLogica.Literal(a), ExpresionLogica.Literal(b)));
    }
}
