﻿// <copyright>
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
        public ObservableCollection<TableNode> nodes { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectPage"/> class.
        /// </summary>
        public SelectPage()
        {
            nodes = new ObservableCollection<TableNode>();
            InitializeComponent();

            var excavator = (ExcavatorComponent)App.Current.Properties["excavator"];
            if ( excavator != null )
            {
                CreateControlUI( excavator.dataset.Tables );
            }
            else
            {
                lblNoData.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Hidden;
                lblSelectFields.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Creates the control UI.
        /// </summary>
        /// <param name="tables">The elements.</param>
        private void CreateControlUI( DataTableCollection tables )
        {
            // set tree view to display the table and any properties beneath it
            foreach( DataTable table in tables )
            {
                var tableItem = new TableNode();
                tableItem.Text = table.TableName;
                foreach( DataColumn column in table.Columns )
                {
                    var childItem = new TableNode();
                    childItem.Text = column.ColumnName;
                    childItem.Table.Add( tableItem );
                    tableItem.Columns.Add( childItem );
                }
                
                nodes.Add( tableItem );
            }

            treeView.ItemsSource = nodes;
        }

        /// <summary>
        /// Called when the checkbox is clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void OnCheck( object sender, MouseButtonEventArgs e )
        {
            var selected = (CheckBox)sender;
            if ( selected != null )
            {
                SelectedId.Id = selected.Uid;
            }            
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            var test = treeView.Items;

        }       
    }
}
