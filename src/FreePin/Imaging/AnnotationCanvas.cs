using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Annotations;

namespace FreePin.Imaging
{
    public enum EditorTool
    {
        Select,
        Rectangle,
        Ellipse,
        Arrow,
        Line,
        Freehand,
        Text,
        Mosaic
    }

    public sealed class AnnotationCanvas : Canvas
    {
        private readonly List<Annotation> _annotations = new List<Annotation>();
        private readonly Stack<List<Annotation>> _undo = new Stack<List<Annotation>>();
        private readonly Stack<List<Annotation>> _redo = new Stack<List<Annotation>>();
        private Annotation _selected;
        private Annotation _preview;
        private Point _startPoint;
        private Point _lastPoint;
        private bool _drawing;
        private Annotation _dragAnnotation;
        private bool _dragMoved;
        private List<Annotation> _pendingSnapshot;
        private TextBox _textBox;
        private Point _textPosition;
        private bool _textCommitted;
        private EditorTool _tool = EditorTool.Select;
        private Color _strokeColor = Colors.Red;

        public AnnotationCanvas()
        {
            Background = Brushes.Transparent;
            ClipToBounds = true;
            Focusable = true;
            SnapsToDevicePixels = true;
        }

        public BitmapSource ImageSource { get; private set; }
        public System.Drawing.Bitmap BaseBitmap { get; private set; }

        public IReadOnlyList<Annotation> Annotations
        {
            get { return _annotations; }
        }

        public EditorTool Tool
        {
            get { return _tool; }
            set
            {
                if (_tool == value)
                    return;
                CancelPending();
                _tool = value;
                Cursor = value == EditorTool.Select ? Cursors.Arrow : Cursors.Cross;
            }
        }

        public Color StrokeColor
        {
            get { return _strokeColor; }
            set
            {
                _strokeColor = value;
                if (_selected != null)
                {
                    _selected.Color = value;
                    InvalidateVisual();
                }
            }
        }

        public double StrokeThickness
        {
            get { return _strokeThickness; }
            set
            {
                _strokeThickness = Math.Max(1, value);
                if (_selected != null)
                {
                    _selected.Thickness = _strokeThickness;
                    InvalidateVisual();
                }
            }
        }

        private double _strokeThickness = 3;

        public bool CanUndo
        {
            get { return _undo.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redo.Count > 0; }
        }

        public Annotation SelectedAnnotation
        {
            get { return _selected; }
        }

        public event EventHandler HistoryChanged;

        public void SetImage(BitmapSource source, System.Drawing.Bitmap baseBitmap)
        {
            ImageSource = source;
            BaseBitmap = baseBitmap;
            Width = source == null ? 0 : source.PixelWidth;
            Height = source == null ? 0 : source.PixelHeight;
            InvalidateVisual();
        }

        public void AddExternal(Annotation annotation)
        {
            if (annotation == null)
                return;
            PushSnapshot();
            _annotations.Add(annotation);
            Select(annotation);
            InvalidateVisual();
        }

        public void DeleteSelected()
        {
            if (_selected == null)
                return;
            PushSnapshot();
            _annotations.Remove(_selected);
            _selected = null;
            InvalidateVisual();
            RaiseHistoryChanged();
        }

        public void Undo()
        {
            if (_undo.Count == 0)
                return;
            _redo.Push(Snapshot());
            Restore(_undo.Pop());
            RaiseHistoryChanged();
        }

        public void Redo()
        {
            if (_redo.Count == 0)
                return;
            _undo.Push(Snapshot());
            Restore(_redo.Pop());
            RaiseHistoryChanged();
        }

        public BitmapSource RenderFlattened()
        {
            return AnnotationRenderer.Render(BaseBitmap, _annotations);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ImageSource != null)
            {
                dc.DrawImage(ImageSource, new Rect(0, 0, ImageSource.PixelWidth, ImageSource.PixelHeight));
            }
            else
            {
                dc.DrawRectangle(Brushes.DimGray, null, new Rect(0, 0, ActualWidth, ActualHeight));
            }

            foreach (Annotation annotation in _annotations)
            {
                annotation.Draw(dc, ImageSource);
                if (annotation.IsSelected)
                    DrawSelection(dc, annotation.Bounds);
            }

            if (_preview != null)
                _preview.Draw(dc, ImageSource);
        }

        private static void DrawSelection(DrawingContext dc, Rect bounds)
        {
            if (bounds.IsEmpty)
                return;
            var brush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
            brush.Freeze();
            var pen = new Pen(brush, 1.0)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
            };
            pen.Freeze();
            Rect rect = bounds;
            rect.Inflate(5, 5);
            dc.DrawRectangle(null, pen, rect);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton != MouseButton.Left)
                return;

            Focus();
            Point point = e.GetPosition(this);
            if (_textBox != null)
            {
                CommitText();
                return;
            }

            if (Tool == EditorTool.Select)
            {
                Annotation hit = HitTestAnnotation(point);
                if (hit != null)
                {
                    Select(hit);
                    _pendingSnapshot = Snapshot();
                    _dragAnnotation = hit;
                    _dragMoved = false;
                    _lastPoint = point;
                    CaptureMouse();
                }
                else
                {
                    Select(null);
                }
                e.Handled = true;
                return;
            }

            if (Tool == EditorTool.Text)
            {
                BeginText(point);
                e.Handled = true;
                return;
            }

            _startPoint = point;
            _lastPoint = point;
            _pendingSnapshot = Snapshot();
            _preview = CreateAnnotation(Tool, point);
            _drawing = true;
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point point = e.GetPosition(this);

            if (_dragAnnotation != null)
            {
                double dx = point.X - _lastPoint.X;
                double dy = point.Y - _lastPoint.Y;
                if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                {
                    _dragAnnotation.Move(dx, dy);
                    _dragMoved = true;
                    _lastPoint = point;
                    InvalidateVisual();
                }
                return;
            }

            if (!_drawing || _preview == null)
                return;

            point.X = Math.Max(0, Math.Min(Width, point.X));
            point.Y = Math.Max(0, Math.Min(Height, point.Y));
            UpdatePreview(point);
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_dragAnnotation != null)
            {
                if (_dragMoved && _pendingSnapshot != null)
                    PushSnapshot(_pendingSnapshot);
                _dragAnnotation = null;
                _pendingSnapshot = null;
                ReleaseMouseCapture();
                RaiseHistoryChanged();
                return;
            }

            if (!_drawing)
                return;

            _drawing = false;
            ReleaseMouseCapture();
            if (_preview != null && IsPreviewValid(_preview))
            {
                if (_pendingSnapshot != null)
                    PushSnapshot(_pendingSnapshot);
                _annotations.Add(_preview);
                Select(_preview);
                RaiseHistoryChanged();
            }

            _preview = null;
            _pendingSnapshot = null;
            InvalidateVisual();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_textBox != null)
                return;

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                DeleteSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_drawing)
                {
                    _drawing = false;
                    _preview = null;
                    _pendingSnapshot = null;
                    ReleaseMouseCapture();
                    InvalidateVisual();
                }
                else
                {
                    Select(null);
                }
                e.Handled = true;
                return;
            }

            if (_selected != null && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
            {
                PushSnapshot();
                double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
                double dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
                double dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
                _selected.Move(dx, dy);
                InvalidateVisual();
                RaiseHistoryChanged();
                e.Handled = true;
            }
        }

        private void BeginText(Point point)
        {
            if (_textBox != null)
                CommitText();

            _textPosition = point;
            _textCommitted = false;
            _textBox = new TextBox
            {
                Width = 320,
                MinHeight = 34,
                FontSize = 22,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(_strokeColor),
                Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                AcceptsReturn = false,
                AcceptsTab = false
            };
            Canvas.SetLeft(_textBox, point.X);
            Canvas.SetTop(_textBox, point.Y);
            Children.Add(_textBox);
            _textBox.KeyDown += TextBoxOnKeyDown;
            _textBox.LostFocus += TextBoxOnLostFocus;
            _textBox.Focus();
            Keyboard.Focus(_textBox);
        }

        private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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

        private void TextBoxOnLostFocus(object sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void CommitText()
        {
            if (_textBox == null || _textCommitted)
                return;
            _textCommitted = true;
            string content = _textBox.Text.Trim();
            RemoveTextBox();
            if (content.Length > 0)
            {
                var annotation = new TextAnnotation
                {
                    Position = _textPosition,
                    Content = content,
                    FontSize = 22,
                    Color = _strokeColor,
                    Thickness = 1
                };
                PushSnapshot();
                _annotations.Add(annotation);
                Select(annotation);
                InvalidateVisual();
                RaiseHistoryChanged();
            }
            Focus();
        }

        private void CancelText()
        {
            if (_textBox == null)
                return;
            _textCommitted = true;
            RemoveTextBox();
            Focus();
        }

        private void RemoveTextBox()
        {
            if (_textBox == null)
                return;
            _textBox.KeyDown -= TextBoxOnKeyDown;
            _textBox.LostFocus -= TextBoxOnLostFocus;
            Children.Remove(_textBox);
            _textBox = null;
        }

        private void CancelPending()
        {
            if (_textBox != null)
                CancelText();
            if (_drawing)
            {
                _drawing = false;
                _preview = null;
                _pendingSnapshot = null;
                ReleaseMouseCapture();
            }
        }

        private Annotation CreateAnnotation(EditorTool tool, Point start)
        {
            switch (tool)
            {
                case EditorTool.Rectangle:
                    return new RectangleAnnotation { Area = new Rect(start, start), Color = _strokeColor, Thickness = _strokeThickness };
                case EditorTool.Ellipse:
                    return new EllipseAnnotation { Area = new Rect(start, start), Color = _strokeColor, Thickness = _strokeThickness };
                case EditorTool.Arrow:
                    return new ArrowAnnotation { Start = start, End = start, Color = _strokeColor, Thickness = _strokeThickness };
                case EditorTool.Line:
                    return new LineAnnotation { Start = start, End = start, Color = _strokeColor, Thickness = _strokeThickness };
                case EditorTool.Freehand:
                    return new FreehandAnnotation
                    {
                        Points = new List<Point> { start },
                        Color = _strokeColor,
                        Thickness = _strokeThickness
                    };
                case EditorTool.Mosaic:
                    return new MosaicAnnotation { Area = new Rect(start, start), Color = _strokeColor, Thickness = _strokeThickness };
                default:
                    return null;
            }
        }

        private void UpdatePreview(Point point)
        {
            var freehand = _preview as FreehandAnnotation;
            if (freehand != null)
            {
                if ((point - freehand.Points[freehand.Points.Count - 1]).Length >= 1.2)
                    freehand.Points.Add(point);
                return;
            }

            var rectangle = _preview as RectangleAnnotation;
            if (rectangle != null)
            {
                rectangle.Area = Normalize(_startPoint, point);
                return;
            }
            var ellipse = _preview as EllipseAnnotation;
            if (ellipse != null)
            {
                ellipse.Area = Normalize(_startPoint, point);
                return;
            }
            var arrow = _preview as ArrowAnnotation;
            if (arrow != null)
            {
                arrow.End = point;
                return;
            }
            var line = _preview as LineAnnotation;
            if (line != null)
            {
                line.End = point;
                return;
            }
            var mosaic = _preview as MosaicAnnotation;
            if (mosaic != null)
            {
                mosaic.Area = Normalize(_startPoint, point);
            }
        }

        private static Rect Normalize(Point a, Point b)
        {
            return new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }

        private static bool IsPreviewValid(Annotation preview)
        {
            var rectangle = preview as RectangleAnnotation;
            if (rectangle != null)
                return rectangle.Area.Width >= 3 && rectangle.Area.Height >= 3;
            var ellipse = preview as EllipseAnnotation;
            if (ellipse != null)
                return ellipse.Area.Width >= 3 && ellipse.Area.Height >= 3;
            var mosaic = preview as MosaicAnnotation;
            if (mosaic != null)
                return mosaic.Area.Width >= 4 && mosaic.Area.Height >= 4;
            var line = preview as LineAnnotation;
            if (line != null)
                return (line.End - line.Start).Length >= 3;
            var arrow = preview as ArrowAnnotation;
            if (arrow != null)
                return (arrow.End - arrow.Start).Length >= 3;
            var freehand = preview as FreehandAnnotation;
            if (freehand != null)
                return freehand.Points.Count >= 2;
            return true;
        }

        private Annotation HitTestAnnotation(Point point)
        {
            for (int i = _annotations.Count - 1; i >= 0; i--)
            {
                if (_annotations[i].HitTest(point, 3))
                    return _annotations[i];
            }
            return null;
        }

        private void Select(Annotation annotation)
        {
            foreach (Annotation item in _annotations)
                item.IsSelected = ReferenceEquals(item, annotation);
            _selected = annotation;
            InvalidateVisual();
        }

        private List<Annotation> Snapshot()
        {
            return _annotations.Select(a => a.Clone()).ToList();
        }

        private void PushSnapshot()
        {
            PushSnapshot(Snapshot());
        }

        private void PushSnapshot(List<Annotation> snapshot)
        {
            if (snapshot == null)
                return;
            _undo.Push(snapshot);
            _redo.Clear();
            while (_undo.Count > 60)
            {
                // Drop oldest entry; the stack enumerates newest first.
                List<Annotation>[] items = _undo.ToArray();
                _undo.Clear();
                for (int i = items.Length - 2; i >= 0; i--)
                    _undo.Push(items[i]);
            }
            RaiseHistoryChanged();
        }

        private void Restore(List<Annotation> snapshot)
        {
            _annotations.Clear();
            if (snapshot != null)
            {
                foreach (Annotation annotation in snapshot)
                {
                    annotation.IsSelected = false;
                    _annotations.Add(annotation);
                }
            }
            _selected = null;
            InvalidateVisual();
        }

        private void RaiseHistoryChanged()
        {
            EventHandler handler = HistoryChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
