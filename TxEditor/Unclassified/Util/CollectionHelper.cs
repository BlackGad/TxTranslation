using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Unclassified.Util
{
    public static class CollectionHelper
    {
        #region Static members

        /// <summary>
        ///     Uses the specified functions to add a key/value pair to the
        ///     <see cref="T:System.Collections.Generic.Dictionary`2" /> if the key does not already exist, or to update a
        ///     key/value pair in the <see cref="T:System.Collections.Generic.Dictionary`2" /> if the key already exists.
        /// </summary>
        /// <returns>
        ///     The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the
        ///     result of updateValueFactory (if the key was present).
        /// </returns>
        /// <param name="dictionary">Source <see cref="T:System.Collections.Generic.Dictionary`2" /> instance.</param>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">
        ///     The function used to generate a new value for an existing key based on the key's
        ///     existing value
        /// </param>
        public static void AddOrUpgrade<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
                                                      TKey key,
                                                      Func<TKey, TValue> addValueFactory,
                                                      Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, addValueFactory(key));
            }
            else
            {
                dictionary[key] = updateValueFactory(key, dictionary[key]);
            }
        }

        /// <summary>
        ///     Uses the specified functions to add a key/value pair to the
        ///     <see cref="T:System.Collections.Generic.Dictionary`2" /> if the key does not already exist, or to update a
        ///     key/value pair in the <see cref="T:System.Collections.Generic.Dictionary`2" /> if the key already exists.
        /// </summary>
        /// <returns>
        ///     The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the
        ///     result of updateValueFactory (if the key was present).
        /// </returns>
        /// <param name="dictionary">Source <see cref="T:System.Collections.Generic.Dictionary`2" /> instance.</param>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="value">Value.</param>
        public static void AddOrUpgrade<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
                                                      TKey key,
                                                      TValue value)
        {
            AddOrUpgrade(dictionary, key, k => value, (k, existing) => value);
        }

        /// <summary>
        ///     Enumerates <paramref name="object" /> as generic enumeration and cast it to target type.
        /// </summary>
        /// <param name="object">Source object.</param>
        /// <returns>
        ///     If <paramref name="object" /> is <see cref="IEnumerable" /> and enumeration is null returns empty enumeration,
        ///     source enumeration otherwise.
        /// </returns>
        public static IEnumerable<T> Enumerate<T>(this object @object)
        {
            var enumerable = @object as IEnumerable;
            return enumerable?.OfType<T>() ?? Enumerable.Empty<T>();
        }

        /// <summary>
        ///     Enumerates <paramref name="object" /> as generic enumeration.
        /// </summary>
        /// <param name="object">Source object.</param>
        /// <returns>
        ///     If <paramref name="object" /> is <see cref="IEnumerable" /> and enumeration is null returns empty enumeration,
        ///     source enumeration otherwise.
        /// </returns>
        public static IEnumerable<object> Enumerate(this object @object)
        {
            var enumerable = @object as IEnumerable;
            return enumerable.Enumerate<object>();
        }

        /// <summary>
        ///     Enumerates enumeration.
        /// </summary>
        /// <param name="enumerable">IEnumerable object.</param>
        /// <returns>If enumeration is null returns empty enumeration, source enumeration otherwise.</returns>
        public static IEnumerable<T> Enumerate<T>(this IEnumerable<T> enumerable)
        {
            return enumerable ?? Enumerable.Empty<T>();
        }

        /// <summary>
        ///     Gets existing value or adds new, adds and returns it.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <param name="dictionary">Source dictionary.</param>
        /// <param name="key">Key instance.</param>
        /// <param name="addFactory">Factory for new dictionary entry. Activator.CreateInstance if factory not set.</param>
        /// <returns>Dictionary entry bey key.</returns>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> addFactory = null)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            addFactory = addFactory ?? (localKey => (TValue)Activator.CreateInstance(typeof(TValue)));
            if (!dictionary.ContainsKey(key)) dictionary.Add(key, addFactory(key));
            return dictionary[key];
        }

        #endregion
    }
}