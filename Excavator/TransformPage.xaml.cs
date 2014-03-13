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
using System.Windows;
using System.Windows.Controls;

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
                lblNoData.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Hidden;
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
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            if ( !string.IsNullOrEmpty( txtDataEncryption.Text ) )
            {
                var appConfig = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.None );
                appConfig.AppSettings.Settings.Add( "DataEncryptionKey", txtDataEncryption.Text );
                appConfig.AppSettings.Settings.Add( "ImportUser", txtImportUser.Text );

                try
                {
                    appConfig.Save( ConfigurationSaveMode.Modified );
                    ConfigurationManager.RefreshSection( "appSettings" );
                    var progressPage = new ProgressPage( excavator );
                    this.NavigationService.Navigate( progressPage );
                }
                catch
                {
                    lblNoData.Content = "Unable to save the DataEncryptionKey. Please check the permissions on the current directory.";
                    lblNoData.Visibility = Visibility.Visible;
                }
            }
            else
            {
                lblNoData.Content = "Please enter the DataEncryptionKey. This key is required to import financial accounts.";
                lblNoData.Visibility = Visibility.Visible;
            }
        }

        #endregion
    }
}
