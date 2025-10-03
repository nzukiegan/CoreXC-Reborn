Imports System.Data
Imports System.Data.SqlClient
Imports System.IO
Imports System.Drawing

Public Class FormEditProvider

    Private ReadOnly connectionString As String
    Private ReadOnly providerRow As DataRowView
    Private currentLogoPath As String
    Private providerId As Integer
    Public Sub New(connStr As String, rowView As DataRowView)
        InitializeComponent()

        connectionString = connStr
        providerRow = rowView

        Integer.TryParse(providerRow("operator_id").ToString(), providerId)
        TextBoxName.Text = Convert.ToString(providerRow("operator_name"))
        TextBoxPLMN.Text = If(providerRow("plmn") IsNot Nothing, Convert.ToString(providerRow("plmn")), "")
        TextBoxMCC.Text = If(providerRow("mcc") IsNot Nothing, Convert.ToString(providerRow("mcc")), "")
        TextBoxMNC.Text = If(providerRow("mnc") IsNot Nothing, Convert.ToString(providerRow("mnc")), "")
        TextBoxDescription.Text = If(providerRow.Row.Table.Columns.Contains("description") AndAlso providerRow("description") IsNot DBNull.Value, Convert.ToString(providerRow("description")), "")

        If providerRow.Row.Table.Columns.Contains("logo_url") AndAlso providerRow("logo_url") IsNot DBNull.Value Then
            currentLogoPath = Convert.ToString(providerRow("logo_url"))
            LoadLogoIntoPictureBox(currentLogoPath)
        Else
            currentLogoPath = ""
        End If

        AddHandler PictureBoxLogo.Click, AddressOf PictureBoxLogo_Click
        AddHandler ButtonSave.Click, AddressOf ButtonSave_Click
        AddHandler ButtonCancel.Click, AddressOf ButtonCancel_Click
    End Sub

    Private Sub LoadLogoIntoPictureBox(path As String)
        Try
            If File.Exists(path) Then
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read)
                    Dim img As Image = Image.FromStream(fs)
                    PictureBoxLogo.Image = New Bitmap(img)
                    img.Dispose()
                End Using
            Else
                PictureBoxLogo.Image = Nothing
            End If
        Catch ex As Exception
            PictureBoxLogo.Image = Nothing
        End Try
    End Sub

    Private Sub PictureBoxLogo_Click(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            If ofd.ShowDialog() = DialogResult.OK Then
                Dim selected As String = ofd.FileName

                Dim resourcesPath As String = Path.Combine(Application.StartupPath, "Resources")
                If Not Directory.Exists(resourcesPath) Then Directory.CreateDirectory(resourcesPath)

                Dim fileName As String = Path.GetFileName(selected)
                Dim destPath As String = Path.Combine(resourcesPath, fileName)

                Try
                    File.Copy(selected, destPath, True)
                    currentLogoPath = destPath
                    LoadLogoIntoPictureBox(currentLogoPath)
                Catch ex As Exception
                    MessageBox.Show("Unable to copy selected image: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Sub ButtonCancel_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub ButtonSave_Click(sender As Object, e As EventArgs)
        Dim name As String = TextBoxName.Text.Trim()
        Dim plmn As String = TextBoxPLMN.Text.Trim()
        Dim mcc As Integer
        Dim mnc As Integer

        If String.IsNullOrWhiteSpace(name) Then
            MessageBox.Show("Provider name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not String.IsNullOrWhiteSpace(TextBoxMCC.Text) AndAlso Not Integer.TryParse(TextBoxMCC.Text.Trim(), mcc) Then
            MessageBox.Show("MCC must be a number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not String.IsNullOrWhiteSpace(TextBoxMNC.Text) AndAlso Not Integer.TryParse(TextBoxMNC.Text.Trim(), mnc) Then
            MessageBox.Show("MNC must be a number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim desc As String = TextBoxDescription.Text.Trim()

        ' Update DB
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = $"UPDATE operators
                                       SET operator_name = @name, plmn = @plmn, mcc = @mcc, mnc = @mnc, logo_url = @logo, description = @desc, updated_at = SYSUTCDATETIME()
                                       WHERE operator_id = @id"

                Using cmd As New SqlCommand(query, conn)
                    cmd.Parameters.AddWithValue("@name", name)
                    cmd.Parameters.AddWithValue("@plmn", If(String.IsNullOrEmpty(plmn), DBNull.Value, CType(plmn, Object)))
                    If String.IsNullOrWhiteSpace(TextBoxMCC.Text) Then
                        cmd.Parameters.AddWithValue("@mcc", DBNull.Value)
                    Else
                        cmd.Parameters.AddWithValue("@mcc", mcc)
                    End If
                    If String.IsNullOrWhiteSpace(TextBoxMNC.Text) Then
                        cmd.Parameters.AddWithValue("@mnc", DBNull.Value)
                    Else
                        cmd.Parameters.AddWithValue("@mnc", mnc)
                    End If
                    cmd.Parameters.AddWithValue("@logo", If(String.IsNullOrEmpty(currentLogoPath), DBNull.Value, CType(currentLogoPath, Object)))
                    cmd.Parameters.AddWithValue("@desc", If(String.IsNullOrEmpty(desc), DBNull.Value, CType(desc, Object)))
                    cmd.Parameters.AddWithValue("@id", providerId)

                    cmd.ExecuteNonQuery()
                End Using
            End Using

            MessageBox.Show("Provider updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.DialogResult = DialogResult.OK
            Me.Close()

        Catch ex As Exception
            MessageBox.Show("Error saving provider: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Class