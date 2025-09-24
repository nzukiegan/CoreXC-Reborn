Imports System.Data.SqlClient
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Drawing

Public Class Form6
    Private ReadOnly connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"
    Private providerFilter As List(Of String)
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(filter As List(Of String))
        providerFilter = filter
        Console.WriteLine("Operator filter " & providerFilter.ToString())
        InitializeComponent()
    End Sub

    Private Sub Form6_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ApplyTheme()
        LoadGSMCellsData()

        AddHandler DataGridView1.CellValueChanged, AddressOf DataGridView1_CellValueChanged
        AddHandler DataGridView1.CurrentCellDirtyStateChanged, AddressOf DataGridView1_CurrentCellDirtyStateChanged
    End Sub

    Private Sub LoadGSMCellsData()
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                Dim filters As List(Of String) = Nothing

                If providerFilter IsNot Nothing AndAlso providerFilter.Count > 0 Then
                    filters = providerFilter.Select(Function(f) f.ToLower().Trim()).ToList()
                Else
                    filters = New List(Of String)()
                End If

                Dim gsmQuery As String = "
    SELECT gsm_id, ProviderName, plmn, mcc, mnc, band, arfcn, lac, cell_id, bsic, rssi, nb_cell, [Timestamp]
    FROM gsm_cells
"

                If filters.Count > 0 AndAlso Not filters.Contains("All") Then
                    Dim paramNames As New List(Of String)()
                    For i As Integer = 0 To filters.Count - 1
                        paramNames.Add("@p" & i)
                    Next
                    gsmQuery &= " WHERE LOWER(ProviderName) IN (" & String.Join(",", paramNames) & ")"
                End If

                Dim gsmTable As New DataTable()
                Using adapter As New SqlDataAdapter(gsmQuery, connection)
                    If filters.Count > 0 AndAlso Not filters.Contains("All") Then
                        For i As Integer = 0 To filters.Count - 1
                            adapter.SelectCommand.Parameters.AddWithValue("@p" & i, filters(i))
                            Console.WriteLine($"Parameter added: @p{i} = {filters(i)}")
                        Next
                    End If
                    adapter.Fill(gsmTable)
                End Using

                Dim result As New DataTable()
                result.Columns.Add("rank", GetType(Integer))
                result.Columns.Add("plmn", GetType(String))
                result.Columns.Add("band", GetType(String))
                result.Columns.Add("channel", GetType(Integer))
                result.Columns.Add("gsmId", GetType(Integer))

                Dim allArfcns As New List(Of Tuple(Of Integer, String, String, Integer))()

                For Each row As DataRow In gsmTable.Rows
                    If Not IsDBNull(row("nb_cell")) Then
                        Dim mcc As Integer = row.Field(Of Integer)("mcc")
                        Dim mnc As Integer = row.Field(Of Integer)("mnc")
                        Dim gsmId As Integer = row.Field(Of Integer)("gsm_id")
                        Dim band As String = row.Field(Of String)("band")
                        Dim plmnStr As String = $"{mcc:D3}{mnc:D2}"

                        Dim raw As String = row.Field(Of String)("nb_cell")
                        Dim parsed As List(Of Integer) = raw.Trim("["c, "]"c) _
                    .Split(New Char() {","c}, StringSplitOptions.RemoveEmptyEntries) _
                    .Select(Function(s) Integer.Parse(s.Trim())) _
                    .ToList()

                        For Each arfcn In parsed
                            allArfcns.Add(Tuple.Create(arfcn, plmnStr, band, gsmId))
                        Next
                    End If
                Next

                Dim finalRanked = allArfcns.GroupBy(Function(x) x.Item1) _
                .Select(Function(g) New With {
                    .Arfcn = g.Key,
                    .Count = g.Count(),
                    .Plmn = g.First().Item2,
                    .Band = g.First().Item3,
                    .GsmId = g.First().Item4
                }) _
                .OrderByDescending(Function(x) x.Count) _
                .ThenBy(Function(x) x.Arfcn) _
                .Take(4) _
                .ToList()

                Dim rankCounter As Integer = 1
                For Each entry In finalRanked
                    Dim r = result.NewRow()
                    r("rank") = rankCounter
                    r("plmn") = entry.Plmn
                    r("band") = entry.Band
                    r("channel") = entry.Arfcn
                    r("gsmId") = entry.GsmId
                    result.Rows.Add(r)
                    rankCounter += 1
                Next

                DataGridView1.DataSource = result
                DataGridView1.AutoGenerateColumns = False
                DataGridView1.Columns.Clear()

                Dim colRank As New DataGridViewTextBoxColumn() With {.Name = "rank", .HeaderText = "RANK", .DataPropertyName = "rank", .Width = 60}
                Dim colPlmn As New DataGridViewTextBoxColumn() With {.Name = "plmn", .HeaderText = "PLMN", .DataPropertyName = "plmn", .Width = 100}
                Dim colBand As New DataGridViewTextBoxColumn() With {.Name = "band", .HeaderText = "BAND", .DataPropertyName = "band", .Width = 150}
                Dim colChannel As New DataGridViewTextBoxColumn() With {.Name = "channel", .HeaderText = "CHANNEL", .DataPropertyName = "channel", .Width = 100}

                Dim colChosenCh As New DataGridViewTextBoxColumn() With {
                .Name = "chosenCh",
                .HeaderText = "Chosen Channel",
                .DataPropertyName = "chosenCh",
                .Visible = False
            }

                Dim gsmIdCh As New DataGridViewTextBoxColumn() With {
                .Name = "gsmId",
                .HeaderText = "Gsm Id",
                .DataPropertyName = "gsmId",
                .Visible = False
            }

                DataGridView1.Columns.AddRange({colRank, colPlmn, colBand, colChannel, colChosenCh, gsmIdCh})

                Dim applyColumn As New DataGridViewButtonColumn() With {
                .Name = "apply",
                .HeaderText = "Apply Choice",
                .UseColumnTextForButtonValue = False,
                .Width = 120
            }
                applyColumn.DefaultCellStyle.BackColor = Color.Green
                applyColumn.DefaultCellStyle.ForeColor = Color.White
                DataGridView1.Columns.Add(applyColumn)

                DataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                UpdateApplyButtonsState()
            End Using
        Catch ex As Exception
            Console.WriteLine("Error loading GSM channels: " & ex.StackTrace)
        End Try
    End Sub

    Private Function GsmChannelDefaultBand(channel As Integer) As String
        Select Case channel
            Case 1, 2 : Return "GSM 900"
            Case 3, 4 : Return "GSM 1800"
            Case Else : Return "Unknown"
        End Select
    End Function

    Private Function ChannelsForGsmBand(band As String) As Integer()
        Select Case band
            Case "GSM 900" : Return {1, 2}
            Case "GSM 1800" : Return {3, 4}
            Case Else : Return Array.Empty(Of Integer)()
        End Select
    End Function

    Private Sub UpdateApplyButtonsState()
        Dim channelUsage As New Dictionary(Of Integer, Integer)

        For Each row As DataGridViewRow In DataGridView1.Rows
            If row.IsNewRow Then Continue For

            Dim btnCell = TryCast(row.Cells("apply"), DataGridViewButtonCell)
            If btnCell Is Nothing Then Continue For

            Dim bandObj = row.Cells("band").Value
            Console.WriteLine("Band to select " & bandObj?.ToString())
            Dim targetCh() As Integer = ChannelsForGsmBand(bandObj?.ToString())

            If targetCh.Length = 0 Then
                btnCell.Value = "N/A"
                btnCell.Style.BackColor = Color.Gray
                btnCell.Style.ForeColor = Color.White
                row.Cells("chosenCh").Value = DBNull.Value
                Continue For
            End If

            Dim chosenCh As Integer = targetCh _
            .OrderBy(Function(c)
                         If channelUsage.ContainsKey(c) Then
                             Return channelUsage(c)
                         Else
                             Return 0
                         End If
                     End Function) _
            .First()

            btnCell.Value = $"Apply to Ch{chosenCh}"
            btnCell.Style.BackColor = Color.Green
            btnCell.Style.ForeColor = Color.White
            row.Cells("chosenCh").Value = chosenCh

            If channelUsage.ContainsKey(chosenCh) Then
                channelUsage(chosenCh) += 1
            Else
                channelUsage(chosenCh) = 1
            End If
        Next
    End Sub

    Private Sub DataGridView1_CurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If DataGridView1.IsCurrentCellDirty Then
            DataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub DataGridView1_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 Then
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
                Return
            End If

            ApplyToBaseStation(e.RowIndex)
        End If
    End Sub

    Private Sub ApplyToBaseStation(rowIndex As Integer)
        Try
            Dim row As DataGridViewRow = DataGridView1.Rows(rowIndex)

            Dim chosenObj = row.Cells("chosenCh").Value
            Dim channelNumber As Integer = If(chosenObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(chosenObj.ToString()), Convert.ToInt32(chosenObj), 0)

            If channelNumber < 5 Then
                Dim isLte As Integer = 0
                Using conn As New SqlClient.SqlConnection(connectionString)
                    conn.Open()
                    Dim sql As String = "SELECT ISNULL(is_lte,0) FROM base_stations WHERE channel_number = @channel_number"
                    Using cmd As New SqlClient.SqlCommand(sql, conn)
                        cmd.Parameters.AddWithValue("@channel_number", channelNumber)
                        Dim result = cmd.ExecuteScalar()
                        If result IsNot Nothing Then
                            isLte = Convert.ToInt32(result)
                        End If
                    End Using
                End Using

                If isLte = 1 Then
                    MessageBox.Show("Your channel is in LTE mode, switch RAT first.")
                    Return
                End If
            End If

            Dim gsmId1 As Integer = Convert.ToInt32(row.Cells("gsmId").Value)

            If gsmId1 <= 0 Then
                MessageBox.Show("No gsm_id found for this row. Cannot fetch gsm_cells record.")
                Return
            End If

            Dim arfcn As Integer = 0
            Dim mcc As Integer = 0
            Dim mnc As Integer = 0
            Dim lac As Integer = 0
            Dim cellId As Long = 0L
            Dim bsic As Integer = 0
            Dim band As String = String.Empty

            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim sql As String = "
                SELECT gsm_id, plmn, mcc, mnc, arfcn, lac, cell_id, bsic, band, rssi, [Timestamp]
                FROM gsm_cells
                WHERE gsm_id = @gsmId
            "
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.AddWithValue("@gsmId", gsmId1)

                    Using rdr As SqlDataReader = cmd.ExecuteReader()
                        If rdr.Read() Then
                            If Not rdr.IsDBNull(rdr.GetOrdinal("mcc")) Then mcc = Convert.ToInt32(rdr("mcc"))
                            If Not rdr.IsDBNull(rdr.GetOrdinal("mnc")) Then mnc = Convert.ToInt32(rdr("mnc"))
                            If Not rdr.IsDBNull(rdr.GetOrdinal("lac")) Then lac = Convert.ToInt32(rdr("lac"))
                            If Not rdr.IsDBNull(rdr.GetOrdinal("cell_id")) Then cellId = Convert.ToInt64(rdr("cell_id"))
                            If Not rdr.IsDBNull(rdr.GetOrdinal("bsic")) Then bsic = Convert.ToInt32(rdr("bsic"))
                            If Not rdr.IsDBNull(rdr.GetOrdinal("band")) Then band = rdr("band").ToString()
                        Else
                            MessageBox.Show($"No gsm_cells record found for gsm_id = {gsmId1}.")
                            Return
                        End If
                    End Using
                End Using
            End Using

            Dim arfcnObj = row.Cells("channel").Value
            arfcn = Convert.ToInt32(arfcnObj)

            ApplyToChannel(channelNumber, gsmId1, arfcn, mcc, mnc, lac, cellId, bsic, band)
            Dim ipA As String = Form1.GetChannelIPAddress(channelNumber)
            Form1.SendSwitchCommand(ipA, "gsm")
            Form1.ApplyGsmBaseChannelSettings(ipA, mcc, mnc, arfcn, bsic, lac, cellId)
            MessageBox.Show($"Applied to channel: {channelNumber})")
            UpdateApplyButtonsState()
        Catch ex As Exception
            Console.WriteLine("Error applying to base station: " & ex.Message)
        End Try
    End Sub

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

                Dim bandMHz As Integer = ExtractBandMHz(band)

                If exists > 0 Then
                    Dim updateQuery As String = "UPDATE base_stations SET gsm_id = @gsmId, earfcn = @earfcn, 
                                              mcc = @mcc, mnc = @mnc, lac = @lac, cid = @cellId, 
                                              bsic = @bsic, band = @band, is_gsm = 1, is_lte = 0, is_wcdma= 0, last_updated = SYSUTCDATETIME()
                                              WHERE channel_number = @channelNumber"

                    Using updateCmd As New SqlCommand(updateQuery, connection)
                        updateCmd.Parameters.AddWithValue("@gsmId", gsmId)
                        updateCmd.Parameters.AddWithValue("@earfcn", arfcn)
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
                                              VALUES (@channelNumber, 1, @gsmId, @earfcn, @mcc, @mnc, 
                                              @lac, @cellId, @bsic, @band, SYSUTCDATETIME())"

                    Using insertCmd As New SqlCommand(insertQuery, connection)
                        insertCmd.Parameters.AddWithValue("@channelNumber", channelNumber)
                        insertCmd.Parameters.AddWithValue("@gsmId", gsmId)
                        insertCmd.Parameters.AddWithValue("@earfcn", arfcn)
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
        Catch ex As Exception
            MessageBox.Show("Error applying to channel " & channelNumber & ": " & ex.Message)
        End Try
    End Sub


    Private Function ExtractBandMHz(band As String) As Integer
        If String.IsNullOrWhiteSpace(band) Then Return 0

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
                If row.Cells("channel").Value IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(row.Cells("channel").Value.ToString()) Then
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