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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Excavator.Utility;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for SelectPage.xaml
    /// </summary>
    public partial class SelectPage : Page
    {
        private ExcavatorComponent excavator;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectPage"/> class.
        /// </summary>
        public SelectPage( ExcavatorComponent parameter = null )
        {
            InitializeComponent();
            if ( parameter != null )
            {
                excavator = parameter;
                if ( excavator.DataNodes.Count > 0 )
                {
                    excavator.DataNodes[0].Checked = true; //preview on load
                    PreviewData( excavator.DataNodes[0].Id );
                }
                treeView.ItemsSource = new ObservableCollection<DataNode>( excavator.DataNodes );
            }
            else
            {
                lblNoData.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Hidden;
                grdPreviewData.Visibility = Visibility.Hidden;
            }
        }

        #endregion Constructor

        #region Events

        /// <summary>
        /// Called when the checkbox is clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void Checkbox_OnClick( object sender, MouseButtonEventArgs e )
        {
            var selected = (CheckBox)sender;
            if ( selected != null )
            {
                SelectedId.Id = selected.Uid;
            }
        }

        /// <summary>
        /// Handles the MouseDown event of the TextBlock control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void TextBlock_MouseDown( object sender, MouseButtonEventArgs e )
        {
            var textBlock = (TextBlock)sender;
            if ( textBlock != null )
            {
                PreviewData( (string)textBlock.Tag );
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the TextBlock control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
        private void TextBlock_KeyDown( object sender, KeyEventArgs e )
        {
            var textBlock = (TextBlock)sender;
            if ( textBlock != null )
            {
                PreviewData( (string)textBlock.Tag );
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSelectAll control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnSelectAll_Click( object sender, RoutedEventArgs e )
        {
            foreach ( var node in excavator.DataNodes )
            {
                node.Checked = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the btnUnselectAll control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnUnselectAll_Click( object sender, RoutedEventArgs e )
        {
            foreach ( var node in excavator.DataNodes )
            {
                node.Checked = false;
            }
        }

        /// <summary>
        /// Handles the Click event of the btnBack control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnBack_Click( object sender, RoutedEventArgs e )
        {
            this.NavigationService.GoBack();
        }

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            var transformPage = new ConfigurationPage( excavator );
            this.NavigationService.Navigate( transformPage );
        }

        #endregion Events

        #region Async Tasks

        /// <summary>
        /// Previews the data for the selected node.
        /// </summary>
        /// <param name="tableNode">The table node.</param>
        private void PreviewData( string selectedNodeId )
        {
            BackgroundWorker bwLoadPreview = new BackgroundWorker();
            bwLoadPreview.DoWork += bwLoadPreview_DoWork;
            bwLoadPreview.RunWorkerCompleted += bwLoadPreview_RunWorkerCompleted;
            bwLoadPreview.RunWorkerAsync( selectedNodeId );
        }

        /// <summary>
        /// Handles the DoWork event of the bwLoadPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwLoadPreview_DoWork( object sender, DoWorkEventArgs e )
        {
            e.Result = excavator.PreviewData( (string)e.Argument );
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwLoadPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwLoadPreview_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            if ( e.Error == null && (DataTable)e.Result != null )
            {
                DataTable tablePreview = e.Result as DataTable;
                this.Dispatcher.Invoke( (Action)( () =>
                {
                    grdPreviewData.ItemsSource = tablePreview.DefaultView;
                    grdPreviewData.Visibility = Visibility.Visible;
                    lblEmptyDataset.Visibility = Visibility.Hidden;
                } ) );
            }
            else
            {
                lblEmptyDataset.Visibility = Visibility.Visible;
                grdPreviewData.Visibility = Visibility.Hidden;
            }
        }

        #endregion Async Tasks
    }
}