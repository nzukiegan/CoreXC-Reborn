Imports System.Data.SqlClient
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Drawing

Public Class Form5
    Private ReadOnly connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"
    Private providerFilter As List(Of String)

    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(filter As List(Of String))
        providerFilter = filter
        InitializeComponent()
    End Sub

    Private Sub Form5_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ApplyTheme()
        LoadLTECellsData()

        AddHandler DataGridView1.CellValueChanged, AddressOf DataGridView1_CellValueChanged
        AddHandler DataGridView1.CurrentCellDirtyStateChanged, AddressOf DataGridView1_CurrentCellDirtyStateChanged
    End Sub

    Private Shared Function ParseNbEarfcn(raw As String) As List(Of KeyValuePair(Of Integer, Integer))
        Dim res As New List(Of KeyValuePair(Of Integer, Integer))()
        If String.IsNullOrWhiteSpace(raw) Then Return res
        Dim pairRx As New Regex("\[\s*(\d+)\s*[,;\s]\s*(\d+)\s*\]", RegexOptions.Compiled)
        For Each m As Match In pairRx.Matches(raw)
            Dim ear As Integer = Integer.Parse(m.Groups(1).Value)
            Dim wt As Integer = Integer.Parse(m.Groups(2).Value)
            res.Add(New KeyValuePair(Of Integer, Integer)(ear, wt))
        Next
        Return res
    End Function

    Private Shared Function RecommendEarfcnFromPairs(pairs As List(Of KeyValuePair(Of Integer, Integer))) As Integer?
        If pairs Is Nothing OrElse pairs.Count = 0 Then Return Nothing
        Dim maxW = pairs.Max(Function(p) p.Value)
        Dim candidates = pairs.Where(Function(p) p.Value = maxW).Select(Function(p) p.Key)
        Return candidates.Min()
    End Function

    Private Shared Function MapEarfcnToBandInfo(earfcn As Integer) As (bx As String, mhz As String, duplex As String)
        Dim data = {
        Tuple.Create(1, 599, "B1", "2100", "FDD-LTE"),
        Tuple.Create(600, 1199, "B2", "1900", "FDD-LTE"),
        Tuple.Create(1200, 1949, "B3", "1800", "FDD-LTE"),
        Tuple.Create(1950, 2399, "B4", "1700", "FDD-LTE"),
        Tuple.Create(2400, 2649, "B5", "850", "FDD-LTE"),
        Tuple.Create(2650, 2749, "B6", "800", "FDD-LTE"),
        Tuple.Create(2750, 3449, "B7", "2600", "FDD-LTE"),
        Tuple.Create(3450, 3799, "B8", "900", "FDD-LTE"),
        Tuple.Create(3800, 4149, "B9", "1800", "FDD-LTE"),
        Tuple.Create(4150, 4749, "B10", "1700", "FDD-LTE"),
        Tuple.Create(4750, 4999, "B11", "1500", "FDD-LTE"),
        Tuple.Create(5000, 5179, "B12", "700", "FDD-LTE"),
        Tuple.Create(5180, 5279, "B13", "700", "FDD-LTE"),
        Tuple.Create(5280, 5379, "B14", "700", "FDD-LTE"),
        Tuple.Create(5730, 5849, "B17", "700", "FDD-LTE"),
        Tuple.Create(5850, 5999, "B18", "800", "FDD-LTE"),
        Tuple.Create(6000, 6149, "B19", "850", "FDD-LTE"),
        Tuple.Create(6150, 6449, "B20", "800", "FDD-LTE"),
        Tuple.Create(6450, 6599, "B21", "1500", "FDD-LTE"),
        Tuple.Create(6600, 7399, "B28", "700", "FDD-LTE"),
        Tuple.Create(9000, 9209, "B32", "1500", "FDD-LTE"),
        Tuple.Create(36000, 36199, "B33", "1900", "TDD-LTE"),
        Tuple.Create(36200, 36349, "B34", "2000", "TDD-LTE"),
        Tuple.Create(36350, 36949, "B35", "1900", "TDD-LTE"),
        Tuple.Create(36950, 37549, "B36", "1900", "TDD-LTE"),
        Tuple.Create(37550, 37749, "B37", "1900", "TDD-LTE"),
        Tuple.Create(37750, 38249, "B38", "2600", "TDD-LTE"),
        Tuple.Create(38250, 38649, "B39", "1900", "TDD-LTE"),
        Tuple.Create(38650, 39649, "B40", "2300", "TDD-LTE"),
        Tuple.Create(39650, 41589, "B41", "2500", "TDD-LTE"),
        Tuple.Create(41590, 43589, "B42", "3500", "TDD-LTE"),
        Tuple.Create(43590, 45589, "B43", "3700", "TDD-LTE")
    }

        For Each t In data
            If earfcn >= t.Item1 AndAlso earfcn <= t.Item2 Then
                Return (t.Item3, t.Item4, t.Item5)
            End If
        Next
        Return ("", "", "")
    End Function

    Private Shared Function ChannelsForBandCode(bx As String) As Integer()
        Select Case bx.ToUpperInvariant()
            Case "B8"  ' 900 MHz
                Return {1, 2}
            Case "B3"  ' 1800 MHz
                Return {3, 4}
            Case "B1"  ' 2100 MHz
                Return {5, 6}
            Case "B5"  ' 850 MHz
                Return {7, 8}
            Case "B40" ' 2300 MHz (TDD)
                Return {9, 10}
            Case "B28" ' 700 MHz
                Return {11, 12}
            Case "B7"  ' 2600 MHz
                Return {13, 14}
            Case Else
                Return Array.Empty(Of Integer)()
        End Select
    End Function

    Private Sub LoadLTECellsData()
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                Dim filters As List(Of String) = Nothing

                If providerFilter IsNot Nothing AndAlso providerFilter.Count > 0 Then
                    filters = providerFilter.Select(Function(f) f.ToLower().Trim()).ToList()
                Else
                    filters = New List(Of String)()
                End If


                Dim lteQuery As String = "SELECT lte_id, provider_name, plmn, mcc, mnc, band, earfcn, nb_earfcn, cell_id, rsrp, [Timestamp]
                          FROM lte_cells"

                If filters.Count > 0 AndAlso Not filters.Contains("All") Then
                    Dim paramNames As New List(Of String)()
                    For i As Integer = 0 To filters.Count - 1
                        paramNames.Add("@p" & i)
                    Next
                    lteQuery &= " WHERE LOWER(provider_name) IN (" & String.Join(",", paramNames) & ")"
                End If


                Dim lteTable As New DataTable()
                Using adapter As New SqlDataAdapter(lteQuery, connection)
                    If filters.Count > 0 AndAlso Not filters.Contains("All") Then
                        For i As Integer = 0 To filters.Count - 1
                            adapter.SelectCommand.Parameters.AddWithValue("@p" & i, filters(i))
                            Console.WriteLine($"Parameter added: @p{i} = {filters(i)}")
                        Next
                    End If
                    adapter.Fill(lteTable)
                End Using

                Dim result As New DataTable()
                result.Columns.Add("channel", GetType(Integer))
                result.Columns.Add("mcc", GetType(Integer))
                result.Columns.Add("mnc", GetType(Integer))
                result.Columns.Add("band", GetType(String))
                result.Columns.Add("earfcn", GetType(Integer))
                result.Columns.Add("lte_id", GetType(Integer))
                result.Columns.Add("cell_id", GetType(Long))

                For ch As Integer = 1 To 14
                    Dim r = result.NewRow()
                    r("channel") = ch
                    r("band") = ChannelDefaultBand(ch)
                    r("mcc") = DBNull.Value
                    r("mnc") = DBNull.Value
                    r("earfcn") = DBNull.Value
                    r("lte_id") = DBNull.Value
                    r("cell_id") = DBNull.Value
                    result.Rows.Add(r)
                Next

                Dim filled As New HashSet(Of Integer)()

                For Each row As DataRow In lteTable.Rows
                    Dim earfcnList As New List(Of Integer)()

                    If Not row.IsNull("earfcn") Then
                        Dim mainStr = row("earfcn").ToString().Trim()
                        Dim mainVal As Integer
                        If Integer.TryParse(mainStr, mainVal) Then
                            earfcnList.Add(mainVal)
                        End If
                    End If

                    Dim nbRaw As String = If(row("nb_earfcn") Is DBNull.Value, "", row("nb_earfcn").ToString())
                    If Not String.IsNullOrWhiteSpace(nbRaw) Then
                        Dim matches = System.Text.RegularExpressions.Regex.Matches(nbRaw, "\[(\d+),\s*\d+\]")
                        For Each m As Match In matches
                            Dim nbVal As Integer
                            If Integer.TryParse(m.Groups(1).Value, nbVal) Then
                                earfcnList.Add(nbVal)
                            End If
                        Next
                    End If

                    If earfcnList.Count = 0 Then Continue For

                    For Each earfcn As Integer In earfcnList
                        Dim bandInfo = MapEarfcnToBandInfo(earfcn)
                        If String.IsNullOrEmpty(bandInfo.bx) Then Continue For

                        Dim targetCh = ChannelsForBandCode(bandInfo.bx)
                        If targetCh.Length = 0 Then Continue For

                        For Each ch In targetCh
                            If Not filled.Contains(ch) Then
                                Dim dst = result.Rows.Cast(Of DataRow)().First(Function(rr) Convert.ToInt32(rr("channel")) = ch)

                                dst("band") = bandInfo.bx & " (" & bandInfo.mhz & " MHz)"
                                dst("mcc") = If(row("mcc") Is DBNull.Value, DBNull.Value, Convert.ToInt32(row("mcc")))
                                dst("mnc") = If(row("mnc") Is DBNull.Value, DBNull.Value, Convert.ToInt32(row("mnc")))
                                dst("earfcn") = earfcn
                                dst("lte_id") = If(row("lte_id") Is DBNull.Value, DBNull.Value, Convert.ToInt32(row("lte_id")))
                                dst("cell_id") = If(row("cell_id") Is DBNull.Value, DBNull.Value, Convert.ToInt64(row("cell_id")))

                                filled.Add(ch)
                                Exit For
                            End If
                        Next
                    Next
                Next


                DataGridView1.DataSource = result
                DataGridView1.AutoGenerateColumns = False
                DataGridView1.Columns.Clear()

                Dim colChannel As New DataGridViewTextBoxColumn() With {.Name = "channel", .HeaderText = "CHANNEL", .DataPropertyName = "channel", .ReadOnly = True, .Width = 60}
                Dim colMcc As New DataGridViewTextBoxColumn() With {.Name = "mcc", .HeaderText = "MCC", .DataPropertyName = "mcc", .Width = 80}
                Dim colMnc As New DataGridViewTextBoxColumn() With {.Name = "mnc", .HeaderText = "MNC", .DataPropertyName = "mnc", .Width = 80}
                Dim colBand As New DataGridViewTextBoxColumn() With {.Name = "band", .HeaderText = "BAND", .DataPropertyName = "band", .Width = 180}
                Dim colEarfcn As New DataGridViewTextBoxColumn() With {.Name = "earfcn", .HeaderText = "EARFCN", .DataPropertyName = "earfcn", .Width = 100}
                Dim colLteId As New DataGridViewTextBoxColumn() With {.Name = "lte_id", .DataPropertyName = "lte_id", .Visible = False}
                Dim colCellId As New DataGridViewTextBoxColumn() With {.Name = "cell_id", .DataPropertyName = "cell_id", .Visible = False}

                DataGridView1.Columns.AddRange({colChannel, colMcc, colMnc, colBand, colEarfcn, colLteId, colCellId})

                Dim applyColumn As New DataGridViewButtonColumn() With {
                    .Name = "apply",
                    .HeaderText = "Apply Choice",
                    .Text = "Apply",
                    .UseColumnTextForButtonValue = False,
                    .Width = 90
                }
                applyColumn.DefaultCellStyle.BackColor = Color.Green
                applyColumn.DefaultCellStyle.ForeColor = Color.White
                DataGridView1.Columns.Add(applyColumn)

                DataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                UpdateApplyButtonsState()
            End Using

        Catch ex As Exception
            Console.WriteLine("Error loading LTE channels: " & ex.Message)
        End Try
    End Sub


    Private Function ChannelDefaultBand(channel As Integer) As String
        Select Case channel
            Case 1, 2
                Return "Band 8 (900 MHz)"
            Case 3, 4
                Return "Band 3 (1800 MHz)"
            Case 5, 6
                Return "Band 1 (2100 MHz)"
            Case 7, 8
                Return "Band 5 (850 MHz)"
            Case 9
                Return "Band 40 (TDD 2300 MHz)"
            Case 10
                Return "Band 40 (TDD 2300 MHz)"
            Case 11, 12
                Return "Band 28 (700 MHz)"
            Case 13, 14
                Return "Band 7 (2600 MHz)"
            Case Else
                Return "Unknown"
        End Select
    End Function

    Private Sub UpdateApplyButtonsState()
        For Each row As DataGridViewRow In DataGridView1.Rows
            If row.IsNewRow Then Continue For

            Dim btnCell = TryCast(row.Cells("apply"), DataGridViewButtonCell)
            If btnCell Is Nothing Then Continue For

            Dim earfcnObj = row.Cells("earfcn").Value

            If earfcnObj Is Nothing OrElse String.IsNullOrWhiteSpace(earfcnObj.ToString()) Then
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

    Private Sub DataGridView1_CurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If DataGridView1.IsCurrentCellDirty Then
            DataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub DataGridView1_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 Then
            If DataGridView1.Columns(e.ColumnIndex).Name = "earfcn" OrElse DataGridView1.Columns(e.ColumnIndex).Name = "band" Then
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

    Private Sub ApplyToBaseStation(rowIndex As Integer)
        Dim row As DataGridViewRow = DataGridView1.Rows(rowIndex)
        Dim channelNumber As Integer = Convert.ToInt32(row.Cells("channel").Value)

        Dim earfcnObj = row.Cells("earfcn").Value
        Dim bandObj = row.Cells("band").Value
        Dim mccObj = row.Cells("mcc").Value
        Dim mncObj = row.Cells("mnc").Value
        Dim lteIdObj = row.Cells("lte_id").Value
        Dim cellIdObj = row.Cells("cell_id").Value

        If earfcnObj Is Nothing OrElse String.IsNullOrWhiteSpace(earfcnObj.ToString()) Then
            MessageBox.Show($"Channel {channelNumber} has no candidate EARFCN / band. Nothing to apply.")
            Return
        End If

        Dim earfcn As Integer = Convert.ToInt32(earfcnObj)
        Dim mcc As Integer = If(mccObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(mccObj.ToString()), Convert.ToInt32(mccObj), 0)
        Dim mnc As Integer = If(mncObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(mncObj.ToString()), Convert.ToInt32(mncObj), 0)
        Dim band As String = If(bandObj IsNot Nothing, bandObj.ToString(), String.Empty)
        Dim lteId As Integer = If(lteIdObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(lteIdObj.ToString()), Convert.ToInt32(lteIdObj), 0)
        Dim cellId As Long = If(cellIdObj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(cellIdObj.ToString()), Convert.ToInt64(cellIdObj), 0L)

        ApplyToChannel(channelNumber, lteId, earfcn, mcc, mnc, 0, cellId, band)

        MessageBox.Show($"Applied to channel: {channelNumber}")

        UpdateApplyButtonsState()
    End Sub

    ' ---------------------------
    Private Sub ApplyToChannel(channelNumber As Integer, lteId As Integer, earfcn As Integer,
                              mcc As Integer, mnc As Integer, lac As Integer, cellId As Long, band As String)
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
                    Dim updateQuery As String = "UPDATE base_stations SET lte_id = @lteId, earfcn = @earfcn, 
                                              mcc = @mcc, mnc = @mnc, lac = @lac, cid = @cellId, 
                                              band = @band, is_lte = 1, last_updated = SYSUTCDATETIME()
                                              WHERE channel_number = @channelNumber"

                    Using updateCmd As New SqlCommand(updateQuery, connection)
                        updateCmd.Parameters.AddWithValue("@lteId", lteId)
                        updateCmd.Parameters.AddWithValue("@earfcn", earfcn)
                        updateCmd.Parameters.AddWithValue("@mcc", mcc)
                        updateCmd.Parameters.AddWithValue("@mnc", mnc)
                        updateCmd.Parameters.AddWithValue("@lac", lac)
                        updateCmd.Parameters.AddWithValue("@cellId", cellId)

                        If bandMHz > 0 Then
                            updateCmd.Parameters.AddWithValue("@band", bandMHz)
                        Else
                            updateCmd.Parameters.AddWithValue("@band", DBNull.Value)
                        End If

                        updateCmd.Parameters.AddWithValue("@channelNumber", channelNumber)

                        updateCmd.ExecuteNonQuery()
                    End Using
                Else
                    Dim insertQuery As String = "INSERT INTO base_stations (channel_number, is_lte, lte_id, 
                                              earfcn, mcc, mnc, lac, cid, band, last_updated)
                                              VALUES (@channelNumber, 1, @lteId, @earfcn, @mcc, @mnc, 
                                              @lac, @cellId, @band, SYSUTCDATETIME())"

                    Using insertCmd As New SqlCommand(insertQuery, connection)
                        insertCmd.Parameters.AddWithValue("@channelNumber", channelNumber)
                        insertCmd.Parameters.AddWithValue("@lteId", lteId)
                        insertCmd.Parameters.AddWithValue("@earfcn", earfcn)
                        insertCmd.Parameters.AddWithValue("@mcc", mcc)
                        insertCmd.Parameters.AddWithValue("@mnc", mnc)
                        insertCmd.Parameters.AddWithValue("@lac", lac)
                        insertCmd.Parameters.AddWithValue("@cellId", cellId)

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

        ' look for explicit "XXX MHz"
        Dim m As Match = Regex.Match(band, "(\d{3,4})\s*MHz", RegexOptions.IgnoreCase)
        Dim value As Integer
        If m.Success AndAlso Integer.TryParse(m.Groups(1).Value, value) Then
            Return value
        End If

        ' fallback: map Band N to typical MHz (based on your mapping)
        m = Regex.Match(band, "Band\s*(\d+)", RegexOptions.IgnoreCase)
        If m.Success AndAlso Integer.TryParse(m.Groups(1).Value, value) Then
            Select Case value
                Case 1 : Return 2100
                Case 2 : Return 1900
                Case 3 : Return 1800
                Case 4 : Return 2100
                Case 5 : Return 850
                Case 7 : Return 2600
                Case 8 : Return 900
                Case 9 : Return 1800
                Case 12, 13 : Return 700
                Case 40 : Return 2300
                Case 41 : Return 2500
                Case Else : Return 0
            End Select
        End If

        ' final fallback: search for any 3-4 digit number
        m = Regex.Match(band, "(\d{3,4})")
        If m.Success AndAlso Integer.TryParse(m.Groups(1).Value, value) Then
            Return value
        End If

        Return 0
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        For Each row As DataGridViewRow In DataGridView1.Rows
            If Not row.IsNewRow Then
                If row.Cells("earfcn").Value IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(row.Cells("earfcn").Value.ToString()) Then
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
            .RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(128, 0, 0) ' dark maroon
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
