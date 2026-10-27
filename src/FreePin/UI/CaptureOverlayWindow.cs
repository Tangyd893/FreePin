using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using FreePin.Native;
using System.Windows.Threading;
using FreePin.Annotations;
using FreePin.Imaging;
using FreePin.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;

namespace FreePin.UI
{
    public enum CaptureAction
    {
        Copy,
        Pin,
        Save,
        Ocr,
        LongCapture,
        Cancel
    }

    public sealed class CaptureResult
    {
        public DrawingBitmap Image { get; set; }
        public BitmapSource Source { get; set; }
        public CaptureAction Action { get; set; }
        public DrawingRectangle ScreenBounds { get; set; }
    }

    public sealed class CaptureOverlayWindow : Window
    {
        private readonly ScreenFrame _frame;
        private readonly double _scale;
        private readonly Action<CaptureResult> _completed;
        private readonly CaptureSurface _surface;
        private readonly Border _smartBorder;
        private readonly CaptureToolbar _toolbar;
        private readonly PixelMagnifier _magnifier;
        private Rect _toolbarRect;
        private bool _placing;
        private bool _finished;
        private readonly bool _smartSelectionEnabled;
        private readonly bool _ownsSmartSelection;
        private readonly SmartSelectionService _smartSelection;
        private readonly DispatcherTimer _smartTimer;
        private readonly Stopwatch _smartClock = Stopwatch.StartNew();
        private long _lastFastTimestamp;
        private bool _smartDetecting;
        private bool _smartPendingRefine;
        private int _smartRetryCount;
        private Point _smartPointer;
        private int _smartRequest;

        public CaptureStyle CurrentStyle { get; }

        public CaptureOverlayWindow(ScreenFrame frame, double dpiScale, Action<CaptureResult> completed,
            CaptureStyle initialStyle = null, bool smartSelectionEnabled = true,
            SmartSelectionService sharedSmartSelection = null)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _completed = completed;
            _scale = dpiScale <= 0 ? 1 : dpiScale;

            Title = "FreePin - 截图";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Left = frame.Bounds.Left / _scale;
            Top = frame.Bounds.Top / _scale;
            Width = frame.Bounds.Width / _scale;
            Height = frame.Bounds.Height / _scale;

            var root = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
            _surface = new CaptureSurface(frame, _scale, initialStyle);
            CurrentStyle = _surface.Style;
            _surface.Width = Width;
            _surface.Height = Height;

            _smartSelectionEnabled = smartSelectionEnabled;
            if (_smartSelectionEnabled)
            {
                _ownsSmartSelection = sharedSmartSelection == null;
                _smartSelection = sharedSmartSelection ?? new SmartSelectionService();
                _smartTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
                _smartTimer.Tick += (s, e) => RequestSmartDetection();
            }

            _surface.SelectionChanged += (s, e) =>
            {
                CancelSmartDetection();
                UpdateChrome();
            };
            _surface.AnnotationsChanged += (s, e) => UpdateHistory();
            _surface.StyleChanged += (s, e) =>
            {
                _toolbar.SetStyle();
                UpdateMagnifier();
            };
            _surface.ToolChanged += (s, e) =>
            {
                _toolbar.SetTool(_surface.Tool, false);
                UpdateCursorForToolbar();
                UpdateMagnifier();
            };
            _surface.CursorMoved += OnSurfaceCursorMoved;
            _surface.SmartCandidateChanged += (s, e) => UpdateSmartBorder();
            root.Children.Add(_surface);

            Color accent = UiTheme.Accent;
            _smartBorder = new Border
            {
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.8),
                Background = new SolidColorBrush(Color.FromArgb(18, accent.R, accent.G, accent.B)),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                SnapsToDevicePixels = true
            };
            root.Children.Add(_smartBorder);

            _magnifier = new PixelMagnifier();
            root.Children.Add(_magnifier);

            _toolbar = new CaptureToolbar(_surface.Style);
            _toolbar.ToolChanged += tool => _surface.Tool = tool;
            _toolbar.ColorChanged += color => _surface.SetColor(color);
            _toolbar.ThicknessChanged += value => _surface.SetThickness(value);
            _toolbar.FontSizeChanged += value => _surface.SetFontSize(value);
            _toolbar.RoundedChanged += value => _surface.SetRoundedRectangle(value);
            _toolbar.UndoRequested += () => _surface.Undo();
            _toolbar.RedoRequested += () => _surface.Redo();
            _toolbar.ActionRequested += action =>
            {
                if (action == CaptureAction.Cancel)
                    Cancel();
                else
                    Finish(action);
            };
            _toolbar.Visibility = Visibility.Collapsed;
            root.Children.Add(_toolbar);

            Content = root;
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (s, e) =>
            {
                CancelSmartDetection();
                if (_ownsSmartSelection && _smartSelection != null)
                    _smartSelection.Dispose();
                _frame.Dispose();
                if (!_finished)
                {
                    // Nothing to hand over; the overlay was cancelled.
                }
            };
            Loaded += (s, e) =>
            {
                Activate();
                _surface.Focus();
                Keyboard.Focus(_surface);
                UpdateChrome();
                UpdateFastCandidate(GetCursorScreen());
                OnSurfaceCursorMoved(s, e);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Keep the tool-window overlay out of Alt+Tab and Task View.
            NativeMethods.MarkToolWindow(new WindowInteropHelper(this).Handle);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_finished || _surface.IsTextEditing)
                return;

            if (e.Key == Key.Escape)
            {
                if (!_surface.CancelPending())
                    Cancel();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter || (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
            {
                if (_surface.HasSelection)
                    Finish(CaptureAction.Copy);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (_surface.HasSelection)
                    Finish(CaptureAction.Save);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) == 0)
            {
                CaptureTool? tool = ToolForKey(e.Key);
                if (tool.HasValue)
                {
                    _toolbar.SetTool(tool.Value);
                    e.Handled = true;
                    return;
                }
            }

            if (_surface.HandleKey(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
            }
        }

        private static CaptureTool? ToolForKey(Key key)
        {
            switch (key)
            {
                case Key.V: return CaptureTool.Select;
                case Key.P: return CaptureTool.Pen;
                case Key.L: return CaptureTool.Line;
                case Key.A: return CaptureTool.Arrow;
                case Key.R: return CaptureTool.Rectangle;
                case Key.T: return CaptureTool.Text;
                case Key.I: return CaptureTool.Eyedropper;
                default: return null;
            }
        }

        private void UpdateChrome()
        {
            bool hasSelection = _surface.HasSelection && !_surface.IsCreatingSelection;
            _toolbar.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            if (hasSelection)
            {
                DrawingRectangle selection = _surface.Selection;
                _toolbar.SetSelectionSize(selection.Width, selection.Height);
                SchedulePlaceToolbar();
            }
            UpdateHistory();
            UpdateSmartBorder();
            UpdateMagnifier();
        }

        private void UpdateSmartBorder()
        {
            if (_smartBorder == null)
                return;

            if (!_smartSelectionEnabled || _finished ||
                _surface.HasSelection || _surface.IsCreatingSelection ||
                !_surface.HasSmartCandidate)
            {
                _smartBorder.Visibility = Visibility.Collapsed;
                return;
            }

            Rect candidate = ToLogical(_surface.SmartCandidate);
            if (candidate.Width < 2 || candidate.Height < 2)
            {
                _smartBorder.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.SetLeft(_smartBorder, Math.Round(candidate.X));
            Canvas.SetTop(_smartBorder, Math.Round(candidate.Y));
            _smartBorder.Width = Math.Max(2, Math.Round(candidate.Width));
            _smartBorder.Height = Math.Max(2, Math.Round(candidate.Height));
            _smartBorder.Visibility = Visibility.Visible;
        }

        private void UpdateHistory()
        {
            _toolbar.SetHistory(_surface.CanUndo, _surface.CanRedo);
        }

        private void UpdateCursorForToolbar()
        {
            _surface.Focus();
        }

        private void SchedulePlaceToolbar()
        {
            if (_placing)
                return;
            _placing = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _placing = false;
                PlaceToolbar();
            }));
        }

        private void PlaceToolbar()
        {
            if (_finished || _toolbar.Visibility != Visibility.Visible)
                return;

            double overlayWidth = ActualWidth > 0 ? ActualWidth : Width;
            double overlayHeight = ActualHeight > 0 ? ActualHeight : Height;
            if (overlayWidth <= 0 || overlayHeight <= 0)
                return;

            _toolbar.MaxWidth = Math.Max(1, overlayWidth - 16);
            _toolbar.Measure(new Size(_toolbar.MaxWidth, double.PositiveInfinity));
            Size toolbarSize = _toolbar.DesiredSize;
            if (toolbarSize.Width <= 0 || toolbarSize.Height <= 0)
                toolbarSize = new Size(_toolbar.ActualWidth, _toolbar.ActualHeight);
            if (toolbarSize.Width <= 0 || toolbarSize.Height <= 0)
                return;

            Rect selection = ToLogical(_surface.Selection);
            _toolbarRect = ComputeToolbarRect(new Size(overlayWidth, overlayHeight), selection, toolbarSize);
            Canvas.SetLeft(_toolbar, _toolbarRect.X);
            Canvas.SetTop(_toolbar, _toolbarRect.Y);
            UpdateMagnifier();
        }

        internal static Rect ComputeToolbarRect(
            Size overlaySize, Rect selection, Size toolbarSize,
            double gap = 8, double edge = 8)
        {
            if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
                return new Rect();

            double width = Math.Max(1, toolbarSize.Width);
            double height = Math.Max(1, toolbarSize.Height);
            double maxX = Math.Max(edge, overlaySize.Width - width - edge);
            double x = selection.X + (selection.Width - width) / 2;
            x = Math.Max(edge, Math.Min(maxX, x));

            double below = selection.Bottom + gap;
            double above = selection.Top - gap - height;
            double y;
            if (below + height <= overlaySize.Height - edge)
                y = below;
            else if (above >= edge)
                y = above;
            else
            {
                double maxY = Math.Max(edge, overlaySize.Height - height - edge);
                y = Math.Max(edge, Math.Min(maxY, selection.Bottom - height - gap));
            }

            return new Rect(Math.Round(x), Math.Round(y), Math.Round(width), Math.Round(height));
        }

        private void OnSurfaceCursorMoved(object sender, EventArgs e)
        {
            UpdateMagnifier();
            if (!_smartSelectionEnabled || _finished || _surface.HasSelection ||
                _surface.IsCreatingSelection || _surface.IsSmartPendingClick ||
                _surface.IsDrawingAnnotation)
            {
                CancelSmartDetection();
                return;
            }

            Point previousPointer = _smartPointer;
            _smartPointer = GetCursorScreen();
            if (_smartPointer != previousPointer)
                _smartRetryCount = 0;
            long now = _smartClock.ElapsedMilliseconds;
            if (now - _lastFastTimestamp >= 14)
            {
                _lastFastTimestamp = now;
                UpdateFastCandidate(_smartPointer);
            }

            if (_smartTimer == null)
                return;

            // Do not reset the timer on every mouse move: that was the source of
            // the "not following the cursor" feeling. Refinement runs at most a
            // few times per second, while the fast window/control preview above
            // updates on every rendered frame.
            _smartPendingRefine = true;
            if (!_smartDetecting && !_smartTimer.IsEnabled)
                _smartTimer.Start();
        }

        private void UpdateFastCandidate(Point pointer)
        {
            if (_smartSelection == null || _finished || _surface.HasSelection ||
                _surface.IsCreatingSelection)
            {
                return;
            }

            var smartPoint = new SmartPoint(pointer.X, pointer.Y);
            SmartSelectionCandidate window = _smartSelection.FindWindow(smartPoint);
            if (window == null)
            {
                _surface.ClearSmartCandidate();
                return;
            }

            SetSmartCandidateLocal(window);
            SmartSelectionCandidate cached = _smartSelection.FindCachedControl(window, smartPoint);
            if (cached != null)
                SetSmartCandidateLocal(cached);
        }

        private void SetSmartCandidateLocal(SmartSelectionCandidate candidate)
        {
            if (candidate == null)
                return;
            DrawingRectangle bounds = candidate.Bounds;
            _surface.SetSmartCandidate(new DrawingRectangle(
                bounds.X - _frame.Bounds.Left,
                bounds.Y - _frame.Bounds.Top,
                bounds.Width,
                bounds.Height));
        }

        private void CancelSmartDetection()
        {
            if (_smartTimer != null)
                _smartTimer.Stop();
            _smartPendingRefine = false;
            _smartRetryCount = 0;
            _smartRequest++;
        }

        private void RequestSmartDetection()
        {
            if (_smartTimer != null)
                _smartTimer.Stop();
            if (!_smartSelectionEnabled || _finished || _surface.HasSelection ||
                _surface.IsCreatingSelection || _surface.IsSmartPendingClick)
            {
                return;
            }
            if (_smartDetecting)
            {
                _smartPendingRefine = true;
                return;
            }

            int request = ++_smartRequest;
            _smartDetecting = true;
            _smartPendingRefine = false;
            Point pointer = _smartPointer;
            _ = DetectSmartAsync(pointer, request);
        }

        private async Task DetectSmartAsync(Point pointer, int request)
        {
            bool foundControl = false;
            try
            {
                SmartSelectionCandidate candidate = await _smartSelection.DetectAsync(pointer.X, pointer.Y);
                if (_finished || request != _smartRequest || !_smartSelectionEnabled ||
                    _surface.HasSelection || _surface.IsCreatingSelection || _surface.IsSmartPendingClick)
                {
                    return;
                }

                if (candidate == null)
                {
                    _surface.ClearSmartCandidate();
                    return;
                }

                foundControl = candidate.Kind == SmartCandidateKind.Control;
                SetSmartCandidateLocal(candidate);
            }
            catch (Exception ex)
            {
                Log.Warn("智能选区检测失败: " + ex.Message);
            }
            finally
            {
                _smartDetecting = false;
                bool retry = !foundControl && _smartRetryCount < 2 &&
                    !_finished && request == _smartRequest &&
                    _smartSelectionEnabled && !_surface.HasSelection &&
                    !_surface.IsCreatingSelection && !_surface.IsSmartPendingClick;
                if (retry)
                    _smartRetryCount++;
                if ((_smartPendingRefine || retry) && _smartTimer != null &&
                    !_finished && request == _smartRequest && _smartSelectionEnabled &&
                    !_surface.HasSelection && !_surface.IsCreatingSelection &&
                    !_surface.IsSmartPendingClick)
                {
                    _smartTimer.Start();
                }
            }
        }

        private Point GetCursorScreen()
        {
            NativeMethods.POINT point;
            if (NativeMethods.GetCursorPos(out point))
                return new Point(point.X, point.Y);
            return new Point(_frame.Bounds.Left + _surface.MagnifierAnchor.X,
                _frame.Bounds.Top + _surface.MagnifierAnchor.Y);
        }

        private void UpdateMagnifier()
        {
            if (_finished || !_surface.HasSelection || _surface.IsCreatingSelection ||
                _toolbarRect.IsEmpty)
            {
                _magnifier.Hide();
                return;
            }

            // The sampler is part of the fixed toolbar chrome. Its position is
            // anchored to the toolbar, not to the mouse, so it never wanders.
            _magnifier.Update(_frame, _surface.MagnifierAnchor);
            if (_magnifier.Visibility != Visibility.Visible)
                return;

            _magnifier.Measure(new Size(_magnifier.Width, _magnifier.Height));
            Size size = _magnifier.DesiredSize;
            double overlayWidth = ActualWidth > 0 ? ActualWidth : Width;
            double overlayHeight = ActualHeight > 0 ? ActualHeight : Height;
            Rect placed = ComputeMagnifierRect(new Size(overlayWidth, overlayHeight), _toolbarRect, size);
            Canvas.SetLeft(_magnifier, placed.X);
            Canvas.SetTop(_magnifier, placed.Y);
        }

        private static Rect ComputeMagnifierRect(Size overlay, Rect toolbar, Size magnifier,
            double gap = 8, double edge = 8)
        {
            var candidates = new[]
            {
                new Rect(toolbar.Right + gap, toolbar.Top + (toolbar.Height - magnifier.Height) / 2,
                    magnifier.Width, magnifier.Height),
                new Rect(toolbar.Left - gap - magnifier.Width,
                    toolbar.Top + (toolbar.Height - magnifier.Height) / 2,
                    magnifier.Width, magnifier.Height),
                new Rect(toolbar.Right - magnifier.Width, toolbar.Top - gap - magnifier.Height,
                    magnifier.Width, magnifier.Height),
                new Rect(toolbar.Right - magnifier.Width, toolbar.Bottom + gap,
                    magnifier.Width, magnifier.Height)
            };
            foreach (Rect candidate in candidates)
            {
                Rect clamped = ClampRect(candidate, overlay, edge);
                if (!Intersects(clamped, toolbar))
                    return clamped;
            }
            return ClampRect(candidates[0], overlay, edge);
        }

        private static Rect ClampRect(Rect rect, Size overlay, double edge)
        {
            double x = Math.Max(edge, Math.Min(overlay.Width - rect.Width - edge, rect.X));
            double y = Math.Max(edge, Math.Min(overlay.Height - rect.Height - edge, rect.Y));
            return new Rect(Math.Round(x), Math.Round(y), Math.Round(rect.Width), Math.Round(rect.Height));
        }

        private static bool Intersects(Rect a, Rect b)
        {
            return !a.IsEmpty && !b.IsEmpty && a.IntersectsWith(b);
        }

        private Rect ToLogical(DrawingRectangle rect)
        {
            return new Rect(rect.X / _scale, rect.Y / _scale, rect.Width / _scale, rect.Height / _scale);
        }

        private Point ToLogical(Point physical)
        {
            return new Point(physical.X / _scale, physical.Y / _scale);
        }

        private void Cancel()
        {
            if (_finished)
                return;
            _finished = true;
            Hide();
            Close();
        }

        private void Finish(CaptureAction action)
        {
            if (_finished || !_surface.HasSelection)
                return;

            DrawingRectangle selection = _surface.Selection;
            DrawingBitmap cropped;
            try
            {
                cropped = ScreenCapture.Crop(_frame.Bitmap, selection);
            }
            catch (Exception ex)
            {
                Log.Error("裁剪截图失败", ex);
                MessageBox.Show(this, "裁剪截图失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BitmapSource source;
            DrawingBitmap resultImage;
            try
            {
                if (action == CaptureAction.LongCapture)
                {
                    source = ScreenCapture.ToBitmapSource(cropped);
                    source.Freeze();
                    resultImage = cropped;
                }
                else
                {
                    List<Annotation> annotations = _surface.Annotations.Select(a => a.Clone()).ToList();
                    source = AnnotationRenderer.Render(cropped, annotations);
                    source.Freeze();
                    resultImage = ImageUtil.ToBitmap(source);
                    cropped.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error("合成标注失败", ex);
                NotifyFailure("合成标注失败：" + ex.Message);
                cropped.Dispose();
                return;
            }

            var result = new CaptureResult
            {
                Image = resultImage,
                Source = source,
                Action = action,
                ScreenBounds = new DrawingRectangle(
                    _frame.Bounds.Left + selection.X,
                    _frame.Bounds.Top + selection.Y,
                    selection.Width,
                    selection.Height)
            };
            _finished = true;

            try
            {
                if (_completed != null)
                    _completed(result);
            }
            catch (Exception ex)
            {
                Log.Error("处理截图结果失败", ex);
            }

            Hide();
            Close();
        }

        private void NotifyFailure(string message)
        {
            try
            {
                MessageBox.Show(this, message, "FreePin", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                // Ignore UI failures in the overlay.
            }
        }
    }
}
