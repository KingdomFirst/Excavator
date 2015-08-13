using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// Partial of BinaryFile import that holds a Person map
    /// </summary>
    public partial class BinaryFileComponent
    {
        public void MapPeople( DataNode zipFile )
        {
            var asdf = ConfigurationManager.AppSettings;
            var storageLocation = FileTypes;
        }
    }
}