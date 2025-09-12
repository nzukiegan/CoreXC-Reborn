Imports System.Data.SqlClient

Public Class Form4

    Private connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim tableName As String = TextBox1.Text.Trim()
        Dim locationCol As String = TextBox2.Text.Trim()
        Dim createDate As DateTime = DateTimePicker1.Value

        If String.IsNullOrEmpty(tableName) Then
            MessageBox.Show("Please enter a table name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim schemaSql As String = "
                    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'tasking_list')
                        EXEC('CREATE SCHEMA tasking_list')
                "
                Using schemaCmd As New SqlCommand(schemaSql, conn)
                    schemaCmd.ExecuteNonQuery()
                End Using

                Dim sql As String = $"
                    IF NOT EXISTS (
                        SELECT * FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE t.name = '{tableName}' AND s.name = 'tasking_list'
                    )
                    BEGIN
                        CREATE TABLE [tasking_list].[{tableName}] (
                            id INT PRIMARY KEY IDENTITY(1,1),
                            location NVARCHAR(255) NOT NULL,
                            date_create DATETIME2 NOT NULL
                        )
                    END"

                Using cmd As New SqlCommand(sql, conn)
                    cmd.ExecuteNonQuery()
                End Using

                Dim insertSql As String = $"INSERT INTO [tasking_list].[{tableName}] (location, date_create) VALUES (@location, @date_create)"
                Using insertCmd As New SqlCommand(insertSql, conn)
                    insertCmd.Parameters.AddWithValue("@location", locationCol)
                    insertCmd.Parameters.AddWithValue("@date_create", createDate)
                    insertCmd.ExecuteNonQuery()
                End Using
            End Using

            MessageBox.Show($"Table '[tasking_list].[{tableName}]' created (if not existed) and first record inserted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show("Error creating table: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Class
