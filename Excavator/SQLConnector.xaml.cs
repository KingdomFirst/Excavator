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
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Excavator
{
    /// <summary>
    /// SQL Connector control, implemented by Jake Ginnivan (http://github.com/JakeGinnivan/SqlConnectionControl)
    /// Licensed under the MIT open-source license, 2014
    /// </summary>
    [ContentProperty( "Connection" )]
    public partial class SQLConnector : INotifyPropertyChanged
    {
        private readonly SqlConnectorTasks sqlTasks;
        private static ObservableCollection<string> servers;
        private static readonly object LockServers = new object();

        private readonly BackgroundWorker dbLoader = new BackgroundWorker();
        private readonly ObservableCollection<string> databases = new ObservableCollection<string>();

        public event PropertyChangedEventHandler PropertyChanged;

        private string lastServer;
        private string headerText = "Sql Configuration";
        private bool serversLoading;

        private static readonly ConnectionString DefaultValue = new ConnectionString
        {
            IntegratedSecurity = false,
            MultipleActiveResultSets = true
        };

        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register( "Connections",
            typeof( FrameworkElement ),
            typeof( SQLConnector ) );

        public static readonly DependencyProperty ConnectionStringProperty = DependencyProperty.Register( "ConnectionString",
            typeof( ConnectionString ),
            typeof( SQLConnector ),
            new FrameworkPropertyMetadata(
                DefaultValue,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                ConnectionStringChanged
            )
        );

        private static void ConnectionStringChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            var builder = (SQLConnector)d;
            if ( e.NewValue == null )
            {
                builder.Dispatcher.BeginInvoke( (Action)( () => d.SetValue( ConnectionStringProperty, DefaultValue ) ) );
            }
            else
            {
                builder.RegisterNewConnectionString( (ConnectionString)e.NewValue );
            }
        }

        public SQLConnector()
            : this( new SqlConnectorTasks() )
        {
            InitializeComponent();
        }

        public ConnectionString ConnectionString
        {
            get { return (ConnectionString)GetValue( ConnectionStringProperty ); }
            set { SetValue( ConnectionStringProperty, value ); }
        }

        public FrameworkElement Footer
        {
            get
            {
                return (FrameworkElement)GetValue( FooterProperty );
            }
            set
            {
                SetValue( FooterProperty, value );
            }
        }

        public SQLConnector( SqlConnectorTasks smoTasks )
        {
            sqlTasks = smoTasks;

            dbLoader.DoWork += DbLoaderDoWork;
            dbLoader.RunWorkerCompleted += DbLoaderRunWorkerCompleted;
        }

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
                newValue.PropertyChanged += ConnectionStringPropertyChanged;
        }

        private void ConnectionStringPropertyChanged( object sender, PropertyChangedEventArgs e )
        {
            //Server has changed, reload
            if ( e.PropertyName == "Server" && !dbLoader.IsBusy )
            {
                dbLoader.RunWorkerAsync( ConnectionString );
                OnPropertyChanged( "DatabasesLoading" );
            }

            GetBindingExpression( ConnectionStringProperty ).UpdateSource();
        }

        public ObservableCollection<string> Servers
        {
            get
            {
                lock ( LockServers )
                {
                    if ( servers == null )
                    {
                        servers = new ObservableCollection<string>();
                        ServersLoading = true;
                        LoadServersAsync();
                    }
                }

                return servers;
            }
        }

        public ObservableCollection<string> Databases
        {
            get { return databases; }
        }

        public bool ServersLoading
        {
            get
            {
                return serversLoading;
            }
            private set
            {
                serversLoading = value;
                OnPropertyChanged( "ServersLoading" );
            }
        }

        public bool DatabasesLoading
        {
            get
            {
                return dbLoader.IsBusy;
            }
        }

        public string Header
        {
            get { return headerText; }
            set
            {
                headerText = value;
                OnPropertyChanged( "Header" );
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

        private void DbLoaderDoWork( object sender, DoWorkEventArgs e )
        {
            var connString = e.Argument as ConnectionString;

            //No need to refesh databases if last server is the same as current server
            if ( connString == null || lastServer == connString.Server )
                return;

            lastServer = connString.Server;

            if ( string.IsNullOrEmpty( connString.Server ) ) return;

            e.Result = sqlTasks.GetDatabases( connString );
        }

        private void DbLoaderRunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            if ( e.Error == null )
            {
                var databases = e.Result as List<string>;
                if ( databases == null ) return;

                lastServer = null;
                foreach ( var database in databases.OrderBy( d => d ) )
                {
                    databases.Add( database );
                }
            }
            else if ( ConnectionString.Server != lastServer )
            {
                dbLoader.RunWorkerAsync( ConnectionString );
                return;
            }

            OnPropertyChanged( "DatabasesLoading" );
        }

        private void LoadServersAsync()
        {
            var serverLoader = new BackgroundWorker();
            serverLoader.DoWork += ( ( sender, e ) => e.Result = sqlTasks.SqlServers.OrderBy( r => r ).ToArray() );

            serverLoader.RunWorkerCompleted += ( ( sender, e ) =>
            {
                foreach ( var server in (string[])e.Result )
                {
                    servers.Add( server );
                }
                ServersLoading = false;
            } );

            serverLoader.RunWorkerAsync();
        }
    }

    /// <summary>
    /// Handles all the tasks that fire when a server or database is selected
    /// </summary>
    public class SqlConnectorTasks
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
                conn.Open();
                var serverConnection = new ServerConnection( conn );
                var server = new Server( serverConnection );
                databases.AddRange( from Database database in server.Databases select database.Name );
            }

            return databases;
        }

        public List<string> GetTables( ConnectionString connectionString )
        {
            using ( var conn = new SqlConnection( connectionString.WithDatabase( "master" ) ) )
            {
                conn.Open();
                var serverConnection = new ServerConnection( conn );
                var server = new Server( serverConnection );
                return server.Databases[connectionString.Database].Tables.Cast<Table>()
                    .Select( t => t.Name ).ToList();
            }
        }
    }

    /// <summary>
    /// Builds the connection string as soon as properties are changed on the SQLConnector
    /// </summary>
    public class ConnectionString : INotifyPropertyChanged
    {
        private readonly SqlConnectionStringBuilder _builder = new SqlConnectionStringBuilder
        {
            MultipleActiveResultSets = true,
            IntegratedSecurity = true
        };

        public ConnectionString() { }

        public ConnectionString( string connectionString )
        {
            _builder.ConnectionString = connectionString;
        }

        public static implicit operator string( ConnectionString connectionString )
        {
            return connectionString.ToString();
        }

        public override string ToString()
        {
            if ( Server.EndsWith( ".sdf" ) )
                if ( string.IsNullOrEmpty( Password ) )
                    return new SqlConnectionStringBuilder { DataSource = Server }.ConnectionString;
                else
                    return new SqlConnectionStringBuilder { DataSource = Server, Password = Password }.ConnectionString;

            return _builder.ConnectionString;
        }

        public ConnectionString WithDatabase( string databaseName )
        {
            return new ConnectionString
            {
                Server = Server,
                Database = databaseName,
                IntegratedSecurity = IntegratedSecurity,
                UserName = UserName,
                Password = Password,
                MultipleActiveResultSets = MultipleActiveResultSets
            };
        }

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

        private void OnPropertyChanged( string propertyName )
        {
            if ( PropertyChanged == null ) return;

            PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
