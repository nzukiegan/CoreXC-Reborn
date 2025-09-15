Imports System.Data.SqlClient

Public Class Form4

    Private connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim rawSchemaName As String = TextBox1.Text.Trim()
        Dim prefix As String = "op_"

        If String.IsNullOrEmpty(rawSchemaName) Then
            MessageBox.Show("Please enter a schema name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        ' Ensure schema name starts with prefix
        Dim schemaName As String
        If rawSchemaName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            schemaName = rawSchemaName
        Else
            schemaName = prefix & rawSchemaName
        End If

        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                ' Create schema if not exists
                Dim schemaSql As String =
                    $"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{schemaName}')
                        EXEC('CREATE SCHEMA [{schemaName}]')"
                Using schemaCmd As New SqlCommand(schemaSql, conn)
                    schemaCmd.ExecuteNonQuery()
                End Using

                ' Create scan_results table
                Dim scanResultsSql As String = $"
                    IF NOT EXISTS (
                        SELECT * FROM sys.tables 
                        WHERE name = 'scan_results' AND schema_id = SCHEMA_ID('{schemaName}')
                    )
                    BEGIN
                        CREATE TABLE [{schemaName}].[scan_results] (
                            result_no BIGINT PRIMARY KEY IDENTITY(1,1),
                            date_event DATETIME2 NOT NULL,
                            location_name NVARCHAR(255) NOT NULL,
                            source NVARCHAR(100),
                            provider_name NVARCHAR(100),
                            mcc INT,
                            mnc INT,
                            imsi NVARCHAR(64),
                            imei NVARCHAR(64),
                            guti NVARCHAR(64),
                            signal_level FLOAT,
                            time_advance INT,
                            longitude FLOAT,
                            latitude FLOAT,
                            phone_model NVARCHAR(150),
                            event NVARCHAR(150),
                            count INT
                        )
                    END"
                Using cmd As New SqlCommand(scanResultsSql, conn)
                    cmd.ExecuteNonQuery()
                End Using

                ' Create blacklist table
                Dim blacklistSql As String = $"
                    IF NOT EXISTS (
                        SELECT * FROM sys.tables 
                        WHERE name = 'blacklist' AND schema_id = SCHEMA_ID('{schemaName}')
                    )
                    BEGIN
                        CREATE TABLE [{schemaName}].[blacklist] (
                            blacklist_id INT PRIMARY KEY IDENTITY(1,1),
                            imei NVARCHAR(50),
                            imsi NVARCHAR(64),
                            created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        )
                    END"
                Using cmd As New SqlCommand(blacklistSql, conn)
                    cmd.ExecuteNonQuery()
                End Using

                Dim whitelistSql As String = $"
                    IF NOT EXISTS (
                        SELECT * FROM sys.tables 
                        WHERE name = 'whitelist' AND schema_id = SCHEMA_ID('{schemaName}')
                    )
                    BEGIN
                        CREATE TABLE [{schemaName}].[whitelist] (
                            whitelist_id INT PRIMARY KEY IDENTITY(1,1),
                            imei NVARCHAR(50),
                            imsi NVARCHAR(50),
                            created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        )
                    END"
                Using cmd As New SqlCommand(whitelistSql, conn)
                    cmd.ExecuteNonQuery()
                End Using

            End Using

            MessageBox.Show($"Schema '{schemaName}' created (if not existed) with tables scan_results, blacklist, and whitelist.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show("Error creating schema/tables: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Class
