using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Windows;
using OrcaMDF.Core.Engine;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for FrontEndLoader.xaml
    /// </summary>
    public partial class FrontEndLoader : Window, INotifyPropertyChanged
    {
        #region Initializer Methods

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public FrontEndLoader()
        {
            InitializeComponent();
            Steps = new ObservableCollection<string>();
            Steps.Add( "Connection" );
            Steps.Add( "Transformation" );
            Steps.Add( "Preview" );
            Steps.Add( "Save" );
            Steps.Add( "Complete" );

            excavatorTypes = new List<ExcavatorComponent>();
            foreach ( Type type in Assembly.GetAssembly( typeof( ExcavatorComponent ) ).GetTypes()
                .Where( type => type.IsClass && !type.IsAbstract && type.IsSubclassOf( typeof( ExcavatorComponent ) ) ) )
            {
                excavatorTypes.Add( (ExcavatorComponent)Activator.CreateInstance( type, null ) );
            }

            databaseTypes.ItemsSource = excavatorTypes;
            numProgress = Increment = ( 100 / Steps.Count() );
            navProgress.Style = (Style)this.Resources["NavStyle"];

            // watch this window for changes
            this.DataContext = this;
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
                        dbModel.Load( database );
                    }
                }
                else
                {
                    MessageBox.Show( "Error: Could not read mdf file. Please make sure the file is not in use." );
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnConnect_Click( object sender, RoutedEventArgs e )
        {
            //SqlConnection thisConnection = new SqlConnection( @"Server=(local);Database=Sample_db;Trusted_Connection=Yes;" );
            //thisConnection.Open();

            //string Get_Data = "SELECT * FROM emp";

            //SqlCommand cmd = thisConnection.CreateCommand();
            //cmd.CommandText = Get_Data;

            //SqlDataAdapter sda = new SqlDataAdapter( cmd );
            //DataTable dt = new DataTable( "emp" );
            //sda.Fill( dt );

            //dataGrid1.ItemsSource = dt.DefaultView;
        }    

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            Progress += Increment;

            btnPrevious.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handles the Click event of the btnPrevious control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnPrevious_Click( object sender, RoutedEventArgs e )
        {
            Progress -= Increment;

            if ( Progress / Increment == 1 )
            {
                btnPrevious.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region Progress Class

        /// <summary>
        /// Numeric value of the current progress 
        /// </summary>
        private int numProgress;

        /// <summary>
        /// List of possible excavator types
        /// </summary>
        private List<ExcavatorComponent> excavatorTypes;

        /// <summary>
        /// Gets or sets the increment value.
        /// </summary>
        /// <value>
        /// The increment.
        /// </value>
        public int Increment { get; set; }

        /// <summary>
        /// Gets or sets the progress indicator.
        /// </summary>
        public int Progress
        {
            get { return numProgress; }
            set
            {
                numProgress = value;
                OnPropertyChanged( "Progress" );
            }
        }

        /// <summary>
        /// Gets or sets the process steps.
        /// </summary>        
        public ObservableCollection<string> Steps
        {
            get;
            set;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged( string propertyName )
        {
            if ( PropertyChanged != null )
            {
                PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }

        #endregion

        
    }
}
