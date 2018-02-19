// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Layout
{
    /// <summary>
    /// Manages measuring and arranging of controls.
    /// </summary>
    public class LayoutManager : ILayoutManager
    {
        private const int MaxCountOfProcessedControlsInLayoutPass = 100;
        private const int MaxControlLayoutCyclesCount = 10;

        private int _currentMeasuredPasses = 0;
        private int _currentArrangedPasses = 0;
        private readonly Dictionary<ILayoutable, int> _currentMeasured = new Dictionary<ILayoutable, int>();
        private readonly Dictionary<ILayoutable, int> _currentArranged = new Dictionary<ILayoutable, int>();

        private readonly HashSet<ILayoutable> _toMeasure = new HashSet<ILayoutable>();
        private readonly HashSet<ILayoutable> _toArrange = new HashSet<ILayoutable>();
        private bool _queued;
        private bool _running;

        /// <summary>
        /// Gets the layout manager.
        /// </summary>
        public static ILayoutManager Instance => AvaloniaLocator.Current.GetService<ILayoutManager>();

        /// <inheritdoc/>
        public void InvalidateMeasure(ILayoutable control)
        {
            Contract.Requires<ArgumentNullException>(control != null);
            Dispatcher.UIThread.VerifyAccess();

            if (GetCount(control, _currentMeasured) >= MaxControlLayoutCyclesCount)
            {
                //TODO: a possible layout cycle what to do, just log it and leave it????
                return;
            }

            _toMeasure.Add(control);
            _toArrange.Add(control);

            QueueLayoutPass();
        }

        /// <inheritdoc/>
        public void InvalidateArrange(ILayoutable control)
        {
            Contract.Requires<ArgumentNullException>(control != null);
            Dispatcher.UIThread.VerifyAccess();

            if (GetCount(control, _currentArranged) >= MaxControlLayoutCyclesCount)
            {
                //TODO: a possible layout cycle what to do, just log it and leave it????
                return;
            }

            _toArrange.Add(control);

            QueueLayoutPass();
        }

        /// <inheritdoc/>
        public void ExecuteLayoutPass()
        {
            const int MaxPasses = 3;

            Dispatcher.UIThread.VerifyAccess();

            if (!_running)
            {
                _running = true;

                Logger.Information(
                    LogArea.Layout,
                    this,
                    "Started layout pass. To measure: {Measure} To arrange: {Arrange}",
                    _toMeasure.Count,
                    _toArrange.Count);

                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                try
                {
                    for (var pass = 0; pass < MaxPasses; ++pass)
                    {
                        ExecuteMeasurePass();
                        ExecuteArrangePass();

                        if (BreakArrange() || BreakMeasure()) break;

                        if (_toMeasure.Count == 0)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    _running = false;
                }

                stopwatch.Stop();
                Logger.Information(LogArea.Layout, this,
                    "Layout pass finised in {Time}, Measured {Measured}, Arranged {Arranged}",
                    stopwatch.Elapsed, _currentMeasuredPasses, _currentArrangedPasses);

                _currentArrangedPasses = 0;
                _currentMeasuredPasses = 0;
                _currentMeasured.Clear();
                _currentArranged.Clear();
            }

            _queued = false;

            if (_toMeasure.Count > 0 || _toArrange.Count > 0)
            {
                QueueLayoutPass();
            }
        }

        /// <inheritdoc/>
        public void ExecuteInitialLayoutPass(ILayoutRoot root)
        {
            Measure(root);
            Arrange(root);

            // Running the initial layout pass may have caused some control to be invalidated
            // so run a full layout pass now (this usually due to scrollbars; its not known
            // whether they will need to be shown until the layout pass has run and if the
            // first guess was incorrect the layout will need to be updated).
            ExecuteLayoutPass();
        }

        private void ExecuteMeasurePass()
        {
            while (_toMeasure.Count > 0)
            {
                var next = _toMeasure.First();
                Measure(next);

                ++_currentMeasuredPasses;
                if (BreakMeasure()) break;
            }
        }

        private void ExecuteArrangePass()
        {
            while (_toArrange.Count > 0 && _toMeasure.Count == 0)
            {
                var next = _toArrange.First();
                Arrange(next);

                ++_currentArrangedPasses;
                if (BreakArrange()) break;
            }
        }

        private void Measure(ILayoutable control)
        {
            var root = control as ILayoutRoot;
            var parent = control.VisualParent as ILayoutable;

            if (root != null)
            {
                root.Measure(root.MaxClientSize);
            }
            else if (parent != null && !parent.IsMeasureValid)
            {
                Measure(parent);
            }

            if (!control.IsMeasureValid)
            {
                //fix possible null reference
                control.Measure(control.PreviousMeasure ?? Size.Infinity);
                IncrementCount(control, _currentMeasured);
            }

            _toMeasure.Remove(control);
        }

        private void Arrange(ILayoutable control)
        {
            var root = control as ILayoutRoot;
            var parent = control.VisualParent as ILayoutable;

            if (root != null)
            {
                root.Arrange(new Rect(root.DesiredSize));
            }
            else if (parent != null && !parent.IsArrangeValid)
            {
                Arrange(parent);
            }

            if (control.PreviousArrange.HasValue)
            {
                control.Arrange(control.PreviousArrange.Value);
                IncrementCount(control, _currentArranged);
            }

            _toArrange.Remove(control);
        }

        private bool BreakMeasure() => _currentMeasuredPasses > MaxCountOfProcessedControlsInLayoutPass;

        private bool BreakArrange() => _currentArrangedPasses > MaxCountOfProcessedControlsInLayoutPass;

        private static void IncrementCount(ILayoutable control, Dictionary<ILayoutable, int> dict)
        {
            int cnt;
            dict.TryGetValue(control, out cnt);
            dict[control] = ++cnt;
        }

        private static int GetCount(ILayoutable control, Dictionary<ILayoutable, int> dict)
        {
            int cnt;
            return dict.TryGetValue(control, out cnt) ? cnt : -1;
        }

        private void QueueLayoutPass()
        {
            if (!_queued)
            {
                Dispatcher.UIThread.InvokeAsync(ExecuteLayoutPass, DispatcherPriority.Render);
                _queued = true;
            }
        }
    }
}