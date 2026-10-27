using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using FreePin.Native;
using DrawingRectangle = System.Drawing.Rectangle;

namespace FreePin.Services
{
    public enum SmartCandidateKind
    {
        Window,
        Control
    }

    public readonly struct SmartPoint
    {
        public SmartPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    public sealed class SmartSelectionCandidate
    {
        public DrawingRectangle Bounds { get; set; }
        public string Name { get; set; }
        public SmartCandidateKind Kind { get; set; }
        public IntPtr Hwnd { get; set; }

        public bool IsEmpty
        {
            get { return Hwnd == IntPtr.Zero || Bounds.Width < 2 || Bounds.Height < 2; }
        }
    }

    /// <summary>
    /// Finds the window or accessible control under a physical screen point.
    /// The screenshot overlay is topmost, so point-hit-testing APIs such as
    /// WindowFromPoint or AutomationElement.FromPoint would return FreePin itself.
    /// We enumerate windows and inspect their accessibility trees instead.
    /// </summary>
    public sealed class SmartSelectionService : IDisposable
    {
        private const int CacheMilliseconds = 750;
        private const int MaxUiaElements = 5000;

        private readonly int _ownProcessId;
        private readonly BlockingCollection<ControlRequest> _requests =
            new BlockingCollection<ControlRequest>(new ConcurrentQueue<ControlRequest>(), 4);
        private readonly Dictionary<IntPtr, ControlCache> _controlCache =
            new Dictionary<IntPtr, ControlCache>();
        private readonly Thread _worker;
        private volatile bool _disposed;

        public SmartSelectionService()
        {
            _ownProcessId = Process.GetCurrentProcess().Id;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "FreePin Smart Selection"
            };
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
        }

        /// <summary>
        /// Fast, synchronous top-level window lookup. Returns null when no
        /// suitable window is under the point.
        /// </summary>
        public SmartSelectionCandidate FindWindow(SmartPoint point)
        {
            if (_disposed)
                return null;

            SmartSelectionCandidate result = null;
            try
            {
                NativeMethods.EnumWindows((hWnd, lParam) =>
                {
                    if (result != null)
                        return false;

                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(hWnd, out processId);
                    if (processId == (uint)_ownProcessId)
                        return true;
                    if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                        return true;
                    if (NativeMethods.IsWindowCloaked(hWnd))
                        return true;

                    long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GwlExStyle);
                    if ((exStyle & NativeMethods.WsExTransparent) != 0)
                        return true;

                    string className = NativeMethods.GetWindowClassName(hWnd);
                    if (className == "Progman" || className == "WorkerW")
                        return true;

                    NativeMethods.RECT rect;
                    if (!NativeMethods.TryGetExtendedFrameBounds(hWnd, out rect) &&
                        !NativeMethods.GetWindowRect(hWnd, out rect))
                        return true;
                    if (rect.Right - rect.Left < 2 || rect.Bottom - rect.Top < 2)
                        return true;
                    if (!Contains(rect, point))
                        return true;

                    result = new SmartSelectionCandidate
                    {
                        Hwnd = hWnd,
                        Bounds = new DrawingRectangle(rect.Left, rect.Top,
                            rect.Right - rect.Left, rect.Bottom - rect.Top),
                        Name = WindowLabel(hWnd, className),
                        Kind = SmartCandidateKind.Window
                    };
                    return false;
                }, IntPtr.Zero);
            }
            catch
            {
                return null;
            }
            return result;
        }

        /// <summary>
        /// Window first, then a bounded accessibility lookup for a child control.
        /// Returns the window candidate if the control lookup cannot finish quickly.
        /// </summary>
        public async Task<SmartSelectionCandidate> DetectAsync(double x, double y)
        {
            if (_disposed)
                return null;

            var point = new SmartPoint(x, y);
            SmartSelectionCandidate window = FindWindow(point);
            if (window == null)
                return null;

            using (var cancellation = new CancellationTokenSource())
            {
                Task<SmartSelectionCandidate> task = FindControlAsync(window, point, cancellation.Token);
                Task finished = await Task.WhenAny(task, Task.Delay(200)).ConfigureAwait(false);
                if (!ReferenceEquals(finished, task))
                {
                    cancellation.Cancel();
                    return window;
                }
                return await task.ConfigureAwait(false) ?? window;
            }
        }

        public Task<SmartSelectionCandidate> FindControlAsync(
            SmartSelectionCandidate window, SmartPoint point, CancellationToken token)
        {
            if (_disposed || window == null || window.IsEmpty || token.IsCancellationRequested)
                return Task.FromResult(window);

            var completion = new TaskCompletionSource<SmartSelectionCandidate>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new ControlRequest(window, point, completion, token);
            if (!_requests.TryAdd(request))
            {
                completion.TrySetResult(window);
                return completion.Task;
            }
            return completion.Task;
        }

        private void WorkerLoop()
        {
            foreach (ControlRequest request in _requests.GetConsumingEnumerable())
            {
                if (_disposed || request.Token.IsCancellationRequested)
                {
                    request.Completion.TrySetResult(request.Window);
                    continue;
                }

                try
                {
                    SmartSelectionCandidate control = FindControlCore(request.Window, request.Point);
                    request.Completion.TrySetResult(control ?? request.Window);
                }
                catch
                {
                    request.Completion.TrySetResult(request.Window);
                }
            }
        }

        private SmartSelectionCandidate FindControlCore(
            SmartSelectionCandidate window, SmartPoint point)
        {
            List<ControlCandidate> controls = GetControls(window);
            ControlCandidate best = null;
            double bestArea = double.MaxValue;
            foreach (ControlCandidate candidate in controls)
            {
                if (!candidate.Contains(point))
                    continue;
                double area = candidate.Area;
                if (area >= bestArea)
                    continue;
                bestArea = area;
                best = candidate;
            }

            if (best == null)
                return window;

            return new SmartSelectionCandidate
            {
                Hwnd = window.Hwnd,
                Bounds = best.Bounds,
                Name = best.Name,
                Kind = SmartCandidateKind.Control
            };
        }

        private List<ControlCandidate> GetControls(SmartSelectionCandidate window)
        {
            ControlCache cached;
            if (_controlCache.TryGetValue(window.Hwnd, out cached) &&
                DateTime.UtcNow <= cached.Expires &&
                cached.WindowBounds == window.Bounds)
            {
                return cached.Controls;
            }

            var controls = new List<ControlCandidate>();
            try
            {
                AutomationElement root = AutomationElement.FromHandle(window.Hwnd);
                if (root != null)
                {
                    AutomationElementCollection descendants = root.FindAll(
                        TreeScope.Descendants, Condition.TrueCondition);
                    int count = Math.Min(descendants.Count, MaxUiaElements);
                    double windowArea = Math.Max(1, window.Bounds.Width * window.Bounds.Height);
                    for (int i = 0; i < count; i++)
                    {
                        AutomationElement element = descendants[i];
                        try
                        {
                            if (element.Current.IsOffscreen)
                                continue;

                            System.Windows.Rect rect = element.Current.BoundingRectangle;
                            if (rect.Width < 8 || rect.Height < 8)
                                continue;
                            if (double.IsNaN(rect.Width) || double.IsNaN(rect.Height) ||
                                double.IsInfinity(rect.Width) || double.IsInfinity(rect.Height))
                                continue;

                            var bounds = new DrawingRectangle(
                                (int)Math.Round(rect.Left), (int)Math.Round(rect.Top),
                                (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
                            if (!Intersects(bounds, window.Bounds))
                                continue;

                            double area = (double)bounds.Width * bounds.Height;
                            // A document/pane covering almost the whole window is not a
                            // useful "block"; let the window candidate handle it.
                            if (area >= windowArea * 0.9)
                                continue;

                            string name = element.Current.Name;
                            if (string.IsNullOrWhiteSpace(name))
                                name = element.Current.AutomationId;
                            if (string.IsNullOrWhiteSpace(name))
                                name = element.Current.ClassName;

                            controls.Add(new ControlCandidate
                            {
                                Bounds = bounds,
                                Name = name ?? string.Empty,
                                Area = area
                            });
                        }
                        catch
                        {
                            // Accessibility providers are allowed to fail per element.
                        }
                    }
                }
            }
            catch
            {
                // No accessibility tree; the window candidate remains usable.
            }

            _controlCache[window.Hwnd] = new ControlCache
            {
                WindowBounds = window.Bounds,
                Controls = controls,
                Expires = DateTime.UtcNow.AddMilliseconds(CacheMilliseconds)
            };
            return controls;
        }

        private static bool Contains(NativeMethods.RECT rect, SmartPoint point)
        {
            return point.X >= rect.Left && point.X < rect.Right &&
                   point.Y >= rect.Top && point.Y < rect.Bottom;
        }

        private static bool Intersects(DrawingRectangle a, DrawingRectangle b)
        {
            DrawingRectangle intersection = DrawingRectangle.Intersect(a, b);
            return intersection.Width > 0 && intersection.Height > 0;
        }

        private static string WindowLabel(IntPtr hWnd, string className)
        {
            string title = NativeMethods.GetWindowTitle(hWnd);
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();
            return string.IsNullOrWhiteSpace(className) ? "窗口" : className;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _requests.CompleteAdding();
            }
            catch
            {
                // ignored
            }
            try
            {
                if (!_worker.Join(800))
                    _worker.Abort();
            }
            catch
            {
                // ignored
            }
        }

        private sealed class ControlRequest
        {
            public ControlRequest(SmartSelectionCandidate window, SmartPoint point,
                TaskCompletionSource<SmartSelectionCandidate> completion, CancellationToken token)
            {
                Window = window;
                Point = point;
                Completion = completion;
                Token = token;
            }

            public SmartSelectionCandidate Window { get; }
            public SmartPoint Point { get; }
            public TaskCompletionSource<SmartSelectionCandidate> Completion { get; }
            public CancellationToken Token { get; }
        }

        private sealed class ControlCache
        {
            public DrawingRectangle WindowBounds;
            public List<ControlCandidate> Controls;
            public DateTime Expires;
        }

        private sealed class ControlCandidate
        {
            public DrawingRectangle Bounds;
            public string Name;
            public double Area;

            public bool Contains(SmartPoint point)
            {
                return point.X >= Bounds.Left && point.X < Bounds.Right &&
                       point.Y >= Bounds.Top && point.Y < Bounds.Bottom;
            }
        }
    }
}
