using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        protected override int VisualChildrenCount => 1;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Potential Code Quality Issues", "RECS0018:Comparison of floating point numbers with equality operator", Justification = "Comparing same assigned number")]
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
