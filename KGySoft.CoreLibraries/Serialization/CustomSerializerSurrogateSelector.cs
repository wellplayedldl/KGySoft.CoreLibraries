﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: CustomSerializerSurrogateSelector.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2019 - All Rights Reserved
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;

using KGySoft.CoreLibraries;
using KGySoft.Reflection;

#endregion

namespace KGySoft.Serialization
{
    /// <summary>
    /// An <see cref="ISurrogateSelector"/> implementation that makes possible to serialize and deserialize any objects,
    /// including non-serializable ones by an <see cref="IFormatter"/> such as <see cref="BinarySerializationFormatter"/>
    /// or the legacy <see cref="BinaryFormatter"/>.
    /// <br/>See the <strong>Remarks</strong> section for details and examples.
    /// </summary>
    /// <remarks>
    /// <para>By using the <see cref="CustomSerializerSurrogateSelector"/> you can serialize and deserialize any types.
    /// <note>The <see cref="BinarySerializationFormatter"/> is also able to serialize non-serializable types by itself
    /// by using the <see cref="BinarySerializationOptions.RecursiveSerializationAsFallback"/> option. But the
    /// <see cref="CustomSerializerSurrogateSelector"/> provides more control and can used also for other formatters.</note>
    /// </para>
    /// <para>To serialize a non-serializable type, or a type, which contains non-serializable types, it is usually enough to assign
    /// a <see cref="CustomSerializerSurrogateSelector"/> to the formatter:
    /// <code lang="C#"><![CDATA[
    /// var formatter = new BinaryFormatter { SurrogateSelector = new CustomSerializerSurrogateSelector() };
    /// formatter.Serialize(stream, myObject);]]></code></para>
    /// <h1 class="heading">Solving compatibility issues between different platforms</h1>
    /// <note>Some types that are serializable in .NET Framework are not serializable in .NET Core/.NET Standard.
    /// This class can be a solution also for this problem. However, the solution is not always as simple as assigning
    /// a <see cref="CustomSerializerSurrogateSelector"/> instance to the <see cref="IFormatter.SurrogateSelector"/> property.
    /// See some cases and their solution in the list below.</note>
    /// <para>
    /// <list type="definition">
    /// <item><term>Basic case</term><description>If the only difference is that the type, which is serializable in the original platform
    /// is not marked by the <see cref="SerializableAttribute"/> in the new one (eg.: <see cref="MemoryStream"/> is not serializable
    /// in .NET Core but otherwise is still the same as in .NET Framework), then it is enough to use the default <see cref="CustomSerializerSurrogateSelector"/>:
    /// <code lang="C#"><![CDATA[
    /// // deserializing a MemoryStream in .NET Core that was serialized in .NET Framework
    /// var formatter = new BinaryFormatter { SurrogateSelector = new CustomSerializerSurrogateSelector() };
    /// var memoryStream = (MemoryStream)formatter.Deserialize(streamSerializedInNetFramework); ]]></code></description></item>
    /// <item><term>The original type implements <see cref="ISerializable"/></term><description>In .NET Core there are several types that still
    /// implement <see cref="ISerializable"/>, though their <see cref="ISerializable.GetObjectData">ISerializable.GetObjectData</see> method
    /// throws a <see cref="PlatformNotSupportedException"/> (eg. <see cref="DBNull"/>, <see cref="Type"/> and <see cref="Assembly"/> in .NET Core 2).
    /// It still can change, for example, in .NET Core 3.0 <see cref="DBNull"/> is serializable again, <see cref="Assembly"/> remained the same and
    /// from <see cref="Type"/> the <see cref="ISerializable"/> implementation has also been removed along with the <see cref="SerializableAttribute"/>.
    /// For such cases there are more possible solutions:
    /// <list type="bullet">
    /// <item>In best cases it is enough to use the <see cref="BinarySerializationFormatter"/> both for serializing and deserializing without any surrogate selector.
    /// It natively supports <see cref="Type"/> instances in all platforms</item>
    /// <item>When serializing by <see cref="CustomSerializerSurrogateSelector"/> you can set the <see cref="IgnoreISerializable"/> property
    /// to force serializing the object by fields. If the fields are the same on the deserializing side, then the object will be deserializable
    /// by using the <see cref="CustomSerializerSurrogateSelector"/> on both sides with the same settings.</item>
    /// <item>If fields have also changed you can customize the serializing and deserializing process by handling the events (see below).</item>
    /// </list></description></item>
    /// <item><term>Providing backwards compatibility</term><description>The <strong>Basic case</strong> might not work the other way around:
    /// serializing an object in .NET Core might not be deserializable in .NET Framework, for example. It can be avoided if:
    /// <list type="bullet">
    /// <item>You use <see cref="BinarySerializationFormatter"/> instead of <see cref="BinaryFormatter"/>. It supports many types natively, including
    /// many core collections and simple types. These types are always encoded in a platform-independent way, which is also very compact.</item>
    /// <item>You can serialize the types using the <see cref="ForwardedTypesSerializationBinder"/> with setting the <see cref="ForwardedTypesSerializationBinder.WriteLegacyIdentity"/>
    /// property to <see langword="true"/>&#160;to use assembly identities of the .NET Framework for most public types. These types can be resolved
    /// in all platforms. This solution works even with <see cref="BinaryFormatter"/>.</item>
    /// <item>Alternatively, you can use the <see cref="BinarySerializationFormatter"/> with the <see cref="BinarySerializationOptions.OmitAssemblyQualifiedNames"/>,
    /// or the <see cref="WeakAssemblySerializationBinder"/> (works also with <see cref="BinaryFormatter"/>) to allow to omit assembly identities in the
    /// serialized stream. Such a stream can be deserialized if all the assemblies that contain the types to be deserialized are already loaded and the
    /// full names of the types are unambiguous in the loaded assemblies.</item>
    /// </list>
    /// </description></item>
    /// <item><term>Deserializing refactored types</term><description>If the object was serialized by fields but the field names have been
    /// refactored since then, then you can handle the <see cref="SettingField"/> event where you can either lookup and set the appropriate
    /// <see cref="SettingFieldEventArgs.Field"/> or do whatever custom processing and set the <see cref="HandledEventArgs.Handled"/> property
    /// to <see langword="true"/>&#160;to indicate that you processed the entry manually. If you can initialize the complete object by yourself
    /// based on the serialized <see cref="SerializationInfo"/>, then you can handle the <see cref="Deserializing"/> event and set
    /// the <see cref="HandledEventArgs.Handled"/> property to <see langword="true"/>.</description></item>
    /// <item><term>Basic case</term><description>The only difference is that the type in the new platform</description></item>
    /// </list></para>
    /// </remarks>
    /// <seealso cref="NameInvariantSurrogateSelector" />
    /// <seealso cref="BinarySerializationFormatter" />
    public sealed class CustomSerializerSurrogateSelector : ISurrogateSelector, ISerializationSurrogate, IDisposable
    {
        #region Fields

        private ISurrogateSelector next;
        private bool disposed;
        private EventHandler<SerializingEventArgs> serializingEventHandler;
        private EventHandler<ObjectDataObtainedEventArgs> objectDataObtainedEventHandler;
        private EventHandler<DeserializingEventArgs> deserializingEventHandler;
        private EventHandler<ObjectDataRestoredEventArgs> objectDataRestoredEventHandler;
        private EventHandler<GettingFieldEventArgs> gettingFieldEventHandler;
        private EventHandler<SettingFieldEventArgs> settingFieldEventHandler;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when an object is being serialized, before saving its inner content.
        /// <br/>If you populate the <see cref="SerializingEventArgs.SerializationInfo"/> manually make sure you set the <see cref="HandledEventArgs.Handled"/>
        /// property to <see langword="true"/>&#160;to omit the default serialization logic.
        /// </summary>
        public event EventHandler<SerializingEventArgs> Serializing
        {
            add => serializingEventHandler += value;
            remove => serializingEventHandler -= value;
        }

        /// <summary>
        /// Occurs when the <see cref="SerializationInfo"/> of the object to be serialized has been obtained.
        /// You still can adjust its content before the actual serialization.
        /// </summary>
        public event EventHandler<ObjectDataObtainedEventArgs> ObjectDataObtained
        {
            add => objectDataObtainedEventHandler += value;
            remove => objectDataObtainedEventHandler -= value;
        }

        /// <summary>
        /// Occurs when an object is being deserialized.
        /// <br/>If initialize the <see cref="SerializingEventArgs.Object"/> manually make sure you set the <see cref="HandledEventArgs.Handled"/>
        /// property to <see langword="true"/>&#160;to omit the default deserialization logic.
        /// </summary>
        public event EventHandler<DeserializingEventArgs> Deserializing
        {
            add => deserializingEventHandler += value;
            remove => deserializingEventHandler -= value;
        }

        /// <summary>
        /// Occurs when the <see cref="SerializationInfo"/> of the object to be deserialized has been processed.
        /// </summary>
        public event EventHandler<ObjectDataRestoredEventArgs> ObjectDataRestored
        {
            add => objectDataRestoredEventHandler += value;
            remove => objectDataRestoredEventHandler -= value;
        }

        /// <summary>
        /// Occurs when a field value is about to be retrieved on serialization.
        /// You can adjust the <see cref="GettingFieldEventArgs.Name"/>, <see cref="GettingFieldEventArgs.Value"/> and <see cref="GettingFieldEventArgs.Type"/>
        /// of the entry to be stored, or set the <see cref="HandledEventArgs.Handled"/>
        /// property to <see langword="true"/>&#160;to prevent storing any value for the current <see cref="GettingFieldEventArgs.Field"/>.
        /// The <see cref="HandledEventArgs.Handled"/> property might be initialized to <see langword="true"/>&#160;for fields that are marked
        /// by the <see cref="NonSerializedAttribute"/>.
        /// </summary>
        public event EventHandler<GettingFieldEventArgs> GettingField
        {
            add => gettingFieldEventHandler += value;
            remove => gettingFieldEventHandler -= value;
        }

        /// <summary>
        /// Occurs when a field value is about to be set on deserialization.
        /// You can adjust the associated <see cref="SettingFieldEventArgs.Field"/> and its desired <see cref="SettingFieldEventArgs.Value"/> to be set
        /// or you can set the <see cref="HandledEventArgs.Handled"/> property to <see langword="true"/>&#160;to prevent setting any field by the default logic.
        /// </summary>
        public event EventHandler<SettingFieldEventArgs> SettingField
        {
            add => settingFieldEventHandler += value;
            remove => settingFieldEventHandler -= value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the <see cref="ISerializable"/> implementation should be ignored.
        /// The value of this property can be overridden by handling the <see cref="Serializing"/> event.
        /// <br/>Default value: <see langword="false"/>.
        /// </summary>
        /// <value>
        /// <see langword="true"/>&#160;to serialize objects by fields even if it implements <see cref="ISerializable"/>;
        /// otherwise, <see langword="false"/>.
        /// </value>
        public bool IgnoreISerializable { get; set; }

        /// <summary>
        /// Gets or sets whether the fields that are marked by <see cref="NonSerializedAttribute"/> should be forcibly serialized.
        /// The value of this property can be overridden by handling the <see cref="GettingField"/> event.
        /// <br/>Default value: <see langword="false"/>.
        /// </summary>
        /// <value>
        /// <see langword="true"/>&#160;to serialize fields even if they are marked by <see cref="NonSerializedAttribute"/>;
        /// otherwise, <see langword="false"/>.
        /// </value>
        public bool IgnoreNonSerializedAttribute { get; set; }

        /// <summary>
        /// Gets or sets whether serialization data without a corresponding field should be silently ignored on deserialization.
        /// The field setting can be adjusted by handling the <see cref="SettingField"/> event.
        /// <br/>Default value: <see langword="false"/>.
        /// </summary>
        public bool IgnoreNonExistingFields { get; set; }

        #endregion

        #region Methods

        #region Public Methods

        /// <summary>
        /// Specifies the next <see cref="ISurrogateSelector"/> for surrogates to examine if the current instance does not have a surrogate for the specified type and assembly in the specified context.
        /// </summary>
        /// <param name="selector">The next surrogate selector to examine.</param>
        /// <exception cref="SecurityException">The caller does not have the required permission.</exception>
        [SecurityCritical]
        public void ChainSelector(ISurrogateSelector selector) => next = selector;

        /// <summary>
        /// Returns the next surrogate selector in the chain.
        /// </summary>
        /// <returns>
        /// The next surrogate selector in the chain or null.
        /// </returns>
        [SecurityCritical]
        public ISurrogateSelector GetNextSelector() => next;

        /// <summary>
        /// Finds the surrogate that represents the specified object's type, starting with the specified surrogate selector for the specified serialization context.
        /// </summary>
        /// <returns>
        /// The appropriate surrogate for the given type in the given context.
        /// </returns>
        /// <param name="type">The <see cref="Type"/> of object that needs a surrogate.</param>
        /// <param name="context">The source or destination context for the current serialization.</param>
        /// <param name="selector">When this method returns, contains a <see cref="ISurrogateSelector"/> that holds a reference to the surrogate selector where the appropriate surrogate was found.</param>
        /// <exception cref="SecurityException">The caller does not have the required permission.</exception>
        [SecurityCritical]
        public ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type), Res.ArgumentNull);

            if (!type.IsPrimitive && type != Reflector.StringType && !type.HasElementType)
            {
                selector = this;
                return this;
            }

            if (next != null)
                return next.GetSurrogate(type, context, out selector);

            selector = null;
            return null;
        }

        /// <summary>
        /// Releases the resources held by this <see cref="CustomSerializerSurrogateSelector"/> instance.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            (next as IDisposable)?.Dispose();
            serializingEventHandler = null;
            objectDataObtainedEventHandler = null;
            deserializingEventHandler = null;
            objectDataRestoredEventHandler = null;
            gettingFieldEventHandler = null;
            settingFieldEventHandler = null;
        }

        #endregion

        #region Private Methods

        private void GetCustomObjectData(ISerializable serializable, SerializationInfo info, StreamingContext context) => serializable.GetObjectData(info, context);

        private void SetCustomObjectData(ISerializable serializable, SerializationInfo info, StreamingContext context)
        {
            if (!Accessors.TryInvokeCtor(serializable, info, context))
                throw new SerializationException(Res.BinarySerializationMissingISerializableCtor(serializable.GetType()));
        }

        private void GetDefaultObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            Type type = obj.GetType();
            var existingNames = new Dictionary<string, int>();
            for (Type t = type; t != Reflector.ObjectType; t = t.BaseType)
            {
                // ReSharper disable once PossibleNullReferenceException
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    var e = new GettingFieldEventArgs(obj, context, info, field)
                    {
                        Name = field.Name,
                        Value = field.Get(obj),
                        Type = field.FieldType,
                        Handled = !IgnoreNonSerializedAttribute && field.IsNotSerialized,
                    };

                    int usedCount = existingNames.GetValueOrDefault(e.Name);
                    if (usedCount == 0)
                        existingNames[e.Name] = 1;
                    else
                    {
                        existingNames[e.Name] = ++usedCount;
                        e.Name += usedCount.ToString(CultureInfo.InvariantCulture);
                    }

                    OnGettingField(e);
                    if (e.Handled)
                        continue;
                    info.AddValue(e.Name, e.Value, e.Type);
                }
            }
        }

        private void SetDefaultObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (info.MemberCount == 0)
                return;

            Dictionary<string, FieldInfo> fields = BinarySerializer.GetFieldsWithUniqueNames(obj.GetType(), false);
            foreach (SerializationEntry entry in info)
            {
                var e = new SettingFieldEventArgs(obj, context, info, entry)
                {
                    Value = entry.Value,
                    Field = fields.GetValueOrDefault(entry.Name)
                };

                OnSettingField(e);
                if (e.Handled || e.Field == null && IgnoreNonExistingFields)
                    continue;

                if (e.Field == null)
                    throw new SerializationException(Res.SerializationMissingField(obj.GetType(), entry.Name));

                e.Field.Set(obj, e.Value);
            }
        }

        private void OnSerializing(SerializingEventArgs e) => serializingEventHandler?.Invoke(this, e);
        private void OnObjectDataObtained(ObjectDataObtainedEventArgs e) => objectDataObtainedEventHandler?.Invoke(this, e);
        private void OnDeserializing(DeserializingEventArgs e) => deserializingEventHandler?.Invoke(this, e);
        private void OnObjectDataRestored(ObjectDataRestoredEventArgs e) => objectDataRestoredEventHandler?.Invoke(this, e);
        private void OnGettingField(GettingFieldEventArgs e) => gettingFieldEventHandler?.Invoke(this, e);
        private void OnSettingField(SettingFieldEventArgs e) => settingFieldEventHandler?.Invoke(this, e);

        #endregion

        #region Explicitly Implemented Interface Methods

        [SecurityCritical]
        void ISerializationSurrogate.GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj), Res.ArgumentNull);
            if (info == null)
                throw new ArgumentNullException(nameof(info), Res.ArgumentNull);

            var e = new SerializingEventArgs(obj, context, info) { IgnoreISerializable = IgnoreISerializable };
            OnSerializing(e);
            if (e.Handled)
                return;

            if (!e.IgnoreISerializable && obj is ISerializable serializable)
                GetCustomObjectData(serializable, info, context);
            else
                GetDefaultObjectData(obj, info, context);

            OnObjectDataObtained(new ObjectDataObtainedEventArgs(obj, context, info));
        }

        [SecurityCritical]
        object ISerializationSurrogate.SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj), Res.ArgumentNull);
            if (info == null)
                throw new ArgumentNullException(nameof(info), Res.ArgumentNull);

            var e = new DeserializingEventArgs(obj, context, info) { IgnoreISerializable = IgnoreISerializable };
            OnDeserializing(e);
            if (e.Handled)
                return obj;

            if (!e.IgnoreISerializable && obj is ISerializable serializable)
                SetCustomObjectData(serializable, info, context);
            else
                SetDefaultObjectData(obj, info, context);

            OnObjectDataRestored(new ObjectDataRestoredEventArgs(obj, context, info));
            return obj;
        }

        #endregion

        #endregion
    }
}