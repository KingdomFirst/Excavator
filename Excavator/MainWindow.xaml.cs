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
using System.Windows;
using System.Windows.Input;
using System.Configuration;
using System.Windows.Navigation;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for FrontEndLoader.xaml
    /// </summary>
    public partial class MainWindow : NavigationWindow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();            
        }

        /// <summary>
        /// Goes to the page the execute handler specifies.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ExecutedRoutedEventArgs"/> instance containing the event data.</param>
        private void GoToPageExecuteHandler( object sender, ExecutedRoutedEventArgs e )
        {
            NavigationService.Navigate( new Uri( (string)e.Parameter, UriKind.Relative ) );
        }

        /// <summary>
        /// Sets whether the event handler can execute.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="CanExecuteRoutedEventArgs"/> instance containing the event data.</param>
        private void GoToPageCanExecuteHandler( object sender, CanExecuteRoutedEventArgs e )
        {
            e.CanExecute = true;
        }
    }
}
