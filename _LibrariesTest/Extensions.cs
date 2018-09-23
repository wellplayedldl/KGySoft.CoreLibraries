﻿using System;
using System.Collections;
using System.Collections.Generic;
using KGySoft;

namespace _LibrariesTest
{
    internal static class Extensions
    {
        /// <summary>
        /// Creates an <see cref="IEnumerable{T}"/> of <see cref="DictionaryEntry"/> elements from an <see cref="IDictionaryEnumerator"/>.
        /// </summary>
        /// <param name="enumerator">The <see cref="IDictionaryEnumerator"/> to create an <see cref="IEnumerable{T}"/> from.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> that enumerates the elements of the input <paramref name="enumerator"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="enumerator"/> is <see langword="null"/>.</exception>
        /// <remarks><note type="caution">Unlike the usual <see cref="IEnumerable{T}"/> implementations, the result of this method cannot be enumerated more than once.</note></remarks>
        public static IEnumerable<DictionaryEntry> ToEnumerable(this IDictionaryEnumerator enumerator)
        {
            if (enumerator == null)
                throw new ArgumentNullException(nameof(enumerator), Res.Get(Res.ArgumentNull));

            while (enumerator.MoveNext())
                yield return enumerator.Entry;
        }

        /// <summary>
        /// Creates an <see cref="IEnumerable{T}"/> <see cref="KeyValuePair{TKey,TValue}"/> elements from an <see cref="IDictionaryEnumerator"/>.
        /// </summary>
        /// <typeparam name="TKey">The type of the key elements of the <paramref name="enumerator"/>.</typeparam>
        /// <typeparam name="TValue">The type of the value elements of the <paramref name="enumerator"/>.</typeparam>
        /// <param name="enumerator">The <see cref="IDictionaryEnumerator"/> to create an <see cref="IEnumerable{DictionaryEntry}"/> from.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> that enumerates the elements of the input <paramref name="enumerator"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="enumerator"/> is <see langword="null"/>.</exception>
        /// <remarks><note type="caution">Unlike the usual <see cref="IEnumerable{T}"/> implementations, the result of this method cannot be enumerated more than once.</note></remarks>
        public static IEnumerable<KeyValuePair<TKey, TValue>> ToEnumerable<TKey, TValue>(this IDictionaryEnumerator enumerator)
        {
            if (enumerator == null)
                throw new ArgumentNullException(nameof(enumerator), Res.Get(Res.ArgumentNull));

            while (enumerator.MoveNext())
                yield return new KeyValuePair<TKey, TValue>((TKey)enumerator.Key, (TValue)enumerator.Value);
        }
    }
}
