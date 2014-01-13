//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using OrcaMDF.Core.Engine;

namespace Excavator
{
    /// <summary>
    /// 
    /// </summary>
    public partial class FrontEndLoader : Form
    {
        #region Control Methods 

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public FrontEndLoader()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the Load event of the Program control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Program_Load( object sender, EventArgs e )
        {
            //var dbInterfaces = typeof( ExcavatorComponent ).GetInterfaces();

            var excavatorTypes = new List<ExcavatorComponent>();
            foreach ( Type type in
                Assembly.GetAssembly( typeof( ExcavatorComponent ) ).GetTypes()
                .Where( myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf( typeof( ExcavatorComponent ) ) ) )
            {
                excavatorTypes.Add( (ExcavatorComponent)Activator.CreateInstance( type, null ) );
            }
                        
            lstDatabaseType.DataSource = excavatorTypes;

        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnUpload control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnUpload_Click( object sender, EventArgs e )
        {
            var mdfPicker = new OpenFileDialog();
            mdfPicker.Filter = "SQL Database files|*.mdf";
            mdfPicker.AddExtension = false;

            if ( mdfPicker.ShowDialog() == DialogResult.OK )
            {
                var db = new Database( mdfPicker.FileName );
                if ( db != null )
                {
                    var dbType = lstDatabaseType.SelectedValue.ToString();
                }
                else
                {
                    MessageBox.Show( "Error: Could not read mdf file." );
                }
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the lstDatabaseType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void lstDatabaseType_SelectedIndexChanged( object sender, EventArgs e )
        {

        }

        #endregion 
    }
}
