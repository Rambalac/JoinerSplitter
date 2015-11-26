using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace JoinerSplitter
{
    class ListViewInsertMarkAdorner : Adorner
    {
        public FrameworkElement view;
        public ListViewInsertMarkAdorner(Control adornedElement) : base(adornedElement)
        {
            this.view = new ListViewInsertMarkAdornerView();
            //view = new Rectangle { Fill = new SolidColorBrush(Colors.Black) };
            view.Width = adornedElement.RenderSize.Width;
            //view.Height = adornedElement.RenderSize.Height;
            view.IsHitTestVisible = false;
        }

        protected override Visual GetVisualChild(int index) => view;

#pragma warning disable RECS0018 // Comparison with 0
        protected override int VisualChildrenCount => (offset == 0) ? 0 : 1;

        public double Offset
        {
            get
            {
                return offset;
            }

            set
            {
                if (value != 0 && offset != value)
                {
                    offset = value;
                    (Parent as AdornerLayer)?.Update(AdornedElement);
                }
            }
        }
#pragma warning restore RECS0018 // Comparison with 0

        double offset;

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
        /// <param name="constraint"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size constraint)
        {
            view.Measure(constraint);
            return view.DesiredSize;
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

    }

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
}
