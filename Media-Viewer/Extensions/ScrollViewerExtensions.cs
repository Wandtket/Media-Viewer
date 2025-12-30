using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaViewer.Extensions
{
    public static class ScrollViewerExtensions
    {
        public static readonly DependencyProperty ZoomToFitProperty =
            DependencyProperty.RegisterAttached("ZoomToFit", typeof(bool), typeof(ScrollViewerExtensions),
                new PropertyMetadata(false, OnZoomToFitChanged));

        public static bool GetZoomToFit(DependencyObject obj) => (bool)obj.GetValue(ZoomToFitProperty);
        public static void SetZoomToFit(DependencyObject obj, bool value) => obj.SetValue(ZoomToFitProperty, value);

        private static void OnZoomToFitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer && e.NewValue is bool zoomToFit && zoomToFit)
            {
                scrollViewer.Loaded += (s, _) => ZoomContentToFit(scrollViewer);
            }
        }

        private static void ZoomContentToFit(ScrollViewer scrollViewer)
        {
            if (scrollViewer.Content is FrameworkElement content)
            {
                content.Loaded += (s, _) =>
                {
                    double zoomFactor = Math.Min(
                        scrollViewer.ViewportWidth / content.ActualWidth,
                        scrollViewer.ViewportHeight / content.ActualHeight);

                    scrollViewer.ChangeView(null, null, (float)zoomFactor);
                };
            }
        }
    }
}
