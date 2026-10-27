using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Annotations;
using FreePin.Native;
using FreePin.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = System.Windows.Point;

namespace FreePin.UI
{
    internal sealed class CaptureSurface : Canvas
    {
        private static readonly Color DimColor = Color.FromArgb(112, 16, 14, 12);
        private static readonly double[] HandlePoints = { 0, 0.5, 1 };
        private const double HandleSize = 7;
        private const double HandleHitTolerance = 8;
        private const double MinSelection = 2;

        private readonly ScreenFrame _frame;
        private readonly double _scale;
        private readonly List<Annotation> _annotations = new List<Annotation>();
        private readonly Stack<List<Annotation>> _undo = new Stack<List<Annotation>>();
        private readonly Stack<List<Annotation>> _redo = new Stack<List<Annotation>>();
        private readonly CaptureStyle _style = new CaptureStyle();

        private DrawingRectangle _selection;
        private CaptureTool _tool = CaptureTool.Select;
        private CaptureTool _toolBeforeEyedropper = CaptureTool.Pen;
        private SelectionHandle _handle = SelectionHandle.None;
        private Annotation _preview;
        private Annotation _selected;
        private Annotation _dragAnnotation;
        private List<Annotation> _pendingSnapshot;
        private TextBox _textBox;
        private Point _textPoint;
        private bool _creating;
        private bool _resizing;
        private bool _moving;
        private bool _drawing;
        private bool _draggingAnnotation;
        private bool _dragMoved;
        private bool _clearOnCreate;
        private Point _lastLogical;
        private Point _lastPhysical;
        private Point _dragStartLogical;
        private Point _dragStartPhysical;
        private DrawingRectangle _dragStartSelection;

        public CaptureSurface(ScreenFrame frame, double scale)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _scale = scale <= 0 ? 1 : scale;
            Background = Brushes.Transparent;
            ClipToBounds = true;
            Focusable = true;
            SnapsToDevicePixels = true;
            Cursor = Cursors.Cross;
            InputMethod.SetIsInputMethodEnabled(this, false);
        }

        public event EventHandler SelectionChanged;
        public event EventHandler AnnotationsChanged;
        public event EventHandler StyleChanged;
        public event EventHandler ToolChanged;
        public event EventHandler CursorMoved;

        public DrawingRectangle Selection
        {
            get { return _selection; }
        }

        public bool HasSelection
        {
            get { return _selection.Width >= MinSelection && _selection.Height >= MinSelection; }
        }

        public bool IsCreatingSelection
        {
            get { return _creating; }
        }

        public bool IsDrawingAnnotation
        {
            get { return _drawing || _draggingAnnotation; }
        }

        public bool IsAdjustingSelection
        {
            get { return _creating || _resizing || _moving; }
        }

        public bool IsTextEditing
        {
            get { return _textBox != null; }
        }

        public Point LastPhysicalPoint
        {
            get { return _lastPhysical; }
        }

        public Point MagnifierAnchor
        {
            get
            {
                if (_resizing || _handle != SelectionHandle.None)
                    return CaptureGeometry.HandlePoint(PhysicalRect(_selection), _handle);
                return _lastPhysical;
            }
        }

        public new CaptureStyle Style
        {
            get { return _style; }
        }

        public CaptureTool Tool
        {
            get { return _tool; }
            set
            {
                if (_tool == value)
                    return;
                if (_textBox != null)
                    CommitText();
                CancelPending();
                if (value == CaptureTool.Eyedropper && _tool != CaptureTool.Eyedropper)
                    _toolBeforeEyedropper = _tool;
                _tool = value;
                UpdateCursor();
                ToolChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
        }

        public IReadOnlyList<Annotation> Annotations
        {
            get { return _annotations; }
        }

        public bool CanUndo
        {
            get { return _undo.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redo.Count > 0; }
        }

        public void SetColor(Color color)
        {
            _style.Color = color;
            if (_selected != null)
                _selected.Color = color;
            StyleChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void SetThickness(double thickness)
        {
            _style.Thickness = Math.Max(1, Math.Min(40, thickness));
            if (_selected != null)
                _selected.Thickness = _style.Thickness;
            StyleChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void SetFontSize(double size)
        {
            _style.FontSize = Math.Max(10, Math.Min(96, size));
            var text = _selected as TextAnnotation;
            if (text != null)
                text.FontSize = _style.FontSize;
            StyleChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void SetRoundedRectangle(bool rounded)
        {
            _style.RoundedRectangle = rounded;
            var rect = _selected as RectangleAnnotation;
            if (rect != null)
                rect.CornerRadius = rounded ? _style.CornerRadius : 0;
            StyleChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void SetCornerRadius(double radius)
        {
            _style.CornerRadius = Math.Max(0, Math.Min(48, radius));
            var rect = _selected as RectangleAnnotation;
            if (rect != null && _style.RoundedRectangle)
                rect.CornerRadius = _style.CornerRadius;
            StyleChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void Undo()
        {
            if (_undo.Count == 0)
                return;
            if (_textBox != null)
                CommitText();
            _redo.Push(Snapshot());
            Restore(_undo.Pop());
            RaiseAnnotationsChanged();
        }

        public void Redo()
        {
            if (_redo.Count == 0)
                return;
            if (_textBox != null)
                CommitText();
            _undo.Push(Snapshot());
            Restore(_redo.Pop());
            RaiseAnnotationsChanged();
        }

        public void DeleteSelected()
        {
            if (_selected == null)
                return;
            PushSnapshot();
            _annotations.Remove(_selected);
            _selected = null;
            InvalidateVisual();
            RaiseAnnotationsChanged();
        }

        public bool CancelPending()
        {
            if (_textBox != null)
            {
                CancelText();
                return true;
            }
            if (_drawing || _preview != null)
            {
                _drawing = false;
                _preview = null;
                ReleaseMouseCapture();
                InvalidateVisual();
                return true;
            }
            if (_creating)
            {
                _creating = false;
                ReleaseMouseCapture();
                if (!HasSelection)
                    _selection = new DrawingRectangle();
                RaiseSelectionChanged();
                return true;
            }
            return false;
        }

        public void PickColor(Point physicalPoint)
        {
            var point = ClampPhysical(physicalPoint);
            int x = Math.Max(0, Math.Min(_frame.Bitmap.Width - 1, (int)Math.Round(point.X)));
            int y = Math.Max(0, Math.Min(_frame.Bitmap.Height - 1, (int)Math.Round(point.Y)));
            System.Drawing.Color picked = _frame.Bitmap.GetPixel(x, y);
            SetColor(Color.FromArgb(picked.A, picked.R, picked.G, picked.B));
            _tool = _toolBeforeEyedropper == CaptureTool.Eyedropper ? CaptureTool.Select : _toolBeforeEyedropper;
            UpdateCursor();
            ToolChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public bool HandleKey(Key key, ModifierKeys modifiers)
        {
            if (_textBox != null)
                return false;

            if (key == Key.Escape)
                return CancelPending();

            bool control = (modifiers & ModifierKeys.Control) != 0;
            if (control && key == Key.Z)
            {
                Undo();
                return true;
            }
            if (control && key == Key.Y)
            {
                Redo();
                return true;
            }
            if (key == Key.Delete || key == Key.Back)
            {
                if (_selected != null)
                {
                    DeleteSelected();
                    return true;
                }
                return false;
            }

            if (key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down)
            {
                double dx = key == Key.Left ? -1 : key == Key.Right ? 1 : 0;
                double dy = key == Key.Up ? -1 : key == Key.Down ? 1 : 0;
                if (_selected != null && _tool == CaptureTool.Select)
                {
                    PushSnapshot();
                    _selected.Move(dx, dy);
                    RaiseAnnotationsChanged();
                    InvalidateVisual();
                    return true;
                }
                if (!HasSelection)
                    return false;

                if (_resizing)
                {
                    NudgeActiveHandle(dx, dy);
                    return true;
                }

                bool shift = (modifiers & ModifierKeys.Shift) != 0;
                if (shift || control)
                {
                    NudgeSelectionEdge(key, shift);
                    return true;
                }

                _selection = ToDrawing(CaptureGeometry.Move(PhysicalRect(_selection), dx, dy, FrameBounds()));
                RaiseSelectionChanged();
                InvalidateVisual();
                return true;
            }
            return false;
        }

        private void NudgeActiveHandle(double dx, double dy)
        {
            Rect moved = CaptureGeometry.Resize(PhysicalRect(_selection), _handle, dx, dy, FrameBounds(), MinSelection);
            _selection = ToDrawing(moved);
            _dragStartSelection = _selection;
            _dragStartPhysical = _lastPhysical;
            RaiseSelectionChanged();
            InvalidateVisual();
        }

        private void NudgeSelectionEdge(Key key, bool shrink)
        {
            double amount = shrink ? 1 : -1;
            SelectionHandle handle;
            double dx = 0;
            double dy = 0;
            switch (key)
            {
                case Key.Left:
                    handle = SelectionHandle.West;
                    dx = amount;
                    break;
                case Key.Right:
                    handle = SelectionHandle.East;
                    dx = -amount;
                    break;
                case Key.Up:
                    handle = SelectionHandle.North;
                    dy = amount;
                    break;
                default:
                    handle = SelectionHandle.South;
                    dy = -amount;
                    break;
            }
            _selection = ToDrawing(CaptureGeometry.Resize(PhysicalRect(_selection), handle, dx, dy,
                FrameBounds(), MinSelection));
            RaiseSelectionChanged();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var full = new Rect(0, 0, ActualWidth, ActualHeight);
            if (full.Width <= 0 || full.Height <= 0)
                return;

            dc.DrawImage(_frame.ImageSource, full);
            dc.DrawRectangle(new SolidColorBrush(DimColor), null, full);

            if (HasSelection)
            {
                Rect selected = ToLogical(_selection);
                dc.PushClip(new RectangleGeometry(selected));
                dc.DrawImage(_frame.ImageSource, full);
                dc.Pop();
            }

            DrawAnnotations(dc);

            if (HasSelection)
            {
                Rect selected = ToLogical(_selection);
                var white = new SolidColorBrush(Colors.White);
                white.Freeze();
                var border = new Pen(white, 1.5);
                border.Freeze();
                dc.DrawRectangle(null, border, selected);
                DrawHandles(dc, selected);
            }
            else
            {
                DrawHint(dc, full);
            }

            if (_creating || _resizing || _moving || _drawing)
            {
                var line = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1)
                {
                    DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
                };
                line.Freeze();
                dc.DrawLine(line, new Point(_lastLogical.X, 0), new Point(_lastLogical.X, ActualHeight));
                dc.DrawLine(line, new Point(0, _lastLogical.Y), new Point(ActualWidth, _lastLogical.Y));
            }
        }

        private void DrawAnnotations(DrawingContext dc)
        {
            if (!HasSelection)
                return;

            Rect selected = ToLogical(_selection);
            dc.PushClip(new RectangleGeometry(selected));
            dc.PushTransform(new TranslateTransform(selected.X, selected.Y));
            dc.PushTransform(new ScaleTransform(1 / _scale, 1 / _scale));

            foreach (Annotation annotation in _annotations)
            {
                annotation.Draw(dc, _frame.ImageSource);
                if (annotation == _selected)
                {
                    var bounds = annotation.Bounds;
                    if (!bounds.IsEmpty)
                    {
                        var pen = new Pen(new SolidColorBrush(Color.FromArgb(210, 180, 78, 50)), 1.5)
                        {
                            DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
                        };
                        pen.Freeze();
                        bounds.Inflate(4, 4);
                        dc.DrawRectangle(null, pen, bounds);
                    }
                }
            }

            if (_preview != null)
                _preview.Draw(dc, _frame.ImageSource);

            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        private void DrawHint(DrawingContext dc, Rect full)
        {
            var text = new FormattedText("拖动鼠标框选区域 · 框选后可直接标注 · Enter 复制 · Esc 取消",
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal,
                    FontWeights.Normal, FontStretches.Normal), 15,
                new SolidColorBrush(Color.FromRgb(255, 249, 240)), 1.0);
            double width = text.WidthIncludingTrailingWhitespace + 30;
            double height = text.Height + 18;
            var box = new Rect((full.Width - width) / 2, 26, width, height);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(224, 47, 42, 37)), null, box, 7, 7);
            dc.DrawText(text, new Point(box.X + 15, box.Y + 9));
        }

        private static void DrawHandles(DrawingContext dc, Rect rect)
        {
            var fill = new SolidColorBrush(Color.FromRgb(255, 251, 243));
            fill.Freeze();
            var pen = new Pen(new SolidColorBrush(UiTheme.Accent), 1.4);
            pen.Freeze();
            foreach (double fy in HandlePoints)
            {
                foreach (double fx in HandlePoints)
                {
                    if (fx == 0.5 && fy == 0.5)
                        continue;
                    var point = new Point(rect.Left + rect.Width * fx, rect.Top + rect.Height * fy);
                    dc.DrawRectangle(fill, pen, new Rect(point.X - HandleSize / 2, point.Y - HandleSize / 2,
                        HandleSize, HandleSize));
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Keyboard.Focus(this);
            _lastPhysical = PointerPhysical();
            _lastLogical = ToLogical(_lastPhysical);

            if (_textBox != null)
            {
                CommitText();
                e.Handled = true;
                return;
            }

            if (!HasSelection)
            {
                BeginCreate();
                e.Handled = true;
                return;
            }

            Rect selected = PhysicalRect(_selection);
            SelectionHandle handle = CaptureGeometry.HitTest(selected, _lastPhysical, HandleHitTolerance * _scale);
            if (handle != SelectionHandle.None)
            {
                BeginResize(handle);
                e.Handled = true;
                return;
            }

            if (!selected.Contains(_lastPhysical))
            {
                if (_tool == CaptureTool.Select)
                    BeginCreate();
                e.Handled = true;
                return;
            }

            if (_tool == CaptureTool.Select)
            {
                Annotation hit = HitAnnotation(ToRelativePhysical(_lastPhysical));
                if (hit != null)
                {
                    Select(hit);
                    _dragAnnotation = hit;
                    _draggingAnnotation = true;
                    _dragMoved = false;
                    _pendingSnapshot = Snapshot();
                    _dragStartPhysical = _lastPhysical;
                    CaptureMouse();
                    InvalidateVisual();
                }
                else
                {
                    _selected = null;
                    BeginMove();
                }
            }
            else if (_tool == CaptureTool.Text)
            {
                BeginText(_lastPhysical);
            }
            else if (_tool == CaptureTool.Eyedropper)
            {
                PickColor(_lastPhysical);
            }
            else
            {
                BeginDrawing();
            }
            e.Handled = true;
        }

        private void BeginCreate()
        {
            CancelPending();
            _creating = true;
            _selection = new DrawingRectangle();
            _dragStartLogical = _lastLogical;
            _dragStartPhysical = _lastPhysical;
            _clearOnCreate = true;
            _selected = null;
            CaptureMouse();
            Cursor = Cursors.Cross;
            RaiseSelectionChanged();
            InvalidateVisual();
        }

        private void BeginResize(SelectionHandle handle)
        {
            _resizing = true;
            _handle = handle;
            _dragStartSelection = _selection;
            _dragStartPhysical = _lastPhysical;
            CaptureMouse();
            UpdateCursor();
            InvalidateVisual();
        }

        private void BeginMove()
        {
            _moving = true;
            _dragStartSelection = _selection;
            _dragStartPhysical = _lastPhysical;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            InvalidateVisual();
        }

        private void BeginDrawing()
        {
            if (!HasSelection)
                return;
            _pendingSnapshot = Snapshot();
            _dragStartPhysical = ClampToSelection(_lastPhysical);
            _preview = CreateAnnotation(_tool, ToRelativePhysical(_dragStartPhysical));
            _drawing = _preview != null;
            if (_drawing)
            {
                CaptureMouse();
                InvalidateVisual();
            }
        }

        private void BeginText(Point physicalPoint)
        {
            if (!HasSelection)
                return;
            CommitText();
            Point relative = ToRelativePhysical(ClampToSelection(physicalPoint));
            _pendingSnapshot = Snapshot();
            _textPoint = relative;

            double logicalLeft = (_selection.X + relative.X) / _scale;
            double logicalTop = (_selection.Y + relative.Y) / _scale;
            double available = Math.Max(80, (_selection.Width - relative.X) / _scale - 4);
            double width = Math.Min(320, available);
            logicalLeft = Math.Min(logicalLeft, ActualWidth - width - 4);

            _textBox = new TextBox
            {
                Width = width,
                FontFamily = UiTheme.UiFont,
                FontSize = Math.Max(11, _style.FontSize / _scale),
                Foreground = UiTheme.Brush(_style.Color),
                Background = UiTheme.Brush(Color.FromArgb(245, 255, 251, 243)),
                BorderBrush = UiTheme.Brush(UiTheme.Accent),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                AcceptsReturn = false,
                TextWrapping = TextWrapping.NoWrap
            };
            InputMethod.SetIsInputMethodEnabled(_textBox, true);
            _textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown;
            _textBox.LostKeyboardFocus += (s, e) => CommitText();
            SetLeft(_textBox, logicalLeft);
            SetTop(_textBox, logicalTop);
            Children.Add(_textBox);
            _textBox.Focus();
            Keyboard.Focus(_textBox);
        }

        private void TextBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                CommitText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelText();
                e.Handled = true;
            }
        }

        private void CommitText()
        {
            if (_textBox == null)
                return;
            string content = _textBox.Text == null ? string.Empty : _textBox.Text.Trim();
            RemoveTextBox();
            if (content.Length == 0)
            {
                _pendingSnapshot = null;
                Focus();
                return;
            }
            var annotation = new TextAnnotation
            {
                Position = _textPoint,
                Content = content,
                FontSize = _style.FontSize,
                Color = _style.Color,
                Thickness = 1
            };
            _annotations.Add(annotation);
            Select(annotation);
            _undo.Push(_pendingSnapshot ?? new List<Annotation>());
            _pendingSnapshot = null;
            _redo.Clear();
            Focus();
            RaiseAnnotationsChanged();
            InvalidateVisual();
        }

        private void CancelText()
        {
            RemoveTextBox();
            _pendingSnapshot = null;
            Focus();
            InvalidateVisual();
        }

        private void RemoveTextBox()
        {
            if (_textBox == null)
                return;
            _textBox.PreviewKeyDown -= TextBoxOnPreviewKeyDown;
            Children.Remove(_textBox);
            _textBox = null;
        }

        private Annotation CreateAnnotation(CaptureTool tool, Point start)
        {
            switch (tool)
            {
                case CaptureTool.Pen:
                    return new FreehandAnnotation
                    {
                        Points = new List<Point> { start },
                        Color = _style.Color,
                        Thickness = _style.Thickness
                    };
                case CaptureTool.Line:
                    return new LineAnnotation
                    {
                        Start = start,
                        End = start,
                        Color = _style.Color,
                        Thickness = _style.Thickness
                    };
                case CaptureTool.Arrow:
                    return new ArrowAnnotation
                    {
                        Start = start,
                        End = start,
                        Color = _style.Color,
                        Thickness = _style.Thickness
                    };
                case CaptureTool.Rectangle:
                    return new RectangleAnnotation
                    {
                        Area = new Rect(start, start),
                        CornerRadius = _style.RoundedRectangle ? _style.CornerRadius : 0,
                        Color = _style.Color,
                        Thickness = _style.Thickness
                    };
                default:
                    return null;
            }
        }

        private void UpdatePreview(Point physicalPoint)
        {
            if (!HasSelection || _drawing == false || _preview == null)
                return;
            Point end = ToRelativePhysical(ClampToSelection(physicalPoint));
            Point start = _dragStartPhysical;
            Point relativeStart = ToRelativePhysical(start);

            var freehand = _preview as FreehandAnnotation;
            if (freehand != null)
            {
                if (freehand.Points.Count == 0 || (end - freehand.Points[freehand.Points.Count - 1]).Length >= 1.2)
                    freehand.Points.Add(end);
                InvalidateVisual();
                return;
            }

            var line = _preview as LineAnnotation;
            if (line != null)
            {
                line.Start = relativeStart;
                line.End = end;
            }
            var arrow = _preview as ArrowAnnotation;
            if (arrow != null)
            {
                arrow.Start = relativeStart;
                arrow.End = end;
            }
            var rect = _preview as RectangleAnnotation;
            if (rect != null)
                rect.Area = Normalize(new Rect(relativeStart, end));
            InvalidateVisual();
        }

        private void FinishDrawing()
        {
            if (!_drawing || _preview == null)
                return;
            bool valid = true;
            var line = _preview as LineAnnotation;
            if (line != null)
                valid = (line.End - line.Start).Length >= 2;
            var arrow = _preview as ArrowAnnotation;
            if (arrow != null)
                valid = (arrow.End - arrow.Start).Length >= 2;
            var rect = _preview as RectangleAnnotation;
            if (rect != null)
                valid = rect.Area.Width >= 2 && rect.Area.Height >= 2;
            var freehand = _preview as FreehandAnnotation;
            if (freehand != null)
                valid = freehand.Points.Count >= 2;

            if (valid)
            {
                _annotations.Add(_preview);
                _undo.Push(_pendingSnapshot ?? new List<Annotation>());
                _redo.Clear();
                Select(_preview);
                RaiseAnnotationsChanged();
            }
            _preview = null;
            _drawing = false;
            _pendingSnapshot = null;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point physical = PointerPhysical();
            Point previousPhysical = _lastPhysical;
            _lastLogical = ToLogical(physical);
            _lastPhysical = physical;

            if (_creating)
            {
                UpdateCreatingSelection();
            }
            else if (_resizing)
            {
                double dx = physical.X - _dragStartPhysical.X;
                double dy = physical.Y - _dragStartPhysical.Y;
                _selection = ToDrawing(CaptureGeometry.Resize(PhysicalRect(_dragStartSelection), _handle,
                    dx, dy, FrameBounds(), MinSelection));
                RaiseSelectionChanged();
                InvalidateVisual();
            }
            else if (_moving)
            {
                double dx = physical.X - _dragStartPhysical.X;
                double dy = physical.Y - _dragStartPhysical.Y;
                _selection = ToDrawing(CaptureGeometry.Move(PhysicalRect(_dragStartSelection), dx, dy,
                    FrameBounds(), MinSelection));
                RaiseSelectionChanged();
                InvalidateVisual();
            }
            else if (_drawing)
            {
                UpdatePreview(physical);
            }
            else if (_draggingAnnotation && _dragAnnotation != null)
            {
                _dragAnnotation.Move(physical.X - previousPhysical.X, physical.Y - previousPhysical.Y);
                _dragMoved = true;
                InvalidateVisual();
            }
            else
            {
                UpdateCursor();
            }

            CursorMoved?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        private void UpdateCreatingSelection()
        {
            double dx = _lastPhysical.X - _dragStartPhysical.X;
            double dy = _lastPhysical.Y - _dragStartPhysical.Y;
            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
                return;

            if (_clearOnCreate)
            {
                _clearOnCreate = false;
                _annotations.Clear();
                _undo.Clear();
                _redo.Clear();
                _selected = null;
                RaiseAnnotationsChanged();
            }

            Point start = ClampPhysical(_dragStartPhysical);
            Point end = ClampPhysical(_lastPhysical);
            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);
            double right = Math.Max(start.X, end.X);
            double bottom = Math.Max(start.Y, end.Y);
            _selection = new DrawingRectangle(
                (int)Math.Round(left), (int)Math.Round(top),
                (int)Math.Round(right - left), (int)Math.Round(bottom - top));
            RaiseSelectionChanged();
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_creating)
            {
                UpdateCreatingSelection();
                _creating = false;
                ReleaseMouseCapture();
                if (!HasSelection)
                {
                    _selection = new DrawingRectangle();
                }
                RaiseSelectionChanged();
                UpdateCursor();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            if (_resizing || _moving)
            {
                _resizing = false;
                _moving = false;
                _handle = SelectionHandle.None;
                ReleaseMouseCapture();
                UpdateCursor();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            if (_drawing)
            {
                UpdatePreview(_lastPhysical);
                FinishDrawing();
                ReleaseMouseCapture();
                UpdateCursor();
                e.Handled = true;
                return;
            }
            if (_draggingAnnotation)
            {
                if (_dragMoved && _dragAnnotation != null)
                {
                    _undo.Push(_pendingSnapshot ?? new List<Annotation>());
                    _redo.Clear();
                    RaiseAnnotationsChanged();
                }
                _draggingAnnotation = false;
                _dragAnnotation = null;
                _dragMoved = false;
                _pendingSnapshot = null;
                ReleaseMouseCapture();
                UpdateCursor();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double delta = e.Delta > 0 ? 1 : -1;
            if (_tool == CaptureTool.Text || _selected is TextAnnotation)
                SetFontSize(_style.FontSize + delta * 2);
            else
                SetThickness(_style.Thickness + delta);
            UpdateThicknessCursor();
            e.Handled = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_resizing && !_moving && !_creating && !_drawing)
                Cursor = _tool == CaptureTool.Select ? Cursors.Arrow : Cursors.Cross;
        }

        private void Select(Annotation annotation)
        {
            foreach (Annotation item in _annotations)
                item.IsSelected = false;
            _selected = annotation;
            if (_selected != null)
                _selected.IsSelected = true;
            InvalidateVisual();
        }

        private Annotation HitAnnotation(Point relativePoint)
        {
            for (int i = _annotations.Count - 1; i >= 0; i--)
            {
                if (_annotations[i].HitTest(relativePoint, 5))
                    return _annotations[i];
            }
            return null;
        }

        private void RaiseSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseAnnotationsChanged()
        {
            AnnotationsChanged?.Invoke(this, EventArgs.Empty);
        }

        private List<Annotation> Snapshot()
        {
            return _annotations.Select(a => a.Clone()).ToList();
        }

        private void PushSnapshot()
        {
            _undo.Push(Snapshot());
            _redo.Clear();
        }

        private void Restore(List<Annotation> snapshot)
        {
            _annotations.Clear();
            if (snapshot != null)
                _annotations.AddRange(snapshot);
            _selected = null;
            InvalidateVisual();
        }

        private void UpdateThicknessCursor()
        {
            // Wheel updates are intentionally silent; the toolbar reads the shared style on its own.
        }

        private void UpdateCursor()
        {
            if (_textBox != null)
            {
                Cursor = Cursors.IBeam;
                return;
            }
            if (_tool == CaptureTool.Eyedropper || _creating || !HasSelection)
            {
                Cursor = Cursors.Cross;
                return;
            }
            if (_resizing)
            {
                Cursor = CursorForHandle(_handle);
                return;
            }
            Rect selected = PhysicalRect(_selection);
            SelectionHandle handle = CaptureGeometry.HitTest(selected, _lastPhysical, HandleHitTolerance * _scale);
            if (handle != SelectionHandle.None)
            {
                Cursor = CursorForHandle(handle);
                return;
            }
            if (_tool == CaptureTool.Select && selected.Contains(_lastPhysical))
                Cursor = Cursors.SizeAll;
            else
                Cursor = _tool == CaptureTool.Select ? Cursors.Arrow : Cursors.Cross;
        }

        private static Cursor CursorForHandle(SelectionHandle handle)
        {
            switch (handle)
            {
                case SelectionHandle.NorthWest:
                case SelectionHandle.SouthEast:
                    return Cursors.SizeNWSE;
                case SelectionHandle.NorthEast:
                case SelectionHandle.SouthWest:
                    return Cursors.SizeNESW;
                case SelectionHandle.North:
                case SelectionHandle.South:
                    return Cursors.SizeNS;
                case SelectionHandle.West:
                case SelectionHandle.East:
                    return Cursors.SizeWE;
                default:
                    return Cursors.Arrow;
            }
        }

        private Point PointerPhysical()
        {
            NativeMethods.POINT p;
            if (!NativeMethods.GetCursorPos(out p))
                return _lastPhysical;
            return new Point(p.X - _frame.Bounds.Left, p.Y - _frame.Bounds.Top);
        }

        private static Rect PhysicalRect(DrawingRectangle r)
        {
            return new Rect(r.X, r.Y, r.Width, r.Height);
        }

        private Point ClampPhysical(Point point)
        {
            return new Point(
                Math.Max(0, Math.Min(_frame.Bounds.Width, point.X)),
                Math.Max(0, Math.Min(_frame.Bounds.Height, point.Y)));
        }

        private Point ClampToSelection(Point physical)
        {
            if (!HasSelection)
                return ClampPhysical(physical);
            double x = Math.Max(_selection.Left, Math.Min(_selection.Right, physical.X));
            double y = Math.Max(_selection.Top, Math.Min(_selection.Bottom, physical.Y));
            return new Point(x, y);
        }

        private Point ToPhysical(Point logical)
        {
            return new Point(logical.X * _scale, logical.Y * _scale);
        }

        private Point ToLogical(Point physical)
        {
            return new Point(physical.X / _scale, physical.Y / _scale);
        }

        private Rect ToLogical(DrawingRectangle rect)
        {
            return new Rect(rect.X / _scale, rect.Y / _scale, rect.Width / _scale, rect.Height / _scale);
        }

        private DrawingRectangle ToDrawing(Rect rect)
        {
            int left = (int)Math.Round(rect.Left);
            int top = (int)Math.Round(rect.Top);
            int right = (int)Math.Round(rect.Right);
            int bottom = (int)Math.Round(rect.Bottom);
            return new DrawingRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        private Rect FrameBounds()
        {
            return new Rect(0, 0, _frame.Bounds.Width, _frame.Bounds.Height);
        }

        private Point ToRelativePhysical(Point point)
        {
            return new Point(point.X - _selection.X, point.Y - _selection.Y);
        }

        private static Rect Normalize(Rect rect)
        {
            double x = Math.Min(rect.Left, rect.Right);
            double y = Math.Min(rect.Top, rect.Bottom);
            return new Rect(x, y, Math.Abs(rect.Width), Math.Abs(rect.Height));
        }
    }
}
