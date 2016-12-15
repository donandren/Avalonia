// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace VirtualizationTest.ViewModels
{
    internal class ItemViewModel : ReactiveObject
    {
        private string _prefix;
        private int _index;

        public ItemViewModel(int index, string prefix = "Item")
        {
            _prefix = prefix;
            _index = index;

            var items = Enumerable.Range(1, 10).Select(i => $"cboItem {i}").ToArray();
            var rl = new ReactiveList<string>();
            rl.AddRange(items);
            Items = rl;
            //Items = items;
        }

        public string Header => $"{_prefix} {_index}";

        public IEnumerable<string> Items { get; }
    }
}