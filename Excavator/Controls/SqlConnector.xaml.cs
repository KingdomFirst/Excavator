// The MIT License (MIT)

// Copyright (c) 2014 Jake Ginnivan

// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Excavator.Utility;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Excavator
{
    /// <summary>
    /// Modified SQL Connector control, originally by Jake Ginnivan
    /// http://github.com/JakeGinnivan/SqlConnectionControl
    /// Licensed under the MIT open-source license, 2014
    /// </summary>
    public partial class SqlConnector : INotifyPropertyChanged
    {
        #region Fields

        private string _lastServer;
        private string _header = "SQL Server Connection";
        private readonly SqlTasks _smoTasks;
        private static ObservableCollection<string> _servers;
        private static readonly object ServersLock = new object();

        private readonly BackgroundWorker _dbLoader = new BackgroundWorker();

        private readonly ObservableCollection<string> _databases = new ObservableCollection<string>();

        private static readonly ConnectionString DefaultValue = new ConnectionString();

        private static void ConnectionStringChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            var builder = (SqlConnector)d;
            if ( e.NewValue == null )
            {
                builder.Dispatcher.BeginInvoke( (Action)( () => d.SetValue( ConnectionStringProperty, DefaultValue ) ) );
            }
            else
            {
                builder.RegisterNewConnectionString( (ConnectionString)e.NewValue );
            }
        }

        private void OnPropertyChanged( params string[] propertyNames )
        {
            if ( PropertyChanged == null ) return;

            foreach ( var propertyName in propertyNames )
            {
                PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty ConnectionStringProperty = DependencyProperty.Register( "ConnectionString"
            , typeof( ConnectionString ), typeof( SqlConnector ), new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                ConnectionStringChanged
            )
        );

        public ConnectionString ConnectionString
        {
            get
            {
                var currentString = (ConnectionString)GetValue( ConnectionStringProperty );
                if ( currentString == null )
                {
                    currentString = new ConnectionString();
                }

                return currentString;
            }
            set
            {
                SetValue( ConnectionStringProperty, value );
            }
        }

        public ObservableCollection<string> Servers
        {
            get
            {
                lock ( ServersLock )
                {
                    if ( _servers == null || _servers.Count == 0 )
                    {
                        _servers = new ObservableCollection<string>();
                        LoadServersAsync();
                    }
                }

                return _servers;
            }
        }

        public ObservableCollection<string> Databases
        {
            get { return _databases; }
        }

        public string Header
        {
            get { return _header; }
            set
            {
                _header = value;
                OnPropertyChanged( "Header" );
            }
        }

        private void ValidateConnection( object sender, RoutedEventArgs e )
        {
            if ( ConnectionString.IntegratedSecurity && !string.IsNullOrEmpty( ConnectionString.Password ) )
            {
                ConnectionString.Password = string.Empty;
                ConnectionString.UserName = string.Empty;
            }
            OnPropertyChanged( "ConnectionString" );
        }

        public bool DatabasesLoading
        {
            get
            {
                return _dbLoader.IsBusy;
            }
        }

        #endregion Fields

        #region Constructor

        public SqlConnector()
            : this( new SqlTasks() )
        {
            InitializeComponent();
        }

        public SqlConnector( SqlTasks smoTasks )
        {
            ConnectionString.Database = string.Empty;
            _smoTasks = smoTasks;
            _dbLoader.DoWork += DbLoaderDoWork;
            _dbLoader.RunWorkerCompleted += DbLoaderRunWorkerCompleted;
        }

        #endregion Constructor

        #region Internal Methods

        public static void SetConnectionString( DependencyObject dp, ConnectionString value )
        {
            dp.SetValue( ConnectionStringProperty, value );
        }

        public static ConnectionString GetConnectionString( DependencyObject dp )
        {
            return (ConnectionString)dp.GetValue( ConnectionStringProperty );
        }

        private void RegisterNewConnectionString( ConnectionString newValue )
        {
            if ( newValue != null )
            {
                ConnectionStringPropertyChanged( this, new PropertyChangedEventArgs( "Server" ) ); //takes care of initialization of the database list
                newValue.PropertyChanged += ConnectionStringPropertyChanged;
            }
        }

        private void ConnectionStringPropertyChanged( object sender, PropertyChangedEventArgs e )
        {
            //Server has changed, reload
            if ( ( e.PropertyName.Equals( "Server" ) || e.PropertyName.Equals( "IntegratedSecurity" ) ) && !_dbLoader.IsBusy )
            {
                _dbLoader.RunWorkerAsync( ConnectionString );
                OnPropertyChanged( "DatabasesLoading" );
            }

            var binding = GetBindingExpression( ConnectionStringProperty );
            if ( binding != null )
            {
                binding.UpdateSource();
            }
        }

        #endregion Internal Methods

        #region Async Tasks

        private void DbLoaderDoWork( object sender, DoWorkEventArgs e )
        {
            var connString = e.Argument as ConnectionString;

            if ( connString == null ) return;
            if ( string.IsNullOrEmpty( connString.Server ) ) return;

            _lastServer = connString.Server;
            e.Result = _smoTasks.GetDatabases( connString );
        }

        private void DbLoaderRunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            _databases.Clear();
            if ( e.Error == null )
            {
                var databases = e.Result as List<string>;
                if ( databases == null ) return;

                _lastServer = null;
                foreach ( var database in databases.OrderBy( d => d ) )
                {
                    _databases.Add( database );
                }
            }
            else if ( ConnectionString.Server != _lastServer )
            {
                _dbLoader.RunWorkerAsync( ConnectionString );
                return;
            }

            //this breaks the binding so it's removed and the equivalent action is added below it.
            //this.Dispatcher.Invoke( (Action)( () =>
            //{
            //    SqlDatabaseName.SelectedItem = _databases.FirstOrDefault();
            //} ) );
            ConnectionString.Database = _databases.FirstOrDefault();

            OnPropertyChanged( "DatabasesLoading" );
        }

        private void LoadServersAsync()
        {
            var serverLoader = new BackgroundWorker();
            serverLoader.DoWork += ( ( sender, e ) => e.Result = _smoTasks.SqlServers.OrderBy( r => r ).ToArray() );

            serverLoader.RunWorkerCompleted += ( ( sender, e ) =>
            {
                foreach ( var server in (string[])e.Result )
                {
                    _servers.Add( server );
                }
            } );

            serverLoader.RunWorkerAsync();
        }

        #endregion Async Tasks
    }

    /// <summary>
    /// Holds the current connection string
    /// </summary>
    public class ConnectionString : INotifyPropertyChanged
    {
        #region Fields

        private readonly SqlConnectionStringBuilder _builder = new SqlConnectionStringBuilder
        {
        };

        private void OnPropertyChanged( string propertyName )
        {
            if ( PropertyChanged == null ) return;

            PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Server
        {
            get
            {
                return _builder.DataSource;
            }
            set
            {
                if ( _builder.DataSource == value ) return;
                _builder.DataSource = value;
                OnPropertyChanged( "Server" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public string Database
        {
            get
            {
                return _builder.InitialCatalog;
            }
            set
            {
                if ( _builder.InitialCatalog == value ) return;
                _builder.InitialCatalog = value;
                OnPropertyChanged( "Database" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public string UserName
        {
            get
            {
                return _builder.UserID;
            }
            set
            {
                if ( _builder.UserID == value ) return;
                _builder.UserID = value;
                OnPropertyChanged( "UserName" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public bool MultipleActiveResultSets
        {
            get
            {
                return _builder.MultipleActiveResultSets;
            }
            set
            {
                if ( _builder.MultipleActiveResultSets == value ) return;
                _builder.MultipleActiveResultSets = value;
                OnPropertyChanged( "MultipleActiveResultSets" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public int ConnectionTimeout
        {
            get
            {
                return _builder.ConnectTimeout;
            }
            set
            {
                if ( _builder.ConnectTimeout == value ) return;
                _builder.ConnectTimeout = value;
                OnPropertyChanged( "ConnectionTimeout" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public string Password
        {
            get
            {
                return _builder.Password;
            }
            set
            {
                if ( _builder.Password == value ) return;
                _builder.Password = value;
                OnPropertyChanged( "Password" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public bool IntegratedSecurity
        {
            get
            {
                return _builder.IntegratedSecurity;
            }
            set
            {
                if ( _builder.IntegratedSecurity == value ) return;
                _builder.IntegratedSecurity = value;
                OnPropertyChanged( "IntegratedSecurity" );
                OnPropertyChanged( "IsValid" );
            }
        }

        public bool IsValid()
        {
            return
                ( !string.IsNullOrEmpty( Server ) && Server.EndsWith( ".sdf" ) ) ||
                ( !string.IsNullOrEmpty( Server ) &&
                 !string.IsNullOrEmpty( Database ) &&
                 ( IntegratedSecurity || ( !string.IsNullOrEmpty( UserName ) && !string.IsNullOrEmpty( Password ) ) ) );
        }

        #endregion Fields

        #region Constructor

        public ConnectionString()
        { }

        public ConnectionString( string connectionString )
        {
            _builder.ConnectionString = connectionString;
        }

        // Create a copy of this connection string with the specified database instead of the current
        public ConnectionString WithDatabase( string databaseName )
        {
            return new ConnectionString
            {
                Server = Server,
                Database = databaseName,
                IntegratedSecurity = IntegratedSecurity,
                UserName = UserName,
                Password = Password,
                MultipleActiveResultSets = MultipleActiveResultSets,
                ConnectionTimeout = ConnectionTimeout
            };
        }

        #endregion Constructor

        #region Internal Methods

        public static implicit operator string( ConnectionString connectionString )
        {
            return connectionString.ToString();
        }

        public override string ToString()
        {
            if ( Server.EndsWith( ".sdf" ) )
            {
                if ( string.IsNullOrEmpty( Password ) )
                {
                    return new SqlConnectionStringBuilder { DataSource = Server }.ConnectionString;
                }
                else
                {
                    return new SqlConnectionStringBuilder { DataSource = Server, Password = Password }.ConnectionString;
                }
            }

            return _builder.ConnectionString;
        }

        #endregion Internal Methods
    }

    /// <summary>
    /// Contains background tasks that fire when the control is updated
    /// </summary>
    public class SqlTasks
    {
        public IEnumerable<string> SqlServers
        {
            get
            {
                return SmoApplication.EnumAvailableSqlServers().AsEnumerable()
                    .Select( r => r["Name"].ToString() );
            }
        }

        public List<string> GetDatabases( ConnectionString connectionString )
        {
            var databases = new List<string>();
            using ( var conn = new SqlConnection( connectionString.WithDatabase( "master" ) ) )
            {
                try
                {
                    conn.Open();
                    var serverConnection = new ServerConnection( conn );
                    var server = new Server( serverConnection );
                    databases.AddRange( from Database database in server.Databases select database.Name );
                }
                catch ( SqlException e )
                {
                    databases.Add( e.Errors[0].Message.Take( 50 ).ToString() );
                }
            }

            return databases;
        }
    }
}