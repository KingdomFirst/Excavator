using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Shapes;

namespace Excavator
{
    /// <summary>
    /// Displays a stepped progress bar.  Inherits from a list control 
    /// in order to display grouped items instead of creating a user 
    /// control instance for each item.
    /// </summary>
    public class NavigationBar : ItemsControl
    {
        #region Fields

        /// <summary>
        /// Gets or sets the progress number.
        /// </summary>
        /// <value>
        /// The progress.
        /// </value>
        public int Progress
        {
            get { return (int)base.GetValue( ProgressProperty ); }
            set { base.SetValue( ProgressProperty, value ); }
        }

        /// <summary>
        /// The progress step dependency on the progress number
        /// </summary>
        public static DependencyProperty ProgressProperty = DependencyProperty.Register( "Progress", typeof( int ), typeof( NavigationBar ), new FrameworkPropertyMetadata( 0, null, UpdateProgress ) );

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the <see cref="NavigationBar"/> class.
        /// </summary>
        static NavigationBar()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationBar"/> class.
        /// </summary>
        public NavigationBar()
        {
        }

        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        private static object UpdateProgress( DependencyObject target, object value )
        {
            var progressBar = (NavigationBar)target;
            int progress = (int)value;
            if ( progress < 0 )
            {
                progress = 0;
            }
            else if ( progress > 100 )
            {
                progress = 100;
            }
            return progress;
        }

        #endregion
    }
}