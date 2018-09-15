﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using KGySoft.Libraries.Reflection;
using KGySoft.Libraries.Resources;

namespace KGySoft.Libraries.Serialization
{
    /// <summary>
    /// XElement version of XML deserialization.
    /// Actually a static class with base types - hence marked as abstract. Unlike on serialization no fields are used so no instance is needed.
    /// </summary>
    internal abstract class XElementDeserializer : XmlDeserializerBase
    {
        /// <summary>
        /// Deserializes an XML content to an object.
        /// </summary>
        public static object Deserialize(XElement content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content), Res.Get(Res.ArgumentNull));

            if (content.Name.LocalName != XmlSerializer.ElementObject)
                throw new ArgumentException(Res.Get(Res.XmlRootExpected, content.Name.LocalName), nameof(content));

            if (content.IsEmpty)
                return null;

            XAttribute attrType = content.Attribute(XmlSerializer.AttributeType);

            Type objType = null;
            if (attrType != null)
            {
                objType = Reflector.ResolveType(attrType.Value);
                if (objType == null)
                    throw new ReflectionException(Res.Get(Res.XmlCannotResolveType, attrType.Value));
            }

            if (TryDeserializeObject(objType, content, null, out var result))
                return result;

            if (attrType == null)
                throw new ArgumentException(Res.Get(Res.XmlRootTypeMissing), nameof(content));
            throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, objType));
        }

        /// <summary>
        /// Deserializes inner content of an object or collection.
        /// </summary>
        public static void DeserializeContent(XElement parent, object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj), Res.Get(Res.ArgumentNull));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent), Res.Get(Res.ArgumentNull));
            Type objType = obj.GetType();

            // deserialize IXmlSerializable content
            XAttribute attrFormat = parent.Attribute(XmlSerializer.AttributeFormat);
            if (attrFormat != null && attrFormat.Value == XmlSerializer.AttributeValueCustom)
            {
                if (!(obj is IXmlSerializable xmlSerializable))
                    throw new ArgumentException(Res.Get(Res.NotAnIXmlSerializable, objType));
                DeserializeXmlSerializable(xmlSerializable, parent);
                return;
            }

            // deserialize array
            if (objType.IsArray)
            {
                Array array = (Array)obj;
                DeserializeArray(array, null, parent, false);
                return;
            }

            // Populatable collection: clearing it before restoring (root-level DeserializeContent, collection of read-only properties)
            Type collectionElementType = null;
            if (objType.IsCollection())
            {
                if (!objType.IsReadWriteCollection(obj))
                    throw new SerializationException(Res.Get(Res.XmlDeserializeReadOnlyCollection, objType));

                collectionElementType = objType.GetCollectionElementType();
                IEnumerable collection = (IEnumerable)obj;
                collection.Clear();
            }

            DeserializeMembersAndElements(parent, obj, objType, collectionElementType, null);
        }

        /// <summary>
        /// Deserializes a non-populatable collection by an initializer collection.
        /// </summary>
        private static object DeserializeContentByInitializerCollection(XElement parent, ConstructorInfo collectionCtor, Type collectionElementType, bool isDictionary)
        {
            var initializerCollection = CreateInitializerCollection(collectionElementType, isDictionary);

            var members = new Dictionary<MemberInfo, object>();
            DeserializeMembersAndElements(parent, initializerCollection, collectionCtor.DeclaringType, collectionElementType, members);
            return CreateCollectionByInitializerCollection(collectionCtor, initializerCollection, members);
        }

        /// <summary>
        /// Deserializes the members and elements of <paramref name="objRealType"/>.
        /// Type of <paramref name="obj"/> can be different of <paramref name="objRealType"/> if a proxy collection object is populated for initialization.
        /// In this case members have to be stored for later initialization into <paramref name="members"/> and <paramref name="obj"/> is a populatable collection for sure.
        /// <paramref name="collectionElementType"/> is <see langword="null"/> only if <paramref name="objRealType"/> is not a supported collection.
        /// </summary>
        private static void DeserializeMembersAndElements(XElement parent, object obj, Type objRealType, Type collectionElementType, Dictionary<MemberInfo, object> members)
        {
            foreach (XElement propertyOrItem in parent.Elements())
            {
                PropertyInfo property = objRealType.GetProperty(propertyOrItem.Name.LocalName);
                FieldInfo field = property != null ? null : objRealType.GetField(propertyOrItem.Name.LocalName);
                MemberInfo member = (MemberInfo)property ?? field;
                XAttribute attrType = propertyOrItem.Attribute(XmlSerializer.AttributeType);
                Type itemType = null;
                if (attrType != null)
                {
                    itemType = Reflector.ResolveType(attrType.Value);
                    if (itemType == null)
                        throw new ReflectionException(Res.Get(Res.XmlCannotResolveType, attrType.Value));
                }

                if (itemType == null && member != null)
                    itemType = property?.PropertyType ?? field.FieldType;

                // 1.) real member
                if (member != null)
                {
                    TypeConverter converter = GetTypeConverter(member, itemType);
                    object existingValue = members != null ? null : property != null ? Reflector.GetProperty(obj, property) : Reflector.GetField(obj, field);
                    object result;
                    if (converter?.CanConvertFrom(Reflector.StringType) == true)
                        result = converter.ConvertFromInvariantString(ReadStringValue(propertyOrItem));
                    else if (!TryDeserializeObject(itemType, propertyOrItem, existingValue, out result))
                        throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, itemType));

                    // 1/a.) Cache for later (obj type <> objRealType)
                    if (members != null)
                    {
                        members[member] = result;
                        continue;
                    }

                    // 1/b.) Successfully deserialized into the existing instance (or both are null)
                    if (ReferenceEquals(existingValue, result))
                        continue;

                    // 1.c.) Processing result
                    HandleDeserializedMember(obj, member, result, existingValue);
                    continue;
                }

                if (collectionElementType == null)
                {
                    if (propertyOrItem.Name.LocalName == XmlSerializer.ElementItem)
                        throw new SerializationException(Res.Get(Res.XmlNotACollection, objRealType));
                    throw new ReflectionException(Res.Get(Res.XmlHasNoProperty, objRealType, propertyOrItem.Name.LocalName));
                }

                // 2.) collection element
                if (propertyOrItem.Name.LocalName != XmlSerializer.ElementItem)
                    throw new ArgumentException(Res.Get(Res.XmlItemExpected, propertyOrItem.Name.LocalName));

                IEnumerable collection = (IEnumerable)obj;
                if (propertyOrItem.IsEmpty)
                {
                    collection.Add(null);
                    continue;
                }

                if (TryDeserializeObject(itemType ?? collectionElementType, propertyOrItem, null, out var item))
                {
                    collection.Add(item);
                    continue;
                }

                if (itemType == null)
                    throw new ArgumentException(Res.Get(Res.XmlCannotDetermineElementType, objRealType));
                throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, itemType));
            }
        }

        /// <summary>
        /// Deserialize object - XElement version.
        /// If <paramref name="existingInstance"/> is not <see langword="null"/>, then it is preferred to deserialize its content instead of returning a new instance in <paramref name="result"/>.
        /// <paramref name="existingInstance"/> is considered for IXmlSerializable, arrays, collections and recursive objects.
        /// If <paramref name="result"/> is a different instance to <paramref name="existingInstance"/>, then content if existing instance cannot be deserialized.
        /// </summary>
        private static bool TryDeserializeObject(Type type, XElement element, object existingInstance, out object result)
        {
            // null value
            if (element.IsEmpty && (type == null || !type.IsValueType || type.IsNullable()))
            {
                result = null;
                return true;
            }

            if (type != null && type.IsNullable())
                type = Nullable.GetUnderlyingType(type);

            // a.) If type can be natively parsed, parsing from string
            if (type != null && Reflector.CanParseNatively(type))
            {
                string value = ReadStringValue(element);
                result = Reflector.Parse(type, value);
                return true;
            }

            // b.) Deserialize IXmlSerializable
            string format = element.Attribute(XmlSerializer.AttributeFormat)?.Value;
            if (type != null && format == XmlSerializer.AttributeValueCustom)
            {
                object instance = existingInstance ?? (type.CanBeCreatedWithoutParameters() 
                    ? Reflector.Construct(type) 
                    : throw new ReflectionException(Res.Get(Res.XmlNoDefaultCtor, type)));
                if (!(instance is IXmlSerializable xmlSerializable))
                    throw new ArgumentException(Res.Get(Res.NotAnIXmlSerializable, type));
                DeserializeXmlSerializable(xmlSerializable, element);
                result = xmlSerializable;
                return true;
            }

            // c.) Using type converter of the type if applicable
            if (type != null)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(type);
                if (converter.CanConvertFrom(Reflector.StringType))
                {
                    result = converter.ConvertFromInvariantString(ReadStringValue(element));
                    return true;
                }
            }

            // d.) KeyValuePair (DictionaryEntry is deserialized recursively because its properties are settable)
            if (type?.IsGenericTypeOf(typeof(KeyValuePair<,>)) == true)
            {
                // key
                XElement xItem = element.Element(nameof(KeyValuePair<_,_>.Key));
                if (xItem == null)
                    throw new ArgumentException(Res.Get(Res.XmlKeyValueMissingKey));
                XAttribute xType = xItem.Attribute(XmlSerializer.AttributeType);
                Type keyType = xType != null ? Reflector.ResolveType(xType.Value) : type.GetGenericArguments()[0];
                if (!TryDeserializeObject(keyType, xItem, null, out object key))
                {
                    if (xType != null && keyType == null)
                        throw new ReflectionException(Res.Get(Res.XmlCannotResolveType, xType.Value));
                    throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, keyType));
                }

                // value
                xItem = element.Element(nameof(KeyValuePair<_,_>.Value));
                if (xItem == null)
                    throw new ArgumentException(Res.Get(Res.XmlKeyValueMissingValue));
                xType = xItem.Attribute(XmlSerializer.AttributeType);
                Type valueType = xType != null ? Reflector.ResolveType(xType.Value) : type.GetGenericArguments()[1];
                if (!TryDeserializeObject(valueType, xItem, null, out object value))
                {
                    if (xType != null && valueType == null)
                        throw new ReflectionException(Res.Get(Res.XmlCannotResolveType, xType.Value));
                    throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, valueType));
                }

                var ctor = type.GetConstructor(new[] { keyType, valueType });
                result = Reflector.Construct(ctor, key, value);
                return true;
            }

            // e.) ValueType as binary
            if (type != null && format == XmlSerializer.AttributeValueStructBinary && type.IsValueType)
            {
                byte[] data = Convert.FromBase64String(element.Value);
                XAttribute attrCrc = element.Attribute(XmlSerializer.AttributeCrc);
                if (attrCrc != null)
                {
                    if ($"{Crc32.CalculateHash(data):X8}" != attrCrc.Value)
                        throw new ArgumentException(Res.Get(Res.XmlCrcError));
                }

                result = BinarySerializer.DeserializeStruct(type, data);
                return true;
            }

            // f.) Binary
            if (format == XmlSerializer.AttributeValueBinary)
            {
                if (element.IsEmpty)
                    result = null;
                else
                {
                    byte[] data = Convert.FromBase64String(element.Value);
                    XAttribute attrCrc = element.Attribute(XmlSerializer.AttributeCrc);
                    if (attrCrc != null)
                    {
                        if ($"{Crc32.CalculateHash(data):X8}" != attrCrc.Value)
                            throw new ArgumentException(Res.Get(Res.XmlCrcError));
                    }

                    result = BinarySerializer.Deserialize(data);
                }
                return true;
            }

            // g.) recursive deserialization (including collections)
            if (type != null && !element.IsEmpty)
            {
                // g/1.) array (both existing and new)
                if (type.IsArray)
                {
                    result = DeserializeArray(existingInstance as Array, type.GetElementType(), element, true);
                    return true;
                }

                // g/2.) existing read-write collection
                if (type.IsReadWriteCollection(existingInstance))
                {
                    DeserializeContent(element, existingInstance);
                    result = existingInstance;
                    return true;
                }

                bool isCollection = type.IsSupportedCollectionForReflection(out var defaultCtor, out var collectionCtor, out var elementType, out bool isDictionary);

                // g/3.) New collection by collectionCtor (only if there is no defaultCtor)
                if (isCollection && defaultCtor == null && !type.IsValueType)
                {
                    result = DeserializeContentByInitializerCollection(element, collectionCtor, elementType, isDictionary);
                    return true;
                }

                result = existingInstance ?? (type.CanBeCreatedWithoutParameters()
                    ? Reflector.Construct(type)
                    : throw new ReflectionException(Res.Get(Res.XmlNoDefaultCtor, type)));

                // g/4.) New collection by collectionCtor again (there IS defaultCtor but the new instance is read-only so falling back to collectionCtor)
                if (isCollection && !type.IsReadWriteCollection(result))
                {
                    if (collectionCtor != null)
                    {
                        result = DeserializeContentByInitializerCollection(element, collectionCtor, elementType, isDictionary);
                        return true;
                    }

                    throw new SerializationException(Res.Get(Res.XmlDeserializeReadOnlyCollection, type));
                }

                // g/5.) Newly created collection or any other object (both existing and new)
                DeserializeContent(element, result);
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Array deserialization, XElement version
        /// </summary>
        private static Array DeserializeArray(Array array, Type elementType, XElement element, bool canRecreateArray)
        {
            if (array == null && elementType == null)
                throw new ArgumentNullException(nameof(elementType), Res.Get(Res.ArgumentNull));

            ParseArrayDimensions(element.Attribute(XmlSerializer.AttributeLength)?.Value, element.Attribute(XmlSerializer.AttributeDim)?.Value, out int[] lengths, out int[] lowerBounds);

            // checking existing array or creating a new array
            if (array == null || !CheckArray(array, lengths, lowerBounds, !canRecreateArray))
                array = Array.CreateInstance(elementType, lengths, lowerBounds);

            // has no elements: primitive array (can be restored by BlockCopy)
            if (lengths.Length == 1 && lengths[0] > 0 && !element.HasElements)
            {
                string value = element.Value;
                byte[] data = Convert.FromBase64String(value);
                XAttribute attrCrc = element.Attribute(XmlSerializer.AttributeCrc);
                string crc = attrCrc?.Value;

                if (crc != null)
                {
                    if ($"{Crc32.CalculateHash(data):X8}" != crc)
                        throw new ArgumentException(Res.Get(Res.XmlCrcError));
                }

                int count = data.Length / Marshal.SizeOf(elementType);
                if (data.Length != count)
                    throw new ArgumentException(Res.Get(Res.XmlInconsistentArrayLength, array.Length, count));
                Buffer.BlockCopy(data, 0, array, 0, data.Length);
                return array;
            }

            // complex array: recursive deserialization needed
            Queue<XElement> items = new Queue<XElement>(element.Elements(XmlSerializer.ElementItem));
            if (items.Count != array.Length)
                throw new ArgumentException(Res.Get(Res.XmlInconsistentArrayLength, array.Length, items.Count));

            ArrayIndexer arrayIndexer = lengths.Length > 1 ? new ArrayIndexer(lengths, lowerBounds) : null;
            int deserializedItemsCount = 0;
            while (items.Count > 1)
            {
                XElement item = items.Dequeue();
                Type itemType = null;
                XAttribute attrType = item.Attribute(XmlSerializer.AttributeType);
                if (attrType != null)
                    itemType = Reflector.ResolveType(attrType.Value);
                if (itemType == null)
                    itemType = array.GetType().GetElementType();

                if (TryDeserializeObject(itemType, item, null, out var value))
                {
                    if (arrayIndexer == null)
                        array.SetValue(value, deserializedItemsCount + lowerBounds[0]);
                    else
                    {
                        arrayIndexer.MoveNext();
                        array.SetValue(value, arrayIndexer.Current);
                    }

                    deserializedItemsCount++;
                    continue;
                }

                throw new NotSupportedException(Res.Get(Res.XmlDeserializeNotSupported, itemType));
            }

            return array;
        }

        private static void DeserializeXmlSerializable(IXmlSerializable xmlSerializable, XContainer parent)
        {
            XElement content = parent.Elements().FirstOrDefault();
            if (content == null)
                throw new ArgumentException(Res.Get(Res.XmlNoContent, xmlSerializable.GetType()));
            using (XmlReader xr = XmlReader.Create(new StringReader(content.ToString()), new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreWhitespace = true
            }))
            {
                xr.Read();

                // passing the reader to the object to read itself
                xmlSerializable.ReadXml(xr);
            }
        }

        private static string ReadStringValue(XElement element)
        {
            if (element.IsEmpty)
                return null;

            XAttribute attrEscaped = element.Attribute(XmlSerializer.AttributeEscaped);
            if (attrEscaped == null || attrEscaped.Value != XmlSerializer.AttributeValueTrue)
                return element.Value;

            return Unescape(element.Value);
        }
    }
}
