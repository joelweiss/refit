﻿using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Refit
{
    class FormValueDictionary : IEnumerable<KeyValuePair<string, string>>
    {
        static readonly Dictionary<Type, PropertyInfo[]> propertyCache
            = new Dictionary<Type, PropertyInfo[]>();

        private readonly IList<KeyValuePair<string, string>> formEntries = new List<KeyValuePair<string, string>>();

        public FormValueDictionary(object source, RefitSettings settings)
        {
            if (source == null) return;

            if (source is IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys)
                {
                    var value = dictionary[key];
                    if (value != null) {
                        Add(key.ToString(), settings.FormUrlEncodedParameterFormatter.Format(value, null));
                    }
                }

                return;
            }

            var type = source.GetType();

            lock (propertyCache)
            {
                if (!propertyCache.ContainsKey(type))
                {
                    propertyCache[type] = GetProperties(type);
                }

                foreach (var property in propertyCache[type])
                {
                    var value = property.GetValue(source, null);
                    if (value != null)
                    {
                        var fieldName = GetFieldNameForProperty(property);

                        // see if there's a query attribute
                        var attrib = property.GetCustomAttribute<QueryAttribute>(true);

                        if (value is IEnumerable enumerable) {
                            switch (attrib?.CollectionFormat) {
                                case CollectionFormat.Multi:
                                    foreach (var item in enumerable) {
                                        Add(fieldName, settings.FormUrlEncodedParameterFormatter.Format(item, null));
                                    }
                                    break;
                                case CollectionFormat.Csv:
                                case CollectionFormat.Ssv:
                                case CollectionFormat.Tsv:
                                case CollectionFormat.Pipes:
                                    var delimiter = attrib.CollectionFormat == CollectionFormat.Csv ?  ","
                                        : attrib.CollectionFormat == CollectionFormat.Ssv ? " "
                                        : attrib.CollectionFormat == CollectionFormat.Tsv ? "\t" : "|";

                                    var formattedValues = enumerable
                                        .Cast<object>()
                                        .Select(v => settings.FormUrlEncodedParameterFormatter.Format(v, null));
                                    Add(fieldName, string.Join(delimiter, formattedValues));
                                    break;
                                default:
                                    Add(fieldName, settings.FormUrlEncodedParameterFormatter.Format(value, attrib?.Format));
                                    break;
                            }
                        }
                        else
                        {
                            Add(fieldName, settings.FormUrlEncodedParameterFormatter.Format(value, attrib?.Format));
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Returns a key for each entry. If multiple entries share the same key, the key is returned multiple times.
        /// </summary>
        public IEnumerable<string> Keys => this.Select(it => it.Key);

        /// <summary>
        /// Returns the value of the first entry found with the matching key. Multiple additional entries may use the
        /// same key, but will not be considered. Returns <c>null</c> if no matching entry is found.
        /// </summary>
        public string this[string key]
        {
            get
            {
                foreach (var item in this) {
                    if (key == item.Key) {
                        return item.Value;
                    }
                }

                return null;
            }
        }

        private void Add(string key, string value)
        {
            formEntries.Add(new KeyValuePair<string, string>(key, value));
        }

        string GetFieldNameForProperty(PropertyInfo propertyInfo)
        {
            var name = propertyInfo.GetCustomAttributes<AliasAsAttribute>(true)
                               .Select(a => a.Name)
                               .FirstOrDefault()
                   ?? propertyInfo.GetCustomAttributes<JsonPropertyAttribute>(true)
                                  .Select(a => a.PropertyName)
                                  .FirstOrDefault()
                   ?? propertyInfo.Name;

            var qattrib = propertyInfo.GetCustomAttributes<QueryAttribute>(true)
                           .Select(attr => !string.IsNullOrWhiteSpace(attr.Prefix) ? $"{attr.Prefix}{attr.Delimiter}{name}" : name)
                           .FirstOrDefault();

            return qattrib ?? name;
        }

        PropertyInfo[] GetProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                       .Where(p => p.CanRead && p.GetMethod.IsPublic)
                       .ToArray();
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return formEntries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
