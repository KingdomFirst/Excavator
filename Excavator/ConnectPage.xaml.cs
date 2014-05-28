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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for ConnectPage.xaml
    /// </summary>
    public partial class ConnectPage : Page
    {
        #region Fields

        private FrontEndLoader frontEndLoader;

        private ExcavatorComponent excavator;

        private SqlConnector sqlConnector;

        private ConnectionString existingConnection;

        /// <summary>
        /// Gets or sets the current connection.
        /// </summary>
        /// <value>
        /// The current connection.
        /// </value>
        public ConnectionString CurrentConnection
        {
            get
            {
                return existingConnection;
            }
            set
            {
                existingConnection = value;
                App.ExistingConnection = value; //for back and forth, restore from session
                RaisePropertyChanged( "Connection" );
                RaisePropertyChanged( "ConnectionDescribed" );
            }
        }

        private string _ConnectionDescribed = string.Empty;

        /// <summary>
        /// Highlights the current connection on the connect page.
        /// </summary>
        /// <value>
        /// The connection described.
        /// </value>
        public string ConnectionDescribed
        {
            get
            {
                if ( existingConnection != null )
                {
                    _ConnectionDescribed = "(Current Destination: " + existingConnection.Server + ":" + existingConnection.Database + ")";
                }
                return _ConnectionDescribed;
            }
        }

        private IEnumerable<ExcavatorComponent> _ExcavatorImportDlls = null;

        /// <summary>
        /// Gets or sets the excavator import DLLS.
        /// </summary>
        /// <value>
        /// The excavator import DLLS.
        /// </value>
        public IEnumerable<ExcavatorComponent> ExcavatorImportDlls
        {
            get
            {
                return _ExcavatorImportDlls;
            }
            set
            {
                if ( _ExcavatorImportDlls == value )
                    return;
                _ExcavatorImportDlls = value;
                RaisePropertyChanged( "ExcavatorImportDlls" );
            }
        }

        private ExcavatorComponent _SelectedImportType = null;

        /// <summary>
        /// Gets or sets the type of the selected import.
        /// </summary>
        /// <value>
        /// The type of the selected import.
        /// </value>
        public ExcavatorComponent SelectedImportType
        {
            get
            {
                return _SelectedImportType;
            }
            set
            {
                if ( _SelectedImportType == value )
                    return;
                _SelectedImportType = value;
                RaisePropertyChanged( "SelectedImportType" );
            }
        }

        /// <summary>
        /// Gets the name of the selected type.
        /// </summary>
        /// <param name="src">The source.</param>
        /// <param name="propName">Name of the property.</param>
        /// <returns></returns>
        public static object GetPropValue( object src, string propName )
        {
            if ( src.GetType().GetProperty( propName ) != null )
            {
                return src.GetType().GetProperty( propName ).GetValue( src, null );
            }
            return string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the property changed.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void RaisePropertyChanged( string propertyName )
        {
            if ( PropertyChanged != null )
            {
                PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }

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
                ExcavatorImportDlls = frontEndLoader.excavatorTypes.GroupBy( t => t.FullName ).Select( g => g.FirstOrDefault() );
                SelectedImportType = frontEndLoader.excavatorTypes.FirstOrDefault();
                InitializeDBConnection();
            }
            else
            {
                btnNext.Visibility = Visibility.Hidden;
                lblNoData.Visibility = Visibility.Visible;
                lblDatabaseTypes.Visibility = Visibility.Hidden;
                lstDatabaseTypes.Visibility = Visibility.Hidden;
                lblNoData.Content += string.Format( " ({0})", ConfigurationManager.AppSettings["ExtensionPath"] );
            }

            DataContext = this;
        }

        /// <summary>
        /// Initializes the database connection.
        /// </summary>
        private void InitializeDBConnection()
        {
            if ( App.ExistingConnection != null )
            {
                CurrentConnection = App.ExistingConnection;
            }
            else
            {
                //initialize from app.config
                var appConfig = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.None );
                var rockContext = appConfig.ConnectionStrings.ConnectionStrings["RockContext"];
                if ( rockContext != null )
                {
                    CurrentConnection = new ConnectionString( rockContext.ConnectionString );
                }
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
            sqlConnector = new SqlConnector();
            var modalPanel = new StackPanel();
            var buttonPanel = new StackPanel();
            var cancelBtn = new Button();
            var okBtn = new Button();

            // set background effects
            var mask = new SolidColorBrush();
            var blur = new BlurEffect();
            mask.Color = Colors.White;
            mask.Opacity = .5;
            blur.Radius = 10;
            this.OpacityMask = mask;
            this.Effect = blur;

            sqlConnector.ConnectionString = existingConnection;
            modalPanel.Children.Add( sqlConnector );
            buttonPanel.Orientation = Orientation.Horizontal;
            buttonPanel.HorizontalAlignment = HorizontalAlignment.Right;

            okBtn.Content = "Ok";
            okBtn.IsDefault = true;
            okBtn.Margin = new Thickness( 0, 0, 5, 0 );
            okBtn.Click += btnOk_Click;
            okBtn.Style = (Style)FindResource( "buttonStylePrimary" );
            cancelBtn.Content = "Cancel";
            cancelBtn.IsCancel = true;
            cancelBtn.Style = (Style)FindResource( "buttonStyle" );

            buttonPanel.Children.Add( okBtn );
            buttonPanel.Children.Add( cancelBtn );
            modalPanel.Children.Add( buttonPanel );

            var connectWindow = new Window();
            connectWindow.Content = modalPanel;
            connectWindow.Owner = Window.GetWindow( this );
            connectWindow.ShowInTaskbar = false;
            connectWindow.Background = (Brush)FindResource( "windowBackground" );
            connectWindow.WindowStyle = WindowStyle.None;
            connectWindow.ResizeMode = ResizeMode.NoResize;
            connectWindow.SizeToContent = SizeToContent.WidthAndHeight;
            connectWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var showWindow = connectWindow.ShowDialog();
            this.OpacityMask = null;
            this.Effect = null;

            if ( !string.IsNullOrWhiteSpace( sqlConnector.ConnectionString ) )
            {
                lblDbConnect.Style = (Style)FindResource( "labelStyleSuccess" );
                lblDbConnect.Content = "Successfully connected to the database";
                CurrentConnection = sqlConnector.ConnectionString;
            }

            lblDbConnect.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handles the Click event of the btnOk control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnOk_Click( object sender, RoutedEventArgs e )
        {
            Window.GetWindow( (Button)sender ).DialogResult = true;
            sqlConnector.ConnectionString.MultipleActiveResultSets = true;
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            var appConfig = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.None );
            var rockContext = appConfig.ConnectionStrings.ConnectionStrings["RockContext"];

            if ( excavator != null && ( rockContext != null || !string.IsNullOrWhiteSpace( existingConnection ) ) )
            {
                try
                {
                    if ( sqlConnector != null && !string.IsNullOrWhiteSpace( sqlConnector.ConnectionString ) )
                    {
                        if ( rockContext != null )
                        {
                            rockContext.ConnectionString = sqlConnector.ConnectionString;
                        }
                        else
                        {
                            appConfig.ConnectionStrings.ConnectionStrings.Add( new ConnectionStringSettings( "RockContext", sqlConnector.ConnectionString ) );
                        }

                        appConfig.Save( ConfigurationSaveMode.Modified );
                        ConfigurationManager.RefreshSection( "connectionstrings" );
                    }

                    var selectPage = new SelectPage( excavator );
                    this.NavigationService.Navigate( selectPage );
                }
                catch
                {
                    lblDbConnect.Style = (Style)FindResource( "labelStyleAlert" );
                    lblDbConnect.Content = "Unable to set the database connection. Please check the permissions on the current directory.";
                    lblDbConnect.Visibility = Visibility.Visible;
                }
            }
            else
            {
                lblDbConnect.Style = (Style)FindResource( "labelStyleAlert" );
                lblDbConnect.Content = "Please select a valid source and destination.";
                lblDbConnect.Visibility = Visibility.Visible;
            }
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
            try
            {
                var selectedExcavator = (string)e.Argument;
                var filePicker = new OpenFileDialog();
                filePicker.Multiselect = true;

                var supportedExtensions = frontEndLoader.excavatorTypes.Where( t => t.FullName.Equals( selectedExcavator ) )
                    .Select( t => t.FullName + " |*" + t.ExtensionType ).ToList();
                filePicker.Filter = string.Join( "|", supportedExtensions );

                if ( filePicker.ShowDialog() == true )
                {
                    excavator = frontEndLoader.excavatorTypes.Where( t => t.FullName.Equals( selectedExcavator ) ).FirstOrDefault();
                    if ( excavator != null )
                    {
                        bool loadedSuccessfully = false;
                        foreach ( var file in filePicker.FileNames )
                        {
                            loadedSuccessfully = excavator.LoadSchema( file );
                            e.Cancel = !loadedSuccessfully;
                            if ( e.Cancel )
                                break;
                            Dispatcher.BeginInvoke( (Action)( () =>
                                FilesUploaded.Children.Add( new TextBlock { Text = file } )
                                ) );
                        }
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            }
            catch ( Exception exp )
            {
                App.LogException( "upload file", exp.ToString() );
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
                lblDbUpload.Content = "Successfully read the import file";
            }

            lblDbUpload.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = null;
        }

        #endregion
    }
}
