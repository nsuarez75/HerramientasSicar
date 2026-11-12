#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Fórmula booleana con variables opacas (cada señal o comparación es una variable por su texto), para
// preguntar si unas condiciones se pueden cumplir a la vez. Basada en ExpresionLogica de Sincro Checker.
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Logic;

public abstract record BoolFormula
{
    public static readonly BoolFormula True = new Const(true);
    public static readonly BoolFormula False = new Const(false);

    public static BoolFormula Var(string name) => new Variable(name);

    public static BoolFormula Not(BoolFormula f) => f switch
    {
        Negation n => n.Inner,
        Const c => c.Value ? False : True,
        _ => new Negation(f),
    };

    public static BoolFormula And(IEnumerable<BoolFormula> items)
    {
        var list = new List<BoolFormula>();
        foreach (var item in items)
        {
            switch (item)
            {
                case Const { Value: false }:
                    return False;
                case Const:
                    continue;
                case Conjunction c:
                    list.AddRange(c.Items);
                    break;
                default:
                    list.Add(item);
                    break;
            }
        }
        return list.Count switch { 0 => True, 1 => list[0], _ => new Conjunction(list) };
    }

    public static BoolFormula And(params BoolFormula[] items) => And((IEnumerable<BoolFormula>)items);

    public static BoolFormula Or(IEnumerable<BoolFormula> items)
    {
        var list = new List<BoolFormula>();
        foreach (var item in items)
        {
            switch (item)
            {
                case Const { Value: true }:
                    return True;
                case Const:
                    continue;
                case Disjunction d:
                    list.AddRange(d.Items);
                    break;
                default:
                    list.Add(item);
                    break;
            }
        }
        return list.Count switch { 0 => False, 1 => list[0], _ => new Disjunction(list) };
    }

    public static BoolFormula Or(params BoolFormula[] items) => Or((IEnumerable<BoolFormula>)items);

    // Pasa la lógica de una red a fórmula. Cada contacto es su señal; un comparador, una caja o un flanco
    // es una variable con su texto. Una entrada sin cable no conduce.
    public static BoolFormula From(LadExpression expression) => expression switch
    {
        PowerRail => True,
        Unconnected => False,
        SeriesExpression s => And(s.Items.Select(From)),
        ParallelExpression p => Or(p.Items.Select(From)),
        NotExpression n => Not(From(n.Inner)),
        XorExpression x => x.Items.Select(From).Aggregate((a, b) => Or(And(a, Not(b)), And(Not(a), b))),
        ContactExpression c => c.Negated ? Not(Var(c.Operand.Text)) : Var(c.Operand.Text),
        _ => Var(expression.ToString()),
    };

    // a ⇔ b
    public static BoolFormula Equivalent(BoolFormula a, BoolFormula b) => And(Or(Not(a), b), Or(a, Not(b)));

    // Sustituye una variable por un valor y simplifica.
    public abstract BoolFormula Assign(string variable, bool value);

    public abstract void CollectVariables(ISet<string> variables);

    public ISet<string> Variables()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        CollectVariables(set);
        return set;
    }

    // Busca valores de las variables que hagan verdadera la fórmula; null si es imposible. Solo devuelve
    // las variables de la fórmula (no las auxiliares del solver).
    public Dictionary<string, bool>? Solve() => CnfSolver.Solve(this);

    public bool IsSatisfiable => Solve() != null;

    public sealed record Const(bool Value) : BoolFormula
    {
        public override BoolFormula Assign(string variable, bool value) => this;
        public override void CollectVariables(ISet<string> variables) { }
        public override string ToString() => Value ? "1" : "0";
    }

    public sealed record Variable(string Name) : BoolFormula
    {
        public override BoolFormula Assign(string variable, bool value) => variable == Name ? (value ? True : False) : this;
        public override void CollectVariables(ISet<string> variables) => variables.Add(Name);
        public override string ToString() => Name;
    }

    public sealed record Negation(BoolFormula Inner) : BoolFormula
    {
        public override BoolFormula Assign(string variable, bool value) => Not(Inner.Assign(variable, value));
        public override void CollectVariables(ISet<string> variables) => Inner.CollectVariables(variables);
        public override string ToString() => "NO " + (Inner is Variable ? Inner : "(" + Inner + ")");
    }

    public sealed record Conjunction(IReadOnlyList<BoolFormula> Items) : BoolFormula
    {
        public override BoolFormula Assign(string variable, bool value) => And(Items.Select(i => i.Assign(variable, value)));
        public override void CollectVariables(ISet<string> variables)
        {
            foreach (var item in Items)
            {
                item.CollectVariables(variables);
            }
        }
        public override string ToString() => string.Join(" Y ", Items.Select(i => i is Disjunction ? "(" + i + ")" : i.ToString()));
    }

    public sealed record Disjunction(IReadOnlyList<BoolFormula> Items) : BoolFormula
    {
        public override BoolFormula Assign(string variable, bool value) => Or(Items.Select(i => i.Assign(variable, value)));
        public override void CollectVariables(ISet<string> variables)
        {
            foreach (var item in Items)
            {
                item.CollectVariables(variables);
            }
        }
        public override string ToString() => string.Join(" O ", Items);
    }
}
