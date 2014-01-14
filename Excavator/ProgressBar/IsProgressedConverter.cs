using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows;

namespace ProgressBar
{
    public class ProgressedItemConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts source values to a value for the binding target. The data binding engine calls 
        /// this method when it propagates the values from source bindings to the binding target.
        /// </summary>        
        public object Convert( object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture )
        {
            if ( ( values[0] is ContentPresenter && values[1] is int ) == false )
            {
                return Visibility.Collapsed;
            }

            bool checkNextItem = System.Convert.ToBoolean( parameter.ToString() );
            int progress = (int)values[1];

            var contentPresenter = (ContentPresenter)values[0];
            var progressBar = (ProgressBar)ItemsControl.ItemsControlFromItemContainer( contentPresenter );

            int index = progressBar.ItemContainerGenerator.IndexFromContainer( contentPresenter );
            if ( checkNextItem == true )
            {
                index++;
            }

            int percent = (int)( ( (double)index / progressBar.Items.Count ) * 100 );

            if ( percent < progress )
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a binding target value to the source binding values.
        /// </summary>        
        public object[] ConvertBack( object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture )
        {
            throw new NotSupportedException();
        }
    }
}
