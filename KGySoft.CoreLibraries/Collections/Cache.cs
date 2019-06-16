#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: Cache.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2018 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;

using KGySoft.Annotations;
using KGySoft.CoreLibraries;
using KGySoft.Diagnostics;
using KGySoft.Reflection;

#endregion

namespace KGySoft.Collections
{
    /// <summary>
    /// Represents a generic cache. If an item loader is specified, then cache expansion is transparent: the user needs only to read the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> to retrieve items.
    /// When a non-existing key is accessed, then the item is loaded automatically by the loader function that was passed to the
    /// <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.
    /// If the cache is full (elements <see cref="Count"/> reaches the <see cref="Capacity"/>) and a new element has to be stored, then
    /// the oldest or least recent used element (depends on the value of <see cref="Behavior"/>) is removed from the cache.
    /// <br/>See the <strong>Remarks</strong> section for details and an example.
    /// </summary>
    /// <typeparam name="TKey">Type of the keys stored in the cache.</typeparam>
    /// <typeparam name="TValue">Type of the values stored in the cache.</typeparam>
    /// <remarks>
    /// <para><see cref="Cache{TKey,TValue}"/> type provides a fast-access storage with limited capacity and transparent access. If you need to store
    /// items that are expensive to retrieve (for example from a database or remote service) and you don't want to run out of memory because of
    /// just storing newer and newer elements without getting rid of old ones, then this type might fit your expectations.
    /// Once a value is stored in the cache, its retrieval by using its key is very fast, close to O(1).</para>
    /// <para>A cache store must meet the following three criteria:
    /// <list type="number">
    /// <item><term>Associative access</term><description>Accessing elements works the same way as in case of the <see cref="Dictionary{TKey,TValue}"/> type.
    /// <see cref="Cache{TKey,TValue}"/> implements both the generic <see cref="IDictionary{TKey,TValue}"/> and the non-generic <see cref="IDictionary"/> interfaces so can be
    /// used similarly as <see cref="Dictionary{TKey,TValue}"/> or <see cref="Hashtable"/> types.</description></item>
    /// <item><term>Transparency</term><description>Users of the cache need only to read the cache by its <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> property.
    /// If needed, elements will be automatically loaded on the first access.</description></item>
    /// <item><term>Size management</term><description><see cref="Cache{TKey,TValue}"/> type has a <see cref="Capacity"/>, which is the allowed maximal elements count. If the cache is full, the
    /// oldest or least recent used element will be automatically removed from the cache (see <see cref="Behavior"/> property).</description></item>
    /// </list></para>
    /// <para>Since <see cref="Cache{TKey,TValue}"/> implements <see cref="IDictionary{TKey,TValue}"/> interface, <see cref="Add">Add</see>, <see cref="Remove">Remove</see>, <see cref="ContainsKey">ContainsKey</see> and
    /// <see cref="TryGetValue">TryGetValue</see> methods are available for it, and these methods work exactly the same way as in case the <see cref="Dictionary{TKey,TValue}"/> type. But using these methods
    /// usually are not necessary, unless we want to manually manage cache content or when cache is initialized without an item loader. Normally after cache is instantiated,
    /// it is needed to be accessed only by the getter accessor of its indexer.</para>
    /// <note type="caution">
    /// Serializing a cache instance by <see cref="IFormatter"/> implementations involves the serialization of the item loader delegate. To deserialize a cache the assembly of the loader must be accessible. If you need to
    /// serialize cache instances try to use static methods as data loaders and avoid using anonymous delegates or lambda expressions, otherwise it is not guaranteed that another
    /// implementations or versions of CLR will able to deserialize data and resolve the compiler-generated members.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false">Members of this type are not safe for multi-threaded operations, though a thread-safe accessor can be obtained for the <see cref="Cache{TKey,TValue}"/>
    /// by the <see cref="GetThreadSafeAccessor">GetThreadSafeAccessor</see> method. To get a thread-safe wrapper for all members use the
    /// <see cref="DictionaryExtensions.AsThreadSafe{TKey,TValue}">AsThreadSafe</see> extension method instead.
    /// <note>If a <see cref="Cache{TKey,TValue}"/> instance is wrapped into a <see cref="LockingDictionary{TKey, TValue}"/> instance, then the whole cache will be locked during the time when the item loader delegate is being called.
    /// If that is not desirable consider to use the <see cref="GetThreadSafeAccessor">GetThreadSafeAccessor</see> method instead with the default arguments and access the cache only via the returned accessor.</note>
    /// </threadsafety>
    /// <example>
    /// The following example shows the suggested usage of <see cref="Cache{TKey,TValue}"/>.
    /// <code lang="C#"><![CDATA[
    /// using System;
    /// using System.Collections.Generic;
    /// using KGySoft.Collections;
    /// 
    /// class Example
    /// {
    ///     private static Cache<int, bool> isPrimeCache;
    /// 
    ///     public static void Main()
    ///     {
    ///         // Cache capacity is initialized to store maximum 4 values
    ///         isPrimeCache = new Cache<int, bool>(ItemLoader, 4);
    /// 
    ///         // If cache is full the least recent used element will be deleted
    ///         isPrimeCache.Behavior = CacheBehavior.RemoveLeastRecentUsedElement;
    /// 
    ///         // cache is now empty
    ///         DumpCache();
    /// 
    ///         // reading the cache invokes the loader method
    ///         CheckPrime(13);
    /// 
    ///         // reading a few more values
    ///         CheckPrime(23);
    ///         CheckPrime(33);
    ///         CheckPrime(43);
    /// 
    ///         // dumping content
    ///         DumpCache();
    /// 
    ///         // accessing an already read item does not invoke loader again
    ///         // Now it changes cache order because of the chosen behavior
    ///         CheckPrime(13);
    ///         DumpCache();
    /// 
    ///         // reading a new element with full cache will delete an old one (now 23)
    ///         CheckPrime(111);
    ///         DumpCache();
    /// 
    ///         // but accessing a deleted element causes to load it again
    ///         CheckPrime(23);
    ///         DumpCache();
    /// 
    ///         // dumping some statistics
    ///         Console.WriteLine(isPrimeCache.GetStatistics().ToString());
    ///     }
    /// 
    ///     // This is the item loader method. It can access database or perform slow calculations.
    ///     // If cache is meant to be serialized it should be a static method rather than an anonymous delegate or lambda expression.
    ///     private static bool ItemLoader(int number)
    ///     {
    ///         Console.WriteLine("Item loading has been invoked for value {0}", number);
    /// 
    ///         // In this example item loader checks whether the given number is a prime by a not too efficient algorithm.
    ///         if (number <= 1)
    ///             return false;
    ///         if (number % 2 == 0)
    ///             return true;
    ///         int i = 3;
    ///         int sqrt = (int)Math.Floor(Math.Sqrt(number));
    ///         while (i <= sqrt)
    ///         {
    ///             if (number % i == 0)
    ///                 return false;
    ///             i += 2;
    ///         }
    /// 
    ///         return true;
    ///     }
    /// 
    ///     private static void CheckPrime(int number)
    ///     {
    ///         // cache is used transparently here: indexer is always just read
    ///         bool isPrime = isPrimeCache[number];
    ///         Console.WriteLine("{0} is a prime: {1}", number, isPrime);
    ///     }
    /// 
    ///     private static void DumpCache()
    ///     {
    ///         Console.WriteLine();
    ///         Console.WriteLine("Cache elements count: {0}", isPrimeCache.Count);
    ///         if (isPrimeCache.Count > 0)
    ///         {
    ///             // enumerating through the cache shows the elements in the evaluation order
    ///             Console.WriteLine("Cache elements:");
    ///             foreach (KeyValuePair<int, bool> item in isPrimeCache)
    ///             {
    ///                 Console.WriteLine("\tKey: {0},\tValue: {1}", item.Key, item.Value);
    ///             }
    ///         }
    /// 
    ///         Console.WriteLine();
    ///     }
    /// }
    /// 
    /// // This code example produces the following output:
    /// //
    /// // Cache elements count: 0
    /// // 
    /// // Item loading has been invoked for value 13
    /// // 13 is a prime: True
    /// // Item loading has been invoked for value 23
    /// // 23 is a prime: True
    /// // Item loading has been invoked for value 33
    /// // 33 is a prime: False
    /// // Item loading has been invoked for value 43
    /// // 43 is a prime: True
    /// // 
    /// // Cache elements count: 4
    /// // Cache elements:
    /// // Key: 13,        Value: True
    /// // Key: 23,        Value: True
    /// // Key: 33,        Value: False
    /// // Key: 43,        Value: True
    /// // 
    /// // 13 is a prime: True
    /// // 
    /// // Cache elements count: 4
    /// // Cache elements:
    /// // Key: 23,        Value: True
    /// // Key: 33,        Value: False
    /// // Key: 43,        Value: True
    /// // Key: 13,        Value: True
    /// // 
    /// // Item loading has been invoked for value 111
    /// // 111 is a prime: False
    /// // 
    /// // Cache elements count: 4
    /// // Cache elements:
    /// // Key: 33,        Value: False
    /// // Key: 43,        Value: True
    /// // Key: 13,        Value: True
    /// // Key: 111,       Value: False
    /// // 
    /// // Item loading has been invoked for value 23
    /// // 23 is a prime: True
    /// // 
    /// // Cache elements count: 4
    /// // Cache elements:
    /// // Key: 43,        Value: True
    /// // Key: 13,        Value: True
    /// // Key: 111,       Value: False
    /// // Key: 23,        Value: True
    /// // 
    /// // Cache<Int32, Boolean> cache statistics:
    /// // Count: 4
    /// // Capacity: 4
    /// // Number of writes: 6
    /// // Number of reads: 7
    /// // Number of cache hits: 1
    /// // Number of deletes: 2
    /// // Hit rate: 14,29%]]></code></example>
    /// <seealso cref="CacheBehavior"/>
    [Serializable]
    [DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {" + nameof(Count) + "}; TKey = {typeof(" + nameof(TKey) + ")}; TValue = {typeof(" + nameof(TValue) + ")}; Hit = {" + nameof(Cache<_, _>.GetStatistics) + "()." + nameof(ICacheStatistics.HitRate) + " * 100}%")]
    public class Cache<TKey, TValue> : IDictionary<TKey, TValue>, ICache, ISerializable, IDeserializationCallback
#if !(NET35 || NET40)
        , IReadOnlyDictionary<TKey, TValue>
#endif
    {
        #region Nested Types

        #region Nested classes

        #region Enumerator class

        /// <summary>
        /// Enumerates the elements of a <see cref="Cache{TKey,TValue}"/> instance in the evaluation order.
        /// </summary>
        /// <seealso cref="Cache{TKey,TValue}"/>
        [Serializable]
        private sealed class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
        {
            #region Fields

            private readonly Cache<TKey, TValue> cache;
            private readonly int version;
            private readonly bool isGeneric;

            private int position;
            private int currentIndex;
            private KeyValuePair<TKey, TValue> current;

            #endregion

            #region Properties

            #region Public Properties

            public KeyValuePair<TKey, TValue> Current => current;

            #endregion

            #region Explicitly Implemented Interface Properties

            object IEnumerator.Current
            {
                get
                {
                    if (position == -1 || position == cache.Count)
                        throw new InvalidOperationException(Res.IEnumeratorEnumerationNotStartedOrFinished);
                    if (isGeneric)
                        return current;
                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (position == -1 || position == cache.Count)
                        throw new InvalidOperationException(Res.IEnumeratorEnumerationNotStartedOrFinished);
                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (position == -1 || position == cache.Count)
                        throw new InvalidOperationException(Res.IEnumeratorEnumerationNotStartedOrFinished);
                    return current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (position == -1 || position == cache.Count)
                        throw new InvalidOperationException(Res.IEnumeratorEnumerationNotStartedOrFinished);
                    return current.Value;
                }
            }

            #endregion

            #endregion

            #region Constructors

            internal Enumerator(Cache<TKey, TValue> cache, bool isGeneric)
            {
                this.cache = cache;
                version = cache.version;
                this.isGeneric = isGeneric;
                position = -1;
                currentIndex = -1;
            }

            #endregion

            #region Methods

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (version != cache.version)
                    throw new InvalidOperationException(Res.IEnumeratorCollectionModified);

                if (position < cache.Count)
                    position++;

                if (position == cache.Count)
                    return false;

                if (position == 0)
                {
                    Debug.Assert(cache.first >= 0);
                    currentIndex = cache.first;
                }
                else
                {
                    Debug.Assert(cache.items[currentIndex].NextInOrder >= 0, "Next element not found");
                    currentIndex = cache.items[currentIndex].NextInOrder;
                }

                current = new KeyValuePair<TKey, TValue>(cache.items[currentIndex].Key, cache.items[currentIndex].Value);
                return true;
            }

            public void Reset()
            {
                if (version != cache.version)
                    throw new InvalidOperationException(Res.IEnumeratorCollectionModified);
                position = -1;
                currentIndex = -1;
                current = default;
            }

            #endregion
        }

        #endregion

        #region CacheStatistics class

        /// <summary>
        /// Retrieves statistics of a <see cref="Cache{TKey,TValue}"/> instance.
        /// </summary>
        [Serializable]
        private sealed class CacheStatistics : ICacheStatistics
        {
            #region Fields

            readonly Cache<TKey, TValue> owner;

            #endregion

            #region Properties

            public int Reads => owner.cacheReads;

            public int Writes => owner.cacheWrites;

            public int Deletes => owner.cacheDeletes;

            public int Hits => owner.cacheHit;

            public float HitRate => Reads == 0 ? 0 : (float)Hits / Reads;

            #endregion

            #region Constructors

            internal CacheStatistics(Cache<TKey, TValue> owner) => this.owner = owner;

            #endregion

            #region Methods

            public override string ToString() => Res.CacheStatistics(typeof(TKey).Name, typeof(TValue).Name, owner.Count, owner.Capacity, Writes, Reads, Hits, Deletes, HitRate);

            #endregion
        }

        #endregion

        #region KeysCollection class

        [DebuggerTypeProxy(typeof(DictionaryKeyCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {" + nameof(Count) + "}; TKey = {typeof(" + nameof(TKey) + ")}")]
        [Serializable]
        private sealed class KeysCollection : ICollection<TKey>, ICollection
        {
            #region Fields

            private readonly Cache<TKey, TValue> owner;
            [NonSerialized] private object syncRoot;

            #endregion

            #region Properties

            #region Public Properties

            public int Count => owner.Count;

            public bool IsReadOnly => true;

            #endregion

            #region Explicitly Implemented Interface Properties

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot
            {
                get
                {
                    if (syncRoot == null)
                        Interlocked.CompareExchange(ref syncRoot, new object(), null);
                    return syncRoot;
                }
            }

            #endregion

            #endregion

            #region Constructors

            internal KeysCollection(Cache<TKey, TValue> owner) => this.owner = owner;

            #endregion

            #region Methods

            #region Public Methods

            public bool Contains(TKey item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item), Res.ArgumentNull);
                return owner.ContainsKey(item);
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), Res.ArgumentNull);
                if (arrayIndex < 0 || arrayIndex > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex), Res.ArgumentOutOfRange);
                if (array.Length - arrayIndex < Count)
                    throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(array));

                for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                    array[arrayIndex++] = owner.items[current].Key;
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                if (owner.items == null || owner.first == -1)
                    yield break;

                int version = owner.version;
                for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                {
                    if (version != owner.version)
                        throw new InvalidOperationException(Res.IEnumeratorCollectionModified);

                    yield return owner.items[current].Key;
                }
            }

            #endregion

            #region Explicitly Implemented Interface Methods

            void ICollection<TKey>.Add(TKey item) => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            void ICollection<TKey>.Clear() => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            bool ICollection<TKey>.Remove(TKey item) => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), Res.ArgumentNull);

                if (array is TKey[] keys)
                {
                    CopyTo(keys, index);
                    return;
                }

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), Res.ArgumentOutOfRange);
                if (array.Length - index < Count)
                    throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(array));
                if (array.Rank != 1)
                    throw new ArgumentException(Res.ICollectionCopyToSingleDimArrayOnly, nameof(array));

                if (array is object[] objectArray)
                {
                    for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                        objectArray[index++] = owner.items[current].Key;
                }

                throw new ArgumentException(Res.ICollectionArrayTypeInvalid);
            }

            #endregion

            #endregion
        }

        #endregion

        #region ValuesCollection class

        [DebuggerTypeProxy(typeof(DictionaryValueCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {" + nameof(Count) + "}; TValue = {typeof(" + nameof(TValue) + ")}")]
        [Serializable]
        private sealed class ValuesCollection : ICollection<TValue>, ICollection
        {
            #region Fields

            private readonly Cache<TKey, TValue> owner;
            [NonSerialized] private object syncRoot;

            #endregion

            #region Properties

            #region Public Properties

            public int Count => owner.Count;

            public bool IsReadOnly => true;

            #endregion

            #region Explicitly Implemented Interface Properties

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot
            {
                get
                {
                    if (syncRoot == null)
                        Interlocked.CompareExchange(ref syncRoot, new object(), null);
                    return syncRoot;
                }
            }

            #endregion

            #endregion

            #region Constructors

            internal ValuesCollection(Cache<TKey, TValue> owner) => this.owner = owner;

            #endregion

            #region Methods

            #region Public Methods

            public bool Contains(TValue item) => owner.ContainsValue(item);

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), Res.ArgumentNull);
                if (arrayIndex < 0 || arrayIndex > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex), Res.ArgumentOutOfRange);
                if (array.Length - arrayIndex < Count)
                    throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(array));

                for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                    array[arrayIndex++] = owner.items[current].Value;
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                if (owner.items == null || owner.first == -1)
                    yield break;

                int version = owner.version;
                for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                {
                    if (version != owner.version)
                        throw new InvalidOperationException(Res.IEnumeratorCollectionModified);

                    yield return owner.items[current].Value;
                }
            }

            #endregion

            #region Explicitly Implemented Interface Methods

            void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            void ICollection<TValue>.Clear() => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            bool ICollection<TValue>.Remove(TValue item) => throw new NotSupportedException(Res.ICollectionReadOnlyModifyNotSupported);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), Res.ArgumentNull);

                if (array is TValue[] values)
                {
                    CopyTo(values, index);
                    return;
                }

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), Res.ArgumentOutOfRange);
                if (array.Length - index < Count)
                    throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(array));
                if (array.Rank != 1)
                    throw new ArgumentException(Res.ICollectionCopyToSingleDimArrayOnly, nameof(array));

                if (array is object[] objectArray)
                {
                    for (int current = owner.first; current != -1; current = owner.items[current].NextInOrder)
                        objectArray[index++] = owner.items[current].Value;
                }

                throw new ArgumentException(Res.ICollectionArrayTypeInvalid);
            }

            #endregion

            #endregion
        }

        #endregion

        #region ThreadSafeAccessorProtectLoader class

        private class ThreadSafeAccessorProtectLoader : IThreadSafeCacheAccessor<TKey, TValue>
        {
            #region Fields

            private readonly Cache<TKey, TValue> cache;

            #endregion

            #region Indexers

            public TValue this[TKey key]
            {
                get
                {
                    lock (cache.syncRootForThreadSafeAccessor)
                        return cache[key];
                }
            }

            #endregion

            #region Constructors

            public ThreadSafeAccessorProtectLoader(Cache<TKey, TValue> cache) => this.cache = cache;

            #endregion
        }

        #endregion

        #region ThreadSafeAccessor class

        private class ThreadSafeAccessor : IThreadSafeCacheAccessor<TKey, TValue>
        {
            #region Fields

            private readonly Cache<TKey, TValue> cache;

            #endregion

            #region Indexers

            public TValue this[TKey key]
            {
                get
                {
                    lock (cache.syncRootForThreadSafeAccessor)
                    {
                        if (cache.TryGetValue(key, out TValue result))
                            return result;
                    }

                    // ReSharper disable once InconsistentlySynchronizedField - intended: item loading is not locked
                    TValue newItem = cache.itemLoader.Invoke(key);
                    lock (cache.syncRootForThreadSafeAccessor)
                    {
                        if (cache.TryGetValue(key, out TValue result))
                        {
                            if (cache.DisposeDroppedValues && newItem is IDisposable disposable)
                                disposable.Dispose();
                            return result;
                        }

                        cache.Insert(key, newItem, false);
                    }

                    return newItem;
                }
            }

            #endregion

            #region Constructors

            public ThreadSafeAccessor(Cache<TKey, TValue> cache) => this.cache = cache;

            #endregion
        }

        #endregion

        #endregion

        #region Nested structs

        #region CacheItem struct

        [DebuggerDisplay("[{" + nameof(Key) + "}; {" + nameof(Value) + "}]")]
        private struct CacheItem
        {
            #region Fields

            internal TKey Key;
            internal TValue Value;

            /// <summary>
            /// Hash code (31 bits used), -1 if empty
            /// </summary>
            internal int Hash;

            /// <summary>
            /// Index of a chained item in the current bucket or -1 if last
            /// </summary>
            internal int NextInBucket;

            /// <summary>
            /// Index of next item in the evaluation order or -1 if last
            /// </summary>
            internal int NextInOrder;

            /// <summary>
            /// Index of previous item in the evaluation order or -1 if first
            /// </summary>
            internal int PrevInOrder;

            #endregion
        }

        #endregion

        #endregion

        #endregion

        #region Constants

        private const int defaultCapacity = 128;

        #endregion

        #region Fields

        #region Static Fields

        /// <summary>
        /// A loader function that can be used at constructors if you want to manage element additions to the cache manually.
        /// If you want to get an element with a non-existing key using this loader, a <see cref="KeyNotFoundException"/> will be thrown.
        /// This field is read-only.
        /// <remarks>
        /// When this field is used as loader function at one of the constructors, the <see cref="Cache{TKey,TValue}"/> can be used
        /// similarly to a <see cref="Dictionary{TKey,TValue}"/>: existence of keys should be tested by <see cref="ContainsKey"/> or <see cref="TryGetValue"/>
        /// methods, and elements should be added by <see cref="Add"/> method or by setter of the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)"/> property.
        /// The only difference to a <see cref="Dictionary{TKey,TValue}"/> is that <see cref="Capacity"/> is still maintained so
        /// when the <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> equals to <see cref="Capacity"/>), and
        /// a new element is added, then an element will be dropped from the cache depending on the current <see cref="Behavior"/>.
        /// </remarks>
        /// </summary>
        /// <seealso cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func`2,System.Int32,System.Collections.Generic.IEqualityComparer`1)"/>
        /// <seealso cref="P:KGySoft.Collections.Cache`2.Item(`0)"/>
        /// <seealso cref="Behavior"/>
        private static readonly Func<TKey, TValue> nullLoader = key => throw new KeyNotFoundException(Res.CacheNullLoaderInvoke);

        private static readonly Type typeKey = typeof(TKey);
        private static readonly Type typeValue = typeof(TValue);

        // ReSharper disable StaticMemberInGenericType
        private static readonly bool useEnumKeyComparer = typeKey.IsEnum
#if NET40 || NET45
            && Enum.GetUnderlyingType(typeKey) != Reflector.IntType
#elif !NET35
#error .NET version is not set or not supported!
#endif
            ;

        private static readonly bool useEnumValueComparer = typeValue.IsEnum
#if NET40 || NET45
                && Enum.GetUnderlyingType(typeValue) != Reflector.IntType
#elif !NET35
#error .NET version is not set or not supported!
#endif
            ;

        #endregion

        #region Instance Fields

        private Func<TKey, TValue> itemLoader;
        private IEqualityComparer<TKey> comparer;

        private SerializationInfo deserializationInfo;

        private int capacity;
        private bool ensureCapacity;
        private CacheBehavior behavior = CacheBehavior.RemoveLeastRecentUsedElement;
        private bool disposeDroppedValues;

        private CacheItem[] items;
        private int[] buckets;
        private int usedCount; // used elements in items including deleted ones
        private int deletedCount;
        private int deletedItemsBucket = -1; // First deleted entry among used elements. -1 if there are no deleted elements.
        private int first = -1; // First element both in traversal and in the evaluation order. -1 if empty.
        private int last = -1; // Last (newest) element. -1 if empty.
        private int version;

        private int cacheReads;
        private int cacheHit;
        private int cacheDeletes;
        private int cacheWrites;

        private object syncRoot;
        private object syncRootForThreadSafeAccessor;
        private KeysCollection keysCollection;
        private ValuesCollection valuesCollection;

        #endregion

        #endregion

        #region Properties and Indexers

        #region Properties

        #region Public Properties

        /// <summary>
        /// Gets or sets the capacity of the cache. If new value is smaller than elements count (value of the <see cref="Count"/> property),
        /// then old or least used elements (depending on <see cref="Behavior"/>) will be removed from <see cref="Cache{TKey,TValue}"/>.
        /// <br/>Default value: <c>128</c>, if the <see cref="Cache{TKey,TValue}"/> was initialized without specifying a capacity; otherwise, as it was initialized.
        /// </summary>
        /// <remarks>
        /// <para>If new value is smaller than elements count, then cost of setting this property is O(n), where n is the difference of
        /// <see cref="Count"/> before setting the property and the new capacity to set.</para>
        /// <para>If new value is larger than elements count, and <see cref="EnsureCapacity"/> returns <see langword="true"/>, then cost of setting this property is O(n),
        /// where n is the new capacity.</para>
        /// <para>Otherwise, the cost of setting this property is O(1).</para>
        /// </remarks>
        /// <seealso cref="Count"/>
        /// <seealso cref="Behavior"/>
        /// <seealso cref="EnsureCapacity"/>
        public int Capacity
        {
            get => capacity;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), Res.CacheMinSize);

                if (capacity == value)
                    return;

                capacity = value;
                if (Count - value > 0)
                    DropItems(Count - value);

                if (ensureCapacity)
                    DoEnsureCapacity();
            }
        }

        /// <summary>
        /// Gets or sets the cache behavior when cache is full and an element has to be removed.
        /// The cache is full, when <see cref="Count"/> reaches the <see cref="Capacity"/>.
        /// Default value: <see cref="CacheBehavior.RemoveLeastRecentUsedElement"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When cache is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element
        /// has to be stored, then an element has to be dropped out from the cache. The dropping-out strategy is
        /// specified by this property. The suggested behavior depends on cache usage. See possible behaviors at <see cref="CacheBehavior"/> enumeration.
        /// </para>
        /// <note>
        /// Changing value of this property will not reorganize cache, just switches between the maintaining strategies.
        /// Cache order is maintained on accessing a value.
        /// </note>
        /// </remarks>
        /// <seealso cref="Count"/>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="CacheBehavior"/>
        /// <seealso cref="EnsureCapacity"/>
        public CacheBehavior Behavior
        {
            get => behavior;
            set
            {
                if (!Enum<CacheBehavior>.IsDefined(value))
                    throw new ArgumentOutOfRangeException(nameof(value), Res.EnumOutOfRangeWithValues(value));

                behavior = value;
            }
        }

        /// <summary>
        /// Gets or sets whether adding the first item to the cache or resetting <see cref="Capacity"/> on a non-empty cache should
        /// allocate memory for all cache entries.
        /// <br/>Default value: <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <para>If <see cref="Capacity"/> is large (10,000 or bigger), and the cache is not likely to be full, the recommended value is <see langword="false"/>.</para>
        /// <para>When <see cref="EnsureCapacity"/> is <see langword="true"/>, the full capacity of the inner storage is allocated when the first
        /// item is added to the cache. Otherwise, inner storage is allocated dynamically, doubling the currently used inner
        /// storage until the preset <see cref="Capacity"/> is reached.
        /// <note>When <see cref="EnsureCapacity"/> is <see langword="false"/>&#160;and <see cref="Capacity"/> is not a power of 2, then after the last storage doubling
        /// the internally allocated storage can be bigger than <see cref="Capacity"/>. But setting <see langword="true"/>&#160;to this property trims the possibly exceeded size of the internal storage.</note>
        /// <note>Even if <see cref="EnsureCapacity"/> is <see langword="true"/>&#160;(and thus the internal storage is preallocated), adding elements to the cache
        /// consumes some memory for each added element.</note>
        /// </para>
        /// <para>When cache is not empty and <see cref="EnsureCapacity"/> is just turned on, the cost of setting this property is O(n),
        /// where n is <see cref="Count"/>. In any other cases cost of setting this property is O(1).</para>
        /// </remarks>
        /// <seealso cref="Capacity"/>
        public bool EnsureCapacity
        {
            get => ensureCapacity;
            set
            {
                if (ensureCapacity == value)
                    return;

                ensureCapacity = value;
                if (ensureCapacity)
                    DoEnsureCapacity();
            }
        }

        /// <summary>
        /// Gets the keys stored in the cache in evaluation order.
        /// </summary>
        /// <remarks>
        /// <para>The order of the keys in the <see cref="ICollection{T}"/> represents the evaluation order. When the <see cref="Cache{TKey,TValue}"/> is full, the element with the first key will be dropped.</para>
        /// <para>The returned <see cref="ICollection{T}"/> is not a static copy; instead, the <see cref="ICollection{T}"/> refers back to the keys in the original <see cref="Cache{TKey,TValue}"/>.
        /// Therefore, changes to the <see cref="Cache{TKey,TValue}"/> continue to be reflected in the <see cref="ICollection{T}"/>.</para>
        /// <para>Retrieving the value of this property is an O(1) operation.</para>
        /// <note>The enumerator of the returned collection does not support the <see cref="IEnumerator.Reset">IEnumerator.Reset</see> method.</note>
        /// </remarks>
        public ICollection<TKey> Keys => keysCollection ?? (keysCollection = new KeysCollection(this));

        /// <summary>
        /// Gets the values stored in the cache in evaluation order.
        /// </summary>
        /// <remarks>
        /// <para>The order of the values in the <see cref="ICollection{T}"/> represents the evaluation order. When the <see cref="Cache{TKey,TValue}"/> is full, the element with the value key will be dropped.</para>
        /// <para>The returned <see cref="ICollection{T}"/> is not a static copy; instead, the <see cref="ICollection{T}"/> refers back to the values in the original <see cref="Cache{TKey,TValue}"/>.
        /// Therefore, changes to the <see cref="Cache{TKey,TValue}"/> continue to be reflected in the <see cref="ICollection{T}"/>.</para>
        /// <para>Retrieving the value of this property is an O(1) operation.</para>
        /// <note>The enumerator of the returned collection does not support the <see cref="IEnumerator.Reset">IEnumerator.Reset</see> method.</note>
        /// </remarks>
        public ICollection<TValue> Values => valuesCollection ?? (valuesCollection = new ValuesCollection(this));

        /// <summary>
        /// Gets number of elements currently stored in this <see cref="Cache{TKey,TValue}"/> instance.
        /// </summary>
        /// <seealso cref="Capacity"/>
        public int Count => usedCount - deletedCount;

        /// <summary>
        /// Gets or sets whether internally dropped values are disposed if they implement <see cref="IDisposable"/>.
        /// <br/>Default value: <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <para>If the value of this property is <see langword="true"/>, then a disposable value will be disposed, if
        /// <list type="bullet">
        /// <item>The <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> equals <see cref="Capacity"/>), and
        /// a new item has to be stored so an element has to be dropped.</item>
        /// <item><see cref="Capacity"/> is decreased and therefore elements has to be dropped.</item>
        /// <item>The <see cref="Cache{TKey,TValue}"/> is accessed via an <see cref="IThreadSafeCacheAccessor{TKey,TValue}"/> instance and item for the same <typeparamref name="TKey"/>
        /// has been loaded concurrently so all but one loaded elements have to be discarded.</item>
        /// </list>
        /// </para>
        /// <note>In all cases when values are removed or replaced explicitly by the public members values are not disposed.</note>
        /// </remarks>
        public bool DisposeDroppedValues
        {
            get => disposeDroppedValues;
            set => disposeDroppedValues = value;
        }

        #endregion

        #region Explicitly Implemented Interface Properties

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// This is always a <see langword="false"/>&#160;value for <see cref="Cache{TKey,TValue}"/>.
        /// </returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary"/> object has a fixed size.
        /// </summary>
        /// <returns>
        /// This is always a <see langword="false"/>&#160;value for <see cref="Cache{TKey,TValue}"/>.
        /// </returns>
        bool IDictionary.IsFixedSize => false;

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary"/> object is read-only.
        /// </summary>
        /// <returns>
        /// This is always a <see langword="false"/>&#160;value for <see cref="Cache{TKey,TValue}"/>.
        /// </returns>
        bool IDictionary.IsReadOnly => false;

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> object containing the keys of the <see cref="T:System.Collections.IDictionary"/> object.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.ICollection"/> object containing the keys of the <see cref="T:System.Collections.IDictionary"/> object.
        /// </returns>
        ICollection IDictionary.Keys => (ICollection)Keys;

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> object containing the values in the
        /// <see cref="T:System.Collections.IDictionary"/> object.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.ICollection"/> object containing the values in the <see cref="T:System.Collections.IDictionary"/> object.
        /// </returns>
        ICollection IDictionary.Values => (ICollection)Values;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot
        {
            get
            {
                if (syncRoot == null)
                    Interlocked.CompareExchange(ref syncRoot, new object(), null);
                return syncRoot;
            }
        }

#if !(NET35 || NET40)

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
#endif

        #endregion

        #endregion

        #region Indexers

        #region Public Indexers

        /// <summary>
        /// Gets or sets the value associated with the specified <paramref name="key"/>. When an element with a non-existing key
        /// is read, and an item loader was specified by the appropriate <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>,
        /// then the value is retrieved by the specified loader delegate of this <see cref="Cache{TKey,TValue}"/> instance.
        /// </summary>
        /// <param name="key">Key of the element to get or set.</param>
        /// <returns>The element with the specified <paramref name="key"/>.</returns>
        /// <remarks>
        /// <para>Getting this property retrieves the needed element, while setting adds a new item (or overwrites an already existing item).
        /// If this <see cref="Cache{TKey,TValue}"/> instance was initialized by a non-<see langword="null"/>&#160;item loader, then it is enough to use only the get accessor because that will
        /// load elements into the cache by the delegate instance that was passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.
        /// When the cache was initialized without an item loader, then getting a non-existing key will throw a <see cref="KeyNotFoundException"/>.</para>
        /// <para>If an item loader was passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>, then
        /// it is transparent whether the returned value of this property was in the cache before retrieving it.
        /// To test whether a key exists in the cache, use the <see cref="ContainsKey">ContainsKey</see> method. To retrieve a key only when it already exists in the cache,
        /// use the <see cref="TryGetValue">TryGetValue</see> method.</para>
        /// <para>When the <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> equals to <see cref="Capacity"/>) and
        /// a new item is added, an element (depending on <see cref="Behavior"/> property) will be dropped from the cache.</para>
        /// <para>If <see cref="EnsureCapacity"/> is <see langword="true"/>, getting or setting this property approaches an O(1) operation. Otherwise,
        /// when the capacity of the inner storage must be increased to accommodate a new element, this property becomes an O(n) operation, where n is <see cref="Count"/>.</para>
        /// <para><note type="tip">You can retrieve a thread-safe accessor by the <see cref="GetThreadSafeAccessor">GetThreadSafeAccessor</see> method.</note></para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved, the <see cref="Cache{TKey,TValue}"/> has been initialized without an item loader
        /// and <paramref name="key"/> does not exist in the cache.</exception>
        /// <seealso cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})"/>
        /// <seealso cref="Behavior"/>
        /// <seealso cref="GetThreadSafeAccessor"/>
        public TValue this[TKey key]
        {
            [CollectionAccess(CollectionAccessType.UpdatedContent)]
            get
            {
                int i = GetItemIndex(key);
                cacheReads++;
                if (i >= 0)
                {
                    cacheHit++;
                    if (behavior == CacheBehavior.RemoveLeastRecentUsedElement)
                        InternalTouch(i);
                    return items[i].Value;
                }

                TValue newItem = itemLoader.Invoke(key);
                Insert(key, newItem, false);
                return newItem;
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
                Insert(key, value, false);
            }
        }

        #endregion

        #region Explicitly Implemented Interface Indexers

        /// <summary>
        /// Gets or sets the element with the specified key.
        /// </summary>
        /// <returns>
        /// The element with the specified key.
        /// </returns>
        /// <param name="key">The key of the element to get or set.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="key"/> or <paramref name="value"/> has an invalid type.</exception>
        object IDictionary.this[object key]
        {
            get
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
                if (!typeKey.CanAcceptValue(key))
                    throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(key, typeof(TKey)), nameof(key));
                return this[(TKey)key];
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
                if (!typeKey.CanAcceptValue(key))
                    throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(value, typeof(TKey)), nameof(key));
                if (!typeValue.CanAcceptValue(value))
                    throw new ArgumentException(Res.ICollectionNongenericValueTypeInvalid(value, typeof(TValue)), nameof(value));
                this[(TKey)key] = (TValue)value;
            }
        }

        #endregion

        #endregion

        #endregion

        #region Constructors

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class with default capacity of 128 and no item loader.
        /// </summary>
        /// <remarks>
        /// <para>When <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element is about to be stored, then an
        /// element will be dropped out from the cache. The strategy is controlled by <see cref="Behavior"/> property.</para>
        /// <para>This constructor does not specify an item loader so you have to add elements manually to this <see cref="Cache{TKey,TValue}"/> instance. In this case
        /// the <see cref="Cache{TKey,TValue}"/> can be used similarly to a <see cref="Dictionary{TKey,TValue}"/>: before getting an element, its existence must be checked by <see cref="ContainsKey">ContainsKey</see>
        /// or <see cref="TryGetValue">TryGetValue</see> methods, though <see cref="Capacity"/> is still maintained based on the strategy specified in the <see cref="Behavior"/> property.</para>
        /// </remarks>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="EnsureCapacity"/>
        /// <seealso cref="Behavior"/>
        public Cache() : this(null, defaultCapacity)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class with specified <paramref name="capacity"/> capacity and <paramref name="comparer"/> and no item loader.
        /// </summary>
        /// <param name="capacity"><see cref="Capacity"/> of the <see cref="Cache{TKey,TValue}"/> (possible maximum value of <see cref="Count"/>)</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys. When <see langword="null"/>, <see cref="EnumComparer{TEnum}.Comparer">EnumComparer&lt;TEnum&gt;.Comparer</see>
        /// will be used for <see langword="enum"/>&#160;key types, and <see cref="EqualityComparer{T}.Default">EqualityComparer&lt;T&gt;.Default</see> for other types. This parameter is optional.
        /// <br/>Default value: <see langword="null"/>.</param>
        /// <remarks>
        /// <para>Every key in a <see cref="Cache{TKey,TValue}"/> must be unique according to the specified comparer.</para>
        /// <para>The <paramref name="capacity"/> of a <see cref="Cache{TKey,TValue}"/> is the maximum number of elements that the <see cref="Cache{TKey,TValue}"/> can hold. When <see cref="EnsureCapacity"/>
        /// is <see langword="true"/>, the internal store is allocated when the first element is added to the cache. When <see cref="EnsureCapacity"/> is <see langword="false"/>, then as elements are added to the
        /// <see cref="Cache{TKey,TValue}"/>, the inner storage is automatically increased as required until <see cref="Capacity"/> is reached or exceeded. When <see cref="EnsureCapacity"/> is
        /// turned on while there are elements in the <see cref="Cache{TKey,TValue}"/>, then internal storage will be reallocated to have exactly the same size that <see cref="Capacity"/> defines.
        /// The possible exceeding storage will be trimmed in this case.</para>
        /// <para>When <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element is about to be stored, then an
        /// element will be dropped out from the cache. The strategy is controlled by <see cref="Behavior"/> property.</para>
        /// <para>This constructor does not specify an item loader so you have to add elements manually to this <see cref="Cache{TKey,TValue}"/> instance. In this case
        /// the <see cref="Cache{TKey,TValue}"/> can be used similarly to a <see cref="Dictionary{TKey,TValue}"/>: before getting an element, its existence must be checked by <see cref="ContainsKey">ContainsKey</see>
        /// or <see cref="TryGetValue">TryGetValue</see> methods, though <see cref="Capacity"/> is still maintained based on the strategy specified in the <see cref="Behavior"/> property.</para>
        /// </remarks>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="EnsureCapacity"/>
        /// <seealso cref="Behavior"/>
        public Cache(int capacity, IEqualityComparer<TKey> comparer = null) : this(null, capacity, comparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class with the specified <paramref name="comparer"/>, default capacity of 128 and no item loader.
        /// </summary>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys. When <see langword="null"/>, <see cref="EnumComparer{TEnum}.Comparer">EnumComparer&lt;TEnum&gt;.Comparer</see>
        /// will be used for <see langword="enum"/>&#160;key types, and <see cref="EqualityComparer{T}.Default">EqualityComparer&lt;T&gt;.Default</see> for other types.</param>
        /// <remarks>
        /// <para>Every key in a <see cref="Cache{TKey,TValue}"/> must be unique according to the specified comparer.</para>
        /// <para>When <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element is about to be stored, then an
        /// element will be dropped out from the cache. The strategy is controlled by <see cref="Behavior"/> property.</para>
        /// <para>This constructor does not specify an item loader so you have to add elements manually to this <see cref="Cache{TKey,TValue}"/> instance. In this case
        /// the <see cref="Cache{TKey,TValue}"/> can be used similarly to a <see cref="Dictionary{TKey,TValue}"/>: before getting an element, its existence must be checked by <see cref="ContainsKey">ContainsKey</see>
        /// or <see cref="TryGetValue">TryGetValue</see> methods, though <see cref="Capacity"/> is still maintained based on the strategy specified in the <see cref="Behavior"/> property.</para>
        /// </remarks>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="EnsureCapacity"/>
        /// <seealso cref="Behavior"/>
        public Cache(IEqualityComparer<TKey> comparer) : this(null, defaultCapacity, comparer)
        {
        }

        /// <summary>
        /// Creates a new <see cref="Cache{TKey,TValue}"/> instance with the given <paramref name="itemLoader"/> and <paramref name="comparer"/> using default capacity of 128.
        /// </summary>
        /// <param name="itemLoader">A delegate that contains the item loader routine. This delegate is accessed whenever a non-cached item is about to be loaded by reading the
        /// <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see>.
        /// If <see langword="null"/>, then similarly to a regular <see cref="Dictionary{TKey,TValue}"/>, a <see cref="KeyNotFoundException"/> will be thrown on accessing a non-existing key.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys. When <see langword="null"/>, <see cref="EnumComparer{TEnum}.Comparer">EnumComparer&lt;TEnum&gt;.Comparer</see>
        /// will be used for <see langword="enum"/>&#160;key types, and <see cref="EqualityComparer{T}.Default">EqualityComparer&lt;T&gt;.Default</see> for other types. This parameter is optional.
        /// <br/>Default value: <see langword="null"/>.</param>
        /// <remarks>
        /// <para>Every key in a <see cref="Cache{TKey,TValue}"/> must be unique according to the specified comparer.</para>
        /// <para>When <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element is about to be stored, then an
        /// element will be dropped out from the cache. The strategy is controlled by <see cref="Behavior"/> property.</para>
        /// <para>If you want to add elements manually to the <see cref="Cache{TKey,TValue}"/>, then you can pass <see langword="null"/>&#160;to the <paramref name="itemLoader"/> parameter. In this case
        /// the <see cref="Cache{TKey,TValue}"/> can be used similarly to a <see cref="Dictionary{TKey,TValue}"/>: before getting an element, its existence must be checked by <see cref="ContainsKey">ContainsKey</see>
        /// or <see cref="TryGetValue">TryGetValue</see> methods, though <see cref="Capacity"/> is still maintained based on the strategy specified in the <see cref="Behavior"/> property.</para>
        /// </remarks>
        /// <overloads><see cref="Cache{TKey,TValue}"/> type has four different public constructors for initializing the item loader delegate, capacity and key comparer.</overloads>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="EnsureCapacity"/>
        /// <seealso cref="Behavior"/>
        [CollectionAccess(CollectionAccessType.UpdatedContent)]
        public Cache(Func<TKey, TValue> itemLoader, IEqualityComparer<TKey> comparer) : this(itemLoader, defaultCapacity, comparer)
        {
        }

        /// <summary>
        /// Creates a new <see cref="Cache{TKey,TValue}"/> instance with the given <paramref name="itemLoader"/>, <paramref name="capacity"/> and <paramref name="comparer"/>.
        /// </summary>
        /// <param name="itemLoader">A delegate that contains the item loader routine. This delegate is accessed whenever a non-cached item is about to be loaded by reading the
        /// <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see>.
        /// If <see langword="null"/>, then similarly to a regular <see cref="Dictionary{TKey,TValue}"/>, a <see cref="KeyNotFoundException"/> will be thrown on accessing a non-existing key.</param>
        /// <param name="capacity"><see cref="Capacity"/> of the <see cref="Cache{TKey,TValue}"/> (possible maximum value of <see cref="Count"/>). This parameter is optional.
        /// <br/>Default value: <c>128</c>.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys. When <see langword="null"/>, <see cref="EnumComparer{TEnum}.Comparer">EnumComparer&lt;TEnum&gt;.Comparer</see>
        /// will be used for <see langword="enum"/>&#160;key types, and <see cref="EqualityComparer{T}.Default">EqualityComparer&lt;T&gt;.Default</see> for other types. This parameter is optional.
        /// <br/>Default value: <see langword="null"/>.</param>
        /// <remarks>
        /// <para>Every key in a <see cref="Cache{TKey,TValue}"/> must be unique according to the specified comparer.</para>
        /// <para>The <paramref name="capacity"/> of a <see cref="Cache{TKey,TValue}"/> is the maximum number of elements that the <see cref="Cache{TKey,TValue}"/> can hold. When <see cref="EnsureCapacity"/>
        /// is <see langword="true"/>, the internal store is allocated when the first element is added to the cache. When <see cref="EnsureCapacity"/> is <see langword="false"/>, then as elements are added to the
        /// <see cref="Cache{TKey,TValue}"/>, the inner storage is automatically increased as required until <see cref="Capacity"/> is reached or exceeded. When <see cref="EnsureCapacity"/> is
        /// turned on while there are elements in the <see cref="Cache{TKey,TValue}"/>, then internal storage will be reallocated to have exactly the same size that <see cref="Capacity"/> defines.
        /// The possible exceeding storage will be trimmed in this case.</para>
        /// <para>When <see cref="Cache{TKey,TValue}"/> is full (that is, when <see cref="Count"/> reaches <see cref="Capacity"/>) and a new element is about to be stored, then an
        /// element will be dropped out from the cache. The strategy is controlled by <see cref="Behavior"/> property.</para>
        /// <para>If you want to add elements manually to the <see cref="Cache{TKey,TValue}"/>, then you can pass <see langword="null"/>&#160;to the <paramref name="itemLoader"/> parameter. In this case
        /// the <see cref="Cache{TKey,TValue}"/> can be used similarly to a <see cref="Dictionary{TKey,TValue}"/>: before getting an element, its existence must be checked by <see cref="ContainsKey">ContainsKey</see>
        /// or <see cref="TryGetValue">TryGetValue</see> methods, though <see cref="Capacity"/> is still maintained based on the strategy specified in the <see cref="Behavior"/> property.</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less or equal to 0.</exception>
        /// <overloads><see cref="Cache{TKey,TValue}"/> type has four different public constructors for initializing the item loader delegate, capacity and key comparer.</overloads>
        /// <seealso cref="Capacity"/>
        /// <seealso cref="EnsureCapacity"/>
        /// <seealso cref="Behavior"/>
        [CollectionAccess(CollectionAccessType.UpdatedContent)]
        public Cache(Func<TKey, TValue> itemLoader, int capacity = defaultCapacity, IEqualityComparer<TKey> comparer = null)
        {
            this.itemLoader = itemLoader ?? nullLoader;
            Capacity = capacity;
            this.comparer = comparer ?? (useEnumKeyComparer ? (IEqualityComparer<TKey>)EnumComparer<TKey>.Comparer : EqualityComparer<TKey>.Default);
        }

        #endregion

        #region Protected Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class from serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that stores the data.</param>
        /// <param name="context">The destination (see <see cref="StreamingContext"/>) for this deserialization.</param>
        /// <remarks><note type="inherit">If an inherited type serializes data, which may affect the hashes of the keys, then override
        /// the <see cref="OnDeserialization">OnDeserialization</see> method and use that to restore the data of the derived instance.</note></remarks>
        protected Cache(SerializationInfo info, StreamingContext context)
        {
            // deferring the actual deserialization until all objects are finalized and hashes do not change anymore
            deserializationInfo = info;
        }

        #endregion

        #endregion

        #region Methods

        #region Static Methods

        private static bool IsDefaultComparer(IEqualityComparer<TKey> comparer)
            => useEnumKeyComparer ? EnumComparer<TKey>.Comparer.Equals(comparer) : EqualityComparer<TKey>.Default.Equals(comparer);

        private static IEqualityComparer<TValue> GetValueComparer()
            => useEnumValueComparer ? (IEqualityComparer<TValue>)EnumComparer<TValue>.Comparer : EqualityComparer<TValue>.Default;

        #endregion

        #region Instance Methods

        #region Public Methods

        /// <summary>
        /// Renews the value with the specified <paramref name="key"/> in the evaluation order.
        /// </summary>
        /// <param name="key">The key of the item to renew.</param>
        /// <remarks>
        /// <para><see cref="Cache{TKey,TValue}"/> maintains an evaluation order for the stored elements. When the <see cref="Cache{TKey,TValue}"/> is full
        /// (that is when <see cref="Count"/> equals to <see cref="Capacity"/>), then adding a new element will drop the element, which is the first one in the evaluation order.
        /// By calling this method, the element with the specified <paramref name="key"/> will be sent to the back in the evaluation order.</para>
        /// <para>When <see cref="Behavior"/> is <see cref="CacheBehavior.RemoveLeastRecentUsedElement"/> (which is the default behavior), then whenever an existing element
        /// is accessed in the <see cref="Cache{TKey,TValue}"/>, then it will be touched internally.</para>
        /// <para>This method approaches an O(1) operation.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException"><paramref name="key"/> does not exist in the <see cref="Cache{TKey,TValue}"/>.</exception>
        /// <seealso cref="Behavior"/>
        public void Touch(TKey key)
        {
            int i = GetItemIndex(key);
            if (i >= 0)
            {
                InternalTouch(i);
                version++;
            }
            else
                throw new KeyNotFoundException(Res.CacheKeyNotFound);
        }

        /// <summary>
        /// Refreshes the value of the <paramref name="key"/> in the <see cref="Cache{TKey,TValue}"/> even if it already exists in the cache
        /// by using the item loader that was passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.
        /// </summary>
        /// <param name="key">The key of the item to refresh.</param>
        /// <remarks>
        /// <para>The loaded value will be stored in the <see cref="Cache{TKey,TValue}"/>. If a value already existed in the cache for the given <paramref name="key"/>, then the value will be replaced.</para>
        /// <para><note type="caution">Do not use this method when the <see cref="Cache{TKey,TValue}"/> was initialized without an item loader.</note></para>
        /// <para>To get the refreshed value as well, use <see cref="GetValueUncached"/> method instead.</para>
        /// <para>The cost of this method depends on the cost of the item loader function that was passed to the constructor. Refreshing the already loaded value approaches an O(1) operation.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">The <see cref="Cache{TKey,TValue}"/> has been initialized without an item loader.</exception>
        public void RefreshValue(TKey key) => GetValueUncached(key);

        /// <summary>
        /// Loads the value of the <paramref name="key"/> even if it already exists in the <see cref="Cache{TKey,TValue}"/>
        /// by using the item loader that was passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.
        /// </summary>
        /// <param name="key">The key of the item to reload.</param>
        /// <returns>A <typeparamref name="TValue"/> instance that was retrieved by the item loader that was used to initialize this <see cref="Cache{TKey,TValue}"/> instance.</returns>
        /// <remarks>
        /// <para>To get a value from the <see cref="Cache{TKey,TValue}"/>, and using the item loader only when <paramref name="key"/> does not exist in the cache,
        /// read the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> property.</para>
        /// <para>The loaded value will be stored in the <see cref="Cache{TKey,TValue}"/>. If a value already existed in the cache for the given <paramref name="key"/>, then the value will be replaced.</para>
        /// <para><note type="caution">Do not use this method when the <see cref="Cache{TKey,TValue}"/> was initialized without an item loader.</note></para>
        /// <para>The cost of this method depends on the cost of the item loader function that was passed to the constructor. Handling the already loaded value approaches an O(1) operation.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">The <see cref="Cache{TKey,TValue}"/> has been initialized without an item loader.</exception>
        /// <seealso cref="P:KGySoft.Collections.Cache`2.Item(`0)"/>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Uncached")]
        public TValue GetValueUncached(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);

            TValue result = itemLoader.Invoke(key);
            Insert(key, result, false);
            return result;
        }

        /// <summary>
        /// Determines whether the <see cref="Cache{TKey,TValue}"/> contains a specific value.
        /// </summary>
        /// <param name="value">The value to locate in the <see cref="Cache{TKey,TValue}"/>.
        /// The value can be <see langword="null"/>&#160;for reference types.</param>
        /// <returns><see langword="true"/>&#160;if the <see cref="Cache{TKey,TValue}"/> contains an element with the specified <paramref name="value"/>; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// <para>This method determines equality using the <see cref="EnumComparer{TEnum}.Comparer">EnumComparer&lt;TEnum&gt;.Comparer</see> when <typeparamref name="TValue"/> is an <see langword="enum"/>&#160;type,
        /// or the default equality comparer <see cref="EqualityComparer{T}.Default">EqualityComparer&lt;T&gt;.Default</see> for other <typeparamref name="TValue"/> types.</para>
        /// <para>This method performs a linear search; therefore, this method is an O(n) operation.</para>
        /// </remarks>
        public bool ContainsValue(TValue value)
        {
            if (items == null)
                return false;

            if (value == null)
            {
                for (int i = first; i != -1; i = items[i].NextInOrder)
                {
                    if (items[i].Value == null)
                        return true;
                }

                return false;
            }

            IEqualityComparer<TValue> valueComparer = GetValueComparer();
            for (int i = first; i != -1; i = items[i].NextInOrder)
            {
                if (valueComparer.Equals(value, items[i].Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears the <see cref="Cache{TKey,TValue}"/> and resets statistics.
        /// </summary>
        /// <remarks>
        /// <para>The <see cref="Count"/> property is set to 0, and references to other objects from elements of the collection are also released.
        /// The <see cref="Capacity"/> remains unchanged. The statistics will be reset.</para>
        /// <para>This method is an O(1) operation.</para>
        /// </remarks>
        /// <seealso cref="Clear"/>
        public void Reset()
        {
            Clear();
            cacheReads = 0;
            cacheWrites = 0;
            cacheDeletes = 0;
            cacheHit = 0;
        }

        /// <summary>
        /// Gets an <see cref="ICacheStatistics"/> instance that provides statistical information about this <see cref="Cache{TKey,TValue}"/>.
        /// </summary>
        /// <returns>An <see cref="ICacheStatistics"/> instance that provides statistical information about the <see cref="Cache{TKey,TValue}"/>.</returns>
        /// <remarks>
        /// <para>The returned <see cref="ICacheStatistics"/> instance is a wrapper around the <see cref="Cache{TKey,TValue}"/> and reflects any changes
        /// happened to the cache immediately. Therefore it is not necessary to call this method again whenever new statistics are required.</para>
        /// <para>This method is an O(1) operation.</para>
        /// </remarks>
        public ICacheStatistics GetStatistics() => new CacheStatistics(this);

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="Cache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be <see langword="null"/>&#160;for reference types.</param>
        /// <remarks>
        /// <para>You need to call this method only when this <see cref="Cache{TKey,TValue}"/> instance was initialized without using an item loader.
        /// Otherwise, you need only to read the get accessor of the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> property,
        /// which automatically invokes the item loader to add new items.</para>
        /// <para>If the <paramref name="key"/> of element already exists in the cache, this method throws an exception.
        /// In contrast, using the setter of the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> property replaces the old value with the new one.</para>
        /// <para>If you want to renew an element in the evaluation order, use the <see cref="Touch">Touch</see> method.</para>
        /// <para>If <see cref="EnsureCapacity"/> is <see langword="true"/>&#160;this method approaches an O(1) operation. Otherwise, when the capacity of the inner storage must be increased to accommodate the new element,
        /// this method becomes an O(n) operation, where n is <see cref="Count"/>.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> already exists in the cache.</exception>
        /// <seealso cref="P:KGySoft.Collections.Cache`2.Item(`0)"/>
        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);

            Insert(key, value, true);
        }

        /// <summary>
        /// Removes the value with the specified <paramref name="key"/> from the <see cref="Cache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">Key of the item to remove.</param>
        /// <returns><see langword="true"/>&#160;if the element is successfully removed; otherwise, <see langword="false"/>. This method also returns <see langword="false"/>&#160;if key was not found in the <see cref="Cache{TKey,TValue}"/>.</returns>
        /// <remarks><para>If the <see cref="Cache{TKey,TValue}"/> does not contain an element with the specified key, the <see cref="Cache{TKey,TValue}"/> remains unchanged. No exception is thrown.</para>
        /// <para>This method approaches an O(1) operation.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);

            return InternalRemove(key, default, false);
        }

        /// <summary>
        /// Tries to gets the value associated with the specified <paramref name="key"/> without using the item loader passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/>, if cache contains an element with the specified key; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the <paramref name="key"/> is found;
        /// otherwise, the default value for the type of the <paramref name="value"/> parameter. This parameter is passed uninitialized.</param>
        /// <remarks>
        /// <para>Use this method if the <see cref="Cache{TKey,TValue}"/> was initialized without an item loader, or when you want to determine if a
        /// <paramref name="key"/> exists in the <see cref="Cache{TKey,TValue}"/> and if so, you want to get the value as well.
        /// Reading the <see cref="P:KGySoft.Collections.Cache`2.Item(`0)">indexer</see> property would transparently load a non-existing element by
        /// calling the item loader delegate that was passed to the <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>.</para>
        /// <para>Works exactly the same way as in case of <see cref="Dictionary{TKey,TValue}"/> class. If <paramref name="key"/> is not found, does not use the
        /// item loader passed to the constructor.</para>
        /// <para>If the <paramref name="key"/> is not found, then the <paramref name="value"/> parameter gets the appropriate default value
        /// for the type <typeparamref name="TValue"/>; for example, 0 (zero) for integer types, <see langword="false"/>&#160;for Boolean types, and <see langword="null"/>&#160;for reference types.</para>
        /// <para>This method approaches an O(1) operation.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <seealso cref="P:KGySoft.Collections.Cache`2.Item(`0)"/>
        public bool TryGetValue(TKey key, out TValue value)
        {
            int i = GetItemIndex(key);
            cacheReads++;
            if (i >= 0)
            {
                cacheHit++;
                if (behavior == CacheBehavior.RemoveLeastRecentUsedElement)
                    InternalTouch(i);

                value = items[i].Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Determines whether the <see cref="Cache{TKey,TValue}"/> contains a specific key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="Cache{TKey,TValue}"/>.</param>
        /// <returns><see langword="true"/>&#160;if the <see cref="Cache{TKey,TValue}"/> contains an element with the specified <paramref name="key"/>; otherwise, <see langword="false"/>.</returns>
        /// <remarks><para>This method approaches an O(1) operation.</para></remarks>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool ContainsKey(TKey key) => GetItemIndex(key) >= 0;

        /// <summary>
        /// Removes all keys and values from the <see cref="Cache{TKey,TValue}"/>.
        /// </summary>
        /// <remarks>
        /// <para>The <see cref="Count"/> property is set to 0, and references to other objects from elements of the collection are also released.
        /// The <see cref="Capacity"/> remains unchanged.</para>
        /// <para>This method is an O(1) operation.</para>
        /// </remarks>
        /// <seealso cref="Reset"/>
        public void Clear()
        {
            if (Count == 0)
                return;

            cacheDeletes += Count;
            buckets = null;
            items = null;
            first = -1;
            last = -1;
            usedCount = 0;
            deletedCount = 0;
            deletedItemsBucket = -1;
            version++;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Cache{TKey,TValue}"/> elements in the evaluation order.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator{T}"/> that can be used to iterate through the <see cref="Cache{TKey,TValue}"/>.
        /// </returns>
        /// <remarks>
        /// <note>The returned enumerator supports the <see cref="IEnumerator.Reset">IEnumerator.Reset</see> method.</note>
        /// </remarks>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => new Enumerator(this, true);

        /// <summary>
        /// Gets a thread safe accessor for this <see cref="Cache{TKey,TValue}"/> instance. As it provides only a
        /// single readable indexer, it makes sense only if an item loader was passed to the appropriate
        /// <see cref="M:KGySoft.Collections.Cache`2.#ctor(System.Func{`0,`1},System.Int32,System.Collections.Generic.IEqualityComparer{`0})">constructor</see>
        /// and the cache will not be accessed by other members but via the returned accessor.
        /// </summary>
        /// <param name="protectItemLoader"><see langword="true"/>&#160;to ensure that also the item loader is locked if a new element has to be loaded and
        /// <see langword="false"/>&#160;to allow the item loader to be called parallelly. In latter case the <see cref="Cache{TKey,TValue}"/> is not locked during the time the item loader is being called
        /// but it can happen that values for same key are loaded multiple times and all but one will be discarded. This parameter is optional.
        /// <br/>Default value: <see langword="false"/>.</param>
        /// <returns>An <see cref="IThreadSafeCacheAccessor{TKey,TValue}"/> instance providing a thread-safe readable indexer for this <see cref="Cache{TKey,TValue}"/> instance.</returns>
        public IThreadSafeCacheAccessor<TKey, TValue> GetThreadSafeAccessor(bool protectItemLoader = false)
        {
            if (syncRootForThreadSafeAccessor == null)
                Interlocked.CompareExchange(ref syncRootForThreadSafeAccessor, new object(), null);
            return protectItemLoader ? (IThreadSafeCacheAccessor<TKey, TValue>)new ThreadSafeAccessorProtectLoader(this) : new ThreadSafeAccessor(this);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// In a derived class populates a <see cref="SerializationInfo" /> with the additional data of the derived type needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> to populate with data.</param>
        /// <param name="context">The destination (see <see cref="StreamingContext"/>) for this serialization.</param>
        [SecurityCritical]
        protected virtual void GetObjectData(SerializationInfo info, StreamingContext context) { }

        /// <summary>
        /// In a derived class restores the state the deserialized instance.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that stores the data.</param>
        [SecurityCritical]
        protected virtual void OnDeserialization(SerializationInfo info) { }

        #endregion

        #region Private Methods

        private void Initialize(int suggestedCapacity)
        {
            int bucketSize = PrimeHelper.GetPrime(suggestedCapacity);
            buckets = new int[bucketSize];
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = -1;

            // items.Length <= bucketSize!
            items = new CacheItem[capacity < bucketSize ? capacity : bucketSize];
            usedCount = 0;
            deletedCount = 0;
            first = -1;
            last = -1;
            deletedItemsBucket = -1;
        }

        private void DoEnsureCapacity()
        {
            if (items == null || items.Length == capacity)
                return;

            Resize(capacity);
        }

        private int GetItemIndex(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);

            if (buckets == null)
                return -1;

            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            for (int i = buckets[hashCode % buckets.Length]; i >= 0; i = items[i].NextInBucket)
            {
                if (items[i].Hash == hashCode && comparer.Equals(items[i].Key, key))
                    return i;
            }

            return -1;
        }

        private void InternalTouch(int index)
        {
            // already last: nothing to do
            if (index == last)
                return;

            // extracting from middle
            if (index != first)
                items[items[index].PrevInOrder].NextInOrder = items[index].NextInOrder;
            items[items[index].NextInOrder].PrevInOrder = items[index].PrevInOrder;

            // it was the first one
            Debug.Assert(first != -1, "first is -1 in InternalTouch");
            if (index == first)
                first = items[index].NextInOrder;

            // setting prev/next/last
            items[index].PrevInOrder = last;
            items[index].NextInOrder = -1;
            items[last].NextInOrder = index;
            last = index;
        }

        private bool InternalRemove(TKey key, TValue value, bool ckeckValue)
        {
            if (buckets == null)
                return false;

            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int bucket = hashCode % buckets.Length;
            int prevInBucket = -1;
            for (int i = buckets[bucket]; i >= 0; prevInBucket = i, i = items[i].NextInBucket)
            {
                if (items[i].Hash != hashCode || !comparer.Equals(items[i].Key, key))
                    continue;
                if (ckeckValue && !GetValueComparer().Equals(items[i].Value, value))
                    return false;

                // removing entry from the original bucket
                if (prevInBucket < 0)
                    buckets[bucket] = items[i].NextInBucket;
                else
                    items[prevInBucket].NextInBucket = items[i].NextInBucket;

                // moving entry to a special bucket of removed entries
                items[i].NextInBucket = deletedItemsBucket;
                deletedItemsBucket = i;
                deletedCount++;

                // adjusting first/last
                if (i == last)
                    last = items[i].PrevInOrder;
                if (i == first)
                    first = items[i].NextInOrder;

                // extracting from middle
                if (items[i].PrevInOrder != -1)
                    items[items[i].PrevInOrder].NextInOrder = items[i].NextInOrder;
                if (items[i].NextInOrder != -1)
                    items[items[i].NextInOrder].PrevInOrder = items[i].PrevInOrder;

                // cleanup
                items[i].Hash = -1;
                items[i].Key = default;
                items[i].Value = default;
                items[i].NextInOrder = -1;
                items[i].PrevInOrder = -1;

                cacheDeletes++;
                version++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the first (oldest or the least used) item from the cache.
        /// </summary>
        private void DropFirst()
        {
            Debug.Assert(first != -1, "first is -1 in DropFirst");
            if (disposeDroppedValues && items[first].Value is IDisposable disposable)
                disposable.Dispose();
            Remove(items[first].Key);
        }

        private void DropItems(int amount)
        {
            Debug.Assert(Count >= amount, "Count is too few in DropItems");
            for (int i = 0; i < amount; i++)
                DropFirst();
        }

        /// <summary>
        /// Inserting a new element into the cache
        /// </summary>
        private void Insert(TKey key, TValue value, bool throwIfExists)
        {
            if (buckets == null)
                Initialize(ensureCapacity ? capacity : 1);

            // Ignoring MSB so we can use -1 to sign unused entries
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int targetBucket = hashCode % buckets.Length;

            // searching for an existing key
            for (int i = buckets[targetBucket]; i >= 0; i = items[i].NextInBucket)
            {
                if (items[i].Hash != hashCode || !comparer.Equals(items[i].Key, key))
                    continue;

                if (throwIfExists)
                    throw new ArgumentException(Res.IDictionaryDuplicateKey, nameof(key));

                // overwriting existing element
                if (behavior == CacheBehavior.RemoveLeastRecentUsedElement)
                    InternalTouch(i);
                items[i].Value = value;
                cacheWrites++;
                version++;
                return;
            }

            // if used with full capacity, dropping an element
            if (Count == capacity)
                DropFirst();

            // re-using the removed entries if possible
            int index;
            if (deletedCount > 0)
            {
                index = deletedItemsBucket;
                deletedItemsBucket = items[index].NextInBucket;
                deletedCount--;
            }
            // otherwise, adding a new entry
            else
            {
                // storage expansion is needed
                if (usedCount == items.Length)
                {
                    int oldSize = items.Length;
                    int newSize = capacity >> 1 > oldSize ? oldSize << 1 : capacity;
                    Resize(newSize);
                    targetBucket = hashCode % buckets.Length;
                }

                index = usedCount;
                usedCount++;
            }

            items[index].Hash = hashCode;
            items[index].NextInBucket = buckets[targetBucket];
            items[index].Key = key;
            items[index].Value = value;
            items[index].PrevInOrder = last;
            items[index].NextInOrder = -1;
            buckets[targetBucket] = index;
            if (first == -1)
                first = index;
            if (last != -1)
                items[last].NextInOrder = index;
            last = index;

            cacheWrites++;
            version++;
        }

        private void Resize(int suggestedSize)
        {
            int newBucketSize = PrimeHelper.GetPrime(suggestedSize);
            var newBuckets = new int[newBucketSize];
            for (int i = 0; i < newBuckets.Length; i++)
                newBuckets[i] = -1;

            var newItems = new CacheItem[capacity < newBucketSize ? capacity : newBucketSize];

            // if increasing capacity, then keeping also the deleted entries.
            if (newItems.Length >= items.Length)
                Array.Copy(items, 0, newItems, 0, usedCount);
            else
            {
                // if shrinking, then keeping only the living elements while updating indices
                usedCount = 0;
                for (int i = first; i != -1; i = items[i].NextInOrder)
                {
                    newItems[usedCount] = items[i];
                    newItems[usedCount].PrevInOrder = usedCount - 1;
                    newItems[usedCount].NextInOrder = ++usedCount;
                }

                first = 0;
                last = usedCount - 1;
                newItems[last].NextInOrder = -1;
                deletedCount = 0;
                deletedItemsBucket = -1;
            }

            // re-applying buckets for the new size
            for (int i = 0; i < usedCount; i++)
            {
                if (newItems[i].Hash < 0)
                    continue;

                int bucket = newItems[i].Hash % newBucketSize;
                newItems[i].NextInBucket = newBuckets[bucket];
                newBuckets[bucket] = i;
            }

            buckets = newBuckets;
            items = newItems;
        }

        #endregion

        #region Explicitly Implemented Interface Methods

        /// <summary>
        /// Renews an item in the evaluation order.
        /// </summary>
        /// <param name="key">The key of the item to renew.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> must not be <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> must exist in the cache.</exception>
        void ICache.Touch(object key)
        {
            if (!typeKey.CanAcceptValue(key))
                throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(key, typeof(TKey)), nameof(key));
            Touch((TKey)key);
        }

        /// <summary>
        /// Refreshes the value in the cache even if it was already loaded.
        /// </summary>
        /// <param name="key">The key of the item to refresh.</param>
        void ICache.RefreshValue(object key)
        {
            if (!typeKey.CanAcceptValue(key))
                throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(key, typeof(TKey)), nameof(key));
            RefreshValue((TKey)key);
        }

        /// <summary>
        /// Reloads the value into the cache even if it was already loaded using the item loader that was passed to the constructor.
        /// </summary>
        /// <param name="key">The key of the item to reload.</param>
        /// <returns>Loaded value</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> must not be <see langword="null"/>.</exception>
        object ICache.GetValueUncached(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
            if (!typeKey.CanAcceptValue(key))
                throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(key, typeof(TKey)), nameof(key));
            return GetValueUncached((TKey)key);
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> is found in the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            if (item.Key == null)
                return false;

            int i = GetItemIndex(item.Key);
            return i >= 0 && GetValueComparer().Equals(item.Value, items[i].Value);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>,
        /// starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from
        /// <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0, or larger than length
        /// of <paramref name="array"/>.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="arrayIndex"/> is equal to or greater than the length
        /// of <paramref name="array"/>.
        /// <br/>-or-
        /// <br/>The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available
        /// space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), Res.ArgumentNull);
            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), Res.ArgumentOutOfRange);
            if (array.Length - arrayIndex < Count)
                throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(array));

            for (int i = first; i != -1; i = items[i].NextInOrder)
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(items[i].Key, items[i].Value);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>;
        /// otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original
        /// <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
            => item.Key != null && InternalRemove(item.Key, item.Value, true);

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this, true);

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="T:System.Collections.IDictionary"/> object.
        /// </summary>
        /// <param name="key">The <see cref="T:System.Object"/> to use as the key of the element to add.</param>
        /// <param name="value">The <see cref="T:System.Object"/> to use as the value of the element to add.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="key"/> or <paramref name="value"/> has an invalid type
        /// <br/>-or-
        /// <br/>An element with the same key already exists in the <see cref="T:System.Collections.IDictionary"/> object.</exception>
        void IDictionary.Add(object key, object value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
            if (!typeKey.CanAcceptValue(key))
                throw new ArgumentException(Res.IDictionaryNongenericKeyTypeInvalid(value, typeof(TKey)), nameof(key));
            if (!typeValue.CanAcceptValue(value))
                throw new ArgumentException(Res.ICollectionNongenericValueTypeInvalid(value, typeof(TValue)), nameof(value));

            Add((TKey)key, (TValue)value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IDictionary"/> object contains an element with the specified key.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IDictionary"/> contains an element with the key; otherwise, false.
        /// </returns>
        /// <param name="key">The key to locate in the <see cref="T:System.Collections.IDictionary"/> object.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        bool IDictionary.Contains(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
            return typeKey.CanAcceptValue(key) && ContainsKey((TKey)key);
        }

        /// <summary>
        /// Returns an <see cref="T:System.Collections.IDictionaryEnumerator"/> object for the
        /// <see cref="T:System.Collections.IDictionary"/> object.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IDictionaryEnumerator"/> object for the <see cref="T:System.Collections.IDictionary"/> object.
        /// </returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        IDictionaryEnumerator IDictionary.GetEnumerator() => new Enumerator(this, false);

        /// <summary>
        /// Removes the element with the specified key from the <see cref="T:System.Collections.IDictionary"/> object.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        void IDictionary.Remove(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), Res.ArgumentNull);
            if (typeKey.CanAcceptValue(key))
                Remove((TKey)key);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see cref="T:System.Array"/>,
        /// starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection"/>.
        /// The <see cref="T:System.Array"/> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero,
        /// or larger that <paramref name="array"/> length.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.
        /// <br/>-or-
        /// <br/>The number of elements in the source <see cref="T:System.Collections.ICollection"/> is greater
        /// than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.
        /// <br/>-or-
        /// <br/>Element type of <paramref name="array"/> is neither <see cref="KeyValuePair{TKey,TValue}"/>,
        /// <see cref="DictionaryEntry"/> nor <see cref="object"/>.</exception>
        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), Res.ArgumentNull);
            if (index < 0 || index > array.Length)
                throw new ArgumentOutOfRangeException(nameof(index), Res.ArgumentOutOfRange);
            if (array.Length - index < Count)
                throw new ArgumentException(Res.ICollectionCopyToDestArrayShort, nameof(index));
            if (array.Rank != 1)
                throw new ArgumentException(Res.ICollectionCopyToSingleDimArrayOnly, nameof(array));

            switch (array)
            {
                case KeyValuePair<TKey, TValue>[] keyValuePairs:
                    ((ICollection<KeyValuePair<TKey, TValue>>)this).CopyTo(keyValuePairs, index);
                    return;

                case DictionaryEntry[] dictionaryEntries:
                    for (int i = first; i != -1; i = items[i].NextInOrder)
                        dictionaryEntries[index++] = new DictionaryEntry(items[i].Key, items[i].Value);
                    return;

                case object[] objectArray:
                    for (int i = first; i != -1; i = items[i].NextInOrder)
                        objectArray[index++] = new KeyValuePair<TKey, TValue>(items[i].Key, items[i].Value);
                    return;

                default:
                    throw new ArgumentException(Res.ICollectionArrayTypeInvalid);
            }
        }

        [SecurityCritical]
        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase", Justification = "False alarm, SecurityCriticalAttribute is applied.")]
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info), Res.ArgumentNull);

            info.AddValue(nameof(capacity), capacity);
            info.AddValue(nameof(ensureCapacity), ensureCapacity);
            info.AddValue(nameof(comparer), IsDefaultComparer(comparer) ? null : comparer);
            info.AddValue(nameof(itemLoader), itemLoader.Equals(nullLoader) ? null : itemLoader);
            info.AddValue(nameof(behavior), (byte)behavior);
            info.AddValue(nameof(disposeDroppedValues), disposeDroppedValues);

            int count = Count;
            TKey[] keys = new TKey[count];
            TValue[] values = new TValue[count];
            if (count > 0)
            {
                int i = 0;
                for (int current = first; current != -1; current = items[current].NextInOrder, i++)
                {
                    keys[i] = items[current].Key;
                    values[i] = items[current].Value;
                }
            }

            info.AddValue(nameof(keys), keys);
            info.AddValue(nameof(values), values);

            info.AddValue(nameof(version), version);
            info.AddValue(nameof(cacheReads), cacheReads);
            info.AddValue(nameof(cacheWrites), cacheWrites);
            info.AddValue(nameof(cacheDeletes), cacheDeletes);
            info.AddValue(nameof(cacheHit), cacheHit);

            // custom data of a derived class
            GetObjectData(info, context);
        }

#if !NET35
        [SecuritySafeCritical]
#endif
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            SerializationInfo info = deserializationInfo;

            // may occur with remoting, which calls OnDeserialization twice.
            if (info == null)
                return;

            capacity = info.GetInt32(nameof(capacity));
            ensureCapacity = info.GetBoolean(nameof(ensureCapacity));
            comparer = (IEqualityComparer<TKey>)info.GetValue(nameof(comparer), typeof(IEqualityComparer<TKey>))
                ?? (useEnumKeyComparer ? (IEqualityComparer<TKey>)EnumComparer<TKey>.Comparer : EqualityComparer<TKey>.Default);
            behavior = (CacheBehavior)info.GetByte(nameof(behavior));
            itemLoader = (Func<TKey, TValue>)info.GetValue(nameof(itemLoader), typeof(Func<TKey, TValue>)) ?? nullLoader;
            disposeDroppedValues = info.GetBoolean(nameof(disposeDroppedValues));

            // elements
            TKey[] keys = (TKey[])info.GetValue(nameof(keys), typeof(TKey[]));
            TValue[] values = (TValue[])info.GetValue(nameof(values), typeof(TValue[]));
            int count = keys.Length;
            if (count > 0)
            {
                Initialize(ensureCapacity ? capacity : count);
                for (int i = 0; i < count; i++)
                    Insert(keys[i], values[i], true);
            }

            version = info.GetInt32(nameof(version));
            cacheReads = info.GetInt32(nameof(cacheReads));
            cacheDeletes = info.GetInt32(nameof(cacheWrites));
            cacheDeletes = info.GetInt32(nameof(cacheDeletes));
            cacheHit = info.GetInt32(nameof(cacheHit));

            OnDeserialization(info);

            deserializationInfo = null;
        }

        #endregion

        #endregion

        #endregion
    }
}