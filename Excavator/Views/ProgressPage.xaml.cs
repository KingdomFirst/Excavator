﻿using System;
using System.ComponentModel;
using System.Configuration;
using System.Data.Entity.Validation;
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

                var bwImportData = new BackgroundWorker();
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
        private void UpdateInterface( long progress, string status )
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
                if ( ex is DbEntityValidationException )
                {
                    var validationErrors = ( (DbEntityValidationException)ex ).EntityValidationErrors;
                    if ( validationErrors.Any() )
                    {
                        foreach ( var eve in validationErrors )
                        {
                            ExcavatorComponent.LogException( string.Format( "{0} (Foreign Key: {1})", eve.Entry.Entity.GetType().Name, eve.Entry.Property( "ForeignKey" ).CurrentValue ), string.Format( "Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                eve.Entry.Entity.GetType().Name, eve.Entry.State ) );
                            foreach ( var ve in eve.ValidationErrors )
                            {
                                ExcavatorComponent.LogException( ve.PropertyName, string.Format( "- Property: \"{0}\", Error: \"{1}\"",
                                    ve.PropertyName, ve.ErrorMessage ) );
                            }
                        }
                        exception = validationErrors.FirstOrDefault().ValidationErrors.ToString();
                    }
                }
                else if ( ex.InnerException != null )
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

            var bwTransformData = sender as BackgroundWorker;
            bwTransformData.RunWorkerCompleted -= new RunWorkerCompletedEventHandler( bwImportData_RunWorkerCompleted );
            bwTransformData.DoWork -= new DoWorkEventHandler( bwImportData_DoWork );
            bwTransformData.Dispose();
        }

        #endregion Async Tasks
    }
}
