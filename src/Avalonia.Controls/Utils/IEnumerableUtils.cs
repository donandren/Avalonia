// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Avalonia.Controls.Utils
{
    internal static class IEnumerableUtils
    {
        public static bool Contains(this IEnumerable items, object item)
        {
            return items.IndexOf(item) != -1;
        }

        public static int Count(this IEnumerable items)
        {
            if (items != null)
            {
                var collection = items as ICollection;

                if (collection != null)
                {
                    return collection.Count;
                }
                else
                {
                    return Enumerable.Count(items.Cast<object>());
                }
            }
            else
            {
                return 0;
            }
        }

        public static int IndexOf(this IEnumerable items, object item)
        {
            Contract.Requires<ArgumentNullException>(items != null);

            var list = items as IList;

            if (list != null)
            {
                return list.IndexOf(item);
            }
            else
            {
                int index = 0;

                foreach (var i in items)
                {
                    if (ReferenceEquals(i, item))
                    {
                        return index;
                    }

                    ++index;
                }

                return -1;
            }
        }

        public static object ElementAt(this IEnumerable items, int index)
        {
            Contract.Requires<ArgumentNullException>(items != null);

            var list = items as IList;

            if (list != null)
            {
                return list[index];
            }
            else
            {
                return Enumerable.ElementAt(items.Cast<object>(), index);
            }
        }

        public static IDisposable SubscribeForCollectionChangesSafe(this IEnumerable items,
                                                Action<object, NotifyCollectionChangedEventArgs> callback)
        {
            var incc = items as INotifyCollectionChanged;

            if (incc != null)
            {
                return Observable.FromEventPattern<NotifyCollectionChangedEventHandler,
                                                    NotifyCollectionChangedEventArgs>(
                                                        add => incc.CollectionChanged += add,
                                                        remove => incc.CollectionChanged -= remove)
                                                    .Subscribe(v => callback(v.Sender, v.EventArgs));
            }

            return Disposable.Empty;
        }
    }
}