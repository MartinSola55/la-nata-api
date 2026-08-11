using System;
using System.Collections.Generic;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.Internal;

namespace LaNata_Api.Audit.Helpers.ChangeList
{
    internal class AuditFieldItem : AuditItemBase
    {
        private readonly AuditFormat _format;
        private readonly Func<object, string> _valueResolver;

        public AuditFieldItem(string field, string fieldName, AuditFormat format, Func<object, string> valueResolver)
        {
            Field = field;
            FieldName = string.IsNullOrWhiteSpace(fieldName) ? field : fieldName;
            _format = format;
            _valueResolver = valueResolver;
        }

        public override List<AuditChange> Process(object previous, object updated)
        {
            var changes = new List<AuditChange>();

            var previousValue = Describe(previous);
            var updatedValue = Describe(updated);

            if (previousValue == updatedValue)
                return changes;

            changes.Add(new AuditChange
            {
                Field = Field,
                FieldName = FieldName,
                PreviousValue = previousValue,
                UpdatedValue = updatedValue,
                Description = BuildDescription(previousValue, updatedValue)
            });

            return changes;
        }

        public override KeyValuePair<string, object> Snapshot(object instance)
        {
            return new KeyValuePair<string, object>(Field, Describe(instance));
        }

        #region Private
        private string Describe(object instance)
        {
            var value = AuditReflection.GetPropertyValue(instance, Field);

            if (_valueResolver != null)
            {
                var resolved = value == null ? null : _valueResolver(value);

                return string.IsNullOrWhiteSpace(resolved) ? AuditMessages.EmptyValue() : resolved;
            }

            return AuditFormatter.Describe(value, _format);
        }

        private string BuildDescription(string previousValue, string updatedValue)
        {
            var empty = AuditMessages.EmptyValue();

            if (previousValue == empty)
                return AuditMessages.FieldSet(FieldName, updatedValue);

            if (updatedValue == empty)
                return AuditMessages.FieldCleared(FieldName, previousValue);

            return AuditMessages.FieldUpdated(FieldName, previousValue, updatedValue);
        }
        #endregion
    }
}
