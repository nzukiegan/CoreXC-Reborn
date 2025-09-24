Imports System.Data.SqlClient
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports System.Xml
Imports System.Net
Imports System.Net.Sockets

Public Class DatabaseInitializer
    Private ReadOnly connectionString As String = $"Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Integrated Security=true;"
    Private ReadOnly masterConnection As String
    Private ReadOnly targetDbName As String

    Public Sub New(server As String, databaseName As String)
        masterConnection = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;"
        targetDbName = databaseName
    End Sub

    Public Async Function EnsureDatabaseExistsAsync() As Task
        Using conn As New SqlConnection(masterConnection)
            Await conn.OpenAsync()

            Dim createDbSql As String = $"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{targetDbName}')
            BEGIN
                CREATE DATABASE [{targetDbName}];
            END"

            Using cmd As New SqlCommand(createDbSql, conn)
                Await cmd.ExecuteNonQueryAsync()
            End Using

            Dim checkSql As String = $"SELECT COUNT(*) FROM sys.databases WHERE name = '{targetDbName}'"
            Using checkCmd As New SqlCommand(checkSql, conn)
                Dim result As Integer = Convert.ToInt32(Await checkCmd.ExecuteScalarAsync())
                If result = 0 Then
                    Throw New Exception($"Failed to create database '{targetDbName}'. Check permissions or SQL Server logs.")
                End If
            End Using
        End Using
    End Function

    Public Async Function InitializeBaseStations() As Task
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                For channel As Integer = 1 To 14
                    Dim checkQuery As String = "
                    SELECT COUNT(*) 
                    FROM base_stations 
                    WHERE channel_number = @channel"

                    Dim exists As Boolean
                    Using checkCmd As New SqlCommand(checkQuery, connection)
                        checkCmd.Parameters.AddWithValue("@channel", channel)
                        exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0
                    End Using

                    If Not exists Then
                        Dim insertQuery As String = "
                        INSERT INTO base_stations (channel_number, is_lte)
                        VALUES (@channel, 1);"

                        Using insertCmd As New SqlCommand(insertQuery, connection)
                            insertCmd.Parameters.AddWithValue("@channel", channel)
                            insertCmd.ExecuteNonQuery()
                        End Using
                    End If
                Next

            End Using
        Catch ex As Exception
            Console.WriteLine("Error initializing base stations: " & ex.Message)
        End Try
    End Function

    Public Async Function GetBaseStationsFromBackend(udp As UdpClient) As Task
        Dim buttonIpMap As New Dictionary(Of Integer, String) From {
        {1, "192.168.1.90"},
        {2, "192.168.1.91"},
        {3, "192.168.1.92"},
        {4, "192.168.1.93"},
        {5, "192.168.1.94"},
        {6, "192.168.1.95"},
        {7, "192.168.1.96"},
        {8, "192.168.1.97"},
        {9, "192.168.1.98"},
        {11, "192.168.1.101"},
        {12, "192.168.1.102"},
        {13, "192.168.1.103"},
        {14, "192.168.1.104"}
    }

        For Each kvp In buttonIpMap
            Dim channelNumber As Integer = kvp.Key
            Dim ipAddress As String = kvp.Value

            Try
                Dim cmdBytes As Byte() = Encoding.ASCII.GetBytes("GetCellPara")
                udp.Send(cmdBytes, cmdBytes.Length, ipAddress, 9001)
            Catch ex As Exception
                Console.WriteLine($"Failed to get data for channel {channelNumber} ({ipAddress}): {ex.Message}")
            End Try
        Next
    End Function

    Public Async Function SeedOperatorsAsync() As Task
        Dim targetDbConnection As String = $"Server=(localdb)\MSSQLLocalDB;Database={targetDbName};Integrated Security=true;"
        Dim sb As New StringBuilder()
        sb.AppendLine("IF NOT EXISTS (SELECT 1 FROM operators)")
        sb.AppendLine("BEGIN")
        sb.AppendLine("INSERT INTO operators (operator_name, operator_code, plmn, mcc, mnc, logo_url, description)")
        sb.AppendLine("VALUES")
        sb.AppendLine("('Telkomsel', 'TELKOMSEL', '51010', 510, 10, '', 'Telkomsel main PLMN'),")
        sb.AppendLine("('XLCOMINDO', 'XLCOMINDO', '51011', 510, 11, '', 'XLCOMINDO PLMN'),")
        sb.AppendLine("('THREE', 'THREE', '51089', 510, 89, '', 'Three'),")
        sb.AppendLine("('Indosat', 'INDOSAT', '51001', 510, 01, '', 'Indosat GSM/4G/5G network'),")
        sb.AppendLine("('Indosat', 'INDOSAT', '51021', 510, 21, '', 'Indosat secondary code'),")
        sb.AppendLine("('XLCOMINDO', 'XLCOMINDO', '51007', 510, 07, '', 'XLCOMINDO GSM/4G/5G network'),")
        sb.AppendLine("('Smarfren', 'SMARFREN', '51009', 510, 09, '', 'Smarfren LTE/CDMA network'),")
        sb.AppendLine("('Smarfren ', 'SMARFREN', '51028', 510, 28, '', 'Smarfren test / reserved PLMN');")
        sb.AppendLine("END")

        Try
            Using conn As New SqlConnection(targetDbConnection)
                Await conn.OpenAsync()
                Using cmd As New SqlCommand(sb.ToString(), conn)
                    cmd.CommandTimeout = 0
                    Dim rowsAffected As Integer = Await cmd.ExecuteNonQueryAsync()
                    Console.WriteLine("SeedOperatorsAsync executed successfully. Rows affected: " & rowsAffected)
                End Using
            End Using
        Catch ex As Exception
            Console.WriteLine("Error in SeedOperatorsAsync: " & ex.Message)
            If ex.InnerException IsNot Nothing Then
                Console.WriteLine("Inner Exception: " & ex.InnerException.Message)
            End If
        End Try
    End Function

    Public Async Function ApplySchemaAsync() As Task
        Dim targetDbConnection As String = $"Server=(localdb)\MSSQLLocalDB;Database={targetDbName};Integrated Security=true;"

        Dim sb As New StringBuilder()

        sb.AppendLine("-- USERS TABLE")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE users (")
        sb.AppendLine("    user_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    username NVARCHAR(100) UNIQUE NOT NULL,")
        sb.AppendLine("    password_hash NVARCHAR(255) NOT NULL,")
        sb.AppendLine("    full_name NVARCHAR(200) NOT NULL,")
        sb.AppendLine("    email NVARCHAR(255) UNIQUE,")
        sb.AppendLine("    is_active BIT NOT NULL DEFAULT 1,")
        sb.AppendLine("    last_login DATETIME2,")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- NETWORK TYPES")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'network_types' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE network_types (")
        sb.AppendLine("    network_type_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    network_name NVARCHAR(50) UNIQUE NOT NULL,")
        sb.AppendLine("    description NVARCHAR(500)")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- OPERATORS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'operators' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE operators (")
        sb.AppendLine("    operator_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    operator_name NVARCHAR(150) NOT NULL,")
        sb.AppendLine("    operator_code NVARCHAR(50) NOT NULL,")
        sb.AppendLine("    plmn NVARCHAR(10),")
        sb.AppendLine("    mcc INT,")
        sb.AppendLine("    mnc INT,")
        sb.AppendLine("    logo_url NVARCHAR(500),")
        sb.AppendLine("    description NVARCHAR(500),")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- FREQUENCY BANDS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'frequency_bands' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE frequency_bands (")
        sb.AppendLine("    band_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    band_name NVARCHAR(100) UNIQUE NOT NULL,")
        sb.AppendLine("    rat NVARCHAR(20) NOT NULL,")
        sb.AppendLine("    ul_freq FLOAT NOT NULL,")
        sb.AppendLine("    dl_freq FLOAT NOT NULL,")
        sb.AppendLine("    ul_channel INT,")
        sb.AppendLine("    dl_channel INT")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- GSM CELLS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'gsm_cells' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE gsm_cells (")
        sb.AppendLine("    gsm_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    ProviderName NVARCHAR(100),")
        sb.AppendLine("    plmn NVARCHAR(10),")
        sb.AppendLine("    rat NVARCHAR(10) DEFAULT 'GSM',")
        sb.AppendLine("    band NVARCHAR(50),")
        sb.AppendLine("    mcc INT,")
        sb.AppendLine("    mnc INT,")
        sb.AppendLine("    arfcn INT,")
        sb.AppendLine("    lac INT,")
        sb.AppendLine("    nb_cell NVARCHAR(200),")
        sb.AppendLine("    cell_id BIGINT NOT NULL,")
        sb.AppendLine("    bsic TINYINT,")
        sb.AppendLine("    rssi FLOAT,")
        sb.AppendLine("    Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- LTE CELLS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'lte_cells' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE lte_cells (")
        sb.AppendLine("    lte_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    provider_name NVARCHAR(100),")
        sb.AppendLine("    plmn NVARCHAR(20),")
        sb.AppendLine("    mcc INT,")
        sb.AppendLine("    mnc INT,")
        sb.AppendLine("    band NVARCHAR(50),")
        sb.AppendLine("    pri INT,")
        sb.AppendLine("    pci INT,")
        sb.AppendLine("    earfcn INT,")
        sb.AppendLine("    nb_earfcn NVARCHAR(100),")
        sb.AppendLine("    rat NVARCHAR(10) DEFAULT 'LTE',")
        sb.AppendLine("    lac INT,")
        sb.AppendLine("    cell_id BIGINT NOT NULL,")
        sb.AppendLine("    rsrp FLOAT,")
        sb.AppendLine("    Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- WCDMA CELLS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'wcdma_cells' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE wcdma_cells (")
        sb.AppendLine("    wcdma_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    provider_name NVARCHAR(100),")
        sb.AppendLine("    plmn NVARCHAR(20),")
        sb.AppendLine("    mcc INT,")
        sb.AppendLine("    mnc INT,")
        sb.AppendLine("    band NVARCHAR(50),")
        sb.AppendLine("    psc INT,")
        sb.AppendLine("    earfcn INT,")
        sb.AppendLine("    nbsc INT,")
        sb.AppendLine("    rat NVARCHAR(10) DEFAULT 'WCDMA',")
        sb.AppendLine("    lac INT,")
        sb.AppendLine("    cell_id BIGINT NOT NULL,")
        sb.AppendLine("    rscp FLOAT,")
        sb.AppendLine("    Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- BASE STATIONS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'base_stations' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE base_stations (")
        sb.AppendLine("    base_station_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    channel_number INT NOT NULL,")
        sb.AppendLine("    is_gsm BIT NOT NULL DEFAULT 0,")
        sb.AppendLine("    is_lte BIT NOT NULL DEFAULT 0,")
        sb.AppendLine("    is_wcdma BIT NOT NULL DEFAULT 0,")
        sb.AppendLine("    gsm_id INT,")
        sb.AppendLine("    lte_id INT,")
        sb.AppendLine("    wcdma_id INT,")
        sb.AppendLine("    earfcn INT,")
        sb.AppendLine("    mcc INT,")
        sb.AppendLine("    bsic INT,")
        sb.AppendLine("    mnc INT,")
        sb.AppendLine("    cid INT,")
        sb.AppendLine("    count INT,")
        sb.AppendLine("    lac INT,")
        sb.AppendLine("    band INT,")
        sb.AppendLine("    last_updated DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- LOCATIONS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'locations' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE locations (")
        sb.AppendLine("    location_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    location_name NVARCHAR(200) UNIQUE NOT NULL,")
        sb.AppendLine("    description NVARCHAR(500),")
        sb.AppendLine("    latitude FLOAT,")
        sb.AppendLine("    longitude FLOAT,")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- SETTINGS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'settings' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE settings (")
        sb.AppendLine("    setting_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    setting_name NVARCHAR(100) NOT NULL UNIQUE,")
        sb.AppendLine("    setting_value NVARCHAR(1000) NOT NULL,")
        sb.AppendLine("    setting_group NVARCHAR(50) NOT NULL DEFAULT 'General',")
        sb.AppendLine("    data_type NVARCHAR(20) NOT NULL DEFAULT 'string',")
        sb.AppendLine("    is_secured BIT DEFAULT 0,")
        sb.AppendLine("    min_value NVARCHAR(50),")
        sb.AppendLine("    max_value NVARCHAR(50),")
        sb.AppendLine("    options_json NVARCHAR(1000),")
        sb.AppendLine("    description NVARCHAR(500),")
        sb.AppendLine("    created_by NVARCHAR(50) DEFAULT 'system',")
        sb.AppendLine("    updated_by NVARCHAR(50),")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- DEVICES")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'devices' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE devices (")
        sb.AppendLine("    device_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    device_name NVARCHAR(200) NOT NULL,")
        sb.AppendLine("    device_type_id INT,")
        sb.AppendLine("    imei NVARCHAR(50) UNIQUE,")
        sb.AppendLine("    serial_number NVARCHAR(100) UNIQUE,")
        sb.AppendLine("    mac_address NVARCHAR(50),")
        sb.AppendLine("    is_active BIT NOT NULL DEFAULT 1,")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- IMSI TARGETS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imsi_targets' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imsi_targets (")
        sb.AppendLine("    imsi_target_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    description NVARCHAR(500),")
        sb.AppendLine("    case_ref NVARCHAR(100),")
        sb.AppendLine("    location_id INT REFERENCES locations(location_id),")
        sb.AppendLine("    is_active BIT NOT NULL DEFAULT 1,")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    created_by INT REFERENCES users(user_id)")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imsi_target_numbers' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imsi_target_numbers (")
        sb.AppendLine("    imsi_number_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    imsi_target_id INT NOT NULL REFERENCES imsi_targets(imsi_target_id),")
        sb.AppendLine("    imsi NVARCHAR(64) NOT NULL")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imsi_target_names' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imsi_target_names (")
        sb.AppendLine("    name_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    imsi_target_id INT NOT NULL REFERENCES imsi_targets(imsi_target_id),")
        sb.AppendLine("    target_name NVARCHAR(200) NOT NULL")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- IMEI TARGETS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imei_targets' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imei_targets (")
        sb.AppendLine("    imei_target_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    description NVARCHAR(500),")
        sb.AppendLine("    case_ref NVARCHAR(100),")
        sb.AppendLine("    location_id INT REFERENCES locations(location_id),")
        sb.AppendLine("    is_active BIT NOT NULL DEFAULT 1,")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    created_by INT REFERENCES users(user_id)")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imei_target_numbers' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imei_target_numbers (")
        sb.AppendLine("    imei_number_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    imei_target_id INT NOT NULL REFERENCES imei_targets(imei_target_id),")
        sb.AppendLine("    imei NVARCHAR(50) NOT NULL")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'imei_target_names' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE imei_target_names (")
        sb.AppendLine("    name_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    imei_target_id INT NOT NULL REFERENCES imei_targets(imei_target_id),")
        sb.AppendLine("    target_name NVARCHAR(200) NOT NULL")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- TASK TYPES")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'task_types' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE task_types (")
        sb.AppendLine("    task_type_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    type_name NVARCHAR(20) UNIQUE NOT NULL,")
        sb.AppendLine("    description NVARCHAR(200)")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- TASKS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tasks' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE tasks (")
        sb.AppendLine("    task_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    task_type_id INT NOT NULL REFERENCES task_types(task_type_id),")
        sb.AppendLine("    network_id INT NOT NULL,")
        sb.AppendLine("    target_name NVARCHAR(200),")
        sb.AppendLine("    source NVARCHAR(100),")
        sb.AppendLine("    dl INT,")
        sb.AppendLine("    ul INT,")
        sb.AppendLine("    ul_freq FLOAT,")
        sb.AppendLine("    band NVARCHAR(100),")
        sb.AppendLine("    imei NVARCHAR(50),")
        sb.AppendLine("    target_imsi NVARCHAR(64),")
        sb.AppendLine("    user_id INT REFERENCES users(user_id),")
        sb.AppendLine("    case_ref NVARCHAR(100),")
        sb.AppendLine("    location_id INT REFERENCES locations(location_id),")
        sb.AppendLine("    action NVARCHAR(100),")
        sb.AppendLine("    channel_id INT REFERENCES base_stations(base_station_id),")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),")
        sb.AppendLine("    status NVARCHAR(30)")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- SCAN SESSIONS")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'scan_sessions' AND schema_id = SCHEMA_ID('dbo'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE TABLE scan_sessions (")
        sb.AppendLine("    session_id INT PRIMARY KEY IDENTITY(1,1),")
        sb.AppendLine("    session_name NVARCHAR(200) NOT NULL,")
        sb.AppendLine("    device_id INT REFERENCES devices(device_id),")
        sb.AppendLine("    location_id INT REFERENCES locations(location_id),")
        sb.AppendLine("    start_time DATETIME2 NOT NULL,")
        sb.AppendLine("    end_time DATETIME2,")
        sb.AppendLine("    profile_id INT,")
        sb.AppendLine("    notes NVARCHAR(MAX),")
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()")
        sb.AppendLine(");")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("-- Indexes for settings")
        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_settings_group' AND object_id = OBJECT_ID('settings'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE INDEX idx_settings_group ON settings (setting_group);")
        sb.AppendLine("END")
        sb.AppendLine()

        sb.AppendLine("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_settings_group_name' AND object_id = OBJECT_ID('settings'))")
        sb.AppendLine("BEGIN")
        sb.AppendLine("CREATE INDEX idx_settings_group_name ON settings (setting_group, setting_name);")
        sb.AppendLine("END")
        sb.AppendLine()

        Dim fullSql As String = sb.ToString()

        Using conn As New SqlConnection(targetDbConnection)
            Await conn.OpenAsync()

            Using cmd As New SqlCommand(fullSql, conn)
                cmd.CommandTimeout = 0
                Await cmd.ExecuteNonQueryAsync()
            End Using
        End Using
    End Function
End Class
