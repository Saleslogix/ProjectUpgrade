using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Sage.Platform.Orm.Entities;
using System.Reflection;

namespace Sage.Platform.Upgrade
{
    public class ObjectDiffMerge
    {
        private List<string> _warnings;
        private List<PropertyChange> _changes;

        public IEnumerable<string> Warnings
        {
            get { return _warnings; }
        }

        public IEnumerable<PropertyChange> Changes
        {
            get { return _changes; }
        }

        public ObjectDiffMerge()
        {
            _warnings = new List<string>();
            _changes = new List<PropertyChange>();
        }

        public IEnumerable<PropertyChange> CompareObjects(object currentObject, object previousObject)
        {
            CompareObjects(currentObject, previousObject, string.Empty);
            return Changes;
        }

        private void CompareObjects(object currentObject, object previousObject, string parentName)
        {
            _changes.AddRange(GetAllComparableProperties(currentObject.GetType())
                .Select(prop => CompareProperty(parentName, prop, currentObject, previousObject))
                .Where(change => change != null));
        }

        /// <summary>
        /// /gets all public instance properties without the XmlIgnore attribute
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static List<PropertyInfo> GetAllComparableProperties(Type type)
        {
            var serializableProps = from prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                    let ignoreAttrib = Attribute.GetCustomAttribute(prop, typeof(XmlIgnoreAttribute))
                                    where ignoreAttrib == null && prop.CanRead && prop.CanWrite
                                    select prop;

            return serializableProps.ToList();
        }

        private PropertyChange CompareProperty(string parentPropertyName, PropertyInfo property, object currentObject, object previousObject)
        {
            if (IsSimpleType(property.PropertyType))
                return CompareSimpleProperty(parentPropertyName, property, currentObject, previousObject);

            string propertyName = FormatPropertyName(parentPropertyName, property);

            object currentChildObject = property.GetValue(currentObject, null);
            object previousChildObject = property.GetValue(previousObject, null);

            if (currentChildObject is IEnumerable)
            {
                return CompareEnumerableProperty(parentPropertyName, property, currentObject, previousObject);
            }

            CompareObjects(currentChildObject, previousChildObject, propertyName);
            return null;
        }

        private static PropertyChange CompareSimpleProperty(string parentPropertyName, PropertyInfo property, 
            object currentObject, object previousObject)
        {
            object currentValue = property.GetValue(currentObject, null);
            object previousValue = property.GetValue(previousObject, null);

            if (currentValue == null && previousValue == null)
                return null;

            string propertyName = FormatPropertyName(parentPropertyName, property);
            if (previousValue == null)
                return new PropertyChange(propertyName, PropertyChangeType.Add, null, currentValue);

            if (currentValue == null)
                return new PropertyChange(propertyName, PropertyChangeType.Remove, previousValue, null);

            if (!currentValue.Equals(previousValue))
                return new PropertyChange(propertyName, PropertyChangeType.Change, previousValue, currentValue);

            return null;
        }

        private PropertyChange CompareEnumerableProperty(string parentPropertyName, PropertyInfo property, 
            object currentObject, object previousObject)
        {
            IEnumerable currentValues = property.GetValue(currentObject, null) as IEnumerable;
            IEnumerable previousValues = property.GetValue(previousObject, null) as IEnumerable;

            string propertyName = FormatPropertyName(parentPropertyName, property);

            if (currentValues == null || previousValues == null)
                return new PropertyChange(propertyName, PropertyChangeType.Change, currentValues, previousValues);

            List<object> currentAsList = currentValues.Cast<object>().ToList();
            List<object> previousAsList = previousValues.Cast<object>().ToList();
            if (currentAsList.Count != previousAsList.Count)
                return new PropertyChange(propertyName, PropertyChangeType.Change, previousValues, currentValues);

            for (int i = 0; i < currentAsList.Count; i++)
            {
                CompareObjects(currentAsList[i], previousAsList[i], string.Format("{0}[{1}]", propertyName, i));    
            }

            return null;
        }

        private static string FormatPropertyName(string parentPropertyName, PropertyInfo property)
        {
            if (string.IsNullOrEmpty(parentPropertyName))
                return property.Name;

            return string.Format("{0}.{1}", parentPropertyName, property.Name);
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(DateTime)
                || type == typeof(decimal)
                || type == typeof(string)
                || type == typeof(Guid);    
        }
    }

    public class PropertyChange
    {
        public PropertyChangeType ChangeType { get; set; }
        public string Name { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        public PropertyChange(string name, PropertyChangeType changeType, object oldValue, object newValue)
        {
            Name = name;
            ChangeType = changeType;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public enum PropertyChangeType
    {
        Add,
        Change,
        Remove
    }

    public class TestMe
    {
        public void Test()
        {
            OrmRelationship prevRelationship, currentRelationship;
            var serializer = new XmlSerializer(typeof (OrmRelationship));
            using(var prevStream = System.IO.File.OpenRead(@"C:\Test\SLX_InternalBase\Model\Entity Model\Relationships\Account.ShippingId.ShippingAddress.ManyToOne.Address.AddressId.relationship.xml"))
            {
                prevRelationship = serializer.Deserialize(prevStream) as OrmRelationship;
            }
            using (var currentStream = System.IO.File.OpenRead(@"C:\Test\SLX_Internal\Model\Entity Model\Relationships\Account.Address.a6e41b881fab44f8b33d753230f30ef9.relationship.xml"))
            {
                currentRelationship = serializer.Deserialize(currentStream) as OrmRelationship;
            }

            var diffMerge = new ObjectDiffMerge();
            diffMerge.CompareObjects(currentRelationship, prevRelationship);
        }
    }
}