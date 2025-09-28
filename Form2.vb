Imports System.Data.SqlClient

Public Class Formblacklist
    Private connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"
    Private schema As String = ""

    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(s As String)
        schema = s
        InitializeComponent()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim targetName As String = TextBox3.Text.Trim()
        Dim imsi As String = TextBox1.Text.Trim()
        Dim imei As String = TextBox2.Text.Trim()

        If String.IsNullOrEmpty(targetName) OrElse String.IsNullOrEmpty(imsi) OrElse String.IsNullOrEmpty(imei) Then
            MessageBox.Show("Please fill in all fields.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        AddToBlacklist(targetName, imei, imsi)
    End Sub

    Public Sub AddToBlacklist(targetName As String, imei As String, imsi As String)
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim tableName As String = "[" & schema & "].[blacklist]"

                Dim sql As String = "
                INSERT INTO " & tableName & " (name, imei, imsi)
                VALUES (@name, @imei, @imsi)
            "

                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.AddWithValue("@name", targetName)
                    cmd.Parameters.AddWithValue("@imei", imei)
                    cmd.Parameters.AddWithValue("@imsi", imsi)

                    cmd.ExecuteNonQuery()
                End Using
            End Using
            MessageBox.Show("Target added to blacklist successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error adding to blacklist: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub



End Class