using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TrackPlanner.LinqExtensions
{
    public static class Linqer
    {
        public static T Max<T>(this T a, T b)
            where T : IComparable<T>
        {
            if (Comparer<T>.Default.Compare(a, b) < 0)
                return b;
            else
                return a;
        }
        public static IReadOnlyList<T> ReadOnlyList<T>(this IReadOnlyList<T> list) => list;
       
        // covariance helper 
        public static T Me<T>(this T t) => t;

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> colletion, params T[] elems)
        {
            return Enumerable.Concat(colletion,elems);    
        }

        public static Option<T> SingleOrNone<T>(this IEnumerable<T> enumerable)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext()) // no elements
                    return Option<T>.None;

                var result = iter.Current;
                
                if (iter.MoveNext()) // multiple elements
                    return Option<T>.None;

                return new Option<T>(result);
            }
        }

        public static int GetCapacity<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
            where TKey : notnull
        {
            var dict_type = dictionary.GetType();
            var buckets_field = dict_type.GetField("_buckets", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var buckets = buckets_field.GetValue(dictionary) as int[] ;
            return buckets!.Length;
        }

        public static T RemoveFirst<T>(this List<T> list)
        {
            var result = list[0];
            list.RemoveAt(0);
            return result;
        }

        public static T RemoveLast<T>(this List<T> list)
        {
            var result = list[^1];
            list.RemoveAt(list.Count-1);
            return result;
        }

        public static void Expand<T>(this List<T> list, int size)
        {
            if (size > list.Count)
            {
                if (size > list.Capacity)
                    list.Capacity = size;
                for (int i = size-list.Count; i >= 0; --i)
                    list.Add(default!);
            }
        }

        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> values)
        {
            foreach (T elem in values)
                set.Add(elem);
        }

        public static void RemoveExcept<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
            where TKey : notnull
        {
            bool has_key = dict.TryGetValue(key, out TValue? value);
            dict.Clear();
            if (has_key)
                dict.Add(key, value!);
        }
        
        /// <returns>true if there are more than single element</returns>
        public static bool HasMany<T>(this IEnumerable<T> enumerable)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    return false;
                return iter.MoveNext();
            }
        }

        public static IEnumerable<(T prev, T next)> Slide<T>(this IEnumerable<T> enumerable,bool wrapAround = false)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    yield break;
                
                var first = iter.Current;
                var prev = first;
                Option<T> last = Option<T>.None;
                
                while (iter.MoveNext())
                {
                    var curr = iter.Current;
                    yield return (prev, curr);
                    prev = curr;
                    last = new Option<T>( curr);
                }

                if (last.HasValue && wrapAround)
                    yield return (last.Value, first);
            }
        }

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> enumerable, T extra)
        {
            foreach (T elem in enumerable)
                yield return elem;
            yield return extra;
        }

        public static IEnumerable<(T item, int index)> ZipIndex<T>(this IEnumerable<T> enumerable)
        {
            int count = 0;
            foreach (T elem in enumerable)
                yield return (elem, count++);
        }

        public static IEnumerable<T> ConsecutiveDistinct<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.ConsecutiveDistinctBy(x => x);//;;EqualityComparer<T>.Default);
        }

        public static int IndexOf<T>(this IEnumerable<T> enumerable, T elem)
        {
            return enumerable.IndexOf(it => EqualityComparer<T>.Default.Equals(it, elem));
        }

        public static int IndexOf<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
        {
            int index = 0;
            foreach (T elem in enumerable)
            {
                if (predicate(elem))
                    return index;
                ++index;
            }

            return -1;
        }

        public static Option<T>  FirstOrNone<T>(this IEnumerable<T> enumerable)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    return Option<T>.None;
                else
                    return new Option<T>(iter.Current);
            }
        }

        public static void MinMax<T>(this IEnumerable<T> enumerable, out T min, out T max)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    throw new ArgumentException("No elements in collection");

                min = iter.Current;
                max = iter.Current;

                var comparer = Comparer<T>.Default;
                while (iter.MoveNext())
                {
                    {
                        int comp = comparer.Compare(min, iter.Current);
                        if (comp > 0)
                            min = iter.Current;
                    }
                    {
                        int comp = comparer.Compare(max, iter.Current);
                        if (comp < 0)
                            max = iter.Current;
                    }
                }
            }
        }

        private static IEnumerable<T> take<T>( IEnumerator<T> iter,int count)
        {
            while (count > 0 && iter.MoveNext())
            {
                yield return iter.Current;
                --count;
            }
        }
        
        public static IEnumerable<IEnumerable<TSource>> Partition<TSource>(this IEnumerable<TSource> enumerable, IEnumerable<int> sizes)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                foreach (var size in sizes)
                {
                    yield return take(iter, size);
                }
            }
        }
        
        public static IEnumerable<IEnumerable<TSource>> Partition<TSource,TKey>(this IEnumerable<TSource> enumerable,
            Func<TSource,TKey> keySelector)
        {
            return Partition(enumerable, keySelector, EqualityComparer<TKey>.Default);
        }

        public static IEnumerable<IEnumerable<TSource>> Partition<TSource,TKey>(this IEnumerable<TSource> enumerable,
           Func<TSource,TKey> keySelector, IEqualityComparer<TKey> comparer)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    yield break;

                var buffer = new List<TSource>();

                buffer.Add( iter.Current);

                while (iter.MoveNext())
                {
                    if (!comparer.Equals( keySelector(iter.Current),keySelector( buffer[^1])))
                    {
                        yield return buffer;
                        buffer = new List<TSource>(); // do not use clear!
                    }
                    buffer.Add( iter.Current);
                }

                yield return buffer;
            }
        }

        public static IEnumerable<TSource> ConsecutiveDistinctBy<TSource, TKey>(this IEnumerable<TSource> enumerable,
            Func<TSource, TKey> selector)//, IEqualityComparer<TSource> comparer)
        {
            using (var iter = enumerable.GetEnumerator())
            {
                if (!iter.MoveNext())
                    yield break;

                TKey? last_selected;

                yield return iter.Current;
                last_selected = selector(iter.Current);

                while (iter.MoveNext())
                {
                    TKey? current_selected = selector(iter.Current);
                    if (!object.Equals(current_selected, last_selected))
                    {
                        yield return iter.Current;
                        last_selected = current_selected;
                    }
                }
            }
        }

        /*public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> enumerable, Func<TSource, TKey> selector)
        {
            var seen = new HashSet<TKey>();

            using (var iter = enumerable.GetEnumerator())
            {
                while (iter.MoveNext())
                {
                    TKey key = selector(iter.Current);
                    if (seen.Add(key))
                        yield return iter.Current;
                }
            }
        }*/

        public static Dictionary<TKey, IReadOnlyList<TValue>> Intersect<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> first,
            IEnumerable<KeyValuePair<TKey, TValue>> second)
            where TKey : notnull
        {
            IReadOnlyList<TValue> caster(List<TValue> list) => list;

            return Intersect(first, second, (a, b) => caster(new List<TValue>() { a, b }));
        }

        public static Dictionary<TKey, TResult> Intersect<TKey, TValue, TResult>(IReadOnlyDictionary<TKey, TValue> first, IEnumerable<KeyValuePair<TKey, TValue>> second,
            Func<TValue, TValue, TResult> combine)
            where TKey : notnull
        {
            var result = new Dictionary<TKey, TResult>();
            foreach (var entry in second)
            {
                if (first.TryGetValue(entry.Key, out TValue? idx_along_road))
                {
                    result.Add(entry.Key, combine(idx_along_road, entry.Value));
                }
            }

            return result;
        }

    }
}