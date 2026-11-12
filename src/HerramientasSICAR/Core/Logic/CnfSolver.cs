#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Resuelve una fórmula pasándola a cláusulas (transformación de Tseitin: una variable auxiliar por cada Y/O)
// y buscando con DPLL con propagación unitaria. Así los interlocks con muchas ramas y saltos no hacen
// explotar la búsqueda, que es lo que pasaba probando combinaciones sobre el árbol.
namespace HerramientasSICAR.Core.Logic;

internal static class CnfSolver
{
    public static Dictionary<string, bool>? Solve(BoolFormula formula)
    {
        if (formula is BoolFormula.Const c)
        {
            return c.Value ? new Dictionary<string, bool>(StringComparer.Ordinal) : null;
        }

        var builder = new Builder();
        var root = builder.Encode(formula);
        builder.Clauses.Add([root]);

        var assignment = new int[builder.VariableCount + 1];
        if (!Search(builder.Clauses, assignment))
        {
            return null;
        }
        // Variables de la fórmula sin valor obligado: cualquiera vale; se dejan en 0.
        return builder.Names.ToDictionary(n => n.Key, n => assignment[n.Value] > 0, StringComparer.Ordinal);
    }

    // Literales como enteros: +v verdadero, -v falso. assignment[v]: 1 verdadero, -1 falso, 0 libre.
    private static bool Search(List<int[]> clauses, int[] assignment)
    {
        var trail = new List<int>();
        if (!Propagate(clauses, assignment, trail))
        {
            Undo(assignment, trail);
            return false;
        }

        var variable = PickVariable(clauses, assignment);
        if (variable == 0)
        {
            return true;
        }
        foreach (var value in new[] { 1, -1 })
        {
            assignment[variable] = value;
            if (Search(clauses, assignment))
            {
                return true;
            }
            assignment[variable] = 0;
        }
        Undo(assignment, trail);
        return false;
    }

    private static bool Propagate(List<int[]> clauses, int[] assignment, List<int> trail)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var clause in clauses)
            {
                var unassigned = 0;
                var free = 0;
                var satisfied = false;
                foreach (var literal in clause)
                {
                    var value = assignment[Math.Abs(literal)] * Math.Sign(literal);
                    if (value > 0)
                    {
                        satisfied = true;
                        break;
                    }
                    if (value == 0)
                    {
                        unassigned++;
                        free = literal;
                    }
                }
                if (satisfied)
                {
                    continue;
                }
                if (unassigned == 0)
                {
                    return false;
                }
                if (unassigned == 1)
                {
                    assignment[Math.Abs(free)] = Math.Sign(free);
                    trail.Add(Math.Abs(free));
                    changed = true;
                }
            }
        }
        while (changed);
        return true;
    }

    // La variable libre que más aparece en cláusulas aún no cumplidas.
    private static int PickVariable(List<int[]> clauses, int[] assignment)
    {
        var counts = new Dictionary<int, int>();
        foreach (var clause in clauses)
        {
            if (clause.Any(l => assignment[Math.Abs(l)] * Math.Sign(l) > 0))
            {
                continue;
            }
            foreach (var literal in clause)
            {
                var v = Math.Abs(literal);
                if (assignment[v] == 0)
                {
                    counts[v] = counts.GetValueOrDefault(v) + 1;
                }
            }
        }
        return counts.Count == 0 ? 0 : counts.MaxBy(c => c.Value).Key;
    }

    private static void Undo(int[] assignment, List<int> trail)
    {
        foreach (var v in trail)
        {
            assignment[v] = 0;
        }
    }

    private sealed class Builder
    {
        public Dictionary<string, int> Names { get; } = new(StringComparer.Ordinal);
        public List<int[]> Clauses { get; } = [];
        public int VariableCount { get; private set; }

        private readonly Dictionary<BoolFormula, int> _encoded = [];

        // Devuelve el literal que equivale a la fórmula, añadiendo las cláusulas que lo definen.
        public int Encode(BoolFormula formula)
        {
            switch (formula)
            {
                case BoolFormula.Variable v:
                    if (!Names.TryGetValue(v.Name, out var id))
                    {
                        id = ++VariableCount;
                        Names[v.Name] = id;
                    }
                    return id;
                case BoolFormula.Negation n:
                    return -Encode(n.Inner);
                case BoolFormula.Const c:
                    var constant = ++VariableCount;
                    Clauses.Add([c.Value ? constant : -constant]);
                    return constant;
            }

            if (_encoded.TryGetValue(formula, out var cached))
            {
                return cached;
            }
            var (items, isAnd) = formula switch
            {
                BoolFormula.Conjunction c => (c.Items, true),
                BoolFormula.Disjunction d => (d.Items, false),
                _ => throw new InvalidOperationException("Fórmula desconocida: " + formula),
            };
            var literals = items.Select(Encode).ToArray();
            var x = ++VariableCount;
            if (isAnd)
            {
                // x ⇔ (a ∧ b ∧ …)
                foreach (var l in literals)
                {
                    Clauses.Add([-x, l]);
                }
                Clauses.Add([x, .. literals.Select(l => -l)]);
            }
            else
            {
                // x ⇔ (a ∨ b ∨ …)
                foreach (var l in literals)
                {
                    Clauses.Add([x, -l]);
                }
                Clauses.Add([-x, .. literals]);
            }
            _encoded[formula] = x;
            return x;
        }
    }
}
