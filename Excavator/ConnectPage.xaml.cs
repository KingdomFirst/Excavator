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

using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OrcaMDF.Core.Engine;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectPage : Page
    {
        #region Fields

        public FrontEndLoader frontEndLoader;

        public ExcavatorComponent excavator;

        #endregion

        #region Initializer Methods

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public ConnectPage()
        {
            InitializeComponent();

            frontEndLoader = new FrontEndLoader();
            if ( frontEndLoader.excavatorTypes.Any() )
            {
                lstDatabaseTypes.ItemsSource = frontEndLoader.excavatorTypes.GroupBy( t => t.FullName ).Select( g => g.FirstOrDefault() );
                lstDatabaseTypes.SelectedItem = frontEndLoader.excavatorTypes.FirstOrDefault();
            }
            else
            {
                btnNext.Visibility = Visibility.Hidden;
                lblNoData.Visibility = Visibility.Visible;
                lblDatabaseTypes.Visibility = Visibility.Hidden;
                lstDatabaseTypes.Visibility = Visibility.Hidden;
                lblNoData.Content += string.Format( " ({0})", ConfigurationManager.AppSettings["ExtensionPath"] );
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnUpload control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnUpload_Click( object sender, RoutedEventArgs e )
        {
            Mouse.OverrideCursor = Cursors.Wait;

            BackgroundWorker bwLoadPreview = new BackgroundWorker();
            bwLoadPreview.DoWork += bwPreview_DoWork;
            bwLoadPreview.RunWorkerCompleted += bwPreview_RunWorkerCompleted;
            bwLoadPreview.RunWorkerAsync( lstDatabaseTypes.SelectedValue.ToString() );
        }

        /// <summary>
        /// Handles the Click event of the btnConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnConnect_Click( object sender, RoutedEventArgs e )
        {
            //var sqlConnector = new SQLConnector();
            //var connectWindow = new Window();
            //var mask = new SolidColorBrush();
            //mask.Color = Colors.White;
            //mask.Opacity = .5;
            //var blur = new BlurEffect();
            //blur.Radius = 2;
            //this.OpacityMask = mask;
            //this.Effect = blur;

            //var cancelBtn = new Button();
            //var okBtn = new Button();

            //cancelBtn.Content = "Cancel";
            //cancelBtn.IsCancel = true;
            //cancelBtn.SetValue( Grid.RowProperty, 3 );

            //okBtn.Content = "Ok";
            //okBtn.IsDefault = true;
            //okBtn.SetValue( Grid.RowProperty, 3 );
            //cancelBtn.SetValue( Grid.ColumnProperty, 0 );

            //okBtn.Click += btnOk_Click;
            //cancelBtn.Click += btnCancel_Click;
            //sqlConnector.grdSQLConnect.Children.Add( okBtn );
            //sqlConnector.grdSQLConnect.Children.Add( cancelBtn );

            //connectWindow.Owner = this;
            //connectWindow.Content = sqlConnector;
            //connectWindow.ShowInTaskbar = false;
            //connectWindow.WindowStyle = WindowStyle.None;
            //connectWindow.ResizeMode = ResizeMode.NoResize;
            //connectWindow.SizeToContent = SizeToContent.WidthAndHeight;
            //connectWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //var test = connectWindow.ShowDialog();

            //this.Effect = null;
            //this.OpacityMask = null;
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            var selectPage = new SelectPage( excavator );
            this.NavigationService.Navigate( selectPage );
        }

        #endregion

        #region Async Tasks

        /// <summary>
        /// Handles the DoWork event of the bwLoadSchema control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        private void bwPreview_DoWork( object sender, DoWorkEventArgs e )
        {
            var mdfPicker = new OpenFileDialog();
            mdfPicker.Filter = "SQL Database files|*.mdf";
            mdfPicker.AddExtension = false;

            if ( mdfPicker.ShowDialog() == true )
            {
                var database = new Database( mdfPicker.FileName );
                if ( database != null )
                {
                    var dbType = (string)e.Argument;
                    excavator = frontEndLoader.excavatorTypes.Where( t => t.FullName.Equals( dbType ) ).FirstOrDefault();
                    if ( excavator != null )
                    {
                        bool loadedSuccessfully = excavator.LoadSchema( database );
                        e.Cancel = !loadedSuccessfully;
                    }
                }
            }
            else
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwLoadSchema control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwPreview_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            if ( e.Cancelled != true )
            {
                lblDbUpload.Style = (Style)FindResource( "labelStyleSuccess" );
                lblDbUpload.Content = "Successfully connected to the database";
            }

            lblDbUpload.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = null;
        }

        #endregion
    }
}
