using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OrcaMDF.Core.Engine;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for FrontEndLoader.xaml
    /// </summary>
    public partial class FrontEndLoader : Window
    {

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
        }

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

        
    }
}
