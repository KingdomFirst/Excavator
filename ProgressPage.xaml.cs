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
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for TransformPage.xaml
    /// </summary>
    public partial class ProgressPage : System.Windows.Controls.Page
    {
        public ExcavatorComponent excavator;

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

        #endregion

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
        /// Handles the RequestNavigate event of the Hyperlink control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RequestNavigateEventArgs"/> instance containing the event data.</param>
        private void Issue_RequestNavigate( object sender, RequestNavigateEventArgs e )
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

        /// <summary>
        /// Handles the Click event of the select all hyperlink control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Select_All_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in excavator.TableNodes)
            {
                node.Checked = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the unselect all hyperlink control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Unselect_All_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in excavator.TableNodes)
            {
                node.Checked = false;
            }
        }

        #endregion

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
            var importUser = ConfigurationManager.AppSettings["ImportUser"];
            e.Result = excavator.TransformData( importUser );
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwTransformData control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void bwImportData_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            var rowsImported = (int)e.Result;
            this.Dispatcher.Invoke( (Action)( () =>
            {
                lblHeader.Content = "Import Complete";
                txtProgress.AppendText( "Uploaded all the data!" );
                txtProgress.ScrollToEnd();
                btnClose.Visibility = Visibility.Visible;
            } ) );

            BackgroundWorker bwTransformData = sender as BackgroundWorker;
            bwTransformData.RunWorkerCompleted -= new RunWorkerCompletedEventHandler( bwImportData_RunWorkerCompleted );
            bwTransformData.DoWork -= new DoWorkEventHandler( bwImportData_DoWork );
            bwTransformData.Dispose();
        }

        #endregion
    }
}
