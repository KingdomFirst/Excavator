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

namespace Excavator
{
    /// <summary>
    /// Holds a reference to the currently selected table or column
    /// </summary>
    public class SelectedId
    {
        public static string Id;
    }

    /// <summary>
    /// DatabaseNode is an abstraction of a table and any contained columns
    /// </summary>
    public class DatabaseNode : INotifyPropertyChanged
    {
        #region Fields

        private ObservableCollection<DatabaseNode> _columns;
        private ObservableCollection<DatabaseNode> _table;
        private bool? _isChecked;
        private Type _nodeType;
        private string _name;
        private string _id;
        private object _value;

        /// <summary>
        /// Gets or sets if the node is checked.
        /// </summary>
        /// <value>
        /// The is checked.
        /// </value>
        public bool? Checked
        {
            get
            {
                return _isChecked;
            }
            set
            {
                _isChecked = value;
                RaisePropertyChanged( "Checked" );
            }
        }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>
        /// The text.
        /// </value>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                RaisePropertyChanged( "Name" );
            }
        }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }

        /// <summary>
        /// Gets or sets the type of the node.
        /// </summary>
        /// <value>
        /// The type of the node.
        /// </value>
        public Type NodeType
        {
            get
            {
                return _nodeType;
            }
            set
            {
                _nodeType = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the node.
        /// </summary>
        /// <value>
        /// The value of the node.
        /// </value>
        public object Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        /// <summary>
        /// Gets the child nodes.
        /// </summary>
        /// <value>
        /// The child nodes.
        /// </value>
        public ObservableCollection<DatabaseNode> Columns
        {
            get
            {
                return _columns;
            }
        }

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        /// <value>
        /// The parent node.
        /// </value>
        public ObservableCollection<DatabaseNode> Table
        {
            get
            {
                return _table;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseNode"/> class.
        /// </summary>
        public DatabaseNode()
        {
            _isChecked = true;
            _id = Guid.NewGuid().ToString().ToUpper();
            _columns = new ObservableCollection<DatabaseNode>();
            _table = new ObservableCollection<DatabaseNode>();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Fires on the property changed event.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void RaisePropertyChanged( string propertyName )
        {
            if ( PropertyChanged != null )
            {
                PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }

            int countCheck = 0;
            if ( propertyName == "Checked" )
            {
                if ( Id == SelectedId.Id && Table.Count == 0 && Columns.Count != 0 )
                {
                    SetParent( Columns, Checked );
                }
                if ( Id == SelectedId.Id && Table.Count > 0 && Columns.Count == 0 )
                {
                    SetChild( Table, countCheck );
                }
            }
        }

        /// <summary>
        /// Sets the parent node checked.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="isChecked">The is checked.</param>
        private void SetParent( ObservableCollection<DatabaseNode> items, bool? isChecked )
        {
            foreach ( DatabaseNode item in items )
            {
                item.Checked = isChecked;
                if ( item.Columns.Count != 0 )
                {
                    SetParent( item.Columns, isChecked );
                }
            }
        }

        /// <summary>
        /// Sets the child node checked.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="countCheck">The count check.</param>
        private void SetChild( ObservableCollection<DatabaseNode> items, int countCheck )
        {
            bool complete = false;
            foreach ( var item in items )
            {
                foreach ( var column in item.Columns )
                {
                    if ( column.Checked == true || column.Checked == null )
                    {
                        countCheck++;
                        if ( column.Checked == null )
                            complete = true;
                    }
                }

                if ( countCheck != item.Columns.Count && countCheck != 0 )
                {
                    item.Checked = null;
                }
                else if ( countCheck == 0 )
                {
                    item.Checked = false;
                }
                else if ( countCheck == item.Columns.Count && complete )
                {
                    item.Checked = null;
                }
                else if ( countCheck == item.Columns.Count && !complete )
                {
                    item.Checked = true;
                }

                if ( item.Table.Count != 0 )
                {
                    SetChild( item.Table, 0 );
                }
            }
        }

        #endregion
    }
}
