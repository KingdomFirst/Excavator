using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Effects;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            SetNavigationSteps();

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

        public void SetNavigationSteps()
        {
            Steps = new ObservableCollection<string>();
            Steps.Add( "Connection" );
            Steps.Add( "Transformation" );
            Steps.Add( "Preview" );
            Steps.Add( "Save" );
            Steps.Add( "Complete" );
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
        /// Handles the Click event of the btnOk control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnOk_Click( object sender, RoutedEventArgs e )
        {

        }

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnCancel_Click( object sender, RoutedEventArgs e )
        {

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
