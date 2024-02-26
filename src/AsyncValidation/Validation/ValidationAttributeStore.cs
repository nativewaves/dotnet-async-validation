// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.ComponentModel.DataAnnotations;

internal class ValidationAttributeStore
{
    private static readonly ValidationAttributeStore _singleton = new ValidationAttributeStore();
    private readonly Dictionary<Type, TypeStoreItem> _typeStoreItems = new Dictionary<Type, TypeStoreItem>();

    /// <summary>
    /// Gets the singleton <see cref="ValidationAttributeStore"/>
    /// </summary>
    internal static ValidationAttributeStore Instance
    {
        get
        {
            return _singleton;
        }
    }

    /// <summary>
    /// Retrieves the type level validation attributes for the given type.
    /// </summary>
    /// <param name="validationContext">The context that describes the type.  It cannot be null.</param>
    /// <returns>The collection of validation attributes.  It could be empty.</returns>
    internal IEnumerable<ValidationAttribute> GetTypeValidationAttributes(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem item = GetTypeStoreItem(validationContext.ObjectType);
        return item.ValidationAttributes;
    }

    /// <summary>
    /// Retrieves the <see cref="DisplayAttribute"/> associated with the given type.  It may be null.
    /// </summary>
    /// <param name="validationContext">The context that describes the type.  It cannot be null.</param>
    /// <returns>The display attribute instance, if present.</returns>
    internal DisplayAttribute GetTypeDisplayAttribute(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem item = GetTypeStoreItem(validationContext.ObjectType);
        return item.DisplayAttribute;
    }

    /// <summary>
    /// Retrieves the set of validation attributes for the property
    /// </summary>
    /// <param name="validationContext">The context that describes the property.  It cannot be null.</param>
    /// <returns>The collection of validation attributes.  It could be empty.</returns>
    internal IEnumerable<ValidationAttribute> GetPropertyValidationAttributes(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem typeItem = GetTypeStoreItem(validationContext.ObjectType);
        PropertyStoreItem item = typeItem.GetPropertyStoreItem(validationContext.MemberName);
        return item.ValidationAttributes;
    }

    /// <summary>
    /// Retrieves the <see cref="DisplayAttribute"/> associated with the given property
    /// </summary>
    /// <param name="validationContext">The context that describes the property.  It cannot be null.</param>
    /// <returns>The display attribute instance, if present.</returns>
    internal DisplayAttribute GetPropertyDisplayAttribute(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem typeItem = GetTypeStoreItem(validationContext.ObjectType);
        PropertyStoreItem item = typeItem.GetPropertyStoreItem(validationContext.MemberName);
        return item.DisplayAttribute;
    }

    /// <summary>
    /// Retrieves the Type of the given property.
    /// </summary>
    /// <param name="validationContext">The context that describes the property.  It cannot be null.</param>
    /// <returns>The type of the specified property</returns>
    internal Type GetPropertyType(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem typeItem = GetTypeStoreItem(validationContext.ObjectType);
        PropertyStoreItem item = typeItem.GetPropertyStoreItem(validationContext.MemberName);
        return item.PropertyType;
    }

    /// <summary>
    /// Determines whether or not a given <see cref="ValidationContext"/>'s
    /// <see cref="ValidationContext.MemberName"/> references a property on
    /// the <see cref="ValidationContext.ObjectType"/>.
    /// </summary>
    /// <param name="validationContext">The <see cref="ValidationContext"/> to check.</param>
    /// <returns><c>true</c> when the <paramref name="validationContext"/> represents a property, <c>false</c> otherwise.</returns>
    internal bool IsPropertyContext(ValidationContext validationContext)
    {
        EnsureValidationContext(validationContext);
        TypeStoreItem typeItem = GetTypeStoreItem(validationContext.ObjectType);
        return typeItem.TryGetPropertyStoreItem(validationContext.MemberName, out PropertyStoreItem _);
    }

    /// <summary>
    /// Retrieves or creates the store item for the given type
    /// </summary>
    /// <param name="type">The type whose store item is needed.  It cannot be null</param>
    /// <returns>The type store item.  It will not be null.</returns>
    [SuppressMessage("Microsoft.Usage", "CA2301:EmbeddableTypesInContainersRule", MessageId = "_typeStoreItems", Justification = "This is used for caching the attributes for a type which is fine.")]
    private TypeStoreItem GetTypeStoreItem(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException("type");
        }

        lock (_typeStoreItems)
        {
            TypeStoreItem item = null;
            if (!_typeStoreItems.TryGetValue(type, out item))
            {
                IEnumerable<Attribute> attributes =
                TypeDescriptor.GetAttributes(type).Cast<Attribute>();
                item = new TypeStoreItem(type, attributes);
                _typeStoreItems[type] = item;
            }
            return item;
        }
    }

    /// <summary>
    /// Throws an ArgumentException of the validation context is null
    /// </summary>
    /// <param name="validationContext">The context to check</param>
    private static void EnsureValidationContext(ValidationContext validationContext)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }
    }

    /// <summary>
    /// Private abstract class for all store items
    /// </summary>
    private abstract class StoreItem
    {
        private static readonly IEnumerable<ValidationAttribute> _emptyValidationAttributeEnumerable = new ValidationAttribute[0];

        private readonly IEnumerable<ValidationAttribute> _validationAttributes;

        internal StoreItem(IEnumerable<Attribute> attributes)
        {
            _validationAttributes = attributes.OfType<ValidationAttribute>();
            DisplayAttribute = attributes.OfType<DisplayAttribute>().SingleOrDefault();
        }

        internal IEnumerable<ValidationAttribute> ValidationAttributes
        {
            get
            {
                return _validationAttributes;
            }
        }

        internal DisplayAttribute DisplayAttribute { get; set; }
    }

    /// <summary>
    /// Private class to store data associated with a type
    /// </summary>
    private class TypeStoreItem : StoreItem
    {
        private readonly object _syncRoot = new object();
        private readonly Type _type;
        private Dictionary<string, PropertyStoreItem> _propertyStoreItems;

        internal TypeStoreItem(Type type, IEnumerable<Attribute> attributes)
            : base(attributes)
        {
            _type = type;
        }

        internal PropertyStoreItem GetPropertyStoreItem(string propertyName)
        {
            PropertyStoreItem item = null;
            if (!TryGetPropertyStoreItem(propertyName, out item))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "AttributeStore_Unknown_Property", _type.Name, propertyName), "propertyName");
            }
            return item;
        }

        internal bool TryGetPropertyStoreItem(string propertyName, out PropertyStoreItem item)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException("propertyName");
            }

            if (_propertyStoreItems == null)
            {
                lock (_syncRoot)
                {
                    if (_propertyStoreItems == null)
                    {
                        _propertyStoreItems = CreatePropertyStoreItems();
                    }
                }
            }
            if (!_propertyStoreItems.TryGetValue(propertyName, out item))
            {
                return false;
            }
            return true;
        }

        private Dictionary<string, PropertyStoreItem> CreatePropertyStoreItems()
        {
            Dictionary<string, PropertyStoreItem> propertyStoreItems = new Dictionary<string, PropertyStoreItem>();

            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(_type);
            foreach (PropertyDescriptor property in properties)
            {
                PropertyStoreItem item = new PropertyStoreItem(property.PropertyType, GetExplicitAttributes(property).Cast<Attribute>());
                propertyStoreItems[property.Name] = item;
            }

            return propertyStoreItems;
        }

        public static AttributeCollection GetExplicitAttributes(PropertyDescriptor propertyDescriptor)
        {
            List<Attribute> attributes = new List<Attribute>(propertyDescriptor.Attributes.Cast<Attribute>());
            IEnumerable<Attribute> typeAttributes = TypeDescriptor.GetAttributes(propertyDescriptor.PropertyType).Cast<Attribute>();
            bool removedAttribute = false;
            foreach (Attribute attr in typeAttributes)
            {
                for (int i = attributes.Count - 1; i >= 0; --i)
                {
                    // We must use ReferenceEquals since attributes could Match if they are the same.
                    // Only ReferenceEquals will catch actual duplications.
                    if (object.ReferenceEquals(attr, attributes[i]))
                    {
                        attributes.RemoveAt(i);
                        removedAttribute = true;
                    }
                }
            }
            return removedAttribute ? new AttributeCollection(attributes.ToArray()) : propertyDescriptor.Attributes;
        }
    }

    /// <summary>
    /// Private class to store data associated with a property
    /// </summary>
    private class PropertyStoreItem : StoreItem
    {
        private readonly Type _propertyType;

        internal PropertyStoreItem(Type propertyType, IEnumerable<Attribute> attributes)
            : base(attributes)
        {
            _propertyType = propertyType;
        }

        internal Type PropertyType
        {
            get
            {
                return _propertyType;
            }
        }
    }
}
