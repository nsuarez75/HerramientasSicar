#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Disposición de una expresión como escalón de ladder, en una rejilla: lo que va en serie se pone en la misma
// fila y las ramas en paralelo, una debajo de otra. No sabe nada de dibujar: la usan la vista previa de la app y
// sirve también para las vistas de Ignition.
namespace HerramientasSICAR.Nucleo.Lad;

// Instrucción en la rejilla: ocupa de la columna Column a la siguiente, en la fila Row.
public sealed record LadCell(int Column, int Row, LadExpression Leaf);

// Tramo de cable entre dos nudos de la rejilla (horizontal o vertical).
public sealed record LadWire(int Column1, int Row1, int Column2, int Row2);

public sealed class LadLayout
{
    private readonly List<LadCell> _cells = [];
    private readonly List<LadWire> _wires = [];

    private LadLayout()
    {
    }

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public IReadOnlyList<LadCell> Cells => _cells;
    public IReadOnlyList<LadWire> Wires => _wires;

    public static LadLayout Build(LadExpression expression)
    {
        var layout = new LadLayout();
        (layout.Columns, layout.Rows) = Measure(expression);
        layout.Rows = Math.Max(layout.Rows, 1);
        layout.Place(expression, 0, 0);
        return layout;
    }

    private static (int Width, int Height) Measure(LadExpression expression)
    {
        switch (expression)
        {
            case PowerRail:
                return (0, 1);
            case SeriesExpression series:
                var parts = series.Items.Select(Measure).ToList();
                return (parts.Sum(p => p.Width), parts.Max(p => p.Height));
            case ParallelExpression parallel:
                var branches = parallel.Items.Select(Measure).ToList();
                return (branches.Max(p => p.Width), branches.Sum(p => p.Height));
            case NotExpression { Inner: ContactExpression }:
                return (1, 1);
            case NotExpression not:
                var inner = Measure(not.Inner);
                return (inner.Width + 1, inner.Height);
            case EdgeBox edge:
                var input = Measure(edge.Input);
                return (input.Width + 1, input.Height);
            default:
                return (1, 1);
        }
    }

    private void Place(LadExpression expression, int column, int row)
    {
        switch (expression)
        {
            case PowerRail:
                break;
            case SeriesExpression series:
                foreach (var item in series.Items)
                {
                    Place(item, column, row);
                    column += Measure(item).Width;
                }
                break;
            case ParallelExpression parallel:
                var width = Measure(parallel).Width;
                var y = row;
                var last = row;
                foreach (var item in parallel.Items)
                {
                    Place(item, column, y);
                    var (itemWidth, itemHeight) = Measure(item);
                    if (itemWidth < width)
                    {
                        _wires.Add(new LadWire(column + itemWidth, y, column + width, y));
                    }
                    last = y;
                    y += itemHeight;
                }
                _wires.Add(new LadWire(column, row, column, last));
                _wires.Add(new LadWire(column + width, row, column + width, last));
                break;
            case NotExpression { Inner: ContactExpression contact }:
                _cells.Add(new LadCell(column, row, contact with { Negated = !contact.Negated }));
                break;
            case NotExpression not:
                Place(not.Inner, column, row);
                _cells.Add(new LadCell(column + Measure(not.Inner).Width, row, new UnknownExpression("NOT")));
                break;
            case EdgeBox edge:
                Place(edge.Input, column, row);
                _cells.Add(new LadCell(column + Measure(edge.Input).Width, row, new UnknownExpression(edge.Rising ? "P_TRIG" : "N_TRIG")));
                break;
            default:
                _cells.Add(new LadCell(column, row, expression));
                break;
        }
    }
}
