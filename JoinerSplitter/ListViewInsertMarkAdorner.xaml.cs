namespace JoinerSplitter
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Media;

    internal class ListViewInsertMarkAdorner : Adorner
    {
        private double offset;

        public ListViewInsertMarkAdorner(Control adornedElement)
            : base(adornedElement)
        {
            View = new ListViewInsertMarkAdornerView
            {
                Width = adornedElement.RenderSize.Width,
                IsHitTestVisible = false
            };

            // view = new Rectangle { Fill = new SolidColorBrush(Colors.Black) };

            // view.Height = adornedElement.RenderSize.Height;
        }

#pragma warning disable RECS0018 // Comparison of floating point numbers with equality operator
        public double Offset
        {
            set
            {
                if (value != 0 && offset != value)
                {
                    offset = value;
                    (Parent as AdornerLayer)?.Update(AdornedElement);
                }
            }
        }

        public FrameworkElement View { get; set; }

        protected override int VisualChildrenCount => (offset == 0) ? 0 : 1;
#pragma warning restore RECS0018 // Comparison of floating point numbers with equality operator

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            result.Children.Add(new TranslateTransform(0, offset));
            return result;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            View.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index) => View;

        protected override Size MeasureOverride(Size constraint)
        {
            View.Measure(constraint);
            return View.DesiredSize;
        }
    }
}