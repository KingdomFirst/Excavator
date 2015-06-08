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
using Excavator.Utility;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for ConfigurationPage.xaml
    /// </summary>
    public partial class ConfigurationPage : System.Windows.Controls.Page
    {
        private ExcavatorComponent excavator;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationPage"/> class.
        /// </summary>
        public ConfigurationPage( ExcavatorComponent parameter = null )
        {
            InitializeComponent();
            if ( parameter != null )
            {
                excavator = parameter;
                txtImportUser.Text = ConfigurationManager.AppSettings["ImportUser"];
                txtPasswordKey.Text = ConfigurationManager.AppSettings["PasswordKey"];
                txtDataEncryption.Text = ConfigurationManager.AppSettings["DataEncryptionKey"];

                int reportingNumber;
                Int32.TryParse( ConfigurationManager.AppSettings["ReportingNumber"], out reportingNumber );
                excavator.ReportingNumber = reportingNumber > 0 ? reportingNumber : 100;
            }
            else
            {
                lblNoData.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Hidden;
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
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            if ( !string.IsNullOrEmpty( txtPasswordKey.Text ) && !string.IsNullOrEmpty( txtDataEncryption.Text ) )
            {
                var appConfig = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.None );
                if ( appConfig.AppSettings.Settings.Count < 3 )
                {
                    appConfig.AppSettings.Settings.Add( "ImportUser", string.Empty );
                    appConfig.AppSettings.Settings.Add( "PasswordKey", string.Empty );
                    appConfig.AppSettings.Settings.Add( "DataEncryptionKey", string.Empty );
                }

                try
                {
                    appConfig.AppSettings.Settings["ImportUser"].Value = txtImportUser.Text;
                    appConfig.AppSettings.Settings["PasswordKey"].Value = txtPasswordKey.Text;
                    appConfig.AppSettings.Settings["DataEncryptionKey"].Value = txtDataEncryption.Text;
                    appConfig.Save( ConfigurationSaveMode.Modified );
                    ConfigurationManager.RefreshSection( "appSettings" );

                    var progressPage = new ProgressPage( excavator );
                    this.NavigationService.Navigate( progressPage );
                }
                catch
                {
                    lblNoData.Content = "Unable to save the configuration keys. Please check the permissions on the current directory.";
                    lblNoData.Visibility = Visibility.Visible;
                }
            }
        }

        #endregion Events
    }
}