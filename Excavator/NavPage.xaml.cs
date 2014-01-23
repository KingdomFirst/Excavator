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

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for NavPage.xaml
    /// </summary>
    public partial class NavPage : Page, INotifyPropertyChanged
    {
        /// <summary>
        /// Numeric value of the current progress 
        /// </summary>
        private int numProgress;

        #region Initialize

        /// <summary>
        /// Initializes a new instance of the <see cref="NavPage"/> class.
        /// </summary>
        public NavPage()
        {
            InitializeComponent();
            SetNavigationSteps();

            numProgress = Increment = ( 100 / Steps.Count() );
            navProgress.Style = (Style)this.Resources["NavStyle"];

            // watch this window for changes
            this.DataContext = this;
        }

        /// <summary>
        /// Sets the navigation steps.
        /// </summary>
        public void SetNavigationSteps()
        {
            Steps = new ObservableCollection<string>();
            Steps.Add( "Connect" );
            Steps.Add( "Transform" );
            Steps.Add( "Preview" );
            Steps.Add( "Save" );
            Steps.Add( "Complete" );
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnNext control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnNext_Click( object sender, RoutedEventArgs e )
        {
            if ( Progress == Increment )
            {
                btnPrevious.Visibility = Visibility.Visible;
            }

            Progress += Increment;
        }

        /// <summary>
        /// Handles the Click event of the btnPrevious control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnPrevious_Click( object sender, RoutedEventArgs e )
        {
            Progress -= Increment;

            if ( Progress / Increment == 1 )
            {
                btnPrevious.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region Progress Methods

        /// <summary>
        /// Gets or sets the increment value.
        /// </summary>
        /// <value>
        /// The increment.
        /// </value>
        public int Increment { get; set; }

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
        public ObservableCollection<string> Steps
        {
            get;
            set;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when [property changed].
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
