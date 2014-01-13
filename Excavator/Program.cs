using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OrcaMDF.Core.Engine;

namespace Excavator
{
    public partial class Program : Form
    {
        public Program()
        {
            InitializeComponent();
        }

        private void Program_Load( object sender, EventArgs e )
        {

        }

        private void mdfPicker_FileOk( object sender, CancelEventArgs e )
        {
            // do stuff here
            

        }

        private void btnUpload_Click( object sender, EventArgs e )
        {
            var mdfPicker = new OpenFileDialog();
            mdfPicker.Filter = "SQL Database files|*.mdf";
            mdfPicker.AddExtension = false;

            if ( mdfPicker.ShowDialog() == DialogResult.OK )
            {
                mdfPicker.
                //var db = new Database(  )

            }
            


            
        }
    }
}
