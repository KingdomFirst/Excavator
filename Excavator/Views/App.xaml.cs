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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Excavator.Utility;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        [System.STAThreadAttribute()]
        public static void Main()
        {
            Excavator.App app = new Excavator.App();
            app.InitializeComponent();
            app.Run();
        }

        private static ConnectionString existingConnection;

        /// <summary>
        /// Gets or sets the existing connection.
        /// </summary>
        /// <value>
        /// The existing connection.
        /// </value>
        public static ConnectionString ExistingConnection
        {
            get { return existingConnection; }
            set { existingConnection = value; }
        }

        /// <summary>
        /// Gets the rock version.
        /// </summary>
        /// <value>
        /// The rock version.
        /// </value>
        public static string RockVersion
        {
            get
            {
                var rockAssembly = typeof( Rock.Model.EntityType ).Assembly.GetName();
                return rockAssembly.Version.ToString();
            }
        }

        # region Logging

        /// <summary>
        /// Logs the exception.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="message">The message.</param>
        public static void LogException( string category, string message )
        {
            // Rock ExceptionService logger depends on HttpContext.... so write the message to a file
            try
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                directory = Path.Combine( directory, "Logs" );

                if ( !Directory.Exists( directory ) )
                {
                    Directory.CreateDirectory( directory );
                }

                string filePath = Path.Combine( directory, "ExcavatorExceptions.csv" );
                var errmsg = string.Format( "{0},{1},\"{2}\"\r\n", DateTime.Now.ToString(), category, message );
                File.AppendAllText( filePath, errmsg );

                App.Current.Dispatcher.BeginInvoke( (Action)( () => ShowErrorMessage( errmsg ) ) );
            }
            catch
            {
                // failed to write to database and also failed to write to log file, so there is nowhere to log this error
            }
        }

        /// <summary>
        /// The showing error
        /// </summary>
        private static bool ShowingError = false;

        /// <summary>
        /// Shows the error message.
        /// </summary>
        /// <param name="errmsg">The errmsg.</param>
        private static void ShowErrorMessage( string errmsg )
        {
            if ( ShowingError )
                return;
            ShowingError = true;
            var connectWindow = new LogDialog();
            var LogViewModel = new LogViewModel() { Message = errmsg };
            connectWindow.DataContext = LogViewModel;

            connectWindow.Owner = App.Current.MainWindow;
            connectWindow.ShowInTaskbar = false;
            connectWindow.WindowStyle = WindowStyle.None;
            connectWindow.ResizeMode = ResizeMode.NoResize;
            connectWindow.SizeToContent = SizeToContent.WidthAndHeight;
            connectWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var showWindow = connectWindow.ShowDialog();
            ShowingError = false;
        }

        #endregion Logging
    }
}