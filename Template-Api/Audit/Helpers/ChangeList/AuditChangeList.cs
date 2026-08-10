using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Template_Api.Audit.Models.Internal;

namespace Template_Api.Audit.Helpers.ChangeList
{
    public class AuditChangeList<T>
    {
        private readonly T _previous;
        private readonly T _updated;
        private readonly List<AuditItemBase> _items = [];

        internal AuditChangeList(T previous, T updated)
        {
            _previous = previous;
            _updated = updated;
        }

        #region Items
        public AuditChangeList<T> AddField(Expression<Func<T, object>> field, string fieldName = null, AuditFormat format = AuditFormat.None)
        {
            _items.Add(new AuditFieldItem(AuditReflection.GetMemberName(field), fieldName, format, null));

            return this;
        }

        public AuditChangeList<T> AddLookup(Expression<Func<T, object>> field, Func<object, string> valueResolver, string fieldName = null)
        {
            ArgumentNullException.ThrowIfNull(valueResolver);

            _items.Add(new AuditFieldItem(AuditReflection.GetMemberName(field), fieldName, AuditFormat.None, valueResolver));

            return this;
        }

        public AuditChangeList<T> AddCollection<TItem>(
            Expression<Func<T, object>> collection,
            Func<TItem, object> keySelector,
            Func<TItem, string> nameSelector,
            string fieldName = null)
        {
            ArgumentNullException.ThrowIfNull(keySelector);

            _items.Add(new AuditCollectionItem<TItem>(AuditReflection.GetMemberName(collection), fieldName, keySelector, nameSelector));

            return this;
        }
        #endregion

        #region Build
        public AuditChangeSet Build()
        {
            var changeSet = new AuditChangeSet
            {
                State = _previous == null ? null : [],
                Payload = _updated == null ? null : []
            };

            foreach (var item in _items)
            {
                // Changes only make sense when both sides exist: a creation has no previous
                // version to compare against, and a deletion has no updated one
                if (_previous != null && _updated != null)
                    changeSet.Changes.AddRange(item.Process(_previous, _updated));

                if (_previous != null)
                {
                    var state = item.Snapshot(_previous);
                    changeSet.State[state.Key] = state.Value;
                }

                if (_updated != null)
                {
                    var payload = item.Snapshot(_updated);
                    changeSet.Payload[payload.Key] = payload.Value;
                }
            }

            return changeSet;
        }
        #endregion
    }

    public static class AuditChangeList
    {
        public static AuditChangeList<T> For<T>(T previous, T updated) where T : class
        {
            return new AuditChangeList<T>(previous, updated);
        }

        public static AuditChangeList<T> ForCreated<T>(T created) where T : class
        {
            return new AuditChangeList<T>(null, created);
        }

        public static AuditChangeList<T> ForDeleted<T>(T deleted) where T : class
        {
            return new AuditChangeList<T>(deleted, null);
        }
    }
}
