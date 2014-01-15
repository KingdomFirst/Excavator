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
    /// Displays a stepped progress bar
    /// </summary>
    public class ProgressBar : ItemsControl
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
        public static DependencyProperty ProgressProperty
            = DependencyProperty.Register( "Progress", typeof( int ), typeof( ProgressBar ), new FrameworkPropertyMetadata( 0, null, UpdateProgress ) );

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the <see cref="ProgressBar"/> class.
        /// </summary>
        static ProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata( typeof( ProgressBar ), new FrameworkPropertyMetadata( typeof( ProgressBar ) ) );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressBar"/> class.
        /// </summary>
        public ProgressBar()
        {
            //this.Style = (Style)Application.Current.Resources.FindName( "ProgressBar" );
        }

        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        private static object UpdateProgress( DependencyObject target, object value )
        {
            var progressBar = (ProgressBar)target;
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
