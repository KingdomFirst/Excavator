using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for TransformPage.xaml
    /// </summary>
    public partial class TransformPage : System.Windows.Controls.Page
    {
        public ExcavatorComponent excavator;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformPage"/> class.
        /// </summary>
        public TransformPage( ExcavatorComponent parameter = null )
        {
            InitializeComponent();
            if ( parameter != null )
            {
                excavator = parameter;
            }
            else
            {
                lblDataUpload.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnStart control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnStart_Click( object sender, RoutedEventArgs e )
        {
            
            BackgroundWorker bwTransformData = new BackgroundWorker();
            bwTransformData.DoWork += bwTransformData_DoWork;
            bwTransformData.ProgressChanged += bwTransformData_ProgressChanged;
            bwTransformData.RunWorkerCompleted += bwTransformData_RunWorkerCompleted;
            bwTransformData.RunWorkerAsync();

            // if progressing, check for cancel
            // btnStart.Style = (Style)FindResource( "labelStyle" );
            // btnStart.Content = "Cancel";
        }

        /// <summary>
        /// Handles the Click event of the btnBack control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnBack_Click( object sender, RoutedEventArgs e )
        {
            this.NavigationService.GoBack();
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            // clicked Run in Background
            // wait until all models have been saved
            // Application.Current.Shutdown();
        }        

        #endregion

        #region Async Tasks

        /// <summary>
        /// Handles the DoWork event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwTransformData_DoWork( object sender, DoWorkEventArgs e )
        {
            bool isComplete = excavator.TransformData();
            
        }

        /// <summary>
        /// Handles the ProgressChanged event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void bwTransformData_ProgressChanged( object sender, ProgressChangedEventArgs e )
        {
            //lblUploadProgress.Content = string.Format( "Uploading Scanned Checks {0}%", e.ProgressPercentage );            
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void bwTransformData_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            this.Dispatcher.Invoke( (Action)( () =>
            {
                lblDataUpload.Style = (Style)FindResource( "labelStyleSuccess" );
                lblDataUpload.Content = "Successfully uploaded all the content";
                btnNext.Visibility = Visibility.Visible;
            } ) );
        }

        #endregion
    }
}
