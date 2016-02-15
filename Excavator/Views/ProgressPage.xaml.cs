// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : System.Windows.Controls.Page
    {
        private ExcavatorComponent excavator;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationPage"/> class.
        /// </summary>
        public ProgressPage( ExcavatorComponent parameter = null )
        {
            InitializeComponent();
            if ( parameter != null )
            {
                excavator = parameter;
                excavator.ProgressUpdated += new ReportProgress( UpdateInterface );

                BackgroundWorker bwImportData = new BackgroundWorker();
                bwImportData.DoWork += bwImportData_DoWork;
                bwImportData.RunWorkerCompleted += bwImportData_RunWorkerCompleted;
                bwImportData.WorkerReportsProgress = true;
                bwImportData.RunWorkerAsync();
            }
        }

        #endregion Constructor

        #region Events

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
        /// Handles the RequestNavigate event of the btnIssue control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RequestNavigateEventArgs"/> instance containing the event data.</param>
        private void btnIssue_RequestNavigate( object sender, RequestNavigateEventArgs e )
        {
            Process.Start( new ProcessStartInfo( e.Uri.AbsoluteUri ) );
            e.Handled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnClose_Click( object sender, RoutedEventArgs e )
        {
            Application.Current.Shutdown();
        }

        #endregion Events

        #region Async Tasks

        /// <summary>
        /// Updates the interface.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="status">The status.</param>
        private void UpdateInterface( int progress, string status )
        {
            this.Dispatcher.Invoke( (Action)( () =>
            {
                // use progress in a progress bar?
                txtProgress.AppendText( status );
                txtProgress.ScrollToEnd();
            } ) );
        }

        /// <summary>
        /// Handles the DoWork event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwImportData_DoWork( object sender, DoWorkEventArgs e )
        {
            var settings = ConfigurationManager.AppSettings.AllKeys
                .ToDictionary( t => t.ToString(), t => ConfigurationManager.AppSettings[t].ToString() );

            try
            {
                e.Result = excavator.TransformData( settings );
            }
            catch ( Exception ex )
            {
                var exception = ex.ToString();
                if ( ex.InnerException != null )
                {
                    exception = ex.InnerException.ToString();
                }

                App.LogException( "Transform Data", exception );
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void bwImportData_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            var rowsImported = (int?)e.Result;
            if ( rowsImported > 0 )
            {
                this.Dispatcher.Invoke( (Action)( () =>
                {
                    lblHeader.Content = "Import Complete";
                    txtProgress.AppendText( Environment.NewLine + DateTime.Now.ToLongTimeString() + "  Finished upload." );
                    txtProgress.ScrollToEnd();
                } ) );
            }
            else
            {
                this.Dispatcher.Invoke( (Action)( () =>
                {
                    lblHeader.Content = "Import Failed";
                    txtProgress.AppendText( Environment.NewLine + DateTime.Now.ToLongTimeString() + "  Could not finish upload. Check the exceptions log for details." );
                    txtProgress.ScrollToEnd();
                } ) );
            }

            btnClose.Visibility = Visibility.Visible;

            BackgroundWorker bwTransformData = sender as BackgroundWorker;
            bwTransformData.RunWorkerCompleted -= new RunWorkerCompletedEventHandler( bwImportData_RunWorkerCompleted );
            bwTransformData.DoWork -= new DoWorkEventHandler( bwImportData_DoWork );
            bwTransformData.Dispose();
        }

        #endregion Async Tasks
    }
}