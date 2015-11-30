using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace JoinerSplitter
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ListViewInsertMarkAdornerView : UserControl
    {
        public ListViewInsertMarkAdornerView()
        {
            InitializeComponent();
        }
    }

    internal class ListViewInsertMarkAdorner : Adorner
    {
        public FrameworkElement view;

        private double offset;

        public ListViewInsertMarkAdorner(Control adornedElement) : base(adornedElement)
        {
            this.view = new ListViewInsertMarkAdornerView();
            //view = new Rectangle { Fill = new SolidColorBrush(Colors.Black) };
            view.Width = adornedElement.RenderSize.Width;
            //view.Height = adornedElement.RenderSize.Height;
            view.IsHitTestVisible = false;
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

        protected override int VisualChildrenCount => (offset == 0) ? 0 : 1;
#pragma warning restore RECS0018 // Comparison of floating point numbers with equality operator

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            result.Children.Add(new TranslateTransform(0, offset));
            return result;
        }

        /// <summary>
        /// Override.
        /// </summary>
        /// <param name="finalSize"></param>
        /// <returns></returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            view.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index) => view;

#pragma warning disable RECS0018 // Comparison with 0
#pragma warning restore RECS0018 // Comparison with 0
        /// <summary>
        /// Override.
        /// </summary>
        /// <param name="constraint"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size constraint)
        {
            view.Measure(constraint);
            return view.DesiredSize;
        }
    }
}