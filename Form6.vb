Imports System.Data.SqlClient
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Drawing

Public Class Form6
    Private ReadOnly connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Form6_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ApplyTheme()
        LoadGSMCellsData()

        ' Wire events so Apply button state updates if user edits values
        AddHandler DataGridView1.CellValueChanged, AddressOf DataGridView1_CellValueChanged
        AddHandler DataGridView1.CurrentCellDirtyStateChanged, AddressOf DataGridView1_CurrentCellDirtyStateChanged
    End Sub

    ' ---------------------------
    ' Main loader
    ' ---------------------------
    Private Sub LoadGSMCellsData()
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                Dim gsmQuery As String = "SELECT gsm_id, ProviderName, plmn, mcc, mnc, band, arfcn, lac, cell_id, bsic, rssi, Timestamp FROM gsm_cells"
                Dim gsmTable As New DataTable()

                Using adapter As New SqlDataAdapter(gsmQuery, connection)
                    adapter.Fill(gsmTable)
                End Using

                Dim result As New DataTable()
                result.Columns.Add("channel", GetType(Integer))
                result.Columns.Add("mcc", GetType(Integer))
                result.Columns.Add("mnc", GetType(Integer))
                result.Columns.Add("band", GetType(String))
                result.Columns.Add("arfcn", GetType(Integer))
                result.Columns.Add("gsm_id", GetType(Integer))
                result.Columns.Add("cell_id", GetType(Long))
                result.Columns.Add("bsic", GetType(Integer))

                For ch As Integer = 1 To 14
                    Dim newRow As DataRow = result.NewRow()
                    newRow("channel") = ch
                    newRow("band") = ChannelDefaultBand(ch)

                    Dim candidate As DataRow = Nothing
                    For Each gr As DataRow In gsmTable.Rows
                        Dim bandStr As String = If(gr("band") IsNot DBNull.Value, gr("band").ToString().Trim(), String.Empty)
                        If ChannelNumbersForBand(bandStr).Contains(ch) Then
                            candidate = gr
                            Exit For
                        End If
                    Next

                    If candidate IsNot Nothing Then
                        If candidate("band") IsNot DBNull.Value Then
                            newRow("band") = candidate("band").ToString()
                        End If

                        ' mcc
                        If candidate.Table.Columns.Contains("mcc") AndAlso candidate("mcc") IsNot DBNull.Value Then
                            Dim mccVal As Integer
                            If Integer.TryParse(candidate("mcc").ToString(), mccVal) Then
                                newRow("mcc") = mccVal
                            Else
                                newRow("mcc") = DBNull.Value
                            End If
                        Else
                            newRow("mcc") = DBNull.Value
                        End If

                        ' mnc
                        If candidate.Table.Columns.Contains("mnc") AndAlso candidate("mnc") IsNot DBNull.Value Then
                            Dim mncVal As Integer
                            If Integer.TryParse(candidate("mnc").ToString(), mncVal) Then
                                newRow("mnc") = mncVal
                            Else
                                newRow("mnc") = DBNull.Value
                            End If
                        Else
                            newRow("mnc") = DBNull.Value
                        End If

                        ' arfcn
                        If candidate.Table.Columns.Contains("arfcn") AndAlso candidate("arfcn") IsNot DBNull.Value Then
                            Dim arfcnVal As Integer
                            If Integer.TryParse(candidate("arfcn").ToString(), arfcnVal) Then
                                newRow("arfcn") = arfcnVal
                            Else
                                newRow("arfcn") = DBNull.Value
                            End If
                        Else
                            newRow("arfcn") = DBNull.Value
                        End If

                        ' gsm_id
                        If candidate.Table.Columns.Contains("gsm_id") AndAlso candidate("gsm_id") IsNot DBNull.Value Then
                            Dim gsmIdVal As Integer
                            If Integer.TryParse(candidate("gsm_id").ToString(), gsmIdVal) Then
                                newRow("gsm_id") = gsmIdVal
                            Else
                                newRow("gsm_id") = DBNull.Value
                            End If
                        Else
                            newRow("gsm_id") = DBNull.Value
                        End If

                        ' cell_id
                        If candidate.Table.Columns.Contains("cell_id") AndAlso candidate("cell_id") IsNot DBNull.Value Then
                            Dim cellIdVal As Long
                            If Long.TryParse(candidate("cell_id").ToString(), cellIdVal) Then
                                newRow("cell_id") = cellIdVal
                            Else
                                newRow("cell_id") = DBNull.Value
                            End If
                        Else
                            newRow("cell_id") = DBNull.Value
                        End If

                        ' bsic
                        If candidate.Table.Columns.Contains("bsic") AndAlso candidate("bsic") IsNot DBNull.Value Then
                            Dim bsicVal As Integer
                            If Integer.TryParse(candidate("bsic").ToString(), bsicVal) Then
                                newRow("bsic") = bsicVal
                            Else
                                newRow("bsic") = DBNull.Value
                            End If
                        Else
                            newRow("bsic") = DBNull.Value
                        End If
                    Else
                        ' no candidate: keep default band and leave rest empty
                        newRow("mcc") = DBNull.Value
                        newRow("mnc") = DBNull.Value
                        newRow("arfcn") = DBNull.Value
                        newRow("gsm_id") = DBNull.Value
                        newRow("cell_id") = DBNull.Value
                        newRow("bsic") = DBNull.Value
                    End If

                    result.Rows.Add(newRow)
                Next

                DataGridView1.DataSource = result

                ' Configure columns exactly as requested
                DataGridView1.AutoGenerateColumns = False
                DataGridView1.Columns.Clear()

                Dim colChannel As New DataGridViewTextBoxColumn()
                colChannel.Name = "channel"
                colChannel.HeaderText = "CHANNEL"
                colChannel.DataPropertyName = "channel"
                colChannel.ReadOnly = True
                colChannel.Width = 60
                DataGridView1.Columns.Add(colChannel)

                Dim colMcc As New DataGridViewTextBoxColumn()
                colMcc.Name = "mcc"
                colMcc.HeaderText = "MCC"
                colMcc.DataPropertyName = "mcc"
                colMcc.Width = 80
                DataGridView1.Columns.Add(colMcc)

                Dim colMnc As New DataGridViewTextBoxColumn()
                colMnc.Name = "mnc"
                colMnc.HeaderText = "MNC"
                colMnc.DataPropertyName = "mnc"
                colMnc.Width = 80
                DataGridView1.Columns.Add(colMnc)

                Dim colBand As New DataGridViewTextBoxColumn()
                colBand.Name = "band"
                colBand.HeaderText = "BAND"
                colBand.DataPropertyName = "band"
                colBand.Width = 180
                DataGridView1.Columns.Add(colBand)

                Dim colArfcn As New DataGridViewTextBoxColumn()
                colArfcn.Name = "arfcn"
                colArfcn.HeaderText = "ARFCN"
                colArfcn.DataPropertyName = "arfcn"
                colArfcn.Width = 100
                DataGridView1.Columns.Add(colArfcn)

                Dim colBsic As New DataGridViewTextBoxColumn()
                colBsic.Name = "bsic"
                colBsic.HeaderText = "BSIC"
                colBsic.DataPropertyName = "bsic"
                colBsic.Width = 80
                DataGridView1.Columns.Add(colBsic)

                ' Hidden helpers
                Dim colGsmId As New DataGridViewTextBoxColumn()
                colGsmId.Name = "gsm_id"
                colGsmId.DataPropertyName = "gsm_id"
                colGsmId.Visible = False
                DataGridView1.Columns.Add(colGsmId)

                Dim colCellId As New DataGridViewTextBoxColumn()
                colCellId.Name = "cell_id"
                colCellId.DataPropertyName = "cell_id"
                colCellId.Visible = False
                DataGridView1.Columns.Add(colCellId)

                ' Apply button column (per-row, we will set enable state per cell)
                Dim applyColumn As New DataGridViewButtonColumn()
                applyColumn.Name = "apply"
                applyColumn.HeaderText = "Apply Choice"
                applyColumn.Text = "Apply"
                applyColumn.UseColumnTextForButtonValue = False
                applyColumn.Width = 90
                applyColumn.DefaultCellStyle.BackColor = Color.Green
                applyColumn.DefaultCellStyle.ForeColor = Color.White
                DataGridView1.Columns.Add(applyColumn)

                DataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

                UpdateApplyButtonsState()
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading GSM channels: " & ex.Message)
        End Try
    End Sub

    Private Function ChannelDefaultBand(channel As Integer) As String
        Select Case channel
            Case 1, 2
                Return "GSM 900"
            Case 3, 4
                Return "GSM 1800"
            Case 5, 6
                Return "GSM 1900"
            Case 7, 8
                Return "GSM 850"
            Case 9
                Return "GSM 450"
            Case 10
                Return "GSM 480"
            Case 11, 12
                Return "GSM 750"
            Case 13, 14
                Return "GSM 380"
            Case Else
                Return "Unknown GSM"
        End Select
    End Function

    ' ---------------------------
    ' Map band string to channel numbers (list)
    ' ---------------------------
    Private Function ChannelNumbersForBand(ByVal band As String) As List(Of Integer)
        Dim result As New List(Of Integer)()
        If String.IsNullOrWhiteSpace(band) Then Return result

        Dim bandLower = band.ToLowerInvariant()

        If bandLower.Contains("900") Then
            result.Add(1) : result.Add(2)
        ElseIf bandLower.Contains("1800") Then
            result.Add(3) : result.Add(4)
        ElseIf bandLower.Contains("1900") Then
            result.Add(5) : result.Add(6)
        ElseIf bandLower.Contains("850") Then
            result.Add(7) : result.Add(8)
        ElseIf bandLower.Contains("450") Then
            result.Add(9)
        ElseIf bandLower.Contains("480") Then
            result.Add(10)
        ElseIf bandLower.Contains("750") Then
            result.Add(11) : result.Add(12)
        ElseIf bandLower.Contains("380") Then
            result.Add(13) : result.Add(14)
        ElseIf bandLower.Contains("gsm") Then
            ' Default mapping for generic GSM
            result.Add(1) : result.Add(2) : result.Add(3) : result.Add(4)
        End If

        Return result
    End Function

    ' ---------------------------
    ' Update per-row Apply button state and colors
    ' ---------------------------
    Private Sub UpdateApplyButtonsState()
        For Each row As DataGridViewRow In DataGridView1.Rows
            If row.IsNewRow Then Continue For

            Dim btnCell = TryCast(row.Cells("apply"), DataGridViewButtonCell)
            If btnCell Is Nothing Then Continue For

            Dim arfcnObj = row.Cells("arfcn").Value

            If arfcnObj Is Nothing OrElse String.IsNullOrWhiteSpace(arfcnObj.ToString()) Then
                ' disable
                btnCell.Value = "" ' hide text
                btnCell.Style.BackColor = Color.LightGray
                btnCell.Style.ForeColor = Color.DarkGray
            Else
                ' enable
                btnCell.Value = "Apply"
                btnCell.Style.ForeColor = Color.Green
            End If
        Next
    End Sub

    ' ---------------------------
    ' DataGridView events
    ' ---------------------------
    Private Sub DataGridView1_CurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If DataGridView1.IsCurrentCellDirty Then
            DataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub DataGridView1_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 Then
            ' If ARFCN or band changed - refresh apply states for that row
            If DataGridView1.Columns(e.ColumnIndex).Name = "arfcn" OrElse DataGridView1.Columns(e.ColumnIndex).Name = "band" Then
                UpdateApplyButtonsState()
            End If
        End If
    End Sub

    Private Sub DataGridView1_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView1.CellContentClick
        If e.RowIndex < 0 Then Return
        If DataGridView1.Columns(e.ColumnIndex).Name = "apply" Then
            Dim btnCell = CType(DataGridView1.Rows(e.RowIndex).Cells("apply"), DataGridViewButtonCell)
            If btnCell Is Nothing OrElse btnCell.ReadOnly Then
                ' disabled -> ignore
                Return
            End If

            ApplyToBaseStation(e.RowIndex)
        End If
    End Sub

    ' ---------------------------
    ' Apply single row
    ' ---------------------------
    Private Sub ApplyToBaseStation(rowIndex As Integer)
        Dim row As DataGridViewRow = DataGridView1.Rows(rowIndex)
        Dim channelNumber As Integer = Convert.ToInt32(row.Cells("channel").Value)

        Dim arfcnObj = row.Cells("arfcn").Value
        Dim bandObj = row.Cells("band").Value
        Dim mccObj = row.Cells("mcc").Value
        Dim mncObj = row.Cells("mnc").Value
        Dim bsicObj = row.Cells("bsic").Value
        Dim gsmIdObj = row.Cells("gsm_id").Value
        Dim cellIdObj = row.Cells("cell_id").Value

        If arfcnObj Is Nothing OrElse String.IsNullOrWhiteSpace(arfcnObj.ToString()) Then
            MessageBox.Show($"Channel {channelNumber} has no candidate ARFCN / band. Nothing to apply.")
            Return
        End If

        Dim arfcn As Integer = Convert.ToInt32(arfcnObj)
        Dim mcc As Integer = If(mccObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(mccObj.ToString()), Convert.ToInt32(mccObj), 0)
        Dim mnc As Integer = If(mncObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(mncObj.ToString()), Convert.ToInt32(mncObj), 0)
        Dim bsic As Integer = If(bsicObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(bsicObj.ToString()), Convert.ToInt32(bsicObj), 0)
        Dim band As String = If(bandObj IsNot Nothing, bandObj.ToString(), String.Empty)
        Dim gsmId As Integer = If(gsmIdObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(gsmIdObj.ToString()), Convert.ToInt32(gsmIdObj), 0)
        Dim cellId As Long = If(cellIdObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(cellIdObj.ToString()), Convert.ToInt64(cellIdObj), 0L)

        ApplyToChannel(channelNumber, gsmId, arfcn, mcc, mnc, 0, cellId, bsic, band)

        MessageBox.Show($"Applied to channel: {channelNumber}")

        UpdateApplyButtonsState()
    End Sub

    ' ---------------------------
    ' Apply DB write (insert/update) -> stores numeric MHz for band
    ' ---------------------------
    Private Sub ApplyToChannel(channelNumber As Integer, gsmId As Integer, arfcn As Integer,
                              mcc As Integer, mnc As Integer, lac As Integer, cellId As Long, bsic As Integer, band As String)
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                Dim checkQuery As String = "SELECT COUNT(*) FROM base_stations WHERE channel_number = @channelNumber"
                Dim exists As Integer = 0

                Using checkCmd As New SqlCommand(checkQuery, connection)
                    checkCmd.Parameters.AddWithValue("@channelNumber", channelNumber)
                    exists = Convert.ToInt32(checkCmd.ExecuteScalar())
                End Using

                ' Convert band string (e.g. "GSM 900") to integer MHz (900)
                Dim bandMHz As Integer = ExtractBandMHz(band)

                If exists > 0 Then
                    Dim updateQuery As String = "UPDATE base_stations SET gsm_id = @gsmId, arfcn = @arfcn, 
                                              mcc = @mcc, mnc = @mnc, lac = @lac, cid = @cellId, 
                                              bsic = @bsic, band = @band, is_gsm = 1, last_updated = SYSUTCDATETIME()
                                              WHERE channel_number = @channelNumber"

                    Using updateCmd As New SqlCommand(updateQuery, connection)
                        updateCmd.Parameters.AddWithValue("@gsmId", gsmId)
                        updateCmd.Parameters.AddWithValue("@arfcn", arfcn)
                        updateCmd.Parameters.AddWithValue("@mcc", mcc)
                        updateCmd.Parameters.AddWithValue("@mnc", mnc)
                        updateCmd.Parameters.AddWithValue("@lac", lac)
                        updateCmd.Parameters.AddWithValue("@cellId", cellId)
                        updateCmd.Parameters.AddWithValue("@bsic", bsic)

                        If bandMHz > 0 Then
                            updateCmd.Parameters.AddWithValue("@band", bandMHz)
                        Else
                            updateCmd.Parameters.AddWithValue("@band", DBNull.Value)
                        End If

                        updateCmd.Parameters.AddWithValue("@channelNumber", channelNumber)

                        updateCmd.ExecuteNonQuery()
                    End Using
                Else
                    Dim insertQuery As String = "INSERT INTO base_stations (channel_number, is_gsm, gsm_id, 
                                              arfcn, mcc, mnc, lac, cid, bsic, band, last_updated)
                                              VALUES (@channelNumber, 1, @gsmId, @arfcn, @mcc, @mnc, 
                                              @lac, @cellId, @bsic, @band, SYSUTCDATETIME())"

                    Using insertCmd As New SqlCommand(insertQuery, connection)
                        insertCmd.Parameters.AddWithValue("@channelNumber", channelNumber)
                        insertCmd.Parameters.AddWithValue("@gsmId", gsmId)
                        insertCmd.Parameters.AddWithValue("@arfcn", arfcn)
                        insertCmd.Parameters.AddWithValue("@mcc", mcc)
                        insertCmd.Parameters.AddWithValue("@mnc", mnc)
                        insertCmd.Parameters.AddWithValue("@lac", lac)
                        insertCmd.Parameters.AddWithValue("@cellId", cellId)
                        insertCmd.Parameters.AddWithValue("@bsic", bsic)

                        If bandMHz > 0 Then
                            insertCmd.Parameters.AddWithValue("@band", bandMHz)
                        Else
                            insertCmd.Parameters.AddWithValue("@band", DBNull.Value)
                        End If

                        insertCmd.ExecuteNonQuery()
                    End Using
                End If
            End Using

            UpdateMainFormBaseStation(channelNumber)
        Catch ex As Exception
            MessageBox.Show("Error applying to channel " & channelNumber & ": " & ex.Message)
        End Try
    End Sub

    Private Sub UpdateMainFormBaseStation(channelNumber As Integer)
        Console.WriteLine("Updating main form base station " & channelNumber)
        Dim mainForm As Form1 = Application.OpenForms.OfType(Of Form1)().FirstOrDefault()

        If mainForm IsNot Nothing Then
            mainForm.LoadBaseStationData1()
        Else
            MessageBox.Show("Main form not found. Please ensure Form1 is open.")
        End If
    End Sub

    Private Function ExtractBandMHz(band As String) As Integer
        If String.IsNullOrWhiteSpace(band) Then Return 0

        ' look for explicit numbers in GSM band names
        Dim m As Match = Regex.Match(band, "(\d{3,4})")
        Dim value As Integer
        If m.Success AndAlso Integer.TryParse(m.Groups(1).Value, value) Then
            Return value
        End If

        Return 0
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        For Each row As DataGridViewRow In DataGridView1.Rows
            If Not row.IsNewRow Then
                If row.Cells("arfcn").Value IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(row.Cells("arfcn").Value.ToString()) Then
                    ApplyToBaseStation(row.Index)
                End If
            End If
        Next
        MessageBox.Show("Applied all valid settings to channels")
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim mainForm As Form1 = Application.OpenForms.OfType(Of Form1)().FirstOrDefault()
        If mainForm IsNot Nothing Then mainForm.Show()
        Me.Close()
    End Sub
    Friend WithEvents DataGridView1 As DataGridView
    Friend WithEvents Button1 As Button
    Friend WithEvents Button2 As Button

    Private Sub ApplyTheme()
        With Me.DataGridView1
            .BackgroundColor = Color.FromArgb(240, 240, 240)
            .GridColor = Color.FromArgb(200, 200, 200)
            .EnableHeadersVisualStyles = False
            .RowTemplate.Height = 40

            ' Column headers
            .ColumnHeadersDefaultCellStyle.BackColor = Color.Maroon
            .ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            .ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
            .ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            .ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            .ColumnHeadersHeight = 30

            ' Row headers
            .RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(128, 0, 0)
            .RowHeadersDefaultCellStyle.ForeColor = Color.White
            .RowHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.0!)
            .RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing

            ' Default cell style
            .DefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
            .DefaultCellStyle.ForeColor = Color.FromArgb(51, 51, 51)
            .DefaultCellStyle.Font = New Font("Segoe UI", 8.5!, FontStyle.Bold)
            .DefaultCellStyle.SelectionBackColor = Color.Green
            .DefaultCellStyle.SelectionForeColor = Color.White
            .DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft
            .DefaultCellStyle.Padding = New Padding(3)

            .AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 240)
            .AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(51, 51, 51)

            .SelectionMode = DataGridViewSelectionMode.FullRowSelect
            .MultiSelect = False
            .AllowUserToResizeRows = False
            .AllowUserToAddRows = False
            .AllowUserToDeleteRows = False
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        End With

        Dim buttons As Button() = {Me.Button1, Me.Button2}

        For Each btn As Button In buttons
            btn.BackColor = Color.Maroon
            btn.ForeColor = Color.White
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0
            btn.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
            btn.Cursor = Cursors.Hand
        Next
    End Sub
End Class