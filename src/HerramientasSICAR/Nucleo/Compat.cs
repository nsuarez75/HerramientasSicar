// El núcleo viene de GST-Diag, escrito para .NET 10. Aquí está lo que .NET Framework 4.8 no trae y el núcleo
// usa, con el mismo comportamiento, para poder copiar los archivos de GST-Diag casi sin cambios.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.Nucleo;

internal static class Compat
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull =>
        dictionary.TryGetValue(key, out var value) ? value : default;

    public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source) => source.OrderBy(x => x);

    public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source, IComparer<T> comparer) => source.OrderBy(x => x, comparer);

    // Como en .NET: el primero con la clave mayor; default si no hay ninguno.
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> key)
    {
        var comparer = Comparer<TKey>.Default;
        var found = false;
        TSource? best = default;
        TKey? bestKey = default;
        foreach (var item in source)
        {
            var k = key(item);
            if (!found || comparer.Compare(k, bestKey!) > 0)
            {
                found = true;
                best = item;
                bestKey = k;
            }
        }
        return best;
    }
}
