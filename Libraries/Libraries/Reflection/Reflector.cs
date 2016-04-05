﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

using KGySoft.Libraries.Collections;
using KGySoft.Libraries.Resources;
using KGySoft.Libraries.WinApi;

namespace KGySoft.Libraries.Reflection
{
    /// <summary>
    /// Possible reflection ways in methods of <see cref="Reflector"/> class.
    /// </summary>
    public enum ReflectionWays
    {
        /// <summary>
        /// Auto decision. In most cases it means the <see cref="DynamicDelegate"/> way.
        /// </summary>
        Auto,

        /// <summary>
        /// Dynamic delegate way. In this case first access of a member is slower than accessing it via
        /// system reflection but further accesses are much more faster.
        /// </summary>
        DynamicDelegate,

        /// <summary>
        /// Uses the standard system reflection way.
        /// </summary>
        SystemReflection,

        /// <summary>
        /// Uses type descriptor way. This is the slowest way but is preferable in case of
        /// <see cref="Component"/>s. Not applicable in all cases.
        /// </summary>
        TypeDescriptor
    }

    /// <summary>
    /// Provides reflection routines on objects that are in most case faster than standard System.Reflection ways.
    /// </summary>
    public static class Reflector
    {
        #region Fields
        
        internal static readonly object[] EmptyObjects = new object[0];

        internal static readonly Type ObjectType = typeof(object);
        internal static readonly Type StringType = typeof(string);
        internal static readonly Type EnumType = typeof(Enum);
        internal static readonly Type Type = typeof(Type);
        // ReSharper disable PossibleMistakenCallToGetType.2
        internal static readonly Type RuntimeType = Type.GetType();
        // ReSharper restore PossibleMistakenCallToGetType.2
        // TODO: elements of parseableType types, ByteArrayType
        internal static readonly Type ByteArrayType = typeof(byte[]);

        internal static readonly Assembly mscorlibAssembly = ObjectType.Assembly;
        internal static readonly Assembly SystemAssembly = typeof(Queue<>).Assembly;
        internal static readonly Assembly SystemCoreAssembly = typeof(HashSet<>).Assembly;
        internal static readonly Assembly KGySoftLibrariesAssembly = typeof(Reflector).Assembly;

        private static readonly HashSet<Type> parseableTypes =
            new HashSet<Type>
            {
                StringType, typeof(char), typeof(byte), typeof(sbyte),
                typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
                typeof(float), typeof(double), typeof(decimal), typeof(bool),
                typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
                typeof(DBNull), typeof(IntPtr), typeof(UIntPtr)
            };

        private static Cache<Type, string> defaultMemberCache;
        private static Cache<string, Assembly> assemblyCache;
        private static Cache<string, Type> typeCacheByString;
        private static Cache<Assembly, Cache<string, Type>> typeCacheByAssembly;
        private static object syncRoot;

        #endregion

        #region Constructor

        //static Reflector()
        //{
        //    RegisterTypeConverter<Encoding, EncodingConverter>();
        //    RegisterTypeConverter<Version, VersionConverter>();
        //}

        #endregion

        #region Private Properties

        private static object SyncRoot
        {
            get
            {
                if (syncRoot == null)
                    Interlocked.CompareExchange(ref syncRoot, new object(), null);
                return syncRoot;
            }
        }

        private static Cache<Type, string> DefaultMemberCache
        {
            get
            {
                return defaultMemberCache ?? (defaultMemberCache = new Cache<Type, string>(GetDefaultMember, 16));
            }
        }

        private static Cache<string, Type> TypeCacheByString
        {
            get { return typeCacheByString ?? (typeCacheByString = new Cache<string, Type>(Cache<string, Type>.NullLoader, 64)); }
        }

        private static Cache<Assembly, Cache<string, Type>> TypeCacheByAssembly
        {
            get { return typeCacheByAssembly ?? (typeCacheByAssembly = new Cache<Assembly, Cache<string, Type>>(a => new Cache<string, Type>(Cache<string, Type>.NullLoader, 64), 16)); }
        }

        private static Cache<string, Assembly> AssemblyCache
        {
            get { return assemblyCache ?? (assemblyCache = new Cache<string, Assembly>(Cache<string, Assembly>.NullLoader, 16)); }
        }

        #endregion

        #region Parse from string

        /// <summary>
        /// Parses an object from a string value. Firstly, it tries to parse the type natively.
        /// If type cannot be parsed natively but the type has a type converter that can convert from string,
        /// then type converter is used.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <param name="value">Value in string format to parse. If value is <see langword="null"/> and <paramref name="type"/> is a reference type, returns <see langword="null"/>.</param>
        /// <param name="context">An optional context for assigning the value via type converter.</param>
        /// <param name="culture">Appropriate culture needed for number types.</param>
        /// <returns>The parsed value.</returns>
        /// <remarks>
        /// Natively parsed types:
        /// <list type="bullet">
        /// <item><description><see cref="System.Enum"/> based types</description></item>
        /// <item><description><see cref="string"/></description></item>
        /// <item><description><see cref="char"/></description></item>
        /// <item><description><see cref="byte"/></description></item>
        /// <item><description><see cref="sbyte"/></description></item>
        /// <item><description><see cref="short"/></description></item>
        /// <item><description><see cref="ushort"/></description></item>
        /// <item><description><see cref="int"/></description></item>
        /// <item><description><see cref="uint"/></description></item>
        /// <item><description><see cref="long"/></description></item>
        /// <item><description><see cref="ulong"/></description></item>
        /// <item><description><see cref="float"/></description></item>
        /// <item><description><see cref="double"/></description></item>
        /// <item><description><see cref="decimal"/></description></item>
        /// <item><description><see cref="bool"/></description></item>
        /// <item><description><see cref="Type"/></description></item>
        /// <item><description><see cref="DateTime"/></description></item>
        /// <item><description><see cref="DateTimeOffset"/></description></item>
        /// <item><description><see cref="TimeSpan"/></description></item>
        /// <item><description><see cref="DBNull"/> (if <paramref name="value"/> is <see langword="null"/>, returns null; otherwise, <paramref name="value"/> is ignored)</description></item>
        /// <item><description><see cref="object"/> (<paramref name="value"/> treated as string but value <c>null</c> returns <see langword="null"/> reference and empty value returns a new <see cref="object"/> instance)</description></item>
        /// <item><description><see cref="Nullable{T}"/> of types above: <c>null</c> or empty value returns <see langword="null"/>; oherwise, <paramref name="value"/> is parsed as the underlying type</description></item>
        /// <item><description>Any types that can convert their value from <see cref="string"/> by a <see cref="TypeConverter"/> class.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>, or <paramref name="type"/> is not nullable and <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Parameter <paramref name="value"/> cannot be parsed as <paramref name="type"/></exception>
        /// <exception cref="FormatException">Parameter <paramref name="value"/> cannot be parsed as <paramref name="type"/></exception>
        /// <exception cref="ReflectionException">Parameter <paramref name="value"/> cannot be parsed as <see cref="Type"/> -or- no appropriate <see cref="TypeConverter"/> found to convert <paramref name="value"/> from <see cref="string"/> value.</exception>
        public static object Parse(Type type, string value, ITypeDescriptorContext context, CultureInfo culture)
        {
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            if (value == null)
            {
                if (!type.IsValueType || type.IsNullable())
                {
                   if (CanParseNatively(Nullable.GetUnderlyingType(type)))
                       return null;
                }
                else
                    throw new ArgumentNullException("value", Res.Get(Res.ParsedValueNull));
            }

            if (type.IsNullable() && CanParseNatively(Nullable.GetUnderlyingType(type)))
            {
                if (String.IsNullOrEmpty(value))
                    return null;

                return Parse(Nullable.GetUnderlyingType(type), value, context, culture);
            }

            // ReSharper disable AssignNullToNotNullAttribute
            // ReSharper disable PossibleNullReferenceException
            try
            {
                if (type.IsByRef)
                    type = type.GetElementType();
                if (type.IsEnum)
                    return Enum.Parse(type, value);
                if (type == typeof(string))
                    return value;
                if (type == typeof(char))
                    return Char.Parse(value);
                if (type == typeof(byte))
                    return Byte.Parse(value, culture);
                if (type == typeof(sbyte))
                    return SByte.Parse(value, culture);
                if (type == typeof(short))
                    return Int16.Parse(value, culture);
                if (type == typeof(ushort))
                    return UInt16.Parse(value, culture);
                if (type == typeof(int))
                    return Int32.Parse(value, culture);
                if (type == typeof(uint))
                    return UInt32.Parse(value, culture);
                if (type == typeof(long))
                    return Int64.Parse(value, culture);
                if (type == typeof(ulong))
                    return UInt64.Parse(value, culture);
                if (type == typeof(IntPtr))
                    return (IntPtr)Int64.Parse(value, culture);
                if (type == typeof(UIntPtr))
                    return (UIntPtr)UInt64.Parse(value, culture);
                if (type == typeof(float))
                {
                    float result = Single.Parse(value, culture);
                    if (result.Equals(0f)
                        && (culture == null && value.Trim()[0] == '-'
                            || culture != null && value.Trim().StartsWith(culture.NumberFormat.NegativeSign)))
                    {
                        return -0f;
                    }

                    return result;
                }
                if (type == typeof(double))
                {
                    double result = Double.Parse(value, culture);
                    if (result.Equals(0d) 
                        && (culture == null && value.Trim()[0] == '-'
                            || culture != null && value.Trim().StartsWith(culture.NumberFormat.NegativeSign)))
                    {
                        return -0d;
                    }
                    return result;
                }
                if (type == typeof(decimal))
                    return Decimal.Parse(value, culture);
                if (type == typeof(TimeSpan))
#if NET35
                    return TimeSpan.Parse(value);
#elif NET40 || NET45
                    return TimeSpan.Parse(value, culture);
#else
#error .NET version is not set or not supported!
#endif

                if (type == typeof(bool))
                {
                    StringComparer comparer = StringComparer.OrdinalIgnoreCase;
                    if (comparer.EqualsAny(value, "true", "1"))
                        return true;
                    if (comparer.EqualsAny(value, "false", "0"))
                        return false;
                    throw new ArgumentException(Res.Get(Res.NotABool, value), "value");
                }
                if (type.In(Type, RuntimeType))
                {
                    object result = ResolveType(value);
                    if (result == null)
                        throw new ArgumentException(Res.Get(Res.NotAType, value), "value");
                    return result;
                }
                if (type == typeof(DateTime))
                {
                    DateTimeStyles style = value.EndsWith("Z") ? DateTimeStyles.AdjustToUniversal : DateTimeStyles.None;
                    return DateTime.Parse(value, culture, style);
                }
                if (type == typeof(DateTimeOffset))
                {
                    DateTimeStyles style = value.EndsWith("Z") ? DateTimeStyles.AdjustToUniversal : DateTimeStyles.None;
                    return DateTimeOffset.Parse(value, culture, style);
                }
                if (type == ObjectType)
                {
                    if (value.Length == 0)
                        return new object();
                    return value;
                }
                if (type == typeof(DBNull))
                {
                    return DBNull.Value;
                }
                //if (type == typeof(void))
                //{
                //    if (value == "null" || value.Length == 0)
                //        return null;
                //    throw new ArgumentException("Void type can only hold null value", "value");
                //}

                // Using type converter as a fallback
                TypeConverter converter = TypeDescriptor.GetConverter(type);
                if (converter.CanConvertFrom(context, typeof(string)))
                    return converter.ConvertFrom(context, culture, value);

                throw new NotSupportedException(Res.Get(Res.TypeCannotBeParsed, type));
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ArgumentException(Res.Get(Res.ParseError, type, value, e.Message), "value", e);
            }
            // ReSharper restore AssignNullToNotNullAttribute
            // ReSharper restore PossibleNullReferenceException
        }

        /// <summary>
        /// Parses an object from a string value without context using invariant culture. In first place tries to parse the type natively.
        /// If native conversion fails but the type has a type converter that can convert from string,
        /// then type converter is used.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <param name="value">Value in string to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <remarks>
        /// Natively parsed types:
        /// <list type="bullet">
        /// <item><description><see cref="Enum"/> based types</description></item>
        /// <item><description><see cref="string"/></description></item>
        /// <item><description><see cref="char"/></description></item>
        /// <item><description><see cref="byte"/></description></item>
        /// <item><description><see cref="sbyte"/></description></item>
        /// <item><description><see cref="short"/></description></item>
        /// <item><description><see cref="ushort"/></description></item>
        /// <item><description><see cref="int"/></description></item>
        /// <item><description><see cref="uint"/></description></item>
        /// <item><description><see cref="long"/></description></item>
        /// <item><description><see cref="ulong"/></description></item>
        /// <item><description><see cref="float"/></description></item>
        /// <item><description><see cref="double"/></description></item>
        /// <item><description><see cref="decimal"/></description></item>
        /// <item><description><see cref="bool"/></description></item>
        /// <item><description><see cref="DateTime"/></description></item>
        /// <item><description><see cref="Type"/></description></item>
        /// <item><description><see cref="Void"/> (only "null" or empty value is accepted)</description></item>
        /// <item><description><see cref="object"/> (value treated as string but value "null" returns <see langword="null"/> and empty string returns a new <see cref="object"/> instance)</description></item>
        /// <item><description><see cref="DBNull"/> (only "null", "DBNull" or empty value is accepted)</description></item>
        /// <item><description>Any types that can convert their value from System.String via type converters</description></item>
        /// </list>
        /// </remarks>
        public static object Parse(Type type, string value)
        {
            return Parse(type, value, null, CultureInfo.InvariantCulture);
        }

        internal static bool CanParseNatively(Type type)
        {
            return type.IsEnum || parseableTypes.Contains(type) || type == RuntimeType;
        }

        #endregion

        #region SetProperty

        /// <summary>
        /// Sets a property based on <see cref="PropertyInfo"/> medatada given in <paramref name="property"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="property"/> to set is an instance property, then this parameter should
        /// contain the object instance on which the property setting should be performed.</param>
        /// <param name="property">The property to set.</param>
        /// <param name="value">The desired new value of the property</param>
        /// <param name="indexerParameters">Indexer parameters if <paramref name="property"/> is an indexer. Otherwise, this parameter is omitted.</param>
        /// <param name="way">Preferred access mode. Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a property is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static void SetProperty(object instance, PropertyInfo property, object value, ReflectionWays way, params object[] indexerParameters)
        {
            if (property == null)
                throw new ArgumentNullException("property", Res.Get(Res.ArgumentNull));

            bool isStatic = property.GetSetMethod(true).IsStatic;
            if (instance == null && !isStatic)
                throw new ArgumentNullException("instance", Res.Get(Res.InstanceIsNull));
            if (!property.CanWrite)
                throw new InvalidOperationException(Res.Get(Res.PropertyHasNoSetter, property.DeclaringType, property.Name));

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                PropertyAccessor.GetPropertyAccessor(property).Set(instance, value, indexerParameters);
            }
            else if (way == ReflectionWays.SystemReflection)
            {
                property.SetValue(instance, value, indexerParameters);
            }
            else// if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.SetPropertyTypeDescriptorNotSupported));
            }
        }

        /// <summary>
        /// Sets a property based on <see cref="PropertyInfo"/> medatada given in <paramref name="property"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="property"/> to set is an instance property, then this parameter should
        /// contain the object instance on which the property setting should be performed.</param>
        /// <param name="property">The property to set.</param>
        /// <param name="value">The desired new value of the property</param>
        public static void SetProperty(object instance, PropertyInfo property, object value)
        {
            SetProperty(instance, property, value, ReflectionWays.Auto);
        }

        /// <summary>
        /// Internal implementation of SetInstance/StaticPropertyByName methods
        /// </summary>
        private static void SetPropertyByName(string propertyName, Type type, object instance, object value, ReflectionWays way, object[] indexerParameters)
        {
            // type descriptor
            if (way == ReflectionWays.TypeDescriptor || (way == ReflectionWays.Auto && instance is ICustomTypeDescriptor && (indexerParameters == null || indexerParameters.Length == 0)))
            {
                if (instance == null)
                    throw new NotSupportedException(Res.Get(Res.CannotSetStaticPropertyTypeDescriptor));
                PropertyDescriptor property = TypeDescriptor.GetProperties(instance)[propertyName];
                if (property != null)
                {
                    property.SetValue(instance, value);
                    return;
                }
                throw new ReflectionException(Res.Get(Res.CannotSetPropertyTypeDescriptor, propertyName, type.FullName));
            }

            // lambda or system reflection
            Exception lastException = null;
            for (Type checkedType = type; checkedType.BaseType != null; checkedType = checkedType.BaseType)
            {
                BindingFlags flags = type == checkedType ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy : BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                flags |= instance == null ? BindingFlags.Static : BindingFlags.Instance;
                foreach (PropertyInfo property in checkedType.GetMember(propertyName, MemberTypes.Property, flags))
                {
                    ParameterInfo[] indexParams = property.GetIndexParameters();

                    // skip when parameter count is not corrent
                    if (indexParams.Length != indexerParameters.Length)
                        continue;

                    if (!CheckParameters(indexParams, indexerParameters))
                        continue;
                    try
                    {
                        SetProperty(instance, property, value, way, indexerParameters);
                        return;
                    }
                    catch (TargetInvocationException e)
                    {
                        // invocation was successful, the exception comes from the property itself: we may rethrow the inner exception.
                        throw e.InnerException;
                    }
                    catch (Exception e)
                    {
                        // invoking of the property failed - maybe we tried to invoke an incorrect overloaded version of the property: we skip to the next overloaded property while save last exception
                        lastException = e;
                    }
                }
            }

            if (lastException != null)
                throw lastException;
            throw new ReflectionException(Res.Get(instance == null ? Res.StaticPropertyDoesNotExist : Res.InstancePropertyDoesNotExist, propertyName, type));
        }

        /// <summary>
        /// Sets an instance property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties. To avoid ambiguity (in case of indexers), this method gets
        /// all of the properties of the same name and chooses the first one to which provided <paramref name="indexerParameters"/> fit.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the property should be set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="value">The new desired value of the property to set.</param>
        /// <param name="way">Preferred access mode.
        /// Auto option uses type descriptor mode if <paramref name="instance"/> is <see cref="ICustomTypeDescriptor"/> and <paramref name="indexerParameters"/> are not defined,
        /// otherwise, uses dynamic delegate mode. In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a property is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> or <see cref="ReflectionWays.TypeDescriptor"/> way.
        /// On components you may want to use the <see cref="ReflectionWays.TypeDescriptor"/> way to trigger property change events and to make possible to roll back
        /// changed values on error.
        /// </param>
        /// <param name="indexerParameters">Index parameters if the property to set is an indexer.</param>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static void SetInstancePropertyByName(object instance, string propertyName, object value, ReflectionWays way, params object[] indexerParameters)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                indexerParameters = EmptyObjects;

            Type type = instance.GetType();
            SetPropertyByName(propertyName, type, instance, value, way, indexerParameters);
        }

        /// <summary>
        /// Sets an instance property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties. To avoid ambiguity (in case of indexers), this method gets
        /// all of the properties of the same name and chooses the first one to which provided <paramref name="indexerParameters"/> fit.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the property should be set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="value">The new desired value of the property to set.</param>
        /// <param name="indexerParameters">Index parameters if the property to set is an indexer.</param>
        public static void SetInstancePropertyByName(object instance, string propertyName, object value, params object[] indexerParameters)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                indexerParameters = EmptyObjects;

            Type type = instance.GetType();
            SetPropertyByName(propertyName, type, instance, value, ReflectionWays.Auto, indexerParameters);
        }

        /// <summary>
        /// Sets a static property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the property to set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="value">The new desired value of the property to set.</param>
        /// <param name="way">Preferred access mode.
        /// Auto option uses dynamic delegate mode. In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a property is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> or <see cref="ReflectionWays.TypeDescriptor"/> way.
        /// On components you may want to use the <see cref="ReflectionWays.TypeDescriptor"/> way to trigger property change events and to make possible to roll back
        /// changed values on error.
        /// </param>
        public static void SetStaticPropertyByName(Type type, string propertyName, object value, ReflectionWays way)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            SetPropertyByName(propertyName, type, null, value, way, EmptyObjects);
        }

        /// <summary>
        /// Sets a static property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the property to set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="value">The new desired value of the property to set.</param>
        public static void SetStaticPropertyByName(Type type, string propertyName, object value)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            SetPropertyByName(propertyName, type, null, value, ReflectionWays.Auto, EmptyObjects);
        }

        /// <summary>
        /// Sets the appropriate indexed member of an object <paramref name="instance"/>. The matching indexer is
        /// selected by the provided <paramref name="indexerParameters"/>. If you have already gained the <see cref="PropertyInfo"/> reference of the indexer to
        /// access, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <remarks>
        /// This method does not access indexers of explicit interface implementations. Though <see cref="Array"/>s have
        /// only explicitly implemented interface indexers, this method can be used also for arrays if <paramref name="indexerParameters"/>
        /// can be converted to integer (respectively <see cref="long"/>) values. In case of arrays <paramref name="way"/> is irrelevant.
        /// </remarks>
        /// <param name="instance">The object instance on which the indexer should be set.</param>
        /// <param name="value">The new desired value of the indexer to set.</param>
        /// <param name="way">Preferred access mode.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of an indexer is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// <see cref="ReflectionWays.TypeDescriptor"/> way cannot be used on indexers.
        /// </param>
        /// <param name="indexerParameters">Index parameters.</param>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static void SetIndexedMember(object instance, object value, ReflectionWays way, params object[] indexerParameters)
        {
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                throw new ArgumentNullException("indexerParameters", Res.Get(Res.ArgumentNull));
            if (indexerParameters.Length == 0)
                throw new ArgumentException(Res.Get(Res.EmptyIndices), "indexerParameters");
            if (way == ReflectionWays.TypeDescriptor)
                throw new NotSupportedException(Res.Get(Res.SetIndexerTypeDescriptorNotSupported));

            // Arrays
            Array array = instance as Array;
            if (array != null)
            {
                if (array.Rank != indexerParameters.Length)
                    throw new ArgumentException(Res.Get(Res.IndexParamsLengthMismatch, array.Rank), "indexerParameters");

                long[] indices = new long[indexerParameters.Length];
                try
                {
                    for (int i = 0; i < indexerParameters.Length; i++)
                    {
                        indices[i] = Convert.ToInt64(indexerParameters[i]);
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(Res.Get(Res.IndexParamsTypeMismatch), "indexerParameters", e);
                }
                array.SetValue(value, indices);
                return;
            }

            // Real indexers
            Type type = instance.GetType();
            Exception lastException = null;
            string defaultMemberName;

            for (Type checkedType = type; checkedType != null; checkedType = checkedType.BaseType)
            {
                lock (SyncRoot)
                {
                    defaultMemberName = DefaultMemberCache[checkedType];
                }

                if (String.IsNullOrEmpty(defaultMemberName))
                    continue;

                foreach (PropertyInfo property in checkedType.GetMember(defaultMemberName, MemberTypes.Property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    ParameterInfo[] indexParams = property.GetIndexParameters();

                    // skip when parameter count is not corrent
                    if (indexParams.Length != indexerParameters.Length)
                        continue;

                    if (!CheckParameters(indexParams, indexerParameters))
                        continue;
                    try
                    {
                        SetProperty(instance, property, value, way, indexerParameters);
                        return;
                    }
                    catch (TargetInvocationException e)
                    {
                        // invocation was successful, the exception comes from the indexer itself: we may rethrow the inner exception.
                        throw e.InnerException;
                    }
                    catch (Exception e)
                    {
                        // invoking of the indexer failed - maybe we tried to invoke an incorrect overloaded version of the indexer: we skip to the next overloaded indexer while save last exception
                        lastException = e;
                    }
                }
            }

            if (lastException != null)
                throw lastException;
            throw new ReflectionException(Res.Get(Res.IndexerDoesNotExist, instance.GetType()));
        }

        /// <summary>
        /// Sets the appropriate indexed member of an object <paramref name="instance"/>. The matching indexer is
        /// selected by the provided <paramref name="indexerParameters"/>. If you have already gained the <see cref="PropertyInfo"/> reference of the indexer to
        /// access, then it is recommended to use <see cref="SetProperty(object,System.Reflection.PropertyInfo,object,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <remarks>
        /// This method does not access indexers of explicit interface implementations. Though <see cref="Array"/>s have
        /// only explicitly implemented interface indexers, this method can be used also for arrays if <paramref name="indexerParameters"/>
        /// can be converted to integer (respectively <see cref="long"/>) values.
        /// </remarks>
        /// <param name="instance">The object instance on which the indexer should be set.</param>
        /// <param name="value">The new desired value of the indexer to set.</param>
        /// <param name="indexerParameters">Index parameters.</param>
        public static void SetIndexedMember(object instance, object value, params object[] indexerParameters)
        {
            SetIndexedMember(instance, value, ReflectionWays.Auto, indexerParameters);
        }

        #endregion

        #region GetProperty

        /// <summary>
        /// Gets a property based on <see cref="PropertyInfo"/> medatada given in <paramref name="property"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="property"/> to get is an instance property, then this parameter should
        /// contain the object instance on which the property getting should be performed.</param>
        /// <param name="property">The property to get.</param>
        /// <param name="indexerParameters">Indexer parameters if <paramref name="property"/> is an indexer. Otherwise, this parameter is omitted.</param>
        /// <param name="way">Preferred reflection mode. Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// Auto option uses dynamic delegate mode. In case of dynamic delegate mode first access of a property is slow but
        /// further accesses are faster than the system reflection way.
        /// </param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// <note>
        /// If the property getter changes the inner state of the instance, then to preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object GetProperty(object instance, PropertyInfo property, ReflectionWays way, params object[] indexerParameters)
        {
            if (property == null)
                throw new ArgumentNullException("property", Res.Get(Res.ArgumentNull));
            if (instance == null && !property.GetGetMethod(true).IsStatic)
                throw new ArgumentNullException("instance", Res.Get(Res.InstanceIsNull));
            if (!property.CanRead)
                throw new InvalidOperationException(Res.Get(Res.PropertyHasNoGetter, property.DeclaringType, property.Name));

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                return PropertyAccessor.GetPropertyAccessor(property).Get(instance, indexerParameters);
            }
            else if (way == ReflectionWays.Auto || way == ReflectionWays.SystemReflection)
            {
                return property.GetValue(instance, indexerParameters);
            }
            else //if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.CannotGetPropertyTypeDescriptor));
            }
        }

        /// <summary>
        /// Gets a property based on <see cref="PropertyInfo"/> medatada given in <paramref name="property"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="property"/> to get is an instance property, then this parameter should
        /// contain the object instance on which the property getting should be performed.</param>
        /// <param name="property">The property to get.</param>
        /// <returns>The value of the property.</returns>
        public static object GetProperty(object instance, PropertyInfo property)
        {
            return GetProperty(instance, property, ReflectionWays.Auto, null);
        }

        /// <summary>
        /// Internal implementation of GetInstance/StaticPropertyByName methods
        /// </summary>
        private static object GetPropertyByName(string propertyName, Type type, object instance, ReflectionWays way, object[] indexerParameters)
        {
            // type descriptor
            if (way == ReflectionWays.TypeDescriptor || (way == ReflectionWays.Auto && instance is ICustomTypeDescriptor && (indexerParameters == null || indexerParameters.Length == 0)))
            {
                if (instance == null)
                    throw new NotSupportedException(Res.Get(Res.CannotGetStaticPropertyTypeDescriptor));
                PropertyDescriptor property = TypeDescriptor.GetProperties(instance)[propertyName];
                if (property != null)
                    return property.GetValue(instance);
                throw new ReflectionException(Res.Get(Res.CannotGetPropertyTypeDescriptor, propertyName, type.FullName));
            }

            Exception lastException = null;
            for (Type checkedType = type; checkedType.BaseType != null; checkedType = checkedType.BaseType)
            {
                // lambda or system reflection
                BindingFlags flags = type == checkedType
                    ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                    : BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                flags |= instance == null ? BindingFlags.Static : BindingFlags.Instance;
                foreach (PropertyInfo property in checkedType.GetMember(propertyName, MemberTypes.Property, flags))
                {
                    ParameterInfo[] indexParams = property.GetIndexParameters();

                    // skip when parameter count is not corrent
                    if (indexParams.Length != indexerParameters.Length)
                        continue;

                    if (!CheckParameters(indexParams, indexerParameters))
                        continue;
                    try
                    {
                        return GetProperty(instance, property, way, indexerParameters);
                    }
                    catch (TargetInvocationException e)
                    {
                        // invokation was successful, the exception comes from the property itself: we may rethrow the inner exception.
                        throw e.InnerException;
                    }
                    catch (Exception e)
                    {
                        // invoking of the method failed - maybe we tried to invoke an incorrect overloaded version of the method: we skip to the next overloaded method while save last exception
                        lastException = e;
                    }
                }
            }

            if (lastException != null)
                throw lastException;

            throw new ReflectionException(Res.Get(instance == null ? Res.StaticPropertyDoesNotExist : Res.InstancePropertyDoesNotExist, propertyName, type.FullName));
        }

        /// <summary>
        /// Gets an instance property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties. To avoid ambiguity (in case of indexers), this method gets
        /// all of the properties of the same name and chooses the first one to which provided <paramref name="indexerParameters"/> fit.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the property should be set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="way">Preferred access mode.
        /// Auto option uses type descriptor mode if <paramref name="instance"/> is <see cref="ICustomTypeDescriptor"/> and <paramref name="indexerParameters"/> are not defined,
        /// otherwise, uses dynamic delegate mode. In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a property is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> or <see cref="ReflectionWays.TypeDescriptor"/> way.
        /// On components you may want to use the <see cref="ReflectionWays.TypeDescriptor"/> way to trigger property change events and to make possible to roll back
        /// changed values on error.
        /// </param>
        /// <param name="indexerParameters">Index parameters if the property to set is an indexer.</param>
        /// <returns>Returns the value of the property.</returns>
        /// <remarks>
        /// <note>
        /// If the property getter changes the inner state of the instance, then to preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object GetInstancePropertyByName(object instance, string propertyName, ReflectionWays way, params object[] indexerParameters)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                indexerParameters = EmptyObjects;

            Type type = instance.GetType();
            return GetPropertyByName(propertyName, type, instance, way, indexerParameters);
        }

        /// <summary>
        /// Gets an instance property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties. To avoid ambiguity (in case of indexers), this method gets
        /// all of the properties of the same name and chooses the first one to which provided <paramref name="indexerParameters"/> fit.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the property should be set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="indexerParameters">Index parameters if the property to set is an indexer.</param>
        /// <returns>Returns the value of the property.</returns>
        public static object GetInstancePropertyByName(object instance, string propertyName, params object[] indexerParameters)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                indexerParameters = EmptyObjects;

            Type type = instance.GetType();
            return GetPropertyByName(propertyName, type, instance, ReflectionWays.Auto, indexerParameters);
        }

        /// <summary>
        /// Gets a static property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the property to set.</param>
        /// <param name="propertyName">The property name to set.</param>
        /// <param name="way">Preferred access mode.
        /// Auto option uses dynamic delegate mode. In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a property is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> or <see cref="ReflectionWays.TypeDescriptor"/> way.
        /// On components you may want to use the <see cref="ReflectionWays.TypeDescriptor"/> way to trigger property change events and to make possible to roll back
        /// changed values on error.
        /// </param>
        /// <returns>Returns the value of the property.</returns>
        public static object GetStaticPropertyByName(Type type, string propertyName, ReflectionWays way)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            return GetPropertyByName(propertyName, type, null, way, EmptyObjects);
        }

        /// <summary>
        /// Gets a static property based on a property name given in <paramref name="propertyName"/> parameter.
        /// Property can refer to either public or non-public properties.
        /// If you do not need the property name to be parsed from string, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the property to set.</param>
        /// <param name="propertyName">The property name to set.</param>
        public static object GetStaticPropertyByName(Type type, string propertyName)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            return GetPropertyByName(propertyName, type, null, ReflectionWays.Auto, EmptyObjects);
        }

        /// <summary>
        /// Gets the appropriate indexed member of an object <paramref name="instance"/>. The matching indexer is
        /// selected by the provided <paramref name="indexerParameters"/>. If you have already gained the <see cref="PropertyInfo"/> reference of the indexer to
        /// access, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the indexer should be get.</param>
        /// <param name="way">Preferred access mode.</param>
        /// <param name="indexerParameters">Index parameters.
        /// Auto option uses dynamic delegate mode. In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of an indexer is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// <see cref="ReflectionWays.TypeDescriptor"/> way cannot be used on indexers.
        /// </param>
        /// <returns>The value of the indexer.</returns>
        /// <remarks>
        /// This method does not access indexers of explicit interface implementations. Though <see cref="Array"/>s have
        /// only explicitly implemented interface indexers, this method can be used also for arrays if <paramref name="indexerParameters"/>
        /// can be converted to integer (respectively <see cref="long"/>) values. In case of arrays <paramref name="way"/> parameter is irrelevant.
        /// </remarks>
        public static object GetIndexedMember(object instance, ReflectionWays way, params object[] indexerParameters)
        {
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (indexerParameters == null)
                throw new ArgumentNullException("indexerParameters", Res.Get(Res.ArgumentNull));
            if (indexerParameters.Length == 0)
                throw new ArgumentException(Res.Get(Res.EmptyIndices), "indexerParameters");
            if (way == ReflectionWays.TypeDescriptor)
                throw new NotSupportedException(Res.Get(Res.GetIndexerTypeDescriptorNotSupported));

            // Arrays
            Array array = instance as Array;
            if (array != null)
            {
                if (array.Rank != indexerParameters.Length)
                    throw new ArgumentException(Res.Get(Res.IndexParamsLengthMismatch, array.Rank), "indexerParameters");
                long[] indices = new long[indexerParameters.Length];
                try
                {
                    for (int i = 0; i < indexerParameters.Length; i++)
                    {
                        indices[i] = Convert.ToInt64(indexerParameters[i]);
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(Res.Get(Res.IndexParamsTypeMismatch), "indexerParameters", e);
                }
                return array.GetValue(indices);
            }

            // Real indexers
            Type type = instance.GetType();
            Exception lastException = null;
            string defaultMemberName;

            for (Type checkedType = type; checkedType != null; checkedType = checkedType.BaseType)
            {
                lock (SyncRoot)
                {
                    defaultMemberName = DefaultMemberCache[checkedType];
                }

                if (String.IsNullOrEmpty(defaultMemberName))
                    continue;

                foreach (PropertyInfo property in checkedType.GetMember(defaultMemberName, MemberTypes.Property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    ParameterInfo[] indexParams = property.GetIndexParameters();

                    // skip when parameter count is not corrent
                    if (indexParams.Length != indexerParameters.Length)
                        continue;

                    if (!CheckParameters(indexParams, indexerParameters))
                        continue;
                    try
                    {
                        return GetProperty(instance, property, way, indexerParameters);
                    }
                    catch (TargetInvocationException e)
                    {
                        // invocation was successful, the exception comes from the indexer itself: we may rethrow the inner exception.
                        throw e.InnerException;
                    }
                    catch (Exception e)
                    {
                        // invoking of the indexer failed - maybe we tried to invoke an incorrect overloaded version of the indexer: we skip to the next overloaded indexer while save last exception
                        lastException = e;
                        continue;
                    }
                }
            }
            if (lastException != null)
                throw lastException;
            throw new ReflectionException(Res.Get(Res.IndexerDoesNotExist, instance.GetType()));
        }

        /// <summary>
        /// Gets the appropriate indexed member of an object <paramref name="instance"/>. The matching indexer is
        /// selected by the provided <paramref name="indexerParameters"/>. If you have already gained the <see cref="PropertyInfo"/> reference of the indexer to
        /// access, then it is recommended to use <see cref="GetProperty(object,System.Reflection.PropertyInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <remarks>
        /// This method does not access indexers of explicit interface implementations. Though <see cref="Array"/>s have
        /// only explicitly implemented interface indexers, this method can be used also for arrays if <paramref name="indexerParameters"/>
        /// can be converted to integer (respectively <see cref="long"/>) values.
        /// </remarks>
        /// <param name="instance">The object instance on which the indexer should be get.</param>
        /// <param name="indexerParameters">Index parameters.</param>
        /// <returns>The value of the indexer.</returns>
        public static object GetIndexedMember(object instance, params object[] indexerParameters)
        {
            return GetIndexedMember(instance, ReflectionWays.Auto, indexerParameters);
        }

        #endregion

        #region RunMethod

        /// <summary>
        /// Runs a method based on a <see cref="MethodInfo"/> medatada given in <paramref name="method"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="method"/> to call is an instance method, then this parameter should
        /// contain the object instance on which the method should be called.</param>
        /// <param name="method">The method to invoke.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="genericParameters">Generic type parameters if method is generic. For non-generic methods this parameter is omitted.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object RunMethod(object instance, MethodInfo method, Type[] genericParameters, ReflectionWays way, params object[] parameters)
        {
            if (method == null)
                throw new ArgumentNullException("method", Res.Get(Res.ArgumentNull));
            bool isStatic = method.IsStatic;
            if (instance == null && !isStatic)
                throw new ArgumentNullException("instance", Res.Get(Res.InstanceIsNull));

            // if the method is generic we need the generic arguments and a constructed method with real types
            if (method.IsGenericMethodDefinition)
            {
                if (genericParameters == null)
                    throw new ArgumentNullException("genericParameters", Res.Get(Res.TypeParamsAreNull));
                else if (genericParameters.Length != method.GetGenericArguments().Length)
                    throw new ArgumentException(Res.Get(Res.TypeArgsLengthMismatch), "genericParameters");
                try
                {
                    method = method.MakeGenericMethod(genericParameters);
                }
                catch (Exception e)
                {
                    throw new ReflectionException(Res.Get(Res.CannotCreateGenericMethod), e);
                }
            }

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                return MethodInvoker.GetMethodInvoker(method).Invoke(instance, parameters);
            }
            else if (way == ReflectionWays.SystemReflection)
            {
                return method.Invoke(instance, parameters);
            }
            else// if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.InvokeMethodTypeDescriptorNotSupported));
            }
        }

        /// <summary>
        /// Runs a method based on a <see cref="MethodInfo"/> medatada given in <paramref name="method"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="method"/> to call is an instance method, then this parameter should
        /// contain the object instance on which the method should be called.</param>
        /// <param name="method">The method to invoke.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object RunMethod(object instance, MethodInfo method, ReflectionWays way, params object[] parameters)
        {
            return RunMethod(instance, method, null, way, parameters);
        }

        /// <summary>
        /// Runs a method based on a <see cref="MethodInfo"/> medatada given in <paramref name="method"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="method"/> to call is an instance method, then this parameter should
        /// contain the object instance on which the method should be called.</param>
        /// <param name="method">The method to invoke.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunMethod(object instance, MethodInfo method, params object[] parameters)
        {
            return RunMethod(instance, method, null, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Internal implementation of RunInstance/StaticMethodByName methods
        /// </summary>
        private static object RunMethodByName(string methodName, Type type, object instance, object[] parameters, Type[] genericParameters, ReflectionWays way)
        {
            Exception lastException = null;
            for (Type checkedType = type; checkedType.BaseType != null; checkedType = checkedType.BaseType)
            {
                BindingFlags flags = type == checkedType
                    ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                    : BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                flags |= instance == null ? BindingFlags.Static : BindingFlags.Instance;
                foreach (MethodInfo method in checkedType.GetMember(methodName, MemberTypes.Method, flags))
                {
                    ParameterInfo[] methodParams = method.GetParameters();

                    // skip when parameter count is not corrent
                    if (methodParams.Length != parameters.Length)
                        continue;

                    MethodInfo mi = method;
                    // if the method is generic we need the generic arguments and a constructed method with real types
                    if (mi.IsGenericMethodDefinition)
                    {
                        Type[] genArgs = mi.GetGenericArguments();
                        if (genericParameters.Length != genArgs.Length)
                        {
                            lastException = new ArgumentException(Res.Get(Res.TypeArgsLengthMismatch, genArgs.Length), "genericParameters");
                            continue;
                        }
                        try
                        {
                            mi = mi.MakeGenericMethod(genericParameters);
                            methodParams = mi.GetParameters();
                        }
                        catch (Exception e)
                        {
                            lastException = e;
                            continue;
                        }
                    }

                    if (!CheckParameters(methodParams, parameters))
                        continue;
                    
                    try
                    {
                        return RunMethod(instance, mi, way, parameters);
                    }
                    catch (TargetInvocationException e)
                    {
                        // the method was successfully invoked, the exception comes from the method itself: we may rethrow the inner exception.
                        throw e.InnerException;
                    }
                    catch (Exception e)
                    {
                        // invoking of the method failed - maybe we tried to invoke an incorrect overloaded version of the method: we skip to the next overloaded method while save last exception
                        lastException = e;
                    }
                }
            }

            if (lastException != null)
                throw lastException;

            throw new ReflectionException(Res.Get(instance == null ? Res.StaticMethodDoesNotExist : Res.InstanceMethodDoesNotExist, methodName, type));
        }

        /// <summary>
        /// Runs an instance method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided parameters (<paramref name="genericParameters"/> and <paramref name="parameters"/>) fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the method should be called.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="genericParameters">Generic type parameters if method is generic. For non-generic methods this parameter is omitted.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object RunInstanceMethodByName(object instance, string methodName, Type[] genericParameters, ReflectionWays way, params object[] parameters)
        {
            if (methodName == null)
                throw new ArgumentNullException("methodName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));
            if (parameters == null)
                parameters = EmptyObjects;
            if (genericParameters == null)
                genericParameters = Type.EmptyTypes;

            Type type = instance.GetType();
            return RunMethodByName(methodName, type, instance, parameters, genericParameters, way);
        }

        /// <summary>
        /// Runs an instance method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided parameters (<paramref name="genericParameters"/> and <paramref name="parameters"/>) fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the method should be called.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="genericParameters">Generic type parameters if method is generic. For non-generic methods this parameter is omitted.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunInstanceMethodByName(object instance, string methodName, Type[] genericParameters, params object[] parameters)
        {
            return RunInstanceMethodByName(instance, methodName, genericParameters, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Runs an instance method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided <paramref name="parameters"/> fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the method should be called.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static object RunInstanceMethodByName(object instance, string methodName, ReflectionWays way, params object[] parameters)
        {
            return RunInstanceMethodByName(instance, methodName, null, way, parameters);
        }

        /// <summary>
        /// Runs an instance method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided <paramref name="parameters"/> fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the method should be called.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunInstanceMethodByName(object instance, string methodName, params object[] parameters)
        {
            return RunInstanceMethodByName(instance, methodName, null, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Runs a static method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided parameters (<paramref name="genericParameters"/> and <paramref name="parameters"/>) fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the method to call.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="genericParameters">Generic type parameters if method is generic. For non-generic methods this parameter is omitted.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunStaticMethodByName(Type type, string methodName, Type[] genericParameters, ReflectionWays way, params object[] parameters)
        {
            if (methodName == null)
                throw new ArgumentNullException("methodName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));
            if (parameters == null)
                parameters = EmptyObjects;
            if (genericParameters == null)
                genericParameters = Type.EmptyTypes;

            return RunMethodByName(methodName, type, null, parameters, genericParameters, way);
        }

        /// <summary>
        /// Runs a static method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided parameters (<paramref name="genericParameters"/> and <paramref name="parameters"/>) fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the method to call.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="genericParameters">Generic type parameters if method is generic. For non-generic methods this parameter is omitted.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunStaticMethodByName(Type type, string methodName, Type[] genericParameters, params object[] parameters)
        {
            return RunStaticMethodByName(type, methodName, genericParameters, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Runs a static method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided <paramref name="parameters"/> fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the method to call.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="way">Preferred invocation mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a method is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunStaticMethodByName(Type type, string methodName, ReflectionWays way, params object[] parameters)
        {
            return RunStaticMethodByName(type, methodName, null, way, parameters);
        }

        /// <summary>
        /// Runs a static method based on a method name given in <paramref name="methodName"/> parameter.
        /// Method can refer to either public or non-public methods. To avoid ambiguity, this method gets
        /// all of the methods of the same name and chooses the first one to which provided <paramref name="parameters"/> fit.
        /// If you do not need the method name to be parsed from string, then it is recommended to use <see cref="RunMethod(object,System.Reflection.MethodInfo,System.Type[],KGySoft.Libraries.Reflection.ReflectionWays,object[])"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the method to call.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Returns <see langword="null"/> if return type of the method is <see cref="Void"/>, otherwise, returns
        /// the return value of the invoked method.</returns>
        public static object RunStaticMethodByName(Type type, string methodName, params object[] parameters)
        {
            return RunStaticMethodByName(type, methodName, null, ReflectionWays.Auto, parameters);
        }

        #endregion

        #region Construction

        /// <summary>
        /// Creates a new instance of an object based on a <see cref="ConstructorInfo"/> metadata given in <paramref name="ctor"/> parameter.
        /// </summary>
        /// <param name="ctor">Contains the constructor to invoke.</param>
        /// <param name="way">Preferred invocation mode.
        /// Auto option uses dynamic delegate mode. In case of dynamic delegate mode first object creation is slow but
        /// further calls are faster than the system reflection way. Type descriptor way is possible but uses no service provider.
        /// </param>
        /// <param name="parameters">Constructor parameters.</param>
        /// <returns>The created instance.</returns>
        public static object Construct(ConstructorInfo ctor, ReflectionWays way, params object[] parameters)
        {
            if (ctor == null)
                throw new ArgumentNullException("ctor", Res.Get(Res.ArgumentNull));

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                return ObjectFactory.GetObjectFactory(ctor).Create(parameters);
            }
            else if (way == ReflectionWays.SystemReflection)
            {
                return ctor.Invoke(parameters);
            }
            else// if (way == ReflectionWays.TypeDescriptor)
            {
                return TypeDescriptor.CreateInstance(null, ctor.DeclaringType,
                    (from p in ctor.GetParameters()
                     select p.ParameterType).ToArray(), parameters);
            }
        }

        /// <summary>
        /// Creates a new instance of an object based on a <see cref="ConstructorInfo"/> metadata given in <paramref name="ctor"/> parameter.
        /// </summary>
        /// <param name="ctor">Contains the constructor to invoke.</param>
        /// <param name="parameters">Constructor parameters.</param>
        /// <returns>The created instance.</returns>
        public static object Construct(ConstructorInfo ctor, params object[] parameters)
        {
            return Construct(ctor, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Creates a new instance of given <paramref name="type"/> based on constructor <paramref name="parameters"/>.
        /// If you know the exact constructor to invoke use the <see cref="Construct(System.Reflection.ConstructorInfo,KGySoft.Libraries.Reflection.ReflectionWays,object[])"/> overload
        /// for better performance.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <param name="parameters">Constructor parameters.</param>
        /// <param name="way">Preferred invocation mode.</param>
        /// <returns>The created instance.</returns>
        public static object Construct(Type type, ReflectionWays way, params object[] parameters)
        {
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            if (parameters == null)
                parameters = EmptyObjects;

#if NET35 || NET40 || NET45
            // In case of value types no parameterless constructor would be found - redirecting
            if (type.IsValueType && parameters.Length == 0)
                return Construct(type, way);
#else
#error In .NET 4.6 there can be parameterless struct ctor so move the check above to the end, or make a branch: if no params -> type.GetConstructor(EmptyTypes)
#endif

            Exception lastException = null;
            foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] ctorParams = ctor.GetParameters();

                // skip when parameter count is not corrent
                if (ctorParams.Length != parameters.Length)
                    continue;

                if (!CheckParameters(ctorParams, parameters))
                    continue;

                try
                {
                    return Construct(ctor, way, parameters);
                }
                catch (TargetInvocationException e)
                {
                    // the constructor was successfully invoked, the exception comes from the ctor itself: we may rethrow the inner exception.
                    throw e.InnerException;
                }
                catch (Exception e)
                {
                    // invoking of the constructor failed - maybe we tried to invoke an incorrect overloaded version of the constructor: we skip to the next overloaded ctor while save last exception
                    lastException = e;
                    continue;
                }
            }
            if (lastException != null)
                throw lastException;
            throw new ReflectionException(Res.Get(Res.CtorDoesNotExist, type));
        }

        /// <summary>
        /// Creates a new instance of given <paramref name="type"/> based on constructor <paramref name="parameters"/>.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <param name="parameters">Constructor parameters.</param>
        /// <returns>The created instance.</returns>
        public static object Construct(Type type, params object[] parameters)
        {
            return Construct(type, ReflectionWays.Auto, parameters);
        }

        /// <summary>
        /// Creates a new instance of given type using the default or parameterless constructor.
        /// If <paramref name="type"/> is <see cref="ValueType"/>, then the new instance is created
        /// by without using any constructors.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <param name="way">Preferred invocation mode.
        /// Auto option uses dynamic delegate mode in case of reference types and system reflection mode in case of value types.
        /// In case of dynamic delegate mode first creation of an object of a method is slow but
        /// further calls are faster than the system reflection way except if object to create is value type and no constructor is used.
        /// TypeDescriptor way is possible but not preferred and uses no service provider.
        /// </param>
        /// <returns>The created instance.</returns>
        public static object Construct(Type type, ReflectionWays way)
        {
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            if (way == ReflectionWays.DynamicDelegate || (way == ReflectionWays.Auto && !type.IsValueType))
            {
                return ObjectFactory.GetObjectFactory(type).Create();
            }
            else if (way == ReflectionWays.Auto || way == ReflectionWays.SystemReflection)
            {
                return Activator.CreateInstance(type, true);
            }
            else // if (way == ReflectionWays.TypeDescriptor)
            {
                return TypeDescriptor.CreateInstance(null, type, null, null);
            }
        }

        /// <summary>
        /// Creates a new instance of given type using the default or parameterless constructor.
        /// If <paramref name="type"/> is <see cref="ValueType"/>, then the new instance is created
        /// by without using any constructors.
        /// </summary>
        /// <param name="type">Type of the instance to create.</param>
        /// <returns>The created instance.</returns>
        public static object Construct(Type type)
        {
            return Construct(type, ReflectionWays.Auto);
        }

        /// <summary>
        /// Invokes a constructor on an already created instance.
        /// </summary>
        internal static void InvokeCtor(object instance, ConstructorInfo ctor, params object[] parameters)
        {
            MethodInvoker mi = Accessors.RuntimeMethodHandle_InvokeMethod;
            object signature = Accessors.RuntimeConstructorInfo_Signature.Get(ctor);
#if NET35
            mi.Invoke(ctor.MethodHandle, instance, parameters, signature, ctor.Attributes, ctor.ReflectedType.TypeHandle);
#elif NET40
            if (Accessors.IsNet45)
            {
               mi.Invoke(null, instance, parameters, signature, false);
               return;
            }
  
            mi.Invoke(ctor.MethodHandle, instance, parameters, signature, ctor.Attributes, ctor.ReflectedType.TypeHandle);
#elif NET45
            // the last bool tells whether the method is a ctor but it must be false; otherwise, the fields are cleared.
            mi.Invoke(null, instance, parameters, signature, false);
#else
#error .NET version is not set or not supported!
#endif
        }

        #endregion

        #region SetField

        /// <summary>
        /// Sets a field based on <see cref="FieldInfo"/> medatada given in <paramref name="field"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="field"/> to set is an instance field, then this parameter should
        /// contain the object instance on which the field assignment should be performed.</param>
        /// <param name="field">The field to set.</param>
        /// <param name="value">The desired new value of the field</param>
        /// <param name="way">Preferred access mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a field is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static void SetField(object instance, FieldInfo field, object value, ReflectionWays way)
        {
            if (field == null)
                throw new ArgumentNullException("field", Res.Get(Res.ArgumentNull));
            bool isStatic = field.IsStatic;
            if (instance == null && !isStatic)
                throw new ArgumentNullException("instance", Res.Get(Res.InstanceIsNull));
            if (field.IsLiteral)
                throw new InvalidOperationException(Res.Get(Res.SetConstantField, field.DeclaringType, field.Name));

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                FieldAccessor.GetFieldAccessor(field).Set(instance, value);
            }
            else if (way == ReflectionWays.Auto || way == ReflectionWays.SystemReflection)
            {
                field.SetValue(instance, value);
            }
            else// if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.SetFieldTypeDescriptorNotSupported));
            }
        }

        /// <summary>
        /// Sets a field based on <see cref="FieldInfo"/> medatada given in <paramref name="field"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="field"/> to set is an instance field, then this parameter should
        /// contain the object instance on which the field assignment should be performed.</param>
        /// <param name="field">The field to set.</param>
        /// <param name="value">The desired new value of the field</param>
        public static void SetField(object instance, FieldInfo field, object value)
        {
            SetField(instance, field, value, ReflectionWays.Auto);
        }

        /// <summary>
        /// Internal implementation of SetInstance/StaticFieldByName methods
        /// </summary>
        private static void SetFieldByName(string fieldName, Type type, object instance, object value, ReflectionWays way)
        {
            // type descriptor
            if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.SetFieldTypeDescriptorNotSupported));
            }

            for (Type checkedType = type; checkedType.BaseType != null; checkedType = checkedType.BaseType)
            {
                // lambda or system reflection
                BindingFlags flags = type == checkedType
                    ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                    : BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                flags |= instance == null ? BindingFlags.Static : BindingFlags.Instance;
                foreach (FieldInfo field in checkedType.GetMember(fieldName, MemberTypes.Field, flags))
                {
                    // set first occurence
                    SetField(instance, field, value, way);
                    return;
                }
            }

            throw new ReflectionException(Res.Get(instance == null ? Res.StaticFieldDoesNotExist : Res.InstanceFieldDoesNotExist, fieldName, type));
        }

        /// <summary>
        /// Sets an instance field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="SetField(object,System.Reflection.FieldInfo,object,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the field should be set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="value">The new desired value of the field to set.</param>
        /// <param name="way">Preferred access mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a field is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> or <see cref="ReflectionWays.TypeDescriptor"/> way.
        /// </param>
        /// <remarks>
        /// <note>
        /// To preserve the changed inner state of a value type
        /// you must pass your struct in <paramref name="instance"/> parameter as an <see cref="object"/>.
        /// </note>
        /// </remarks>
        public static void SetInstanceFieldByName(object instance, string fieldName, object value, ReflectionWays way)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));

            Type type = instance.GetType();
            SetFieldByName(fieldName, type, instance, value, way);
        }

        /// <summary>
        /// Sets an instance field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="SetField(object,System.Reflection.FieldInfo,object)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the field should be set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="value">The new desired value of the field to set.</param>
        public static void SetInstanceFieldByName(object instance, string fieldName, object value)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));

            Type type = instance.GetType();
            SetFieldByName(fieldName, type, instance, value, ReflectionWays.Auto);
        }

        /// <summary>
        /// Sets a static field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="SetField(object,System.Reflection.FieldInfo,object,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the field to set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="value">The new desired value of the field to set.</param>
        /// <param name="way">Preferred access mode.
        /// Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// <see cref="ReflectionWays.Auto"/> option uses dynamic delegate mode.
        /// In case of <see cref="ReflectionWays.DynamicDelegate"/> first access of a field is slow but
        /// further calls are faster than the <see cref="ReflectionWays.SystemReflection"/> way.
        /// </param>
        public static void SetStaticFieldByName(Type type, string fieldName, object value, ReflectionWays way)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            SetFieldByName(fieldName, type, null, value, way);
        }

        /// <summary>
        /// Sets a static field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use <see cref="SetField(object,System.Reflection.FieldInfo,object)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the field to set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="value">The new desired value of the field to set.</param>
        public static void SetStaticFieldByName(Type type, string fieldName, object value)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            SetFieldByName(fieldName, type, null, value, ReflectionWays.Auto);
        }

        #endregion

        #region GetField

        /// <summary>
        /// Gets a field based on <see cref="FieldInfo"/> medatada given in <paramref name="field"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="field"/> to get is an instance field, then this parameter should
        /// contain the object instance on which the field getting should be performed.</param>
        /// <param name="field">The field to get.</param>
        /// <param name="way">Preferred reflection mode. Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// Auto option uses dynamic delegate mode. In case of dynamic delegate mode first access of a field is slow but
        /// further accesses are faster than the system reflection way.
        /// </param>
        /// <returns>The value of the field.</returns>
        public static object GetField(object instance, FieldInfo field, ReflectionWays way)
        {
            if (field == null)
                throw new ArgumentNullException("field", Res.Get(Res.ArgumentNull));
            if (instance == null && !field.IsStatic)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));

            if (way == ReflectionWays.Auto || way == ReflectionWays.DynamicDelegate)
            {
                return FieldAccessor.GetFieldAccessor(field).Get(instance);
            }
            else if (way == ReflectionWays.Auto || way == ReflectionWays.SystemReflection)
            {
                return field.GetValue(instance);
            }
            else //if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.GetFieldTypeDescriptorNotSupported));
            }
        }

        /// <summary>
        /// Gets a field based on <see cref="FieldInfo"/> medatada given in <paramref name="field"/> parameter.
        /// </summary>
        /// <param name="instance">If <paramref name="field"/> to get is an instance field, then this parameter should
        /// contain the object instance on which the field getting should be performed.</param>
        /// <param name="field">The field to get.</param>
        /// <returns>The value of the field.</returns>
        public static object GetField(object instance, FieldInfo field)
        {
            return GetField(instance, field, ReflectionWays.Auto);
        }

        /// <summary>
        /// Internal implementation of GetInstance/StaticFieldByName methods
        /// </summary>
        private static object GetFieldByName(string fieldName, Type type, object instance, ReflectionWays way)
        {
            // type descriptor
            if (way == ReflectionWays.TypeDescriptor)
            {
                throw new NotSupportedException(Res.Get(Res.SetFieldTypeDescriptorNotSupported));
            }

            for (Type checkedType = type; checkedType.BaseType != null; checkedType = checkedType.BaseType)
            {
                // lambda or system reflection
                BindingFlags flags = type == checkedType
                    ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                    : BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                flags |= instance == null ? BindingFlags.Static : BindingFlags.Instance;
                foreach (FieldInfo field in checkedType.GetMember(fieldName, MemberTypes.Field, flags))
                {
                    // returning with first field
                    return GetField(instance, field, way);
                }
            }

            throw new ReflectionException(Res.Get(instance == null ? Res.StaticFieldDoesNotExist : Res.InstanceFieldDoesNotExist, fieldName, type));
        }

        /// <summary>
        /// Gets an instance field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="GetField(object,System.Reflection.FieldInfo,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the field value should be get.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="way">Preferred reflection mode. Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// Auto option uses dynamic delegate mode. In case of dynamic delegate mode first access of a field is slow but
        /// further accesses are faster than the system reflection way.
        /// </param>
        /// <returns>Returns the value of the field.</returns>
        public static object GetInstanceFieldByName(object instance, string fieldName, ReflectionWays way)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));

            Type type = instance.GetType();
            return GetFieldByName(fieldName, type, instance, way);
        }

        /// <summary>
        /// Gets an instance field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="GetField(object,System.Reflection.FieldInfo,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="instance">The object instance on which the field should be set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <returns>Returns the value of the field.</returns>
        public static object GetInstanceFieldByName(object instance, string fieldName)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (instance == null)
                throw new ArgumentNullException("instance", Res.Get(Res.ArgumentNull));

            Type type = instance.GetType();
            return GetFieldByName(fieldName, type, instance, ReflectionWays.Auto);
        }

        /// <summary>
        /// Gets a static field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use
        /// <see cref="GetField(object,System.Reflection.FieldInfo,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the field to set.</param>
        /// <param name="fieldName">The field name to set.</param>
        /// <param name="way">Preferred reflection mode. Usable ways: <see cref="ReflectionWays.Auto"/>, <see cref="ReflectionWays.SystemReflection"/>, <see cref="ReflectionWays.DynamicDelegate"/>.
        /// Auto option uses dynamic delegate mode. In case of dynamic delegate mode first access of a field is slow but
        /// further accesses are faster than the system reflection way.
        /// </param>
        /// <returns>Returns the value of the field.</returns>
        public static object GetStaticFieldByName(Type type, string fieldName, ReflectionWays way)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            return GetFieldByName(fieldName, type, null, way);
        }

        /// <summary>
        /// Gets a static field based on a field name given in <paramref name="fieldName"/> parameter.
        /// Field can refer to either public or non-public fields.
        /// If you do not need the field name to be parsed from string, then it is recommended to use <see cref="GetField(object,System.Reflection.FieldInfo,KGySoft.Libraries.Reflection.ReflectionWays)"/>
        /// method for better performance.
        /// </summary>
        /// <param name="type">The type that contains the static the field to set.</param>
        /// <param name="fieldName">The field name to set.</param>
        public static object GetStaticFieldByName(Type type, string fieldName)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName", Res.Get(Res.ArgumentNull));
            if (type == null)
                throw new ArgumentNullException("type", Res.Get(Res.ArgumentNull));

            return GetFieldByName(fieldName, type, null, ReflectionWays.Auto);
        }

        #endregion

        #region Parameters

        /// <summary>
        /// Checks whether the awaited parameter list can receive an actual parameter list
        /// </summary>
        private static bool CheckParameters(ParameterInfo[] awaitedParams, object[] actualParams)
        {
            if (awaitedParams.Length != actualParams.Length)
                return false;

            for (int i = 0; i < awaitedParams.Length; i++)
            {
                if (!awaitedParams[i].ParameterType.CanAcceptValue(actualParams[i]))
                    return false;
            }

            return true;
        }

        #endregion

        #region Assembly resolve

        /// <summary>
        /// Resolves an <paramref name="assemblyName"/> definition by string and gets an <see cref="Assembly"/> instance.
        /// </summary>
        /// <param name="assemblyName">Name of the <see cref="Assembly"/> to retrieve. May contain a fully or partially defined assembly name.</param>
        /// <param name="tryToLoad">If <c>false</c>, searches the assembly among the already loaded assemblies. If <c>true</c>, tries to load the assembly when it is not already loaded.</param>
        /// <param name="matchBySimpleName"><c>true</c> to ignore version, culture and public key token information differences.</param>
        /// <returns>An <see cref="Assembly"/> instance with the loaded assembly.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assemblyName"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="assemblyName"/> is empty.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="tryToLoad"/> is <c>true</c> and the assembly to load from <paramref name="assemblyName"/> cannot be found.</exception>
        /// <exception cref="FileLoadException"><paramref name="tryToLoad"/> is <c>true</c> and the assembly to load from <paramref name="assemblyName"/> could not be loaded.</exception>
        /// <exception cref="BadImageFormatException"><paramref name="tryToLoad"/> is <c>true</c> and the assembly to load from <paramref name="assemblyName"/> has invalid format.</exception>
        public static Assembly ResolveAssembly(string assemblyName, bool tryToLoad, bool matchBySimpleName)
        {
            if (assemblyName == null)
                throw new ArgumentNullException("assemblyName", Res.Get(Res.ArgumentNull));
            if (assemblyName.Length == 0)
                throw new ArgumentException(Res.Get(Res.ArgumentEmpty), "assemblyName");

            Assembly result;
            string key = (matchBySimpleName ? "-" : "+") + assemblyName;
            lock (SyncRoot)
            {
                if (AssemblyCache.TryGetValue(key, out result))
                    return result;
            }

            // 1.) Iterating through loaded assemblies, checking names
            AssemblyName asmName = new AssemblyName(assemblyName);
            string fullName = asmName.FullName;
            string simpleName = asmName.Name;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Simple match. As asmName is parsed, for fully qualified names this will work for sure.
                if (asm.FullName == fullName)
                {
                    result = asm;
                    break;
                }

                AssemblyName nameToCheck = asm.GetName();
                if (nameToCheck.Name != simpleName)
                    continue;

                if (matchBySimpleName)
                {
                    result = asm;
                    break;
                }

                Version version;
                if ((version = asmName.Version) != null && nameToCheck.Version != version)
                    continue;

#if NET35 || NET40
                if (asmName.CultureInfo != null && asmName.CultureInfo.Name != nameToCheck.CultureInfo.Name)
                    continue;
#elif NET45
                if (asmName.CultureName != null && nameToCheck.CultureName != asmName.CultureName)
                    continue;
#else
#error .NET version is not set or not supported!
#endif
                byte[] publicKeyTokenRef, publicKeyTokenCheck;
                if ((publicKeyTokenRef = asmName.GetPublicKeyToken()) != null && (publicKeyTokenCheck = nameToCheck.GetPublicKeyToken()) != null
                    && publicKeyTokenRef.SequenceEqual(publicKeyTokenCheck))
                    continue;

                result = asm;
                break;
            }

            // 2.) Trying to load the assembly
            if (result == null && tryToLoad)
            {
                result = matchBySimpleName ? LoadAssemblyWithPartialName(asmName) : Assembly.Load(asmName);
            }

            if (result != null)
            {
                lock (syncRoot)
                {
                    assemblyCache[key] = result;
                }
            }

            return result;
        }

        /// <summary>
        /// Loads the the assembly with partial name. It is needed because Assembly.LoadWithPartialName is obsolete.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly.</param>
        private static Assembly LoadAssemblyWithPartialName(AssemblyName assemblyName)
        {
            // 1. In case of a system assembly, returning it from the GAC
            string gacPath = GetGacPath(assemblyName.Name);
            if (gacPath != null)
                return Assembly.LoadFrom(gacPath);

            Assembly result = null;

            // 2. Non-GAC assembly: Trying to load the assembly with full name first.
            try
            {
                result = Assembly.Load(assemblyName);
                if (result != null)
                    return result;
            }
            catch (IOException)
            {
                // if version is set, we have a second try
                if (assemblyName.Version == null)
                    throw;
            }

            // 3. Trying to load the assembly without version info
            if (assemblyName.Version != null)
            {
                assemblyName = (AssemblyName)assemblyName.Clone();
                assemblyName.Version = null;
                result = Assembly.Load(assemblyName);
            }

            return result;
        }

        /// <summary>
        /// Gets the path for an assembly if it is in the GAC. Returns the path of the newest available version.
        /// </summary>
        private static string GetGacPath(string name)
        {
            ASSEMBLY_INFO aInfo = new ASSEMBLY_INFO();
            aInfo.cchBuf = 1024;
            aInfo.currentAssemblyPath = new string('\0', aInfo.cchBuf);

            IAssemblyCache ac;
            int hr = Fusion.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                hr = ac.QueryAssemblyInfo(0, name, ref aInfo);
                if (hr < 0)
                    return null;
            }

            return aInfo.currentAssemblyPath;
        }

        #endregion

        #region Type resolve

        /// <summary>
        /// Resolves a type definition given in string. When no assembly is defined in <paramref name="typeName"/>, the type can be defined in any loaded assembly.
        /// </summary>
        /// <param name="typeName">Type declaration in string representation with or without assembly name.</param>
        /// <param name="loadPartiallyDefinedAssemblies"><c>true</c> to load assemblies with partially defined names; <c>false</c> to find partially defined names in already loaded assemblies only.</param>
        /// <param name="matchAssemblyByWeakName"><c>true</c> to allow resolving assembly names by simple assembly name, and ignoring version, culture and public key token information even if they present in <paramref name="typeName"/>.</param>
        /// <returns>The resolved type or <see langword="null"/> when <paramref name="typeName"/> cannot be resolved.</returns>
        /// <remarks>
        /// <para><paramref name="typeName"/> can be generic and may contain fully or partially defined assembly names. When assmebly name is partially defined,
        /// the assembly is attempted to be loaded only when <paramref name="loadPartiallyDefinedAssemblies"/> is <c>true</c>.</para>
        /// <example>
        /// <code lang="C#"><![CDATA[
        /// // mscorlib types are defined wihtout assembly, System.Uri is defined with fully qualified assembly name - it will be loaded if possible
        /// var type = ResolveType("System.Collections.Generic.Dictionary`2[System.String,[System.Uri, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]", false, false);
        /// 
        /// // System.Uri will be resolved even if the loaded System.dll has different version
        /// var type = ResolveType("System.Collections.Generic.Dictionary`2[System.String,[System.Uri, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]", false, true);
        /// 
        /// // System.Uri is defined with partial assembly name - it will be resolved only when System.dll is already loaded
        /// var type = ResolveType("System.Collections.Generic.Dictionary`2[System.String,[System.Uri, System]]", false, false);
        /// 
        /// // System.Uri is defined with partial assembly name, and we allow to load it with partial name, too
        /// var type = ResolveType("System.Collections.Generic.Dictionary`2[System.String,[System.Uri, System]]", true, false);
        /// 
        /// // all types are defined without assembly names: Dictionary and String will be resolved from mscorlib, Uri will be resolved if there is a System.Uri in any loaded assembly.
        /// var type = ResolveType("System.Collections.Generic.Dictionary`2[System.String, System.Uri]", false, false);
        /// ]]></code>
        /// </example>
        /// </remarks>
        /// <exception cref="ReflectionException"><paramref name="typeName"/> cannot be parsed.</exception>
        public static Type ResolveType(string typeName, bool loadPartiallyDefinedAssemblies = false, bool matchAssemblyByWeakName = false)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;

            Type result;
            lock (SyncRoot)
            {
                if (TypeCacheByString.TryGetValue(typeName, out result))
                    return result;

                try
                {
                    // mscorlib type of fully qualified names
                    result = Type.GetType(typeName);
                }
                catch (Exception e)
                {
                    throw new ReflectionException(Res.Get(Res.NotAType, typeName), e);
                }

                if (result != null)
                {
                    typeCacheByString[typeName] = result;
                    return result;
                }
            }

            // no success: partial name or non-mscorlib type without name
            int genericEnd = typeName.LastIndexOf(']');
            int asmNamePos = typeName.IndexOf(',', genericEnd + 1);

            // (partial) assembly name is defined
            if (asmNamePos >= 0)
            {
                Assembly assembly;
                string asmName = typeName.Substring(asmNamePos + 1).Trim();
                try
                {
                    assembly = ResolveAssembly(asmName, loadPartiallyDefinedAssemblies, matchAssemblyByWeakName);
                }
                catch (Exception e)
                {
                    throw new ReflectionException(Res.Get(Res.CannotLoadAssembly, asmName), e);
                }

                if (assembly == null)
                    return null;

                return ResolveType(assembly, typeName.Substring(0, asmNamePos).Trim(), loadPartiallyDefinedAssemblies, matchAssemblyByWeakName);
            }

            // no assembly name is defined (generics: no assembly name for the main type itself)
            // firsty we try to resolve the type in the calling assembly
            Assembly callingAssembly = Assembly.GetCallingAssembly();
            result = ResolveType(callingAssembly, typeName, loadPartiallyDefinedAssemblies, matchAssemblyByWeakName);
            if (result == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == callingAssembly)
                        continue;

                    result = ResolveType(assembly, typeName, loadPartiallyDefinedAssemblies, matchAssemblyByWeakName);
                    if (result != null)
                        break;
                }
            }

            if (result != null)
            {
                lock (syncRoot)
                {
                    typeCacheByString[typeName] = result;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a type definition given in string. Type definition can be generic and should exist in specified <paramref name="assembly"/>.
        /// </summary>
        /// <param name="typeName">Type declaration in string representation.</param>
        /// <param name="assembly">The assembly that may contain the type to retrieve.</param>
        /// <returns>The resolved type or <see langword="null"/> when <paramref name="typeName"/> cannot be resolved.</returns>
        /// <remarks>
        /// <para>The generic type parameters can contain assembly part. However, when the main type definition contains assembly part,
        /// an <see cref="ArgumentException"/> will be thrown. For such type names use <see cref="ResolveType(string,bool,bool)"/> overload instead.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
        /// <exception cref="ReflectionException"><paramref name="typeName"/> cannot be parsed.</exception>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> contains assembly name part, which is allowed only in generic type parameters.</exception>
        public static Type ResolveType(Assembly assembly, string typeName)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly", Res.Get(Res.ArgumentNull));
            if (String.IsNullOrEmpty(typeName))
                return null;

            int genericEnd = typeName.LastIndexOf(']');
            int asmNamePos = typeName.IndexOf(',', genericEnd + 1);
            if (asmNamePos >= 0)
                throw new ArgumentException(Res.Get(Res.TypeWithAssemblyName), "typeName");

            return ResolveType(assembly, typeName, false, false);
        }

        /// <summary>
        /// Resolves the typeName in the specified assembly. loadPartiallyDefinedAssemblies and matchAssemblyByWeakName refer the possible assembly strings in generic arguments.
        /// </summary>
        private static Type ResolveType(Assembly assembly, string typeName, bool loadPartiallyDefinedAssemblies, bool matchAssemblyByWeakName)
        {
            Cache<string, Type> cache;
            Type result;
            lock (SyncRoot)
            {
                cache = TypeCacheByAssembly[assembly];
                if (cache.TryGetValue(typeName, out result))
                {
                    return result;
                }
            }

            try
            {
                // Try 1: A type from specified assembly
                result = assembly.GetType(typeName);

                // Try 2: if result is still null we may check whether a generic type is tried to be created (like: System.Collections.Generic.List`1[System.Globalization.CultureInfo])
                if (result == null)
                {
                    string elementTypeName;
                    string[] genTypeParams;
                    int[] arrayRanks;
                    GetNameAndIndices(typeName, out elementTypeName, out genTypeParams, out arrayRanks);
                    if (genTypeParams != null || arrayRanks != null)
                    {
                        // this should have no assembly info in name
                        result = ResolveType(assembly, elementTypeName, false, false);

                        // processing generic parameters
                        if (result != null && genTypeParams != null)
                        {
                            if (!result.IsGenericTypeDefinition)
                                throw new ReflectionException(Res.Get(Res.ParseNotAGenericType, elementTypeName, typeName));
                            Type[] genArgs = result.GetGenericArguments();
                            if (genArgs.Length != genTypeParams.Length)
                                throw new ReflectionException(Res.Get(Res.ParseTypeArgsLengthMismatch, typeName, genArgs));
                            Type[] typeGenParams = new Type[genTypeParams.Length];
                            for (int i = 0; i < genTypeParams.Length; i++)
                            {
                                if ((typeGenParams[i] = ResolveType(genTypeParams[i], loadPartiallyDefinedAssemblies, matchAssemblyByWeakName)) == null)
                                    throw new ReflectionException(Res.Get(Res.ParseCannotResolveTypeArg, elementTypeName, typeName));
                            }

                            result = result.MakeGenericType(typeGenParams);
                        }

                        // processing ranks
                        if (result != null && arrayRanks != null)
                        {
                            foreach (int rank in arrayRanks)
                            {
                                switch (rank)
                                {
                                    case 1:
                                        // 1 dimensional zero-based array
                                        result = result.MakeArrayType();
                                        break;
                                    case -1:
                                        // 1 dimensional nonzero-based array
                                        result = result.MakeArrayType(1);
                                        break;
                                    default:
                                        // multidimensional array
                                        result = result.MakeArrayType(rank);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new ReflectionException(Res.Get(Res.NotAType, typeName), e);
            }

            if (result != null)
            {
                lock (syncRoot)
                {
                    cache[typeName] = result;
                }
            }

            return result;
        }

        /// <summary>
        /// Separates the name from the indices like TypeGen[gentypeparam, asmname][12,1]. 
        /// Parsed indices are returned as string list.
        /// </summary>
        internal static void GetNameAndIndices(string value, out string name, out string[] genericTypeParams, out int[] arrayRanks)
        {
            int posBeginBracket = value.IndexOf('[');
            if (posBeginBracket < 0)
            {
                name = value;
                genericTypeParams = null;
                arrayRanks = null;
                return;
            }

            name = value.Substring(0, posBeginBracket);

            arrayRanks = GetArrayRanks(ref value);
            posBeginBracket = value.IndexOf('[');
            if (posBeginBracket < 0)
            {
                genericTypeParams = null;
                return;
            }

            int posEndBracket = value.LastIndexOf(']');
            if (posEndBracket < posBeginBracket)
                throw new ReflectionException(Res.Get(Res.TypeSyntaxError, value));
            StringBuilder sb = new StringBuilder(value.Substring(posBeginBracket + 1, posEndBracket - posBeginBracket - 1));

            const string commaPlaceholder = "<%comma%>";
            int depth = 0;
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '[')
                    depth++;
                else if (sb[i] == ']')
                    depth--;
                else if (sb[i] == ',' && depth > 0)
                {
                    sb.Remove(i, 1);
                    sb.Insert(i, commaPlaceholder);
                    i += commaPlaceholder.Length - 1;
                }
            }

            genericTypeParams = sb.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < genericTypeParams.Length; i++)
            {
                genericTypeParams[i] = genericTypeParams[i].Trim().Replace(commaPlaceholder, ",");
                if (genericTypeParams[i].Length > 1 && genericTypeParams[i][0] == '[' && genericTypeParams[i][genericTypeParams[i].Length - 1] == ']')
                    genericTypeParams[i] = genericTypeParams[i].Substring(1, genericTypeParams[i].Length - 2);
            }
            if (genericTypeParams.Length == 0)
                genericTypeParams = null;
        }

        /// <summary>
        /// Gets array ranks in type definition and removes array part of given value.
        /// </summary>
        private static int[] GetArrayRanks(ref string value)
        {
            int posEndBracket = value.LastIndexOf(']');

            if (posEndBracket == -1)
                throw new ReflectionException(Res.Get(Res.TypeSyntaxError, value));

            CircularList<int> result = new CircularList<int>();
            int lastArrayIndex = -1;
            bool inArray = true;
            int rank = 1;
            bool stopped = false;
            for (int i = posEndBracket - 1; i >= 0 && !stopped; i--)
            {
                if (inArray)
                {
                    switch (value[i])
                    {
                        case ',':
                            if (rank < 0)
                                rank = -rank;  // back to + in every dimension
                            rank++;
                            break;
                        case '*':
                            if (rank < 0)
                                stopped = true; // multiple *s in a dimension
                            rank = -rank;
                            break;
                        case '[':
                            // -1 is kept only for 1 dimension  array ([*]) because it is different than [], while [,] and [*,*] are the same.
                            if (rank < -1)
                                rank = -rank;
                            result.Insert(0, rank);
                            inArray = false;
                            lastArrayIndex = i;
                            break;
                        default:
                            if (!Char.IsWhiteSpace(value[i]))
                                stopped = true;
                            break;
                    }
                }
                else // !inArray
                {
                    if (value[i] == ']')
                    {
                        inArray = true;
                        rank = 1;
                    }
                    else if (!Char.IsWhiteSpace(value[i]))
                        stopped = true;
                }
            }

            if (lastArrayIndex != -1)
            {
                value = value.Remove(lastArrayIndex);
            }
            return result.Count == 0 ? null : result.ToArray();
        }

        /// <summary>
        /// Registers a type converter for a type.
        /// </summary>
        /// <typeparam name="T">The type that needs a new converter.</typeparam>
        /// <typeparam name="TC">The needed <see cref="TypeConverter"/> to register.</typeparam>
        /// <remarks>
        /// <para>After calling this method the <see cref="TypeDescriptor.GetConverter(System.Type)">TypeDescriptor.GetConverter</see>
        /// method will return the converter defined in <typeparamref name="TC"/>.</para>
        /// <note>Please note that if <see cref="TypeDescriptor.GetConverter(System.Type)">TypeDescriptor.GetConverter</see>
        /// has already been called for <typeparamref name="T"/> before registering the new converter, then the further calls
        /// after the registering may continue to return the original converter. So make sure you register your custom converters
        /// at the start of your application.</note></remarks>
        public static void RegisterTypeConverter<T, TC>() where TC: TypeConverter
        {
            TypeConverterAttribute attr = new TypeConverterAttribute(typeof(TC));

            if (!TypeDescriptor.GetAttributes(typeof(T)).Contains(attr))
                TypeDescriptor.AddAttributes(typeof(T), attr);
        }

        #endregion

        #region Member Reflection

        /// <summary>
        /// Gets the returned member of a lambda expression providing a refactoring-safe way for
        /// referencing a field, property, constructor or function method.
        /// </summary>
        /// <typeparam name="T">Type of the returned member in the expression.</typeparam>
        /// <param name="expression">A lambda expression returning a member.</param>
        /// <returns>A <see cref="MemberInfo"/> instance that represents the returned member of the <paramref name="expression"/></returns>
        /// <remarks>
        /// <para>Similarly to the <see langword="typeof"/> operator, which provides a refactoring-safe reference to a <see cref="Type"/>,
        /// this method provides a non-string access to a field, property, constructor or function method:
        /// <example><code lang="C#"><![CDATA[
        /// MemberInfo type = typeof(int); // Type: System.Int32 (by the built-in typeof() operator)
        /// MemberInfo ctorList = Reflector.MemberOf(() => new List<int>()); // ConstructorInfo: List<int>().ctor()
        /// MemberInfo methodIndexOf = Reflector.MemberOf(() => default(List<int>).IndexOf(default(int))); // MethodInfo: List<int>.IndexOf(int) - works without a reference to a List
        /// MemberInfo fieldEmpty = Reflector.MemberOf(() => string.Empty); // FieldInfo: String.Empty
        /// MemberInfo propertyLength = Reflector.MemberOf(() => default(string).Length); // PropertyInfo: String.Length - works without a reference to a string
        /// ]]></code></example></para>
        /// <para>Constant fields cannot be reflected by this method because C# compiler emits the value of the constant into
        /// the expression instead of the access of the constant field.</para>
        /// <para>To reflect an action method, you can use the <see cref="MemberOf"/> method.</para>
        /// <para>To reflect methods, you can actually cast the method to a delegate and get its <see cref="Delegate.Method"/> property:
        /// <example><code lang="C#"><![CDATA[
        /// MemberInfo methodIndexOf = ((Action<int>)new List<int>().IndexOf).Method; // MethodInfo: List<int>.IndexOf(int) - a reference to a List is required
        /// ]]></code></example>
        /// <note>
        /// Accessing a method by the delegate cast is usually faster than using this method. However, you must have an instance to
        /// access instance methods. That means that you cannot use the <see langword="default"/> operator for reference types
        /// to access their instance methods. If the constructor of such a type is slow, then using this method can be
        /// more effective to access an instance method.
        /// </note>
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="expression"/> does not return a member.</exception>
        /// <seealso cref="MemberOf"/>
        public static MemberInfo MemberOf<T>(Expression<Func<T>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression", Res.Get(Res.ArgumentNull));

            Expression body = expression.Body;
            MemberExpression member = body as MemberExpression;
            if (member != null)
                return member.Member;

            MethodCallExpression methodCall = body as MethodCallExpression;
            if (methodCall != null)
                return methodCall.Method;

            NewExpression ctor = body as NewExpression;
            if (ctor != null)
                return ctor.Constructor;

            throw new ArgumentException(Res.Get(Res.NotAMember, expression.GetType()), "expression");
        }

        /// <summary>
        /// Gets the accessed action method of a lambda expression by a refactoring-safe way.
        /// </summary>
        /// <param name="expression">A lambda expression accessing an action method.</param>
        /// <returns>A <see cref="MethodInfo"/> instance that represents the accessed method of the <paramref name="expression"/></returns>
        /// <remarks>
        /// <para>Similarly to the <see langword="typeof"/> operator, which provides a refactoring-safe reference to a <see cref="Type"/>,
        /// this method provides a non-string access to an action method:
        /// <example><code lang="C#"><![CDATA[
        /// Type type = typeof(int); // Type: System.Int32 (by the built-in typeof() operator)
        /// MethodInfo methodAdd = Reflector.MemberOf(() => default(List<int>).Add(default(int))); // MethodInfo: List<int>.Add() - works without a reference to a List
        /// ]]></code></example></para>
        /// <para>To reflect a function method, constructor, property or a field, you can use the <see cref="MemberOf{T}"/> method.</para>
        /// <para>To reflect methods, you can actually cast the method to a delegate and get its <see cref="Delegate.Method"/> property:
        /// <example><code lang="C#"><![CDATA[
        /// MethodInfo methodAdd = ((Action<int>)new List<int>().Add).Method; // MethodInfo: List<int>.Add() - a reference to a List is required
        /// ]]></code></example>
        /// <note>
        /// Accessing a method by the delegate cast is usually faster than using this method. However, you must have an instance to
        /// access instance methods. That means that you cannot use the <see langword="default"/> operator for reference types
        /// to access their instance methods. If the constructor of such a type is slow, then using this method can be
        /// more effective to access an instance method.
        /// </note>
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="expression"/> does not access an action method.</exception>
        /// <seealso cref="MemberOf{T}"/>
        public static MethodInfo MemberOf(Expression<Action> expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression", Res.Get(Res.ArgumentNull));

            Expression body = expression.Body;
            MethodCallExpression methodCall = body as MethodCallExpression;
            if (methodCall != null)
                return methodCall.Method;

            throw new ArgumentException(Res.Get(Res.NotAMethod), "expression");
        }

        private static string GetDefaultMember(Type type)
        {
            CustomAttributeData data = null;
            IList<CustomAttributeData> customAttributes = CustomAttributeData.GetCustomAttributes(type);
            for (int i = 0; i < customAttributes.Count; i++)
            {
                if (customAttributes[i].Constructor.DeclaringType == typeof(DefaultMemberAttribute))
                {
                    data = customAttributes[i];
                    break;
                }
            }

            if (data == null)
            {
                return null;
            }
            CustomAttributeTypedArgument argument = data.ConstructorArguments[0];
            return argument.Value as string;
        }

        #endregion
    }
}
