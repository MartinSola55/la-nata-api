using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.Internal;

namespace LaNata_Api.Audit.Helpers.ChangeList
{
    // Compares by key and reports additions and removals. Edits inside an item are not tracked:
    // register the item fields separately if they matter
    internal class AuditCollectionItem<TItem> : AuditItemBase
    {
        private readonly Func<TItem, object> _keySelector;
        private readonly Func<TItem, string> _nameSelector;

        public AuditCollectionItem(string field, string fieldName, Func<TItem, object> keySelector, Func<TItem, string> nameSelector)
        {
            Field = field;
            FieldName = string.IsNullOrWhiteSpace(fieldName) ? field : fieldName;
            _keySelector = keySelector;
            _nameSelector = nameSelector;
        }

        public override List<AuditChange> Process(object previous, object updated)
        {
            var changes = new List<AuditChange>();

            var previousItems = GetItems(previous);
            var updatedItems = GetItems(updated);

            var previousKeys = previousItems.Select(GetKey).ToList();
            var updatedKeys = updatedItems.Select(GetKey).ToList();

            foreach (var item in updatedItems.Where(x => !previousKeys.Contains(GetKey(x))))
                changes.Add(BuildChange(item, added: true));

            foreach (var item in previousItems.Where(x => !updatedKeys.Contains(GetKey(x))))
                changes.Add(BuildChange(item, added: false));

            return changes;
        }

        public override KeyValuePair<string, object> Snapshot(object instance)
        {
            var names = GetItems(instance)
                .Select(GetName)
                .ToList();

            return new KeyValuePair<string, object>(Field, names);
        }

        #region Private
        private List<TItem> GetItems(object instance)
        {
            var value = AuditReflection.GetPropertyValue(instance, Field);

            return value is IEnumerable<TItem> items ? [.. items] : [];
        }

        private string GetKey(TItem item)
        {
            var key = _keySelector(item);

            return Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private string GetName(TItem item)
        {
            var name = _nameSelector == null ? GetKey(item) : _nameSelector(item);

            return string.IsNullOrWhiteSpace(name) ? AuditMessages.EmptyValue() : name;
        }

        private AuditChange BuildChange(TItem item, bool added)
        {
            var name = GetName(item);

            return new AuditChange
            {
                Field = Field,
                FieldName = FieldName,
                PreviousValue = added ? null : name,
                UpdatedValue = added ? name : null,
                Description = added
                    ? AuditMessages.ItemAdded(FieldName, name)
                    : AuditMessages.ItemRemoved(FieldName, name)
            };
        }
        #endregion
    }
}
