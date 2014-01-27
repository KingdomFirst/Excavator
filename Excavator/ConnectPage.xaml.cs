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
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OrcaMDF.Core.Engine;
using System.ComponentModel;
using System.Windows.Controls;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectWindow : Page
    {
        #region Fields

        private List<ExcavatorComponent> excavatorTypes;

        #endregion

        #region Initializer Methods

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public ConnectWindow()
        {
            InitializeComponent();

            var loader = new FrontEndLoader();
            excavatorTypes = loader.excavatorTypes;
            if ( excavatorTypes.Any() )
            {
                databaseTypes.ItemsSource = excavatorTypes;
                databaseTypes.SelectedItem = excavatorTypes.FirstOrDefault();
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
            var mdfPicker = new OpenFileDialog();
            mdfPicker.Filter = "SQL Database files|*.mdf";
            mdfPicker.AddExtension = false;

            if ( mdfPicker.ShowDialog() == true )
            {
                var database = new Database( mdfPicker.FileName );
                if ( database != null )
                {
                    var dbType = databaseTypes.SelectedValue.ToString();
                    ExcavatorComponent dbModel = excavatorTypes.Where( t => t.FullName.Equals( dbType ) ).FirstOrDefault();
                    if ( dbModel != null )
                    {
                        bool isLoaded = dbModel.LoadSchema( database );
                        if ( isLoaded )
                        {
                            return;
                        }
                    }
                }

                MessageBox.Show( "Could not read mdf file. Please make sure the file is not in use." );
            }
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
        
        #endregion

        #region Async Tasks

        /// <summary>
        /// Updates the UI when the dbModel updates the progress.
        /// </summary>
        /// <param name="value">The value.</param>
        private void dbModel_OnProgressUpdate( int value )
        {
            this.Dispatcher.Invoke( (Action)( () =>
            {
                lblProgress.Content = string.Format( "{0}%", value );
            } ) );
        }

        #endregion
        
    }
}
