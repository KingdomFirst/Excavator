using System;
using System.IO;
using System.Windows;

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
