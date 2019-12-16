using MediaSet.Data;
using System;
using System.Collections.Generic;

namespace MediaSet.Import
{
    public static class DictionaryExtensions
    {
        public static void AddIfDoesNotExist<T>(this IDictionary<string, T> lookup, string name) where T : EntityAbstract, new()
        {
            if (!string.IsNullOrWhiteSpace(name) && !lookup.ContainsKey(name))
            {
                lookup.Add(name, new T { Name = name });
            }
        }

        public static void AddIfDoesNotExist<T>(this IDictionary<string, T> lookup, string name, Func<T> objectCreationFunction) where T : EntityAbstract, new()
        {
            if (!string.IsNullOrWhiteSpace(name) && !lookup.ContainsKey(name))
            {
                lookup.Add(name, objectCreationFunction());
            }
        }
    }
}
