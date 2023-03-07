using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SolcNet
{
    public static class EncodingUtils
    {
        static readonly Encoding UTF8_ENCODING = new UTF8Encoding(false, false);

        public static ValueTuple<T1, T2, T3>[] Flatten<T1, T2, T3>(this Dictionary<T1, Dictionary<T2, T3>> dicts)
        {
            return FlattenNestedDictionaries(dicts);
        }

        public static ValueTuple<T1, T2, T3>[] FlattenNestedDictionaries<T1, T2, T3>(Dictionary<T1, Dictionary<T2, T3>> dicts)
        {
            var items = new List<(T1, T2, T3)>();
            foreach (var kp in dicts)
            {
                foreach (var c in kp.Value)
                {
                    items.Add((kp.Key, c.Key, c.Value));
                }
            }
            return items.ToArray();
        }

    }
}
