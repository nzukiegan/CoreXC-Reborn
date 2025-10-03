Imports System.Data.SqlClient
Imports System.IO

Public Class Form9

    Private connectionString As String = ""
    Private selectedLogoPath As String = ""

    Public Sub New(connStr As String)
        InitializeComponent()
        connectionString = connStr
    End Sub

    Private Sub PictureBox1_Click(sender As Object, e As EventArgs) Handles PictureBox1.Click
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            If ofd.ShowDialog() = DialogResult.OK Then
                selectedLogoPath = ofd.FileName

                Dim resourcesPath As String = Path.Combine(Application.StartupPath, "Resources")
                If Not Directory.Exists(resourcesPath) Then
                    Directory.CreateDirectory(resourcesPath)
                End If

                Dim fileName As String = Path.GetFileName(selectedLogoPath)
                Dim destPath As String = Path.Combine(resourcesPath, fileName)
                File.Copy(selectedLogoPath, destPath, True)

                PictureBox1.Image = Image.FromFile(destPath)

                selectedLogoPath = destPath
            End If
        End Using
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim providerName As String = TextBox1.Text
        Dim plmn As String = TextBox2.Text
        Dim mcc As Integer = Integer.Parse(TextBox3.Text)
        Dim mnc As Integer = Integer.Parse(TextBox4.Text)

        If String.IsNullOrWhiteSpace(providerName) OrElse String.IsNullOrEmpty(selectedLogoPath) Then
            MessageBox.Show("Please fill all fields and select a logo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Using conn As New SqlConnection(connectionString)
            conn.Open()
            Dim query As String = $"INSERT INTO operators
                                   (operator_name, operator_code, plmn, mcc, mnc, logo_url) 
                                   VALUES (@name, @code, @plmn, @mcc, @mnc, @logo)"

            Using cmd As New SqlCommand(query, conn)
                cmd.Parameters.AddWithValue("@name", providerName)
                cmd.Parameters.AddWithValue("@code", providerName)
                cmd.Parameters.AddWithValue("@plmn", plmn)
                cmd.Parameters.AddWithValue("@mcc", mcc)
                cmd.Parameters.AddWithValue("@mnc", mnc)
                cmd.Parameters.AddWithValue("@logo", selectedLogoPath)

                cmd.ExecuteNonQuery()
            End Using
        End Using

        MessageBox.Show("Provider added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Me.Close()
    End Sub
End Class
