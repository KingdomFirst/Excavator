// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OrcaMDF.Core.Engine;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Windows.Controls;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for SelectPage.xaml
    /// </summary>
    public partial class SelectPage : Page
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SelectPage"/> class.
        /// </summary>
        public SelectPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the Loaded event of the Page control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var excavator = (ExcavatorComponent)App.Current.Properties["schema"];
            // create the controls for every property inside the schema
            CreateControlUI( excavator.dataset.Tables );
        }




        /// <summary>
        /// Creates the control UI.
        /// </summary>
        /// <param name="elements">The elements.</param>
        private void CreateControlUI( DataTableCollection elements )
        {
            // set tree view to display the element and any properties beneath it
            foreach( var element in elements )
            {
                // get table name
                // get table row and all containing columns
            }

            // set treeView.Items = list
        }
    }
}
