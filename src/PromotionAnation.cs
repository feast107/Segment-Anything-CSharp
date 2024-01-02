using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SAMViewer
{
    // 点标注控件
    public class PointAnnotation : UserControl
    {
        private const int DefaultSize = 10;

        private readonly Shape shape;
        private readonly TextBlock textBlock;
        public SolidColorBrush Brush = Brushes.Red;
        public PointAnnotation(SolidColorBrush brush)
        {
            Brush = brush;
            shape = new Ellipse
            {
                Width = DefaultSize,
                Height = DefaultSize,
                Fill = brush,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            textBlock = new TextBlock
            {
                Text = "Point",
                Foreground = Brushes.Black,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid();
            grid.Children.Add(shape);

            Content = grid;
        }
        public Point Position
        {
            get { return new Point(Canvas.GetLeft(this) + shape.Width / 2, Canvas.GetTop(this) + shape.Height / 2); }
            set
            {
                Canvas.SetLeft(this, value.X - shape.Width / 2);
                Canvas.SetTop(this, value.Y - shape.Height / 2);
            }
        }
        public string Text
        {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }

        //protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        //{
        //    base.OnMouseLeftButtonDown(e);
        //    CaptureMouse();
        //}

        //protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        //{
        //    base.OnMouseLeftButtonUp(e);
        //    ReleaseMouseCapture();
        //}

        //protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        //{
        //    base.OnMouseMove(e);
        //    if (IsMouseCaptured)
        //    {
        //        var newPosition = e.GetPosition(Parent as UIElement);
        //        Canvas.SetLeft(this, newPosition.X - _shape.Width / 2);
        //        Canvas.SetTop(this, newPosition.Y - _shape.Height / 2);
        //    }
        //}
    }
    /// <summary>
    /// 四边形标注
    /// </summary>
    public class RectAnnotation : UserControl
    {
        private readonly Shape shape;
        private readonly TextBlock textBlock;

        public RectAnnotation()
        {
            shape = new Rectangle
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Blue,
                StrokeThickness = 1
            };

            textBlock = new TextBlock
            {
                Text = "Rect",
                Foreground = Brushes.Blue,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid();
            grid.Children.Add(shape);
            //grid.Children.Add(_textBlock);

            Content = grid;
        }

        public Point StartPosition
        {
            get { return new Point(Canvas.GetLeft(this) + shape.Width / 2, Canvas.GetTop(this) + shape.Height / 2); }
            set
            {
                Canvas.SetLeft(this, value.X - shape.Width / 2);
                Canvas.SetTop(this, value.Y - shape.Height / 2);
            }
        }

        public Point LeftUp { get; set; }
        public Point RightBottom { get; set; }

        public double Width
        {
            get { return shape.Width; }
            set { shape.Width = value; }
        }

        public double Height
        {
            get { return shape.Height; }
            set { shape.Height = value; }
        }

        public string Text
        {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            //CaptureMouse();           
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            //ReleaseMouseCapture();
        }

        //protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        //{
        //    base.OnMouseMove(e);
        //    if (IsMouseCaptured)
        //    {
        //        var newPosition = e.GetPosition(Parent as UIElement);
        //        Canvas.SetLeft(this, newPosition.X - _shape.Width / 2);
        //        Canvas.SetTop(this, newPosition.Y - _shape.Height / 2);
        //    }
        //}
    }
}
