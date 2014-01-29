using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Collections;
using System;

namespace Excavator
{
    /// <summary>
    /// Holds a reference to the currently selected table or column
    /// </summary>
    public struct SelectedId
    {
        public static string Id;
    }

    /// <summary>
    /// Database node holds a reference to any other nodes around it in the tree
    /// </summary>
    public class TableNode : INotifyPropertyChanged
    {
        #region Fields

        private ObservableCollection<TableNode> _columns;
        private ObservableCollection<TableNode> _table;
        private bool? _isChecked;
        private string _text;
        private string _id;        

        /// <summary>
        /// Gets or sets if the node is checked.
        /// </summary>
        /// <value>
        /// The is checked.
        /// </value>
        public bool? Checked
        {
            get { return this._isChecked; }
            set
            {
                this._isChecked = value;
                RaisePropertyChanged( "Checked" );
            }
        }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>
        /// The text.
        /// </value>
        public string Text
        {
            get { return this._text; }
            set
            {
                this._text = value;
                RaisePropertyChanged( "Text" );
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
            get { return this._id; }
            set
            {
                this._id = value;
            }
        }

        /// <summary>
        /// Gets the child nodes.
        /// </summary>
        /// <value>
        /// The child nodes.
        /// </value>
        public ObservableCollection<TableNode> Columns
        {
            get { return this._columns; }
        }

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        /// <value>
        /// The parent node.
        /// </value>
        public ObservableCollection<TableNode> Table
        {
            get { return this._table; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TableNode"/> class.
        /// </summary>
        public TableNode()
        {
            _isChecked = true;
            this._id = Guid.NewGuid().ToString();
            _columns = new ObservableCollection<TableNode>();
            _table = new ObservableCollection<TableNode>();            
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
            if ( this.PropertyChanged != null )
                this.PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            int countCheck = 0;
            if ( propertyName == "Checked" )
            {
                if ( this.Id == SelectedId.Id && this.Table.Count == 0 && this.Columns.Count != 0 )
                {
                    SetParent( this.Columns, this.Checked );
                }                
                if ( this.Id == SelectedId.Id && this.Table.Count > 0 && this.Columns.Count == 0 )
                {
                    SetChild( this.Table, countCheck );
                }
            }
        }

        /// <summary>
        /// Sets the parent node checked.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="isChecked">The is checked.</param>
        private void SetParent( ObservableCollection<TableNode> items, bool? isChecked )
        {
            foreach ( TableNode item in items )
            {
                item.Checked = isChecked;
                if ( item.Columns.Count != 0 ) SetParent( item.Columns, isChecked );
            }
        }

        /// <summary>
        /// Sets the child node checked.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="countCheck">The count check.</param>
        private void SetChild( ObservableCollection<TableNode> items, int countCheck )
        {
            bool isNull = false;
            foreach ( TableNode paren in items )
            {
                foreach ( TableNode child in paren.Columns )
                {
                    if ( child.Checked == true || child.Checked == null )
                    {
                        countCheck++;
                        if ( child.Checked == null )
                            isNull = true;
                    }
                }
                if ( countCheck != paren.Columns.Count && countCheck != 0 ) paren.Checked = null;
                else if ( countCheck == 0 ) paren.Checked = false;
                else if ( countCheck == paren.Columns.Count && isNull ) paren.Checked = null;
                else if ( countCheck == paren.Columns.Count && !isNull ) paren.Checked = true;
                if ( paren.Table.Count != 0 ) SetChild( paren.Table, 0 );
            }
        }

        #endregion

    }    
}
