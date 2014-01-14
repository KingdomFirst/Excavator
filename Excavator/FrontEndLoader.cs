//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using System;
using System.Collections.Generic;
using System.Drawing;
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

            tabNavigation.DrawItem += new DrawItemEventHandler( tabNavigation_DrawItem );
        }

        /// <summary>
        /// Handles the Load event of the Program control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Program_Load( object sender, EventArgs e )
        {
            var excavatorTypes = new List<ExcavatorComponent>();
            foreach ( Type type in
                Assembly.GetAssembly( typeof( ExcavatorComponent ) ).GetTypes()
                .Where( type => type.IsClass && !type.IsAbstract && type.IsSubclassOf( typeof( ExcavatorComponent ) ) ) )
            {
                excavatorTypes.Add( (ExcavatorComponent)Activator.CreateInstance( type, null ) );
            }

            lstDatabaseType.DataSource = excavatorTypes;
            lstDatabaseType.Enabled = ( excavatorTypes.Count.Equals( 1 ) );
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
                    MessageBox.Show( "Error: Could not read mdf file. Please make sure the file is not in use." );
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

        #region Internal Methods

        private void tabNavigation_DrawItem( object sender, DrawItemEventArgs e )
        {
            var formView = e.Graphics;
            var textBrush = new SolidBrush( e.ForeColor );

            var currentTab = tabNavigation.TabPages[e.Index];
            var tabBoundary = tabNavigation.GetTabRect( e.Index );

            if ( e.State == DrawItemState.Selected )            
            {
                textBrush = new SolidBrush( Color.White );
                formView.FillRectangle( Brushes.Gray, e.Bounds );
            }
            //else
            //{
            //    textBrush = new SolidBrush( e.ForeColor );
            //    e.DrawBackground();
            //}

            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };            
            formView.DrawString( currentTab.Text, this.Font, textBrush, tabBoundary, new StringFormat( format ) );
        }


        #endregion

    }
}
