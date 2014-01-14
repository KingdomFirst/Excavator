using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using OrcaMDF.Core.Engine;
using System.ComponentModel;
using System.Collections.ObjectModel;
using ProgressBar;

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

            var excavatorTypes = new List<ExcavatorComponent>();
            foreach ( Type type in
                Assembly.GetAssembly( typeof( ExcavatorComponent ) ).GetTypes()
                .Where( type => type.IsClass && !type.IsAbstract && type.IsSubclassOf( typeof( ExcavatorComponent ) ) ) )
            {
                excavatorTypes.Add( (ExcavatorComponent)Activator.CreateInstance( type, null ) );
            }

            lstDatabaseType.ItemsSource = excavatorTypes;

            Process = new ObservableCollection<string>();
            Process.Add( "Connection" );
            Process.Add( "Transformation" );
            Process.Add( "Preview" );
            Process.Add( "Save" );
            Process.Add( "Complete" );
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
                var db = new Database( mdfPicker.FileName );
                if ( db != null )
                {
                    var dbType = lstDatabaseType.SelectedValue.ToString();
                }
                else
                {
                    MessageBox.Show( "Error: Could not read mdf file. Please make sure the file is not in use." );
                }
            }
        }

        /// <summary>
        /// Called when the indicated property is changed.
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
