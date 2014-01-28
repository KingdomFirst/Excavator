using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Collections;
using System;

namespace Excavator
{
    /// <summary>
    /// Database node holds a reference to any other nodes around it in the tree
    /// </summary>
    public class DatabaseNode : INotifyPropertyChanged
    {
        #region Fields

        private ObservableCollection<DatabaseNode> children;
        private ObservableCollection<DatabaseNode> parent;
        private bool? isChecked;
        private string text;
        private string id;        

        /// <summary>
        /// Gets or sets if the node is checked.
        /// </summary>
        /// <value>
        /// The is checked.
        /// </value>
        public bool? IsChecked
        {
            get { return this.isChecked; }
            set
            {
                this.isChecked = value;
                RaisePropertyChanged( "IsChecked" );
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
            get { return this.text; }
            set
            {
                this.text = value;
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
            get { return this.id; }
            set
            {
                this.id = value;
            }
        }

        /// <summary>
        /// Gets the child nodes.
        /// </summary>
        /// <value>
        /// The child nodes.
        /// </value>
        public ObservableCollection<DatabaseNode> ChildNodes
        {
            get { return this.children; }
        }

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        /// <value>
        /// The parent node.
        /// </value>
        public ObservableCollection<DatabaseNode> ParentNode
        {
            get { return this.parent; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseNode"/> class.
        /// </summary>
        public DatabaseNode()
        {
            this.id = Guid.NewGuid().ToString();
            children = new ObservableCollection<DatabaseNode>();
            parent = new ObservableCollection<DatabaseNode>();
            isChecked = true;
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
            if ( propertyName == "IsChecked" )
            {
                if ( this.Id == SelectedNode.Id && this.ParentNode.Count == 0 && this.ChildNodes.Count != 0 )
                {
                    SetParent( this.ChildNodes, this.IsChecked );
                }                
                if ( this.Id == SelectedNode.Id && this.ParentNode.Count > 0 && this.ChildNodes.Count == 0 )
                {
                    SetChild( this.ParentNode, countCheck );
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
                item.IsChecked = isChecked;
                if ( item.ChildNodes.Count != 0 ) SetParent( item.ChildNodes, isChecked );
            }
        }

        /// <summary>
        /// Sets the child node checked.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="countCheck">The count check.</param>
        private void SetChild( ObservableCollection<DatabaseNode> items, int countCheck )
        {
            bool isNull = false;
            foreach ( DatabaseNode paren in items )
            {
                foreach ( DatabaseNode child in paren.ChildNodes )
                {
                    if ( child.IsChecked == true || child.IsChecked == null )
                    {
                        countCheck++;
                        if ( child.IsChecked == null )
                            isNull = true;
                    }
                }
                if ( countCheck != paren.ChildNodes.Count && countCheck != 0 ) paren.IsChecked = null;
                else if ( countCheck == 0 ) paren.IsChecked = false;
                else if ( countCheck == paren.ChildNodes.Count && isNull ) paren.IsChecked = null;
                else if ( countCheck == paren.ChildNodes.Count && !isNull ) paren.IsChecked = true;
                if ( paren.ParentNode.Count != 0 ) SetChild( paren.ParentNode, 0 );
            }
        }

        #endregion

    }

    /// <summary>
    /// Holds a reference to the selected node
    /// </summary>
    public struct SelectedNode
    {
        public static string Id;
    }
}
