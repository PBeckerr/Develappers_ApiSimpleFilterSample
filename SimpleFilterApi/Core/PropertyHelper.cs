using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace SimpleFilterApi.Core
{
    public class PropertyHelper
    {
        private static readonly MethodInfo _callPropertyGetterByReferenceOpenGenericMethod =
            typeof(PropertyHelper).GetMethod("CallPropertyGetterByReference", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _callPropertyGetterOpenGenericMethod =
            typeof(PropertyHelper).GetMethod("CallPropertyGetter", BindingFlags.NonPublic | BindingFlags.Static);

        // Implementation of the fast setter.
        private static readonly MethodInfo _callPropertySetterOpenGenericMethod =
            typeof(PropertyHelper).GetMethod("CallPropertySetter", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly ConcurrentDictionary<Type, PropertyHelper[]> _reflectionCache = new ConcurrentDictionary<Type, PropertyHelper[]>();

        private readonly Func<object, object> _valueGetter;

        /// <summary>
        ///     Initializes a fast property helper. This constructor does not cache the helper.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification =
                             "This is intended the Name is auto set differently per type and the type is internal")]
        public PropertyHelper(PropertyInfo property)
        {
            Contract.Assert(property != null);

            this.Name = property.Name;
            this.PropertyInfo = property;
            this._valueGetter = MakeFastPropertyGetter(property);
        }

        public PropertyInfo PropertyInfo { get; set; }

        public virtual string Name { get; protected set; }

        /// <summary>
        ///     Creates and caches fast property helpers that expose getters for every public get property on the underlying type.
        /// </summary>
        /// <param name="instance">the instance to extract property accessors for.</param>
        /// <returns>a cached array of all public property getters from the underlying type of this instance.</returns>
        public static PropertyHelper[] GetProperties(object instance)
        {
            return GetProperties(instance, CreateInstance, _reflectionCache);
        }

        /// <summary>
        ///     Creates a single fast property getter. The result is not cached.
        /// </summary>
        /// <param name="propertyInfo">propertyInfo to extract the getter for.</param>
        /// <returns>a fast getter.</returns>
        /// <remarks>This method is more memory efficient than a dynamically compiled lambda, and about the same speed.</remarks>
        public static Func<object, object> MakeFastPropertyGetter(PropertyInfo propertyInfo)
        {
            Contract.Assert(propertyInfo != null);

            var getMethod = propertyInfo.GetGetMethod();
            Contract.Assert(getMethod != null);
            Contract.Assert(!getMethod.IsStatic);
            Contract.Assert(getMethod.GetParameters().Length == 0);

            // Instance methods in the CLR can be turned into static methods where the first parameter
            // is open over "this". This parameter is always passed by reference, so we have a code
            // path for value types and a code path for reference types.
            var typeInput = getMethod.ReflectedType;
            var typeOutput = getMethod.ReturnType;

            Delegate callPropertyGetterDelegate;
            if (typeInput.IsValueType)
            {
                // Create a delegate (ref TDeclaringType) -> TValue
                var propertyGetterAsFunc = getMethod.CreateDelegate(typeof(ByRefFunc<,>).MakeGenericType(typeInput, typeOutput));
                var callPropertyGetterClosedGenericMethod =
                    _callPropertyGetterByReferenceOpenGenericMethod.MakeGenericMethod(typeInput, typeOutput);
                callPropertyGetterDelegate = Delegate.CreateDelegate(typeof(Func<object, object>), propertyGetterAsFunc, callPropertyGetterClosedGenericMethod);
            }
            else
            {
                // Create a delegate TDeclaringType -> TValue
                var propertyGetterAsFunc = getMethod.CreateDelegate(typeof(Func<,>).MakeGenericType(typeInput, typeOutput));
                var callPropertyGetterClosedGenericMethod = _callPropertyGetterOpenGenericMethod.MakeGenericMethod(typeInput, typeOutput);
                callPropertyGetterDelegate = Delegate.CreateDelegate(typeof(Func<object, object>), propertyGetterAsFunc, callPropertyGetterClosedGenericMethod);
            }

            return (Func<object, object>) callPropertyGetterDelegate;
        }

        /// <summary>
        ///     Creates a single fast property setter. The result is not cached.
        /// </summary>
        /// <param name="propertyInfo">propertyInfo to extract the getter for.</param>
        /// <returns>a fast setter.</returns>
        /// <remarks>This method is more memory efficient than a dynamically compiled lambda, and about the same speed.</remarks>
        public static Action<TDeclaringType, object> MakeFastPropertySetter<TDeclaringType>(PropertyInfo propertyInfo)
            where TDeclaringType : class
        {
            Contract.Assert(propertyInfo != null);

            var setMethod = propertyInfo.GetSetMethod();

            Contract.Assert(setMethod != null);
            Contract.Assert(!setMethod.IsStatic);
            Contract.Assert(setMethod.GetParameters().Length == 1);
            Contract.Assert(!propertyInfo.ReflectedType.IsValueType);

            // Instance methods in the CLR can be turned into static methods where the first parameter
            // is open over "this". This parameter is always passed by reference, so we have a code
            // path for value types and a code path for reference types.
            var typeInput = propertyInfo.ReflectedType;
            var typeValue = setMethod.GetParameters()[0].ParameterType;

            Delegate callPropertySetterDelegate;

            // Create a delegate TValue -> "TDeclaringType.Property"
            var propertySetterAsAction = setMethod.CreateDelegate(typeof(Action<,>).MakeGenericType(typeInput, typeValue));
            var callPropertySetterClosedGenericMethod = _callPropertySetterOpenGenericMethod.MakeGenericMethod(typeInput, typeValue);
            callPropertySetterDelegate =
                Delegate.CreateDelegate(typeof(Action<TDeclaringType, object>), propertySetterAsAction, callPropertySetterClosedGenericMethod);

            return (Action<TDeclaringType, object>) callPropertySetterDelegate;
        }

        public object GetValue(object instance)
        {
            Contract.Assert(this._valueGetter != null, "Must call Initialize before using this object");

            return this._valueGetter(instance);
        }

        public object GetValueOrDefault(object instance, object defaultValue)
        {
            Contract.Assert(this._valueGetter != null, "Must call Initialize before using this object");

            try
            {
                var returnValue = this._valueGetter(instance);

                if (returnValue is null)
                {
                    return defaultValue;
                }

                return returnValue;
            }
            catch (Exception e)
            {
                return defaultValue;
            }
        }


        protected static PropertyHelper[] GetProperties(object instance,
                                                        Func<PropertyInfo, PropertyHelper> createPropertyHelper,
                                                        ConcurrentDictionary<Type, PropertyHelper[]> cache)
        {
            // Using an array rather than IEnumerable, as this will be called on the hot path numerous times.
            PropertyHelper[] helpers;

            var type = instance.GetType();

            if (!cache.TryGetValue(type, out helpers))
            {
                // We avoid loading indexed properties using the where statement.
                // Indexed properties are not useful (or valid) for grabbing properties off an anonymous object.
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     .Where(prop => prop.GetIndexParameters().Length == 0 &&
                                                    prop.GetMethod != null);

                var newHelpers = new List<PropertyHelper>();

                foreach (var property in properties)
                {
                    var propertyHelper = createPropertyHelper(property);

                    newHelpers.Add(propertyHelper);
                }

                helpers = newHelpers.ToArray();
                cache.TryAdd(type, helpers);
            }

            return helpers;
        }

        private static object CallPropertyGetter<TDeclaringType, TValue>(Func<TDeclaringType, TValue> getter, object @this)
        {
            return getter((TDeclaringType) @this);
        }

        private static object CallPropertyGetterByReference<TDeclaringType, TValue>(ByRefFunc<TDeclaringType, TValue> getter, object @this)
        {
            var unboxed = (TDeclaringType) @this;
            return getter(ref unboxed);
        }

        private static void CallPropertySetter<TDeclaringType, TValue>(Action<TDeclaringType, TValue> setter, object @this, object value)
        {
            setter((TDeclaringType) @this, (TValue) value);
        }

        private static PropertyHelper CreateInstance(PropertyInfo property)
        {
            return new PropertyHelper(property);
        }

        // Implementation of the fast getter.
        private delegate TValue ByRefFunc<TDeclaringType, TValue>(ref TDeclaringType arg);
    }
}