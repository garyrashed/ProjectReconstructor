using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectReconstructor.Extensions
{
    public static class CollectionExtensions
{
        public static string ConcatToString(this IEnumerable collection, string separator)
        {
            var builder = new StringBuilder();
            bool atLeastOneItemInCollection = false;
            foreach (var item in collection)
            {

                builder.Append($"{item}{separator}");
                atLeastOneItemInCollection = true;
            }
            var builderLength = builder.Length;
            var lengthOfSeperator = separator.Length;
            //remove the trailing seperator
            if(atLeastOneItemInCollection)
                builder.Remove(builderLength - lengthOfSeperator, lengthOfSeperator);
            return builder.ToString();
        }
        
        public static string CommaSeperatedString(this IEnumerable collection)
        {
            return collection.ConcatToString(", ");
        }
		
        public static IEnumerable<string> ToStringList<T>(this IEnumerable<T> list)
        {
            return
                list
                    .Select(item => item.ToString())
                    .ToList();
        }

        public static IEnumerable<T> ToIEnumerable<T>(this IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="propertiesToInclude"></param>
        /// <returns></returns>
        public static DataTable ToDataTable<T>(this IList<T> list, List<string> propertiesToInclude = null)
        {
            var properties =
                TypeDescriptor.GetProperties(typeof(T))
                    .Cast<PropertyDescriptor>()
                    .Where(prop => propertiesToInclude == null || propertiesToInclude.Contains(prop.Name))
                    .ToList();

            DataTable table = new DataTable();

            foreach (PropertyDescriptor prop in properties)
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);

            foreach (T item in list)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TK"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T Value<TK, T>(this Dictionary<TK, T> dict, TK key)
        {
            if (dict.ContainsKey(key))
                return dict[key];
            else
                return default(T);
        }

        //// TODO - improve this
        //public static T Previous<T>(this IEnumerable<T> list, T item)
        //    where T : class
        //{
        //    if (list == null || list.Count() <= 1 || item == null)
        //        return default(T);
        //    return list.TakeWhile(i => i != item).Last();
        //}
        public static T Previous<T>(this List<T> list, T item)
            where T : class
        {
            if (list == null || list.Count <= 1 || item == null)
                return default(T);

            int previousIndex = list.IndexOf(item) - 1;
            if (previousIndex < 0)
                return default(T);

            return list[previousIndex];
        }

        public static List<T> DoList<T>(this List<T> list, Action<List<T>> doSomething)
        {
            doSomething(list);
            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> list)
        {
            return new ObservableCollection<T>(list);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="list1"></param>
        /// <param name="newList"></param>
        /// <param name="match"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        public static List<T> Merge<T, TKey>(this List<T> list1, List<T> newList, Func<T, TKey> match, Action<T, T> update)
        {
            var matches =
                list1.Join(
                    newList,
                    i => match(i), i => match(i),
                    (i1, i2) => new { I1 = i1, I2 = i2 }
                )
                .ToList();

            matches
                .ForEach(m => update(m.I1, m.I2));

            var missing =
                newList
                    .Except(matches.Select(m => m.I2))
                    .ToList();

            list1
                .AddRange(missing);

            return list1;
        }

        public static int LastIndexWhere(this string[] arr, string pattern)
        {
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                if (Regex.IsMatch(arr[i], pattern))
                    return i;
            }

            return -1;
        }
        
        //public static List<T> Merge<T, TKey>(this List<T> list1, List<T> newList, Func<T, TKey> match, Action<T, T> update)
    }
}
