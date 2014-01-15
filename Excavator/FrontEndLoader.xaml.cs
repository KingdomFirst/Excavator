using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
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
        #region Fields

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
        public ObservableCollection<string> Process { get; set; }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Initializer Methods

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public FrontEndLoader()
        {
            InitializeComponent();

            excavatorTypes = new List<ExcavatorComponent>();
            foreach ( Type type in Assembly.GetAssembly( typeof( ExcavatorComponent ) ).GetTypes()
                .Where( type => type.IsClass && !type.IsAbstract && type.IsSubclassOf( typeof( ExcavatorComponent ) ) ) )
            {
                excavatorTypes.Add( (ExcavatorComponent)Activator.CreateInstance( type, null ) );
            }

            Process = new ObservableCollection<string>();
            Process.Add( "Connection" );
            Process.Add( "Transformation" );
            Process.Add( "Preview" );
            Process.Add( "Save" );
            Process.Add( "Complete" );

            databaseTypes.ItemsSource = excavatorTypes;
            numProgress = Increment = ( 100 / Process.Count() );
            //DisplayProgressBar();

            // watch this window for changes
            this.DataContext = this;
        }

        /// <summary>
        /// Adds the progress bar.
        /// </summary>
        private void DisplayProgressBar()
        {
            var pb = new NavigationBar();
            pb.Name = "navProgress";
            pb.ItemsSource = Process;
            pb.Progress = Progress;
            pb.SnapsToDevicePixels = true;
            pb.Margin = new Thickness( 30 );            
            pb.Foreground = Brushes.SlateBlue;
            pb.Style = (Style)FindResource( "NavigationBar" );
            grdExcavator.Children.Add( pb );
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
