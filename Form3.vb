Imports System.Data.SqlClient

Public Class Form3
    Private connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"

    Private selectedSchema As String

    Public Sub New(schema As String)
        InitializeComponent()
        selectedSchema = schema
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim imsi As String = TextBox1.Text.Trim()

        If String.IsNullOrEmpty(imsi) Then
            MessageBox.Show("Please enter an IMSI.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim tableName As String = "[" & selectedSchema & "].[whitelist]"
                Dim sql As String = "INSERT INTO " & tableName & " (imsi) VALUES (@imsi)"

                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.AddWithValue("@imsi", imsi)
                    cmd.ExecuteNonQuery()
                End Using
            End Using

            MessageBox.Show("IMSI added to whitelist successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.DialogResult = DialogResult.OK
            Me.Close()

        Catch ex As Exception
            MessageBox.Show("Error inserting IMSI: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
