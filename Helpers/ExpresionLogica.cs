using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.Helpers
{
    /// <summary>
    /// Boolean expression rebuilt from a LAD network. Operands are opaque variables
    /// identified by their text, so the same tag in two steps is the same variable.
    /// </summary>
    public abstract class ExpresionLogica
    {
        public static readonly ExpresionLogica True = new Constante(true);
        public static readonly ExpresionLogica False = new Constante(false);

        public static ExpresionLogica Literal(string texto) => new Variable(texto);

        public static ExpresionLogica Not(ExpresionLogica e)
        {
            if (e is Negacion n) return n.Operando;
            if (e is Constante c) return c.Valor ? False : True;
            return new Negacion(e);
        }

        public static ExpresionLogica And(params ExpresionLogica[] operandos) => And((IEnumerable<ExpresionLogica>)operandos);

        public static ExpresionLogica And(IEnumerable<ExpresionLogica> operandos)
        {
            var lista = new List<ExpresionLogica>();
            foreach (var o in operandos)
            {
                if (o is Constante c)
                {
                    if (!c.Valor) return False;
                    continue;
                }
                if (o is Conjuncion y) lista.AddRange(y.Operandos);
                else lista.Add(o);
            }
            if (lista.Count == 0) return True;
            return lista.Count == 1 ? lista[0] : new Conjuncion(lista);
        }

        public static ExpresionLogica Or(params ExpresionLogica[] operandos) => Or((IEnumerable<ExpresionLogica>)operandos);

        public static ExpresionLogica Or(IEnumerable<ExpresionLogica> operandos)
        {
            var lista = new List<ExpresionLogica>();
            foreach (var o in operandos)
            {
                if (o is Constante c)
                {
                    if (c.Valor) return True;
                    continue;
                }
                if (o is Disyuncion d) lista.AddRange(d.Operandos);
                else lista.Add(o);
            }
            if (lista.Count == 0) return False;
            return lista.Count == 1 ? lista[0] : new Disyuncion(lista);
        }

        /// <summary>Replaces one variable by a constant and simplifies.</summary>
        public abstract ExpresionLogica Asignar(string variable, bool valor);

        public abstract void RecogerVariables(ISet<string> variables);

        /// <summary>Evaluates with the given signal state; missing signals count as 0.</summary>
        public abstract bool Evaluar(IDictionary<string, bool> estado);

        public bool EsConstante(out bool valor)
        {
            valor = this is Constante c && c.Valor;
            return this is Constante;
        }

        /// <summary>
        /// Searches an assignment that makes the expression true (DPLL over the tree).
        /// Returns null when the expression can never be true.
        /// </summary>
        public Dictionary<string, bool> BuscarSolucion()
        {
            var asignacion = new Dictionary<string, bool>();
            return Satisfacer(this, asignacion) ? asignacion : null;
        }

        private static bool Satisfacer(ExpresionLogica e, Dictionary<string, bool> asignacion)
        {
            if (e is Constante c)
                return c.Valor;

            // Literals at the top level of an AND are forced (unit propagation)
            if (LiteralForzado(e, out string forzada, out bool valorForzado))
            {
                asignacion[forzada] = valorForzado;
                if (Satisfacer(e.Asignar(forzada, valorForzado), asignacion))
                    return true;
                asignacion.Remove(forzada);
                return false;
            }

            var variables = new HashSet<string>();
            e.RecogerVariables(variables);
            string v = variables.First();

            foreach (bool valor in new[] { true, false })
            {
                asignacion[v] = valor;
                if (Satisfacer(e.Asignar(v, valor), asignacion))
                    return true;
                asignacion.Remove(v);
            }
            return false;
        }

        private static bool LiteralForzado(ExpresionLogica e, out string variable, out bool valor)
        {
            if (e is Conjuncion y)
            {
                foreach (var o in y.Operandos)
                    if (LiteralForzado(o, out variable, out valor))
                        return true;
            }
            else if (e is Variable v)
            {
                variable = v.Texto;
                valor = true;
                return true;
            }
            else if (e is Negacion n && n.Operando is Variable nv)
            {
                variable = nv.Texto;
                valor = false;
                return true;
            }

            variable = null;
            valor = false;
            return false;
        }

        public sealed class Constante : ExpresionLogica
        {
            public readonly bool Valor;
            public Constante(bool valor) { Valor = valor; }
            public override ExpresionLogica Asignar(string variable, bool valor) => this;
            public override void RecogerVariables(ISet<string> variables) { }
            public override bool Evaluar(IDictionary<string, bool> estado) => Valor;
            public override string ToString() => Valor ? "TRUE" : "FALSE";
        }

        public sealed class Variable : ExpresionLogica
        {
            public readonly string Texto;
            public Variable(string texto) { Texto = texto; }
            public override ExpresionLogica Asignar(string variable, bool valor) =>
                variable == Texto ? (valor ? True : False) : this;
            public override void RecogerVariables(ISet<string> variables) => variables.Add(Texto);
            public override bool Evaluar(IDictionary<string, bool> estado) => estado.TryGetValue(Texto, out bool v) && v;
            public override string ToString() => Texto;
        }

        public sealed class Negacion : ExpresionLogica
        {
            public readonly ExpresionLogica Operando;
            public Negacion(ExpresionLogica operando) { Operando = operando; }
            public override ExpresionLogica Asignar(string variable, bool valor) => Not(Operando.Asignar(variable, valor));
            public override void RecogerVariables(ISet<string> variables) => Operando.RecogerVariables(variables);
            public override bool Evaluar(IDictionary<string, bool> estado) => !Operando.Evaluar(estado);
            public override string ToString() =>
                Operando is Variable ? "NOT " + Operando : "NOT (" + Operando + ")";
        }

        public sealed class Conjuncion : ExpresionLogica
        {
            public readonly List<ExpresionLogica> Operandos;
            public Conjuncion(List<ExpresionLogica> operandos) { Operandos = operandos; }
            public override ExpresionLogica Asignar(string variable, bool valor) => And(Operandos.Select(o => o.Asignar(variable, valor)));
            public override void RecogerVariables(ISet<string> variables) { foreach (var o in Operandos) o.RecogerVariables(variables); }
            public override bool Evaluar(IDictionary<string, bool> estado) => Operandos.All(o => o.Evaluar(estado));
            public override string ToString() =>
                string.Join(" AND ", Operandos.Select(o => o is Disyuncion ? "(" + o + ")" : o.ToString()));
        }

        public sealed class Disyuncion : ExpresionLogica
        {
            public readonly List<ExpresionLogica> Operandos;
            public Disyuncion(List<ExpresionLogica> operandos) { Operandos = operandos; }
            public override ExpresionLogica Asignar(string variable, bool valor) => Or(Operandos.Select(o => o.Asignar(variable, valor)));
            public override void RecogerVariables(ISet<string> variables) { foreach (var o in Operandos) o.RecogerVariables(variables); }
            public override bool Evaluar(IDictionary<string, bool> estado) => Operandos.Any(o => o.Evaluar(estado));
            public override string ToString() => string.Join(" OR ", Operandos);
        }
    }
}
