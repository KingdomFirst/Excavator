using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Excavator
{
    /// <summary>
    /// The last item in the progress bar
    /// </summary>
    public class LastItemConverter : IValueConverter
    {
        /// <summary>
        /// Converts the current item value from the presenter.
        /// </summary>
        public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
        {
            var contentPresenter = (ContentPresenter)value;
            var itemsControl = ItemsControl.ItemsControlFromItemContainer( contentPresenter );
            int index = itemsControl.ItemContainerGenerator.IndexFromContainer( contentPresenter );
            return ( index == ( itemsControl.Items.Count - 1 ) );
        }

        /// <summary>
        /// Converts a value back to the presenter.
        /// </summary>
        public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
        {
            throw new NotSupportedException();
        }
    }
}
