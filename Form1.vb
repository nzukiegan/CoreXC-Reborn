Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Data.SqlClient
Imports System.Drawing.Drawing2D
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Windows.Forms
Imports System.Windows.Drawing
Imports System.Windows.Forms.DataVisualization.Charting
Imports System.Security.Authentication.ExtendedProtection
Imports System.Runtime.Remoting.Channels
Imports System.Text.Json
Imports GMap.NET
Imports GMap.NET.MapProviders
Imports GMap.NET.WindowsForms
Imports GMap.NET.WindowsForms.Markers
Imports GMap.NET.WindowsForms.ToolTips

Public Class Form1

    Private connectionString As String = "Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Trusted_Connection=True;"
    Private buttonStates As New Dictionary(Of Integer, Boolean)()
    Private WithEvents pingTimer As System.Windows.Forms.Timer
    Private originalValues As New Dictionary(Of String, String)
    Private editModeButtons As New Dictionary(Of Integer, Button)()
    Private udpClientLteWcdma As UdpClient
    Private udpClientGsm As UdpClient
    Private receivingThread As Thread
    Private analyzingGsm As Boolean
    Private analyzingLteWcdma As Boolean
    Private progressPanel As Panel
    Private progressLabel As Label
    Private progressBar As ProgressBar
    Private WithEvents refreshTimer As New System.Windows.Forms.Timer()
    Private refreshAngle As Single = 0
    Private refreshIcon As Bitmap
    Private isRefreshing As Boolean = False
    Private refreshBtn As New Button
    Private listenerRunning As Boolean = False
    Private listenerTask As Task
    Private udpClients As New Dictionary(Of String, UdpClient)
    Private listener As UdpClient
    Private isListening As Boolean = False
    Private udp As UdpClient
    Private operatorFilter As List(Of String)
    Private operatorFilter1 As List(Of String)
    Private operatorFilter2 As List(Of String)
    Private operatorFilter3 As List(Of String)
    Private selectedProvider As String = ""
    Private selectedRowIndex As Integer = -1
    Private selectedGridView As DataGridView = Nothing
    Private providerLogos As New Dictionary(Of String, Image)(StringComparer.OrdinalIgnoreCase) From {
        {"indosat", My.Resources.indosat_logo},
        {"smarfren", My.Resources.Smartfren},
        {"telkomsel", My.Resources.Telkomsel},
        {"three", My.Resources.three},
        {"xlcomindo", My.Resources.XL_Image}
    }
    Private selectedSchema As String = String.Empty
    Private selectedLongitude As Double
    Private selectedLatitude As Double
    Private gmap As GMapControl
    Private selectedBimsi As String
    Private selectedBImei As String

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            analyzingGsm = False
            InitializeEditModeButtons()
            InitializeProgressIndicator()
            AddRefreshButton()
            Dim dbInitializer As New DatabaseInitializer("(localdb)\\MSSQLLocalDB", "CoreXCDb1")
            Await dbInitializer.EnsureDatabaseExistsAsync()
            Await dbInitializer.ApplySchemaAsync()
            Await dbInitializer.SeedOperatorsAsync()
            Await dbInitializer.InitializeBaseStations()
            disableAllBtns()
            LoadDataToGridViews()
            ApplyFilterToDataGridViews()
            LoadBaseStationData()
            AddInputConstraints()
            AddAdvancedConstraints()
            SetupValidationEvents()
            pingTimer = New System.Windows.Forms.Timer()
            pingTimer.Interval = 5000 ' Check every 5 seconds
            pingTimer.Start()
            Chart1.Series.Clear()
            Dim series As New Series("Series1")
            series.ChartType = SeriesChartType.Column
            series.IsValueShownAsLabel = True
            Chart1.Series.Add(series)

            Chart1.ChartAreas(0).AxisX.Title = "Provider"
            Chart1.ChartAreas(0).AxisY.Title = "Scan Count"
            Chart1.ChartAreas(0).AxisX.Interval = 1

            StyleChannelAnalyzerComponents()
            StyleSpecificColumns()

            AddHandler DataGridView3.SelectionChanged, AddressOf DataGridView_SelectionChanged
            AddHandler DataGridView2.SelectionChanged, AddressOf DataGridView_SelectionChanged
            AddHandler DataGridView1.SelectionChanged, AddressOf DataGridView_SelectionChanged
            AddHandler ComboBox12.SelectedIndexChanged, AddressOf TechnologyChanged_CH1
            AddHandler ComboBox13.SelectedIndexChanged, AddressOf TechnologyChanged_CH2
            AddHandler ComboBox14.SelectedIndexChanged, AddressOf TechnologyChanged_CH3
            AddHandler ComboBox15.SelectedIndexChanged, AddressOf TechnologyChanged_CH4

            InitializeGMap()

            LoadTaskingList()

            StartUdpListener()


            Task.Run(Sub() UpdateButtonColors())

            MessageBox.Show("Database and schema ready!", "Success")
        Catch ex As Exception
            Console.WriteLine("Database setup error: " & ex.StackTrace)
            MessageBox.Show("Database setup failed: ")
        End Try
    End Sub

    Private Sub InitializeGMap()
        gmap = New GMapControl()
        gmap.Dock = DockStyle.Fill
        Me.Controls.Add(gmap)

        GMaps.Instance.Mode = AccessMode.ServerAndCache
        gmap.MapProvider = GMapProviders.GoogleMap
        gmap.MinZoom = 1
        gmap.MaxZoom = 20
        gmap.Zoom = 12
    End Sub

    Private Sub ShowMapDirection(lat As Double, lon As Double, destLat As Double, destLon As Double)
        gmap.Position = New PointLatLng(lat, lon)

        ' Add markers
        Dim markers = New GMapOverlay("markers")
        Dim startMarker As New GMarkerGoogle(New PointLatLng(lat, lon), GMarkerGoogleType.green_dot)
        Dim endMarker As New GMarkerGoogle(New PointLatLng(destLat, destLon), GMarkerGoogleType.red_dot)
        markers.Markers.Add(startMarker)
        markers.Markers.Add(endMarker)
        gmap.Overlays.Add(markers)

        ' Request route (Google Directions API)
        Dim route As MapRoute = GMapProviders.GoogleMap.GetRoute(
            New PointLatLng(lat, lon),
            New PointLatLng(destLat, destLon),
            False, ' avoid highways?
            False, ' walking mode?
            15     ' zoom level
        )

        If route IsNot Nothing Then
            Dim r As New GMapRoute(route.Points, "Route")
            r.Stroke = New Pen(Color.Blue, 3)
            Dim routesOverlay As New GMapOverlay("routes")
            routesOverlay.Routes.Add(r)
            gmap.Overlays.Add(routesOverlay)
        Else
            MessageBox.Show("Route not found. Make sure internet is available or cache contains route data.", "Error")
        End If
    End Sub

    Private Sub ChannelAnalyzer_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ChannelAnalyzer.SelectedIndexChanged
        If ChannelAnalyzer.SelectedTab Is TabPage2 Then
            LoadBaseStationData1()
        End If
    End Sub

    Private Sub TechnologyChanged_CH1(sender As Object, e As EventArgs)
        If ComboBox12.SelectedItem IsNot Nothing Then
            If ComboBox12.SelectedItem.ToString() = "LTE - FDD" Then
                TextBox97.Enabled = False
            ElseIf ComboBox12.SelectedItem.ToString() = "GSM" Then
                TextBox97.Enabled = True
            End If
        End If
    End Sub

    Private Sub TechnologyChanged_CH2(sender As Object, e As EventArgs)
        If ComboBox13.SelectedItem IsNot Nothing Then
            If ComboBox13.SelectedItem.ToString() = "LTE - FDD" Then
                TextBox98.Enabled = False
            ElseIf ComboBox13.SelectedItem.ToString() = "GSM" Then
                TextBox98.Enabled = True
            End If
        End If
    End Sub

    Private Sub TechnologyChanged_CH3(sender As Object, e As EventArgs)
        If ComboBox14.SelectedItem IsNot Nothing Then
            If ComboBox14.SelectedItem.ToString() = "LTE - FDD" Then
                TextBox102.Enabled = False
            ElseIf ComboBox14.SelectedItem.ToString() = "GSM" Then
                TextBox102.Enabled = True
            End If
        End If
    End Sub

    Private Sub TechnologyChanged_CH4(sender As Object, e As EventArgs)
        If ComboBox15.SelectedItem IsNot Nothing Then
            If ComboBox15.SelectedItem.ToString() = "LTE - FDD" Then
                TextBox106.Enabled = False
            ElseIf ComboBox15.SelectedItem.ToString() = "GSM" Then
                TextBox106.Enabled = True
            End If
        End If
    End Sub

    Private Sub DataGridView_SelectionChanged(sender As Object, e As EventArgs)
        Dim dgv As DataGridView = TryCast(sender, DataGridView)
        If dgv Is Nothing Then Exit Sub



        If dgv.SelectedRows Is Nothing OrElse dgv.SelectedRows.Count = 0 Then Exit Sub

        Dim selectedRow As DataGridViewRow = dgv.SelectedRows(0)
        If selectedRow Is Nothing OrElse selectedRow.IsNewRow Then Exit Sub

        Dim providerName As String = Nothing


        If dgv Is DataGridView3 Then
            operatorFilter = New List(Of String)()
            providerName = Convert.ToString(selectedRow.Cells("Column28").Value)
        ElseIf dgv Is DataGridView1 Then
            operatorFilter2 = New List(Of String)()
            providerName = Convert.ToString(selectedRow.Cells("Column5").Value)
        ElseIf dgv Is DataGridView2 Then
            operatorFilter3 = New List(Of String)()
            providerName = Convert.ToString(selectedRow.Cells("Column16").Value)
        End If

        If Not String.IsNullOrEmpty(providerName) Then
            If dgv Is DataGridView3 Then
                operatorFilter.Add(providerName)
            ElseIf dgv Is DataGridView1 Then
                operatorFilter2.Add(providerName)
            ElseIf dgv Is DataGridView2 Then
                operatorFilter3.Add(providerName)
            End If
            selectedProvider = providerName
            selectedRowIndex = selectedRow.Index
            selectedGridView = dgv
        End If
    End Sub


    Private Sub DataGridView_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        Dim dgv As DataGridView = CType(sender, DataGridView)

        If e.RowIndex >= 0 AndAlso dgv Is selectedGridView AndAlso e.RowIndex = selectedRowIndex Then
            ClearDataGridViewSelection()
        End If
    End Sub

    Public Sub ClearDataGridViewSelection()
        If selectedGridView IsNot Nothing Then
            selectedGridView.ClearSelection()
            operatorFilter = New List(Of String)()
            selectedProvider = ""
            selectedRowIndex = -1
            selectedGridView = Nothing
        End If
    End Sub


    Private Function GetOrCreateClient(address As String) As UdpClient
        If Not udpClients.ContainsKey(address) Then
            udpClients(address) = New UdpClient()
        End If
        Return udpClients(address)
    End Function

    Private Async Sub StartCellOperation(address As String, button As Button)
        Console.WriteLine("Start cell operation callled, address: " & address)
        button.Enabled = False

        Try
            Dim command As String = "StartCell"
            Dim data As Byte() = Encoding.ASCII.GetBytes(command)
            udp.Send(data, data.Length, address, 9001)

        Catch ex As Exception
            Console.WriteLine($"Error communicating with {address}: {ex.Message}")
        Finally
            button.Enabled = True
            button.Text = "Start"
        End Try
    End Sub

    Private Async Sub StopCellOperation(address As String, button As Button)
        Console.WriteLine("Stop cell operation callled, address: " & address)
        Dim originalText As String = button.Text

        Try
            Dim command As String = "StopCell"
            Dim data As Byte() = Encoding.ASCII.GetBytes(command)
            udp.Send(data, data.Length, address, 9001)

        Catch ex As SocketException
            MessageBox.Show($"Could Not connect To cell at {address}. Please check the connection.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As TimeoutException
            MessageBox.Show($"Timeout While communicating With cell at {address}.", "Timeout Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show($"Error stopping cell at {address}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            button.Enabled = True
            button.Text = originalText
        End Try
    End Sub

    Private Sub InitializeProgressIndicator()
        progressPanel = New Panel()
        progressPanel.Size = New Size(400, 80)
        progressPanel.Visible = False
        progressLabel = New Label()
        progressLabel.Text = "Analyzing channels"
        progressLabel.ForeColor = Color.Black
        progressLabel.Location = New Point(60, 15)
        progressLabel.Size = New Size(400, 20)
        progressLabel.TextAlign = ContentAlignment.TopLeft
        progressLabel.Left = 0
        progressBar = New ProgressBar()
        progressBar.Style = ProgressBarStyle.Marquee
        progressBar.Location = New Point(20, 40)
        progressBar.Size = New Size(260, 5)

        progressPanel.Controls.Add(progressLabel)
        progressPanel.Controls.Add(progressBar)

        progressPanel.Location = New Point(Button1.Location.X, Button1.Location.Y + Button1.Height + 100)
        Me.Controls.Add(Me.progressPanel)
    End Sub

    Private Sub pingTimer_Tick(sender As Object, e As EventArgs) Handles pingTimer.Tick
        Task.Run(Sub() UpdateButtonColors())
    End Sub

    Private Function PingHost(ipAddress As String) As Boolean
        Try
            Dim ping As New Ping()
            Dim reply As PingReply = ping.Send(ipAddress, 1000)

            Return reply.Status = IPStatus.Success
        Catch
            Return False
        End Try
    End Function

    Private Sub UpdateButtonColors()
        Dim buttonIpMap As New Dictionary(Of Button, String) From {
        {Button6, "192.168.1.90"},   ' CH1
        {Button8, "192.168.1.91"},   ' CH2
        {Button11, "192.168.1.92"},  ' CH3
        {Button13, "192.168.1.93"},  ' CH4
        {Button15, "192.168.1.94"},  ' CH5
        {Button17, "192.168.1.95"},  ' CH6
        {Button19, "192.168.1.96"},  ' CH7
        {Button21, "192.168.1.97"},  ' CH8
        {Button31, "192.168.1.98"},  ' CH9
        {Button23, "192.168.1.101"}, ' CH11
        {Button25, "192.168.1.102"}, ' CH12
        {Button27, "192.168.1.103"}, ' CH13
        {Button29, "192.168.1.104"}  ' CH14
    }

        ' Update each button color based on ping response
        For Each kvp In buttonIpMap
            Dim isOnline As Boolean = PingHost(kvp.Value)

            ' Use Invoke to update UI thread safely
            If kvp.Key.InvokeRequired Then
                kvp.Key.Invoke(Sub()
                                   kvp.Key.BackColor = If(isOnline, Color.Lime, Color.Red)
                               End Sub)
            Else
                kvp.Key.BackColor = If(isOnline, Color.Lime, Color.Red)
            End If
        Next
    End Sub

    Private Sub LoadBlacklistData()
        Try
            Using connection As New SqlConnection(connectionString)
                Dim tableName As String = "[" & selectedSchema & "].[blacklist]"
                Dim query As String = "SELECT imsi, imei FROM " & tableName

                Dim adapter As New SqlDataAdapter(query, connection)
                Dim table As New DataTable()
                adapter.Fill(table)

                DataGridView9.DataSource = table
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading blacklist: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SearchIMSI()
        Dim searchImsi As String = TextBox96.Text.Trim()
        If String.IsNullOrEmpty(searchImsi) Then
            MessageBox.Show("Please enter an IMSI to search.", "Search IMSI", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim imsiCol As DataGridViewColumn = Nothing
        For Each col As DataGridViewColumn In DataGridView4.Columns
            If String.Equals(col.DataPropertyName, "imsi", StringComparison.OrdinalIgnoreCase) Then
                imsiCol = col
                Exit For
            End If
        Next

        If imsiCol Is Nothing Then
            MessageBox.Show("IMSI column not found in the grid.", "Search IMSI", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim foundIndex As Integer = -1
        For i As Integer = 0 To DataGridView4.Rows.Count - 1
            Dim dr As DataGridViewRow = DataGridView4.Rows(i)
            If dr.IsNewRow Then Continue For

            Dim cellVal As String = If(dr.Cells(imsiCol.Index).Value, String.Empty).ToString().Trim()
            If String.Equals(cellVal, searchImsi, StringComparison.OrdinalIgnoreCase) Then
                foundIndex = i
                Exit For
            End If
        Next

        If foundIndex = -1 Then
            MessageBox.Show($"IMSI '{searchImsi}' not found.", "Search IMSI", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        DataGridView4.ClearSelection()
        Dim foundRow As DataGridViewRow = DataGridView4.Rows(foundIndex)
        foundRow.Selected = True

        DataGridView4.CurrentCell = foundRow.Cells(imsiCol.Index)

        Try
            Dim firstIndex As Integer = Math.Max(0, foundIndex - 2)
            DataGridView4.FirstDisplayedScrollingRowIndex = firstIndex
        Catch ex As Exception

        End Try
    End Sub

    Private Sub ClearChannelsData()
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                Dim query As String = "
                DELETE FROM LTE_CELLS;
                DELETE FROM GSM_CELLS;
                DELETE FROM WCDMA_CELLS;
            "

                Using cmd As New SqlCommand(query, connection)
                    cmd.ExecuteNonQuery()
                End Using
            End Using

            If DataGridView1.InvokeRequired Then
                DataGridView1.Invoke(Sub() CType(DataGridView1.DataSource, DataTable).Clear())
            Else
                CType(DataGridView1.DataSource, DataTable).Clear()
            End If

            If DataGridView2.InvokeRequired Then
                DataGridView2.Invoke(Sub() CType(DataGridView1.DataSource, DataTable).Clear())
            Else
                CType(DataGridView2.DataSource, DataTable).Clear()
            End If

            If DataGridView3.InvokeRequired Then
                DataGridView3.Invoke(Sub() CType(DataGridView1.DataSource, DataTable).Clear())
            Else
                CType(DataGridView3.DataSource, DataTable).Clear()
            End If
        Catch ex As Exception
            Console.WriteLine("Error clearing channel data: " & ex.Message)
        End Try
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        StartChannelAnalyzer()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ShowProgressIndicator()
        progressLabel.Text = "Restarting Channel Analyzer"
        StartChannelAnalyzer()
    End Sub

    Private Sub StartUdpListener()
        If listenerRunning Then Return

        udp = New UdpClient(New IPEndPoint(IPAddress.Any, 9001))

        listenerRunning = True
        listenerTask = Task.Run(
        Async Function()

            While listenerRunning
                Try
                    Dim result As UdpReceiveResult = Await udp.ReceiveAsync()
                    Dim response As String = Encoding.ASCII.GetString(result.Buffer)
                    Dim senderIp As String = result.RemoteEndPoint.Address.ToString()
                    If senderIp = "192.168.1.99" OrElse senderIp = "192.168.1.100" Then
                        Me.Invoke(Sub()
                                      processResponse(response)
                                  End Sub)
                    Else
                        Try
                            ProcessLogEntry(response)
                        Catch ex As Exception
                            Console.WriteLine(ex.Message)
                        End Try
                    End If
                Catch ex As Exception
                    If listenerRunning Then
                        Me.Invoke(Sub()
                                      Console.WriteLine("Listener error: " & ex.Message)
                                  End Sub)
                    End If
                End Try
            End While
        End Function)
    End Sub

    Private Sub ProcessLogEntry(logLine As String)
        Dim pattern As String =
        "^(?<no>\d+)\s+\S+\s+(?<source>\S+)\s+" &
        "time\[(?<time>\d+)\]\s+" &
        "taType\[(?<event>[^\]]+)\]\s+" &
        "imsi\[(?<imsi>\d+)\]\s+" &
        "imei\[(?<imei>[^\]]+)\]\s+" &
        "ulSig\[(?<ulsig>\d+)\]\s+" &
        "ulTa\[(?<ta>\d+)\]\s+" &
        "bl_indi\[(?<count>\d+)\]\s+" &
        "tmsi\[(?<tmsi>[0-9A-Fa-f]+)\]\s*" &
        "lac\[(?<lac>\d+)\]\s+" &
        "dlrscp\[(?<rscp>\d+)\]\s*$"

        Dim m As Match = Regex.Match(logLine, pattern)
        If Not m.Success Then
            Throw New Exception("Log line format not recognized: " & logLine)
        End If

        Dim imsi As String = m.Groups("imsi").Value
        Dim mcc As String = If(imsi.Length >= 3, imsi.Substring(0, 3), "")
        Dim mnc As String = If(imsi.Length >= 5, imsi.Substring(3, 2), "")

        Dim latitude As String = ""
        Dim longitude As String = ""
        Dim lac As Integer = m.Groups("lac").Value

        GetCellLocation(mcc, mnc, lac, latitude, longitude)

        Dim dbHelper As New DatabaseHelper()
        Dim providerName As String = dbHelper.GetProviderName(mcc, mnc)

        Dim row As New Dictionary(Of String, Object) From {
            {"date_event", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"location_name", lac},
            {"source", m.Groups("source").Value},
            {"provider_name", providerName},
            {"mcc", mcc},
            {"mnc", mnc},
            {"imsi", imsi},
            {"imei", m.Groups("imei").Value},
            {"guti", "-"},
            {"signal_Level", m.Groups("rssi").Value},
            {"time_advance", m.Groups("ta").Value},
            {"phone_model", "N/A"},
            {"event", m.Groups("event").Value},
            {"longitude", longitude},
            {"latitude", latitude}
         }

        InsertScanResult(row)
    End Sub

    Public Sub InsertScanResult(row As Dictionary(Of String, Object))
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim checkSql As String = $"SELECT result_no, count FROM [{selectedSchema}].scan_results WHERE imsi = @imsi"
                Dim existingResultNo As Object = Nothing
                Dim existingCount As Integer = 0

                Using checkCmd As New SqlCommand(checkSql, conn)
                    checkCmd.Parameters.AddWithValue("@imsi", row("imsi"))
                    Using reader = checkCmd.ExecuteReader()
                        If reader.Read() Then
                            existingResultNo = reader("result_no")
                            existingCount = Convert.ToInt32(reader("count"))
                        End If
                    End Using
                End Using

                If existingResultNo IsNot Nothing Then
                    Dim updateSql As String = $"
                UPDATE [{selectedSchema}].scan_results 
                SET count = @newCount,
                    date_event = @date_event,
                    location_name = @location_name,
                    source = @source,
                    provider_name = @provider_name,
                    mcc = @mcc,
                    mnc = @mnc,
                    imei = @imei,
                    guti = @guti,
                    signal_level = @signal_level,
                    time_advance = @time_advance,
                    phone_model = @phone_model,
                    event = @event,
                    longitude = @longitude,
                    latitude = @latitude
                WHERE result_no = @result_no"

                    Using updateCmd As New SqlCommand(updateSql, conn)
                        updateCmd.Parameters.AddWithValue("@newCount", existingCount + 1)
                        updateCmd.Parameters.AddWithValue("@result_no", existingResultNo)

                        For Each kvp In row
                            updateCmd.Parameters.AddWithValue("@" & kvp.Key, If(kvp.Value, DBNull.Value))
                        Next

                        updateCmd.ExecuteNonQuery()
                    End Using
                Else
                    row("count") = 1
                    Dim columns = String.Join(",", row.Keys)
                    Dim parameters = String.Join(",", row.Keys.Select(Function(k) "@" & k))

                    Dim insertSql As String = $"INSERT INTO [{selectedSchema}].scan_results ({columns}) VALUES ({parameters})"

                    Using insertCmd As New SqlCommand(insertSql, conn)
                        For Each kvp In row
                            insertCmd.Parameters.AddWithValue("@" & kvp.Key, If(kvp.Value, DBNull.Value))
                        Next
                        insertCmd.ExecuteNonQuery()
                    End Using
                End If

                Dim sourceVal As String = Nothing
                If row.ContainsKey("source") AndAlso row("source") IsNot Nothing Then
                    sourceVal = row("source").ToString()
                End If

                If String.IsNullOrWhiteSpace(sourceVal) Then
                    Return
                End If

                Dim schemas As New List(Of String)()
                Dim schemasSql As String = "SELECT s.name FROM sys.schemas s JOIN sys.tables t ON t.schema_id = s.schema_id WHERE t.name = 'scan_results'"

                Using schemaCmd As New SqlCommand(schemasSql, conn)
                    Using rdr = schemaCmd.ExecuteReader()
                        While rdr.Read()
                            schemas.Add(rdr.GetString(0))
                        End While
                    End Using
                End Using

                Dim uniqueImsiCount As Integer = 0
                If schemas.Count > 0 Then
                    Dim parts As New List(Of String)
                    For Each s In schemas
                        parts.Add($"SELECT imsi FROM [{s}].scan_results WHERE source = @source AND imsi IS NOT NULL")
                    Next

                    Dim unionSql As String = String.Join(" UNION ", parts)
                    Dim countSql As String = $"SELECT COUNT(*) FROM ({unionSql}) AS u"

                    Using countCmd As New SqlCommand(countSql, conn)
                        countCmd.Parameters.AddWithValue("@source", sourceVal)
                        Dim result = countCmd.ExecuteScalar()
                        If result IsNot Nothing AndAlso Not IsDBNull(result) Then
                            uniqueImsiCount = Convert.ToInt32(result)
                        End If
                    End Using
                End If

                Dim channelNumber As Integer = -1
                Dim m As Text.RegularExpressions.Match = Regex.Match(sourceVal, "CH\s*(\d+)", RegexOptions.IgnoreCase)
                If m.Success Then
                    Integer.TryParse(m.Groups(1).Value, channelNumber)
                End If

                If channelNumber > 0 Then
                    Dim existsSql As String = "SELECT COUNT(*) FROM base_stations WHERE channel_number = @channel"
                    Dim existsCount As Integer = 0
                    Using existsCmd As New SqlCommand(existsSql, conn)
                        existsCmd.Parameters.AddWithValue("@channel", channelNumber)
                        existsCount = Convert.ToInt32(existsCmd.ExecuteScalar())
                    End Using

                    If existsCount > 0 Then
                        Dim updBaseSql As String = "UPDATE base_stations SET count = @count, last_updated = SYSUTCDATETIME() WHERE channel_number = @channel"
                        Using updCmd As New SqlCommand(updBaseSql, conn)
                            updCmd.Parameters.AddWithValue("@count", uniqueImsiCount)
                            updCmd.Parameters.AddWithValue("@channel", channelNumber)
                            updCmd.ExecuteNonQuery()
                        End Using
                    Else
                        Dim insBaseSql As String = "INSERT INTO base_stations (channel_number, count, last_updated) VALUES (@channel, @count, SYSUTCDATETIME())"
                        Using insCmd As New SqlCommand(insBaseSql, conn)
                            insCmd.Parameters.AddWithValue("@channel", channelNumber)
                            insCmd.Parameters.AddWithValue("@count", uniqueImsiCount)
                            insCmd.ExecuteNonQuery()
                        End Using
                    End If
                End If

                Dim channelToTextBox As New Dictionary(Of Integer, String) From {
                {1, "TextBox9"},
                {2, "TextBox10"},
                {3, "TextBox16"},
                {4, "TextBox22"},
                {5, "TextBox28"},
                {6, "TextBox34"},
                {7, "TextBox47"},
                {8, "TextBox54"},
                {9, "TextBox89"},
                {11, "TextBox61"},
                {12, "TextBox68"},
                {13, "TextBox75"},
                {14, "TextBox82"}
            }

                If channelNumber > 0 AndAlso channelToTextBox.ContainsKey(channelNumber) Then
                    Dim tbName As String = channelToTextBox(channelNumber)
                    Dim foundControls() As Control = Me.Controls.Find(tbName, True)
                    If foundControls IsNot Nothing AndAlso foundControls.Length > 0 Then
                        Dim tb As Control = foundControls(0)
                        Dim newText As String = uniqueImsiCount.ToString()
                        If tb.InvokeRequired Then
                            tb.Invoke(Sub() tb.Text = newText)
                        Else
                            tb.Text = newText
                        End If
                    End If
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine("InsertScanResult error: " & ex.ToString())
        End Try
    End Sub


    Private Sub updateScanResultDv(row As Dictionary(Of String, Object))
        Dim dt As DataTable = TryCast(DataGridView4.DataSource, DataTable)
        If dt Is Nothing Then Return

        If Not dt.Columns.Contains("rat") Then
            dt.Columns.Add("rat", GetType(String))
        End If

        Dim sourceStr As String = ""
        If row.ContainsKey("source") AndAlso row("source") IsNot Nothing Then
            sourceStr = row("source").ToString().Trim()
        End If

        Dim channelNumber As Integer = 0
        Dim chMatch = System.Text.RegularExpressions.Regex.Match(sourceStr, "\d+")
        If chMatch.Success Then
            Integer.TryParse(chMatch.Value, channelNumber)
        End If

        Dim rat As String = "UNKNOWN"
        If channelNumber > 0 Then
            Try
                Using conn As New SqlClient.SqlConnection(connectionString)
                    conn.Open()
                    Dim bsQuery As String = "
                    SELECT is_gsm, is_lte, is_wcdma
                    FROM base_stations
                    WHERE channel_number = @ch"
                    Using bsCmd As New SqlClient.SqlCommand(bsQuery, conn)
                        bsCmd.Parameters.AddWithValue("@ch", channelNumber)
                        Using r As SqlClient.SqlDataReader = bsCmd.ExecuteReader()
                            If r.Read() Then
                                Dim isGsm As Boolean = False
                                Dim isLte As Boolean = False
                                Dim isWcdma As Boolean = False

                                If Not IsDBNull(r("is_gsm")) Then isGsm = Convert.ToBoolean(r("is_gsm"))
                                If Not IsDBNull(r("is_lte")) Then isLte = Convert.ToBoolean(r("is_lte"))
                                If Not IsDBNull(r("is_wcdma")) Then isWcdma = Convert.ToBoolean(r("is_wcdma"))

                                If isGsm Then
                                    rat = "GSM"
                                ElseIf isWcdma Then
                                    rat = "WCDMA"
                                ElseIf isLte Then
                                    If channelNumber = 9 OrElse channelNumber = 10 Then
                                        rat = "LTE-TDD"
                                    Else
                                        rat = "LTE-FDD"
                                    End If
                                Else
                                    rat = "UNKNOWN"
                                End If
                            End If
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                rat = "UNKNOWN"
            End Try
        End If

        Dim imsiVal As String = ""
        If row.ContainsKey("imsi") AndAlso row("imsi") IsNot Nothing Then
            imsiVal = row("imsi").ToString()
        End If

        Dim existingRow As DataRow = Nothing
        For Each dr As DataRow In dt.Rows
            Dim drImsi As String = If(dr.Table.Columns.Contains("imsi") AndAlso Not dr.IsNull("imsi"), dr("imsi").ToString(), "")
            Dim drRat As String = If(dr.Table.Columns.Contains("rat") AndAlso Not dr.IsNull("rat"), dr("rat").ToString(), "")

            If String.Equals(drImsi, imsiVal, StringComparison.Ordinal) AndAlso
           String.Equals(drRat, rat, StringComparison.OrdinalIgnoreCase) Then
                existingRow = dr
                Exit For
            End If
        Next

        If existingRow IsNot Nothing Then
            Dim currentCount As Integer = 0
            If Not IsDBNull(existingRow("count")) Then
                Integer.TryParse(existingRow("count").ToString(), currentCount)
            End If
            existingRow("count") = currentCount + 1

            If existingRow.Table.Columns.Contains("date_event") Then
                existingRow("date_event") = DateTime.Now
            End If
        Else
            Dim newRow As DataRow = dt.NewRow()
            For Each kvp In row
                If dt.Columns.Contains(kvp.Key) Then
                    newRow(kvp.Key) = If(kvp.Value, DBNull.Value)
                End If
            Next

            If dt.Columns.Contains("date_event") Then
                newRow("date_event") = If(row.ContainsKey("date_event") AndAlso row("date_event") IsNot Nothing,
                                      row("date_event"), DateTime.Now)
            End If

            If dt.Columns.Contains("count") Then
                newRow("count") = 1
            End If

            newRow("rat") = rat

            dt.Rows.InsertAt(newRow, 0)
        End If
    End Sub


    Private Shared Sub GetCellLocation(mcc As String, mnc As String, lac As String, ByRef lat As String, ByRef lon As String)
        Dim apiKey As String = "pk.de77bc92497c387a48400e40fb6e7c5d"
        Dim url As String = $"https://opencellid.org/cell/get?key={apiKey}&mcc={mcc}&mnc={mnc}&lac={lac}&cellid=0&format=json"

        Try
            Dim client As New WebClient()
            Dim response As String = client.DownloadString(url)

            Dim doc As JsonDocument = JsonDocument.Parse(response)
            If doc.RootElement.TryGetProperty("lat", Nothing) AndAlso doc.RootElement.TryGetProperty("lon", Nothing) Then
                lat = doc.RootElement.GetProperty("lat").GetDouble().ToString()
                lon = doc.RootElement.GetProperty("lon").GetDouble().ToString()
            Else
                lat = ""
                lon = ""
            End If
        Catch ex As Exception
            lat = ""
            lon = ""
        End Try
    End Sub

    Private Sub StopUdpListener()
        listenerRunning = False
    End Sub

    Private Sub StartChannelAnalyzer()
        Button1.Enabled = False
        analyzingGsm = True
        analyzingLteWcdma = True
        ShowProgressIndicator()
        progressBar.Visible = True
        ClearChannelsData()

        Try
            Dim bytes = Encoding.ASCII.GetBytes("StartSniffer")
            udp.Send(bytes, bytes.Length, "192.168.1.99", 9001)
            udp.Send(bytes, bytes.Length, "192.168.1.100", 9001)

            PictureBox15.BackColor = Color.Green
            PictureBox16.BackColor = Color.Green
            PictureBox17.BackColor = Color.Green

            Console.WriteLine("StartSniffer requests sent.")
        Catch ex As Exception
            progressBar.Visible = False
            progressLabel.Text = "Error Connecting to boards. Try again."
            Button1.Enabled = True
        End Try
    End Sub


    Private Sub processResponse(response As String)
        Try
            If response.IndexOf("StartSniffer", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Console.WriteLine("StartSniffer → " & response)
                Return
            End If

            If response.IndexOf("GsmSnifferRsltIndi", StringComparison.OrdinalIgnoreCase) >= 0 Then
                ProcessGsmData(response)
                Return
            End If

            If response.IndexOf("LteSnifferRsltIndi", StringComparison.OrdinalIgnoreCase) >= 0 Then
                ProcessLteData(response)
                Return
            End If

            If response.IndexOf("WCDMA", StringComparison.OrdinalIgnoreCase) >= 0 Then
                ProcessWcdmaData(response)
                Return
            End If


        Catch ex As Exception
            Console.WriteLine("Error while processing response: " & ex.Message)
        End Try
    End Sub




    Private Sub ShowProgressIndicator()
        If Me.InvokeRequired Then
            Me.Invoke(Sub() ShowProgressIndicator())
        Else
            progressPanel.Visible = True
        End If
    End Sub

    Private Sub AddRefreshButton()
        refreshBtn.Size = New Size(80, 25)
        refreshBtn.Location = New Point(GroupBox31.Location.X + GroupBox31.Width - 60, GroupBox31.Location.Y - 2)

        refreshIcon = New Bitmap(My.Resources.refreshBtn, New Size(15, 15))
        refreshBtn.Image = refreshIcon
        refreshBtn.ImageAlign = ContentAlignment.MiddleLeft
        refreshBtn.TextImageRelation = TextImageRelation.ImageBeforeText
        refreshBtn.Text = "Refresh"
        refreshBtn.TextAlign = ContentAlignment.MiddleRight

        AddHandler refreshBtn.Click, AddressOf refreshBtn_Click

        Me.Controls.Add(refreshBtn)
        refreshBtn.BringToFront()

        refreshTimer.Interval = 50
    End Sub

    Private Async Sub refreshBtn_Click(sender As Object, e As EventArgs)
        If isRefreshing Then Return
        isRefreshing = True

        refreshTimer.Start()

        Try
            Await Task.Run(Sub()
                               LoadBaseStationData()
                               LoadBaseStationData1()
                               LoadBlacklistData()
                               LoadWhitelistData()
                               LoadChartData()
                               LoadScanResults()
                           End Sub)

            MessageBox.Show("Data refreshed successfully.", "Refreshed",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Error refreshing data: " & ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            refreshTimer.Stop()
            refreshBtn.Image = refreshIcon
            isRefreshing = False
        End Try
    End Sub


    Private Sub refreshTimer_Tick(sender As Object, e As EventArgs) Handles refreshTimer.Tick
        refreshAngle += 10
        If refreshAngle >= 360 Then refreshAngle = 0

        Dim rotated As New Bitmap(refreshIcon.Width, refreshIcon.Height)
        Using g As Graphics = Graphics.FromImage(rotated)
            g.TranslateTransform(refreshIcon.Width \ 2, refreshIcon.Height \ 2)
            g.RotateTransform(refreshAngle)
            g.TranslateTransform(-refreshIcon.Width \ 2, -refreshIcon.Height \ 2)
            g.DrawImage(refreshIcon, New Point(0, 0))
        End Using

        refreshBtn.Image = rotated
    End Sub


    Private Sub CheckAnalysisStatus()
        If Not analyzingGsm AndAlso Not analyzingLteWcdma Then
            If Me.InvokeRequired Then
                Me.Invoke(Sub() CheckAnalysisStatus())
            Else
                Button1.Enabled = True
                HideProgressIndicator()
            End If
        End If
    End Sub

    Private Sub HideProgressIndicator()
        If Me.InvokeRequired Then
            Me.Invoke(Sub() HideProgressIndicator())
        Else
            progressPanel.Visible = False
        End If
    End Sub

    Private Sub SafeUpdateLabel(text As String)
        If progressLabel.InvokeRequired Then
            progressLabel.Invoke(Sub() progressLabel.Text = text)
        Else
            progressLabel.Text = text
        End If
    End Sub

    Private Sub SafeUpdateProgressBar(visible As Boolean)
        If progressBar.InvokeRequired Then
            progressBar.Invoke(Sub() progressBar.Visible = visible)
        Else
            progressBar.Visible = visible
        End If
    End Sub


    Private Sub ProcessLteWcdmaData(data As String)

        Try
            Dim lines As String() = data.Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)

            For Each line As String In lines
                If line.ToLower().Contains("lte") Then
                    ProcessLteData(line)
                ElseIf line.ToLower().Contains("wcdma") Then
                    ProcessWcdmaData(line)
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("Error processing LTE/WCDMA data: " & ex.Message)
        End Try
    End Sub

    Private Sub ProcessGsmData(line As String)
        SafeUpdateLabel(line)
        Try
            Dim plmnMatch As Match = Regex.Match(line, "plmn\[(\d+)\]")
            Dim lacMatch As Match = Regex.Match(line, "lac\[(\d+)\]")
            Dim cidMatch As Match = Regex.Match(line, "cid\[(\d+)\]")
            Dim fcnMatch As Match = Regex.Match(line, "fcn\[(\d+)\]")
            Dim bsicMatch As Match = Regex.Match(line, "bsic\[(\d+)\]")
            Dim rssiMatch As Match = Regex.Match(line, "rssi\[(-?\d+)\]")
            Dim nbFreqMatch As Match = Regex.Match(line, "nbFreq\[(.*)\]")

            If plmnMatch.Success AndAlso cidMatch.Success Then
                Dim plmn As String = plmnMatch.Groups(1).Value
                Dim mcc As Integer = 0
                Dim mnc As Integer = 0

                If plmn = "51000" Then
                    plmn = "51028"
                End If

                If plmn.Length >= 5 Then
                    Integer.TryParse(plmn.Substring(0, 3), mcc)
                    Integer.TryParse(plmn.Substring(3), mnc)
                End If


                Dim lac As Integer = If(lacMatch.Success, Integer.Parse(lacMatch.Groups(1).Value), 0)
                Dim cellId As Long = Long.Parse(cidMatch.Groups(1).Value)
                Dim arfcn As Integer = If(fcnMatch.Success, Integer.Parse(fcnMatch.Groups(1).Value), 0)
                Dim bsic As Integer = If(bsicMatch.Success, Integer.Parse(bsicMatch.Groups(1).Value), 0)
                Dim rssi As Double = If(rssiMatch.Success, Double.Parse(rssiMatch.Groups(1).Value), 0)

                Dim providerName As String = GetOperatorCodeByPLMN(plmn)
                Dim band As String = MapArfcnToBand(arfcn)

                Dim nbFreq As String = If(nbFreqMatch.Success, "[" & nbFreqMatch.Groups(1).Value.Trim() & "]", "[]")

                InsertGsmData(providerName, plmn, mcc, mnc, band, arfcn, lac, nbFreq, cellId, bsic, rssi)
                UpdateGsmDataGridView(providerName, plmn, mcc, mnc, band, arfcn, lac, nbFreq, cellId, bsic)
            Else
                If line.IndexOf("GsmSnifferRsltIndi", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                    line.IndexOf("[-1]", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    analyzingGsm = False
                    PictureBox15.BackColor = Color.Red
                    If Not analyzingLteWcdma AndAlso Not analyzingGsm Then
                        SafeUpdateProgressBar(False)
                        SafeUpdateLabel("Analysis complete.")
                        Button1.Enabled = True
                    End If
                End If

            End If

        Catch ex As Exception
            Debug.WriteLine("Error processing GSM data: " & ex.ToString())
        End Try
    End Sub

    Private Function MapArfcnToBand(arfcn As Integer) As String
        If arfcn >= 1 AndAlso arfcn <= 124 Then
            Return "GSM 900"
        ElseIf arfcn >= 512 AndAlso arfcn <= 885 Then
            Return "GSM 1800"
        Else
            Return "Unknown"
        End If
    End Function

    Private Sub ProcessLteData(line As String)
        SafeUpdateLabel(line)
        Try
            Dim plmnMatch As Match = Regex.Match(line, "plmn\[(\d+)\]")
            Dim tacMatch As Match = Regex.Match(line, "tac\[(\d+)\]")
            Dim cidMatch As Match = Regex.Match(line, "cid\[(\d+)\]")
            Dim fcnMatch As Match = Regex.Match(line, "fcn\[(\d+)\]")
            Dim pciMatch As Match = Regex.Match(line, "pci\[(\d+)\]")
            Dim rsrpMatch As Match = Regex.Match(line, "rsrp\[(-?\d+)\]")
            Dim priMatch As Match = Regex.Match(line, "pri\[(\d+)\]")
            Dim nbFreqMatch As Match = Regex.Match(line, "nbFreq\[(.*)\]")

            If plmnMatch.Success AndAlso cidMatch.Success Then
                Dim plmn As String = plmnMatch.Groups(1).Value
                Dim mcc As Integer = 0
                Dim mnc As Integer = 0

                If plmn = "51000" Then
                    plmn = "51028"
                End If

                If plmn.Length >= 5 Then
                    Integer.TryParse(plmn.Substring(0, 3), mcc)
                    Integer.TryParse(plmn.Substring(3), mnc)
                End If

                Dim tac As Integer = If(tacMatch.Success, Integer.Parse(tacMatch.Groups(1).Value), 0)
                Dim cellId As Long = Long.Parse(cidMatch.Groups(1).Value)
                Dim earfcn As Integer = If(fcnMatch.Success, Integer.Parse(fcnMatch.Groups(1).Value), 0)
                Dim pci As Integer = If(pciMatch.Success, Integer.Parse(pciMatch.Groups(1).Value), 0)
                Dim rsrp As Double = If(rsrpMatch.Success, Double.Parse(rsrpMatch.Groups(1).Value), 0)
                Dim pri As Integer = If(priMatch.Success, Integer.Parse(priMatch.Groups(1).Value), 0)

                Dim providerName As String = GetOperatorCodeByPLMN(plmn)
                Dim band As String = GetBand(earfcn)

                Dim raw As String = If(nbFreqMatch.Success, nbFreqMatch.Groups(1).Value.Trim(), "")
                Dim nbEarfcn As String = raw.Replace(" measured[", "")
                If Not nbEarfcn.StartsWith("[[") Then
                    nbEarfcn = "[" & nbEarfcn
                End If

                Console.WriteLine("nbFreq: " & nbEarfcn)

                InsertLteData(providerName, plmn, mcc, mnc, band, pci, earfcn, nbEarfcn, tac, cellId, rsrp)
                UpdateLteDataGridView(providerName, plmn, mcc, mnc, band, pci, earfcn, nbEarfcn, tac, cellId, rsrp)
            Else
                If line.IndexOf("LteSnifferRsltIndi", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                   line.IndexOf("SNIFER", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                   line.IndexOf("SNIFFER240", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                   line.IndexOf("[-1]", StringComparison.OrdinalIgnoreCase) >= 0 Then

                    analyzingLteWcdma = False

                    PictureBox16.BackColor = Color.Red
                    PictureBox17.BackColor = Color.Red

                    If Not analyzingLteWcdma AndAlso Not analyzingGsm Then
                        SafeUpdateLabel("Analysis complete.")
                        Button1.Enabled = True
                    End If

                End If
            End If

        Catch ex As Exception
            Debug.WriteLine("Error processing LTE data: " & ex.ToString())
        End Try
    End Sub

    Public Function GetOperatorCodeByPLMN(plmn As String) As String
        Dim operatorCode As String = Nothing
        Dim query As String = "SELECT operator_code FROM operators WHERE plmn = @plmn"

        Using connection As New SqlConnection(connectionString)
            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@plmn", plmn)

                connection.Open()
                Dim result As Object = command.ExecuteScalar()
                If result IsNot Nothing Then
                    operatorCode = result.ToString()
                End If
            End Using
        End Using
        Console.WriteLine("Operator code : " & operatorCode)
        Console.WriteLine("plmn : " & plmn)
        Return operatorCode
    End Function

    Public Function GetBand(ByVal earfcn As Integer) As String
        Dim bands As (Name As String, NOffsDL As Integer, NMax As Integer)() = {
        ("Band 1 (2100 MHz)", 0, 599),
        ("Band 2 (1900 MHz)", 600, 1199),
        ("Band 3 (1800 MHz)", 1200, 1949),
        ("Band 4 (1700/2100 MHz)", 1950, 2399),
        ("Band 5 (850 MHz)", 2400, 2649),
        ("Band 7 (2600 MHz)", 2650, 3449),
        ("Band 8 (900 MHz)", 3450, 3799),
        ("Band 9 (1800 MHz)", 3800, 4149),
        ("Band 40 (TDD 2300 MHz)", 38650, 39649),
        ("Band 41 (TDD 2500 MHz)", 39650, 41589)
    }

        For Each b In bands
            If earfcn >= b.NOffsDL AndAlso earfcn <= b.NMax Then
                Return b.Name
            End If
        Next

        Return "Unknown"
    End Function


    Private Sub ProcessWcdmaData(line As String)
        SafeUpdateLabel(line)
        Try
            Dim parts As String() = line.Split(","c)

            If parts.Length >= 12 Then
                Dim providerName As String = parts(0).Trim()
                Dim plmn As String = parts(1).Trim()
                Dim mcc As Integer = Integer.Parse(parts(2).Trim())
                Dim mnc As Integer = Integer.Parse(parts(3).Trim())
                Dim band As String = parts(4).Trim()
                Dim psc As Integer = Integer.Parse(parts(5).Trim())
                Dim earfcn As Integer = Integer.Parse(parts(6).Trim())
                Dim nbsc As Integer = Integer.Parse(parts(7).Trim())
                Dim lac As Integer = Integer.Parse(parts(8).Trim())
                Dim cellId As Long = Long.Parse(parts(9).Trim())
                Dim rscp As Double = Double.Parse(parts(10).Trim())

                InsertWcdmaData(providerName, plmn, mcc, mnc, band, psc, earfcn, nbsc, lac, cellId, rscp)

                UpdateWcdmaDataGridView(providerName, plmn, mcc, mnc, band, psc, earfcn, nbsc, lac, cellId, rscp)
            End If
        Catch ex As Exception
            Debug.WriteLine("Error processing WCDMA data: " & ex.Message)
        End Try
    End Sub

    Private Sub InsertGsmData(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                             band As String, arfcn As Integer, lac As Integer, nbCell As String,
                             cellId As Long, bsic As Byte, rssi As Double)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "INSERT INTO gsm_cells (ProviderName, plmn, rat, mcc, mnc, band, arfcn, lac, nb_cell, cell_id, bsic) " &
                                 "VALUES (@ProviderName, @plmn, @rat, @mcc, @mnc, @band, @arfcn, @lac, @nbCell, @cellId, @bsic)"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ProviderName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@rat", "GSM")
                command.Parameters.AddWithValue("@mcc", mcc)
                command.Parameters.AddWithValue("@mnc", mnc)
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@arfcn", arfcn)
                command.Parameters.AddWithValue("@lac", lac)
                command.Parameters.AddWithValue("@nbCell", nbCell)
                command.Parameters.AddWithValue("@cellId", cellId)
                command.Parameters.AddWithValue("@bsic", bsic)
                command.Parameters.AddWithValue("rssi", rssi)

                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Sub InsertLteData(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                             band As String, pci As Integer, earfcn As Integer, nbEarfcn As String,
                             lac As Integer, cellId As Long, rsrp As Double)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "INSERT INTO lte_cells (provider_name, plmn, mcc, mnc, band, pci, earfcn, nb_earfcn, lac, cell_id, rsrp) " &
                                 "VALUES (@providerName, @plmn, @mcc, @mnc, @band, @pci, @earfcn, @nbEarfcn, @lac, @cellId, @rsrp)"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@providerName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@mcc", mcc)
                command.Parameters.AddWithValue("@mnc", mnc)
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@pci", pci)
                command.Parameters.AddWithValue("@earfcn", earfcn)
                command.Parameters.AddWithValue("@nbEarfcn", nbEarfcn)
                command.Parameters.AddWithValue("@lac", lac)
                command.Parameters.AddWithValue("@cellId", cellId)
                command.Parameters.AddWithValue("@rsrp", rsrp)

                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Sub InsertWcdmaData(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                               band As String, psc As Integer, earfcn As Integer, nbsc As Integer,
                               lac As Integer, cellId As Long, rscp As Double)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "INSERT INTO wcdma_cells (provider_name, plmn, mcc, mnc, band, psc, earfcn, nbsc, lac, cell_id, rscp) " &
                                 "VALUES (@providerName, @plmn, @mcc, @mnc, @band, @psc, @earfcn, @nbsc, @lac, @cellId, @rscp)"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@providerName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@mcc", mcc)
                command.Parameters.AddWithValue("@mnc", mnc)
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@psc", psc)
                command.Parameters.AddWithValue("@earfcn", earfcn)
                command.Parameters.AddWithValue("@nbsc", nbsc)
                command.Parameters.AddWithValue("@lac", lac)
                command.Parameters.AddWithValue("@cellId", cellId)
                command.Parameters.AddWithValue("@rscp", rscp)

                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Sub UpdateGsmDataGridView(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                                  band As String, arfcn As Integer, lac As Integer, nbCell As String,
                                  cellId As Long, bsic As Byte)

        If DataGridView1.InvokeRequired Then
            DataGridView1.Invoke(Sub() UpdateGsmDataGridView(providerName, plmn, mcc, mnc, band, arfcn, lac, nbCell, cellId, bsic))
            Return
        End If

        Dim dt As DataTable = TryCast(DataGridView1.DataSource, DataTable)
        If dt Is Nothing Then
            MessageBox.Show("DataGridView1 is not bound to a DataTable!")
            Return
        End If

        For Each colName In {"providerName", "plmn", "mcc", "mnc", "band", "arfcn", "lac", "nb_cell", "cell_id", "bsic"}
            If dt.Columns.Contains(colName) = False Then
                dt.Columns.Add(colName, GetType(String))
            ElseIf dt.Columns(colName).DataType IsNot GetType(String) Then
                Dim newColName = colName & "_str"
                dt.Columns.Add(newColName, GetType(String))
                For Each row2 As DataRow In dt.Rows
                    row2(newColName) = row2(colName).ToString()
                Next
                dt.Columns.Remove(colName)
                dt.Columns(newColName).ColumnName = colName
            End If
        Next

        Dim row As DataRow = dt.Rows.Cast(Of DataRow)().FirstOrDefault(Function(r) r("cell_id").ToString() = cellId.ToString())
        If row Is Nothing Then
            row = dt.NewRow()
            dt.Rows.Add(row)
        End If

        row("providerName") = If(String.IsNullOrEmpty(providerName), String.Empty, providerName)
        row("plmn") = If(String.IsNullOrEmpty(plmn), String.Empty, plmn)
        row("mcc") = mcc.ToString()
        row("rat") = "GSM"
        row("mnc") = mnc.ToString()
        row("band") = If(String.IsNullOrEmpty(band), String.Empty, band)
        row("arfcn") = arfcn.ToString()
        row("lac") = lac.ToString()
        row("nb_cell") = If(String.IsNullOrEmpty(nbCell), String.Empty, nbCell)
        row("cell_id") = cellId.ToString()
        row("bsic") = bsic.ToString()

        ApplyFilterToDataGridViews()
    End Sub


    Public Sub RefreshBaseStationChannel(channelNumber As Integer)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() RefreshBaseStationChannel(channelNumber))
            Return
        End If

        Console.WriteLine("Invoking base station channel " & channelNumber)

        ' Reload the specific channel data
        Select Case channelNumber
            Case 1
                LoadBaseStationChannel(1, TextBox4, TextBox5, TextBox6, TextBox7, TextBox9, TextBox8, TextBox40, ComboBox12)
            Case 2
                LoadBaseStationChannel(2, TextBox15, TextBox14, TextBox12, TextBox13, TextBox10, TextBox11, TextBox41, ComboBox13)
            Case 3
                LoadBaseStationChannel(3, TextBox21, TextBox20, TextBox18, TextBox19, TextBox16, TextBox17, TextBox42, ComboBox14)
            Case 4
                LoadBaseStationChannel(4, TextBox27, TextBox26, TextBox24, TextBox25, TextBox22, TextBox23, TextBox43, ComboBox15)
            Case 5
                LoadBaseStationChannel(5, TextBox33, TextBox32, TextBox30, TextBox31, TextBox28, TextBox29, TextBox44, ComboBox16)
            Case 6
                LoadBaseStationChannel(6, TextBox39, TextBox38, TextBox36, TextBox37, TextBox34, TextBox35, TextBox45, ComboBox17)
            Case 7
                LoadBaseStationChannel(7, TextBox52, TextBox51, TextBox49, TextBox50, TextBox47, TextBox48, TextBox46, ComboBox18)
            Case 8
                LoadBaseStationChannel(8, TextBox59, TextBox58, TextBox56, TextBox57, TextBox54, TextBox55, TextBox53, ComboBox19)
            Case 9
                LoadChannels9And10()
            Case 10
                LoadChannels9And10()
            Case 11
                LoadBaseStationChannel(11, TextBox65, TextBox66, TextBox63, TextBox64, TextBox61, TextBox62, TextBox60, ComboBox21)
            Case 12
                LoadBaseStationChannel(12, TextBox72, TextBox73, TextBox70, TextBox71, TextBox68, TextBox69, TextBox67, ComboBox22)
            Case 13
                LoadBaseStationChannel(13, TextBox79, TextBox80, TextBox77, TextBox78, TextBox75, TextBox76, TextBox74, ComboBox23)
            Case 14
                LoadBaseStationChannel(14, TextBox86, TextBox87, TextBox84, TextBox85, TextBox82, TextBox83, TextBox81, ComboBox24)
        End Select
    End Sub

    Private Sub UpdateLteDataGridView(providerName As String, plmn As String, mcc As Integer?, mnc As Integer?,
                                  band As String, pci As Integer?, earfcn As String, nbEarfcn As String,
                                  lac As Integer?, cellId As Long?, rsrp As Double?)

        If DataGridView3.InvokeRequired Then
            DataGridView3.Invoke(Sub() UpdateLteDataGridView(providerName, plmn, mcc, mnc, band, pci, earfcn, nbEarfcn, lac, cellId, rsrp))
            Return
        End If

        Dim dt As DataTable = TryCast(DataGridView3.DataSource, DataTable)
        If dt Is Nothing Then
            MessageBox.Show("DataGridView is not bound to a DataTable!")
            Return
        End If

        For Each colName In {"nb_earfcn", "rat", "band", "provider_name", "plmn", "earfcn"}
            If dt.Columns.Contains(colName) AndAlso dt.Columns(colName).DataType IsNot GetType(String) Then
                Dim newColName = colName & "_str"
                dt.Columns.Add(newColName, GetType(String))

                For Each row1 As DataRow In dt.Rows
                    row1(newColName) = row1(colName).ToString()
                Next

                dt.Columns.Remove(colName)
                dt.Columns(newColName).ColumnName = colName
            End If
        Next


        Dim row As DataRow = dt.NewRow()

        ' Assign safely
        row("provider_name") = If(String.IsNullOrEmpty(providerName), String.Empty, providerName)
        row("plmn") = If(String.IsNullOrEmpty(plmn), String.Empty, plmn)
        row("mcc") = If(mcc.HasValue, mcc.Value, DBNull.Value)
        row("mnc") = If(mnc.HasValue, mnc.Value, DBNull.Value)
        row("rat") = "LTE"
        row("band") = If(String.IsNullOrEmpty(band), String.Empty, band)
        row("earfcn") = If(String.IsNullOrEmpty(earfcn), String.Empty, earfcn)
        row("nb_earfcn") = If(String.IsNullOrEmpty(nbEarfcn), String.Empty, nbEarfcn)
        row("pci") = If(pci.HasValue, pci.Value, DBNull.Value)
        row("lac") = If(lac.HasValue, lac.Value, DBNull.Value)
        row("rsrp") = If(rsrp.HasValue, rsrp.Value, DBNull.Value)

        dt.Rows.Add(row)

        ApplyFilterToDataGridViews()
    End Sub

    Private Sub UpdateWcdmaDataGridView(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                                    band As String, psc As Integer, earfcn As Integer, nbsc As Integer,
                                    lac As Integer, cellId As Long, rscp As Double)

        If DataGridView2.InvokeRequired Then
            DataGridView2.Invoke(Sub() UpdateWcdmaDataGridView(providerName, plmn, mcc, mnc, band, psc, earfcn, nbsc, lac, cellId, rscp))
        Else
            Dim rowIndex As Integer = -1

            For i As Integer = 0 To DataGridView2.Rows.Count - 1
                If DataGridView2.Rows(i).Cells("Column10").Value IsNot Nothing AndAlso
               DataGridView2.Rows(i).Cells("Column10").Value.ToString() = cellId.ToString() Then
                    rowIndex = i
                    Exit For
                End If
            Next

            If rowIndex = -1 Then
                rowIndex = DataGridView2.Rows.Add()
            End If

            DataGridView2.Rows(rowIndex).Cells("Column5").Value = providerName
            DataGridView2.Rows(rowIndex).Cells("Column6").Value = plmn
            DataGridView2.Rows(rowIndex).Cells("Column14").Value = mcc
            DataGridView2.Rows(rowIndex).Cells("Column15").Value = mnc
            DataGridView2.Rows(rowIndex).Cells("Column7").Value = band
            DataGridView2.Rows(rowIndex).Cells("Column8").Value = psc
            DataGridView2.Rows(rowIndex).Cells("Column9").Value = earfcn
            DataGridView2.Rows(rowIndex).Cells("Column11").Value = nbsc
            DataGridView2.Rows(rowIndex).Cells("Column12").Value = lac
            DataGridView2.Rows(rowIndex).Cells("Column10").Value = cellId
            DataGridView2.Rows(rowIndex).Cells("Column13").Value = rscp
        End If

        ApplyFilterToDataGridViews()
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Try
            udp.Close()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub TabPage3_Enter(sender As Object, e As EventArgs) Handles TabPage3.Enter
        LoadScanResults()
        LoadChartData()
    End Sub

    Private Sub Button34_Click(sender As Object, e As EventArgs) Handles Button34.Click
        DataGridView4.DataSource = Nothing
        DataGridView4.Rows.Clear()
    End Sub

    Private Sub Button74_Click(sender As Object, e As EventArgs) Handles Button74.Click
        ' Example: load scan results for the selected schema
        If String.IsNullOrEmpty(selectedSchema) Then
            MessageBox.Show("Please select database first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        LoadScanResults()
    End Sub


    Private Sub LoadScanResults()
        Try
            If String.IsNullOrEmpty(selectedSchema) Then
                MessageBox.Show("Please select a database first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim query As String = $"
                SELECT result_no,
                       date_event,
                       location_name,
                       source,
                       provider_name,
                       mcc,
                       mnc,
                       imsi,
                       imei,
                       guti,
                       signal_level,
                       time_advance,
                       longitude,
                       latitude,
                       phone_model,
                       event,
                       count
                FROM [{selectedSchema}].scan_results
                ORDER BY date_event DESC"

                Using cmd As New SqlCommand(query, conn)
                    Using adapter As New SqlDataAdapter(cmd)
                        Dim dt As New DataTable()
                        adapter.Fill(dt)

                        If Not dt.Columns.Contains("rat") Then
                            dt.Columns.Add("rat", GetType(String))
                        End If

                        For Each row As DataRow In dt.Rows
                            Dim source As String = row("source").ToString()
                            Dim channelNumber As Integer = 0
                            If source.StartsWith("CH") Then
                                Integer.TryParse(source.Substring(2), channelNumber)
                            End If

                            If channelNumber > 0 Then
                                Dim bsQuery As String = "
                                SELECT is_gsm, is_lte, is_wcdma
                                FROM base_stations
                                WHERE channel_number = @ch"
                                Using bsCmd As New SqlCommand(bsQuery, conn)
                                    bsCmd.Parameters.AddWithValue("@ch", channelNumber)
                                    Using reader As SqlDataReader = bsCmd.ExecuteReader()
                                        If reader.Read() Then
                                            Dim isGsm As Boolean = Convert.ToBoolean(reader("is_gsm"))
                                            Dim isLte As Boolean = Convert.ToBoolean(reader("is_lte"))
                                            Dim isWcdma As Boolean = Convert.ToBoolean(reader("is_wcdma"))

                                            If isGsm Then
                                                row("rat") = "GSM"
                                            ElseIf isWcdma Then
                                                row("rat") = "WCDMA"
                                            ElseIf isLte Then
                                                If channelNumber = 9 OrElse channelNumber = 10 Then
                                                    row("rat") = "LTE-TDD"
                                                Else
                                                    row("rat") = "LTE-FDD"
                                                End If
                                            End If
                                        End If
                                    End Using
                                End Using
                            End If
                        Next

                        Me.Column43.DataPropertyName = "result_no"
                        Me.Column44.DataPropertyName = "date_event"
                        Me.Column45.DataPropertyName = "location_name"
                        Me.Column46.DataPropertyName = "source"
                        Me.Column51.DataPropertyName = "provider_name"
                        Me.Column52.DataPropertyName = "mcc"
                        Me.Column53.DataPropertyName = "mnc"
                        Me.Column47.DataPropertyName = "imsi"
                        Me.Column48.DataPropertyName = "imei"
                        Me.Column50.DataPropertyName = "guti"
                        Me.Column54.DataPropertyName = "count"
                        Me.Column55.DataPropertyName = "signal_level"
                        Me.Column56.DataPropertyName = "time_advance"
                        Me.Column57.DataPropertyName = "phone_model"
                        Me.Column58.DataPropertyName = "event"
                        Me.Column59.DataPropertyName = "longitude"
                        Me.Column60.DataPropertyName = "latitude"
                        Me.Column611.DataPropertyName = "rat"

                        DataGridView4.DataSource = dt
                    End Using
                End Using
            End Using

        Catch ex As Exception
            MessageBox.Show("Error loading scan results: " & ex.Message)
        End Try
    End Sub

    Private Sub Button36_Click(sender As Object, e As EventArgs) Handles Button36.Click
        SearchIMSI()
    End Sub

    Private Sub Button35_Click(sender As Object, e As EventArgs) Handles Button35.Click
        If selectedLongitude <> 0 AndAlso selectedLatitude <> 0 Then
            Dim loc = LocationHelper.GetCurrentLocation()
            ShowMapDirection(loc.Latitude, loc.Longitude, selectedLatitude, selectedLongitude)
        Else
            MessageBox.Show("Please select a row to display map.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub DataGridView4_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView4.CellClick
        If e.RowIndex < 0 Then Return

        Dim row As DataGridViewRow = DataGridView4.Rows(e.RowIndex)

        If row.Cells("longitude").Value IsNot Nothing AndAlso row.Cells("latitude").Value IsNot Nothing Then
            Double.TryParse(row.Cells("longitude").Value.ToString(), selectedLongitude)
            Double.TryParse(row.Cells("latitude").Value.ToString(), selectedLatitude)
        End If
    End Sub
    Private Sub LoadChartData()
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim tableName As String = "[" & selectedSchema & "].[scan_results]"

                Dim query As String = "
                SELECT source AS channel, COUNT(imsi) AS scan_count
                FROM " & tableName & "
                WHERE source IS NOT NULL AND source <> ''
                GROUP BY source
                ORDER BY channel"

                Using cmd As New SqlCommand(query, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        Chart1.Series("Series1").Points.Clear()

                        While reader.Read()
                            Dim channel As String = reader("channel").ToString()
                            Dim scanCount As Integer = Convert.ToInt32(reader("scan_count"))

                            Chart1.Series("Series1").Points.AddXY(channel, scanCount)
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading chart data: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Sub LoadWhitelistData()
        Try
            Using connection As New SqlConnection(connectionString)
                Dim tableName As String = "[" & selectedSchema & "].[whitelist]"
                Dim query As String = "SELECT imsi FROM " & tableName

                Dim adapter As New SqlDataAdapter(query, connection)
                Dim table As New DataTable()
                adapter.Fill(table)

                DataGridView10.DataSource = table
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading whitelist: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub



    Public Sub LoadBaseStationData1()
        Try
            LoadBaseStationChannel1(1, TextBox4, TextBox5, TextBox6, TextBox7, TextBox9, TextBox8, TextBox40, ComboBox12, PictureBox2)
            LoadBaseStationChannel1(2, TextBox15, TextBox14, TextBox12, TextBox13, TextBox10, TextBox11, TextBox41, ComboBox13, PictureBox3)
            LoadBaseStationChannel1(3, TextBox21, TextBox20, TextBox18, TextBox19, TextBox16, TextBox17, TextBox42, ComboBox14, PictureBox1)
            LoadBaseStationChannel1(4, TextBox27, TextBox26, TextBox24, TextBox25, TextBox22, TextBox23, TextBox43, ComboBox15, PictureBox5)
            LoadBaseStationChannel1(5, TextBox33, TextBox32, TextBox30, TextBox31, TextBox28, TextBox29, TextBox44, ComboBox16, PictureBox6)
            LoadBaseStationChannel1(6, TextBox39, TextBox38, TextBox36, TextBox37, TextBox34, TextBox35, TextBox45, ComboBox17, PictureBox7)
            LoadBaseStationChannel1(7, TextBox52, TextBox51, TextBox49, TextBox50, TextBox47, TextBox48, TextBox46, ComboBox18, PictureBox4)
            LoadBaseStationChannel1(8, TextBox59, TextBox58, TextBox56, TextBox57, TextBox54, TextBox55, TextBox53, ComboBox19, PictureBox12)

            Try
                LoadChannels9And10()
            Catch ex As Exception
                MessageBox.Show(
        $"Error loading channels 9 and 10:{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.StackTrace}",
        "Load Error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error
    )
            End Try

            LoadBaseStationChannel1(11, TextBox65, TextBox66, TextBox63, TextBox64, TextBox61, TextBox62, TextBox60, ComboBox21, PictureBox8)
            LoadBaseStationChannel1(12, TextBox72, TextBox73, TextBox70, TextBox71, TextBox68, TextBox69, TextBox67, ComboBox22, PictureBox11)
            LoadBaseStationChannel1(13, TextBox79, TextBox80, TextBox77, TextBox78, TextBox75, TextBox76, TextBox74, ComboBox23, PictureBox9)
            LoadBaseStationChannel1(14, TextBox86, TextBox87, TextBox84, TextBox85, TextBox82, TextBox83, TextBox81, ComboBox24, PictureBox13)

        Catch ex As Exception
            MessageBox.Show("Error loading base station data: " & ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadBaseStationChannel(channelNumber As Integer, txtMCC As TextBox, txtMNC As TextBox,
                                      txtCID As TextBox, txtLAC As TextBox, txtCount As TextBox,
                                      txtEarfcn As TextBox, txtTechnology As TextBox, cmbBand As ComboBox)
        Using connection As New SqlConnection(connectionString)
            connection.Open()

            Dim query As String = "SELECT mcc, mnc, cid, lac, count, earfcn, band, is_gsm, is_lte, is_wcdma " &
                                  "FROM base_stations WHERE channel_number = @channelNumber"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@channelNumber", channelNumber)

                Using reader As SqlDataReader = command.ExecuteReader()
                    If reader.Read() Then
                        txtMCC.Text = If(reader("mcc") IsNot DBNull.Value, reader("mcc").ToString(), "")
                        txtMNC.Text = If(reader("mnc") IsNot DBNull.Value, reader("mnc").ToString(), "")
                        txtCID.Text = If(reader("cid") IsNot DBNull.Value, reader("cid").ToString(), "")
                        txtLAC.Text = If(reader("lac") IsNot DBNull.Value, reader("lac").ToString(), "")
                        txtCount.Text = If(reader("count") IsNot DBNull.Value, reader("count").ToString(), "")
                        If txtEarfcn IsNot Nothing AndAlso reader("earfcn") IsNot DBNull.Value Then
                            txtEarfcn.Text = reader("earfcn").ToString()
                        End If

                        If reader("is_gsm") IsNot DBNull.Value AndAlso CBool(reader("is_gsm")) Then
                            txtTechnology.Text = "GSM"
                        ElseIf reader("is_lte") IsNot DBNull.Value AndAlso CBool(reader("is_lte")) Then
                            txtTechnology.Text = "LTE-FDD"
                        ElseIf reader("is_wcdma") IsNot DBNull.Value AndAlso CBool(reader("is_wcdma")) Then
                            txtTechnology.Text = "WCDMA"
                        Else
                            txtTechnology.Text = "Unknown"
                        End If

                        If reader("band") IsNot DBNull.Value Then
                            cmbBand.SelectedItem = reader("band").ToString()
                        End If
                    Else
                        ClearTextBoxes(txtMCC, txtMNC, txtCID, txtLAC, txtCount, txtEarfcn, txtTechnology)
                    End If
                End Using
            End Using
        End Using
    End Sub

    Private Sub LoadBaseStationChannel1(channelNumber As Integer,
                                  txtMCC As TextBox,
                                  txtMNC As TextBox,
                                  txtCID As TextBox,
                                  txtLAC As TextBox,
                                  txtCount As TextBox,
                                  txtEarfcn As TextBox,
                                  txtTechnology As TextBox,
                                  cmbBand As ComboBox,
                                  targetPictureBox As PictureBox)

        Console.WriteLine("Loading base station channel")

        Using connection As New SqlConnection(connectionString)
            connection.Open()

            Dim query As String = "SELECT mcc, mnc, cid, lac, count, earfcn, band, is_gsm, is_lte, is_wcdma 
                               FROM base_stations WHERE channel_number = @channelNumber"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@channelNumber", channelNumber)

                Using reader As SqlDataReader = command.ExecuteReader()
                    If reader.Read() Then
                        txtMCC.Text = If(reader("mcc") IsNot DBNull.Value, reader("mcc").ToString(), "")
                        txtMNC.Text = If(reader("mnc") IsNot DBNull.Value, reader("mnc").ToString(), "")
                        txtCID.Text = If(reader("cid") IsNot DBNull.Value, reader("cid").ToString(), "")
                        txtLAC.Text = If(reader("lac") IsNot DBNull.Value, reader("lac").ToString(), "")
                        txtCount.Text = If(reader("count") IsNot DBNull.Value, reader("count").ToString(), "")

                        If txtEarfcn IsNot Nothing AndAlso reader("earfcn") IsNot DBNull.Value Then
                            txtEarfcn.Text = reader("earfcn").ToString()
                        End If

                        If reader("is_gsm") IsNot DBNull.Value AndAlso CBool(reader("is_gsm")) Then
                            txtTechnology.Text = "GSM"
                        ElseIf reader("is_lte") IsNot DBNull.Value AndAlso CBool(reader("is_lte")) Then
                            If channelNumber = 9 OrElse channelNumber = 10 Then
                                txtTechnology.Text = "LTE-TDD"
                            Else
                                txtTechnology.Text = "LTE-FDD"
                            End If

                        ElseIf reader("is_wcdma") IsNot DBNull.Value AndAlso CBool(reader("is_wcdma")) Then
                                txtTechnology.Text = "WCDMA"
                            Else
                                txtTechnology.Text = "Unknown"
                        End If

                        If reader("band") IsNot DBNull.Value Then
                            cmbBand.SelectedItem = reader("band").ToString()
                        End If

                        Dim mcc As Integer = If(reader("mcc") IsNot DBNull.Value, CInt(reader("mcc")), -1)
                        Dim mnc As Integer = If(reader("mnc") IsNot DBNull.Value, CInt(reader("mnc")), -1)

                        If mcc > -1 AndAlso mnc > -1 Then
                            reader.Close()

                            Dim providerQuery As String = "SELECT TOP 1 operator_name, logo_url 
                                                       FROM operators WHERE mcc = @mcc AND mnc = @mnc"

                            Using providerCmd As New SqlCommand(providerQuery, connection)
                                providerCmd.Parameters.AddWithValue("@mcc", mcc)
                                providerCmd.Parameters.AddWithValue("@mnc", mnc)

                                Using providerReader As SqlDataReader = providerCmd.ExecuteReader()
                                    If providerReader.Read() Then
                                        Dim providerName As String = providerReader("operator_name").ToString()

                                        Dim logoImage As Image = Nothing
                                        If providerLogos.TryGetValue(providerName, logoImage) Then
                                            If targetPictureBox IsNot Nothing Then
                                                targetPictureBox.Image = logoImage
                                            End If
                                        Else
                                            Console.WriteLine("No logo resource found for provider: " & providerName)
                                        End If
                                    End If
                                End Using
                            End Using

                        End If
                    Else
                        ClearTextBoxes(txtMCC, txtMNC, txtCID, txtLAC, txtCount, txtEarfcn, txtTechnology)
                    End If
                End Using
            End Using
        End Using
    End Sub


    Public Sub LoadChannels9And10()
        Using connection As New SqlConnection(connectionString)
            connection.Open()

            Dim query9 As String = "SELECT earfcn FROM base_stations WHERE channel_number = 9"
            Using command9 As New SqlCommand(query9, connection)
                Dim earfcn9 As Object = command9.ExecuteScalar()
                If earfcn9 IsNot Nothing AndAlso earfcn9 IsNot DBNull.Value Then
                    TextBox90.Text = earfcn9.ToString()
                Else
                    TextBox90.Text = ""
                End If
            End Using

            Dim query10 As String = "SELECT earfcn FROM base_stations WHERE channel_number = 10"
            Using command10 As New SqlCommand(query10, connection)
                Dim earfcn10 As Object = command10.ExecuteScalar()
                If earfcn10 IsNot Nothing AndAlso earfcn10 IsNot DBNull.Value Then
                    TextBox95.Text = earfcn10.ToString()
                Else
                    TextBox95.Text = ""
                End If
            End Using

            LoadBaseStationChannel1(9, TextBox94, TextBox93, TextBox91, TextBox92, TextBox89, Nothing, TextBox88, ComboBox20, PictureBox10)
        End Using
    End Sub

    Private Sub ClearTextBoxes(ParamArray textBoxes As TextBox())
        For Each txt As TextBox In textBoxes
            If txt IsNot Nothing Then
                txt.Text = ""
            End If
        Next
    End Sub

    Private Sub disableAllBtns()
        ' Disable all "Stored to CHx" buttons on program start
        Button37.Enabled = False
        Button38.Enabled = False
        Button39.Enabled = False
        Button40.Enabled = False
        Button41.Enabled = False
        Button42.Enabled = False
        Button43.Enabled = False
        Button44.Enabled = False
        Button45.Enabled = False
        Button46.Enabled = False
        Button47.Enabled = False
        Button48.Enabled = False
        Button49.Enabled = False
    End Sub

    Private Sub InitializeEditModeButtons()
        editModeButtons.Add(1, Button37)
        editModeButtons.Add(2, Button38)
        editModeButtons.Add(3, Button39)
        editModeButtons.Add(4, Button40)
        editModeButtons.Add(5, Button41)
        editModeButtons.Add(6, Button42)
        editModeButtons.Add(7, Button43)
        editModeButtons.Add(8, Button44)
        editModeButtons.Add(9, Button45)
        editModeButtons.Add(10, Button45)
        editModeButtons.Add(11, Button46)
        editModeButtons.Add(12, Button47)
        editModeButtons.Add(13, Button48)
        editModeButtons.Add(14, Button49)

        ' Initialize button states
        For Each channel In editModeButtons.Keys
            buttonStates.Add(channel, False) ' False = no changes, True = has changes
        Next
    End Sub

    Private Sub SetupValidationEvents()
        ' Add validation events for all input fields
        AddValidationEventsForChannel(1, {TextBox1, TextBox2, TextBox3, TextBox97}, ComboBox12)
        AddValidationEventsForChannel(2, {TextBox101, TextBox100, TextBox99, TextBox98}, ComboBox13)
        AddValidationEventsForChannel(3, {TextBox105, TextBox104, TextBox103, TextBox102}, ComboBox14)
        AddValidationEventsForChannel(4, {TextBox109, TextBox108, TextBox107, TextBox106}, ComboBox15)
        AddValidationEventsForChannel(5, {TextBox113, TextBox112, TextBox111}, ComboBox16)
        AddValidationEventsForChannel(6, {TextBox117, TextBox116, TextBox115}, ComboBox17)
        AddValidationEventsForChannel(7, {TextBox121, TextBox120, TextBox119}, ComboBox18)
        AddValidationEventsForChannel(8, {TextBox125, TextBox124, TextBox123}, ComboBox19)
        AddValidationEventsForChannel(9, {TextBox129, TextBox128, TextBox127}, ComboBox20)
        AddValidationEventsForChannel(10, {TextBox129, TextBox128, TextBox126}, ComboBox20)
        AddValidationEventsForChannel(11, {TextBox133, TextBox132, TextBox131}, ComboBox21)
        AddValidationEventsForChannel(12, {TextBox137, TextBox136, TextBox135}, ComboBox22)
        AddValidationEventsForChannel(13, {TextBox141, TextBox140, TextBox139}, ComboBox23)
        AddValidationEventsForChannel(14, {TextBox145, TextBox144, TextBox143}, ComboBox24)

        Console.WriteLine("Validation events added for channels")

        ' Initial validation for all channels
        For Each channel In editModeButtons.Keys
            Dim textBoxes = GetTextboxesForChannel(channel)
            Dim comboBox = GetComboBoxForChannel(channel)
            ValidateChannelFields(channel, textBoxes, comboBox)
        Next
    End Sub

    Private Sub AddValidationEventsForChannel(channel As Integer, textBoxes As TextBox(), comboBox As ComboBox)
        For Each txt In textBoxes
            AddHandler txt.TextChanged, Sub(s, e)
                                            Dim isValid = ValidateChannelFields(channel, textBoxes, comboBox)
                                            CheckForChanges(channel, textBoxes, comboBox)
                                            UpdateButtonState(channel, isValid)
                                        End Sub
            AddHandler txt.Validated, Sub(s, e) ClearErrorStyle(txt)
        Next

        AddHandler comboBox.SelectedIndexChanged, Sub(s, e)
                                                      Dim isValid = ValidateChannelFields(channel, textBoxes, comboBox)
                                                      CheckForChanges(channel, textBoxes, comboBox)
                                                      UpdateButtonState(channel, isValid)
                                                  End Sub
    End Sub


    Private Function ValidateField(textBox As TextBox, technology As String) As Boolean
        If String.IsNullOrEmpty(textBox.Text) Then
            ApplyErrorStyle(textBox)
            Return False
        End If

        Dim fieldName As String = textBox.Name
        Dim value As String = textBox.Text

        ' Technology-specific validation
        If technology.Contains("GSM") Then
            If fieldName.Contains("TextBox97") OrElse fieldName.Contains("TextBox98") OrElse
               fieldName.Contains("TextBox102") OrElse fieldName.Contains("TextBox106") Then
                ' BSIC validation for GSM
                If Not ValidateBSICValue(value) Then
                    ApplyErrorStyle(textBox)
                    Return False
                End If
            End If
        End If

        ' General numeric validation
        If fieldName.Contains("TextBox1") OrElse fieldName.Contains("TextBox101") OrElse
           fieldName.Contains("TextBox105") OrElse fieldName.Contains("TextBox109") OrElse
           fieldName.Contains("TextBox113") OrElse fieldName.Contains("TextBox117") OrElse
           fieldName.Contains("TextBox121") OrElse fieldName.Contains("TextBox125") OrElse
           fieldName.Contains("TextBox129") OrElse fieldName.Contains("TextBox133") OrElse
           fieldName.Contains("TextBox137") OrElse fieldName.Contains("TextBox141") OrElse
           fieldName.Contains("TextBox145") Then
            ' MCC validation
            If Not ValidateMCCValue(value) Then
                ApplyErrorStyle(textBox)
                Return False
            End If
        ElseIf fieldName.Contains("TextBox2") OrElse fieldName.Contains("TextBox100") OrElse
               fieldName.Contains("TextBox104") OrElse fieldName.Contains("TextBox108") OrElse
               fieldName.Contains("TextBox112") OrElse fieldName.Contains("TextBox116") OrElse
               fieldName.Contains("TextBox120") OrElse fieldName.Contains("TextBox124") OrElse
               fieldName.Contains("TextBox128") OrElse fieldName.Contains("TextBox132") OrElse
               fieldName.Contains("TextBox136") OrElse fieldName.Contains("TextBox140") OrElse
               fieldName.Contains("TextBox144") Then
            ' MNC validation
            If Not ValidateMNCValue(value) Then
                ApplyErrorStyle(textBox)
                Return False
            End If
        ElseIf fieldName.Contains("TextBox3") OrElse fieldName.Contains("TextBox99") OrElse
               fieldName.Contains("TextBox103") OrElse fieldName.Contains("TextBox107") OrElse
               fieldName.Contains("TextBox111") OrElse fieldName.Contains("TextBox115") OrElse
               fieldName.Contains("TextBox119") OrElse fieldName.Contains("TextBox123") OrElse
               fieldName.Contains("TextBox127") OrElse fieldName.Contains("TextBox131") OrElse
               fieldName.Contains("TextBox135") OrElse fieldName.Contains("TextBox139") OrElse
               fieldName.Contains("TextBox143") OrElse fieldName.Contains("TextBox126") Then
            ' EARFCN validation
            If Not ValidateEARFCNValue(value) Then
                ApplyErrorStyle(textBox)
                Return False
            End If
        End If

        ClearErrorStyle(textBox)
        Return True
    End Function

    Private Function ValidateBSICValue(value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value) AndAlso IsNumeric(value)
    End Function

    Private Function ValidateMCCValue(value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value) AndAlso IsNumeric(value)
    End Function

    Private Function ValidateMNCValue(value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value) AndAlso IsNumeric(value)
    End Function

    Private Function ValidateEARFCNValue(value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value) AndAlso IsNumeric(value)
    End Function


    Private Sub ApplyErrorStyle(textBox As TextBox)
        textBox.ForeColor = Color.DarkRed
    End Sub

    Private Sub ClearErrorStyle(textBox As TextBox)
        textBox.BackColor = SystemColors.Window
        textBox.ForeColor = SystemColors.WindowText
    End Sub

    Private Sub UpdateButtonState(channel As Integer, isValid As Boolean)
        If editModeButtons.ContainsKey(channel) Then
            Dim button As Button = editModeButtons(channel)
            Dim hasChanges As Boolean = buttonStates(channel)

            button.Enabled = isValid AndAlso hasChanges

            Console.WriteLine(isValid AndAlso hasChanges)

            If hasChanges Then
                If channel = 9 Then
                    button.Text = $"Save changes to CH9 and CH10"
                    button.ForeColor = Color.White
                Else
                    button.Text = $"Save changes to CH{channel}"
                End If
                button.BackColor = Color.LightGreen
                button.Font = New Font("Arial", 7, FontStyle.Regular)
            Else
                If channel = 9 Then
                    button.Text = $"Stored to CH9 and CH10"
                Else
                    button.Text = $"Stored to CH{channel}"
                End If
                button.ForeColor = Color.Black
                button.BackColor = Color.LightGray
                button.Font = New Font("Arial", 8, FontStyle.Regular)
            End If
        End If
    End Sub

    Private Sub CheckForChanges(channel As Integer, textBoxes As TextBox(), comboBox As ComboBox)
        Dim hasChanges As Boolean = False
        Dim channelKey As String = $"CH{channel}"

        ' Check if combo box has changed
        If originalValues.ContainsKey($"{channelKey}_ComboBox") Then
            If comboBox.Text <> originalValues($"{channelKey}_ComboBox") Then
                hasChanges = True
            End If
        Else
            ' If no original value stored but combo has selection, consider it a change
            If Not String.IsNullOrEmpty(comboBox.Text) Then
                hasChanges = True
            End If
        End If

        ' Check if text boxes have changed
        If Not hasChanges Then
            For Each txt In textBoxes
                Dim key As String = $"{channelKey}_{txt.Name}"
                If originalValues.ContainsKey(key) Then
                    If txt.Text <> originalValues(key) Then
                        hasChanges = True
                        Exit For
                    End If
                Else
                    ' If no original value stored but textbox has value, consider it a change
                    If Not String.IsNullOrEmpty(txt.Text) Then
                        hasChanges = True
                        Exit For
                    End If
                End If
            Next
        End If

        ' Update button state
        buttonStates(channel) = hasChanges

        ' Update button text and enable/disable state
        UpdateButtonState(channel, ValidateChannelFields(channel, textBoxes, comboBox))
    End Sub


    Private Function GetPrimaryTextBox(textBoxes As TextBox(), fieldType As String) As TextBox

        For Each txt In textBoxes
            If txt.Name.Contains("TextBox1") OrElse txt.Name.Contains("TextBox101") OrElse
           txt.Name.Contains("TextBox105") OrElse txt.Name.Contains("TextBox109") OrElse
           txt.Name.Contains("TextBox113") OrElse txt.Name.Contains("TextBox117") OrElse
           txt.Name.Contains("TextBox121") OrElse txt.Name.Contains("TextBox125") OrElse
           txt.Name.Contains("TextBox129") OrElse txt.Name.Contains("TextBox133") OrElse
           txt.Name.Contains("TextBox137") OrElse txt.Name.Contains("TextBox141") OrElse
           txt.Name.Contains("TextBox145") Then
                If fieldType = "MCC" Then Return txt
            ElseIf txt.Name.Contains("TextBox2") OrElse txt.Name.Contains("TextBox100") OrElse
               txt.Name.Contains("TextBox104") OrElse txt.Name.Contains("TextBox108") OrElse
               txt.Name.Contains("TextBox112") OrElse txt.Name.Contains("TextBox116") OrElse
               txt.Name.Contains("TextBox120") OrElse txt.Name.Contains("TextBox124") OrElse
               txt.Name.Contains("TextBox128") OrElse txt.Name.Contains("TextBox132") OrElse
               txt.Name.Contains("TextBox136") OrElse txt.Name.Contains("TextBox140") OrElse
               txt.Name.Contains("TextBox144") Then
                If fieldType = "MNC" Then Return txt
            ElseIf txt.Name.Contains("TextBox3") OrElse txt.Name.Contains("TextBox99") OrElse
               txt.Name.Contains("TextBox103") OrElse txt.Name.Contains("TextBox107") OrElse
               txt.Name.Contains("TextBox111") OrElse txt.Name.Contains("TextBox115") OrElse
               txt.Name.Contains("TextBox119") OrElse txt.Name.Contains("TextBox123") OrElse
               txt.Name.Contains("TextBox127") OrElse txt.Name.Contains("TextBox131") OrElse
               txt.Name.Contains("TextBox135") OrElse txt.Name.Contains("TextBox139") OrElse
               txt.Name.Contains("TextBox143") Then
                If fieldType = "EARFCN" Then Return txt
            ElseIf txt.Name.Contains("TextBox97") OrElse txt.Name.Contains("TextBox98") OrElse
               txt.Name.Contains("TextBox102") OrElse txt.Name.Contains("TextBox106") Then
                If fieldType = "BSIC" Then Return txt
            End If
        Next
        Return Nothing
    End Function

    Private Sub DataGridView9_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView9.CellClick
        If e.RowIndex >= 0 Then
            Dim row As DataGridViewRow = DataGridView9.Rows(e.RowIndex)

            selectedBimsi = row.Cells("imsi").Value.ToString()
            selectedBImei = row.Cells("imei").Value.ToString()
        End If
    End Sub

    Private Sub Button77_Click(sender As Object, e As EventArgs) Handles Button77.Click
        If String.IsNullOrEmpty(selectedBimsi) OrElse String.IsNullOrEmpty(selectedBImei) Then
            MessageBox.Show("Please select a target from the list first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim result As DialogResult = MessageBox.Show(
        "Are you sure you want to delete this target from the blacklist?" & vbCrLf &
        "IMSI: " & selectedBimsi & vbCrLf &
        "IMEI: " & selectedBImei,
        "Confirm Deletion",
        MessageBoxButtons.OKCancel,
        MessageBoxIcon.Question
    )

        If result = DialogResult.OK Then
            Try
                Using conn As New SqlConnection(connectionString)
                    conn.Open()

                    Dim tableName As String = "[" & selectedSchema & "].[blacklist]"
                    Dim sql As String = "DELETE FROM " & tableName & " WHERE imsi = @imsi AND imei = @imei"

                    Using cmd As New SqlCommand(sql, conn)
                        cmd.Parameters.AddWithValue("@imsi", selectedBimsi)
                        cmd.Parameters.AddWithValue("@imei", selectedBImei)
                        Dim rowsAffected As Integer = cmd.ExecuteNonQuery()

                        If rowsAffected > 0 Then
                            MessageBox.Show("Target deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                        Else
                            MessageBox.Show("No matching record found to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
                        End If
                    End Using
                End Using
            Catch ex As Exception
                MessageBox.Show("Error deleting target: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub


    Private Function ValidateChannelFields(channel As Integer, textBoxes As TextBox(), comboBox As ComboBox) As Boolean

        Dim isValid As Boolean = True
        Dim technology As String = If(comboBox?.Text, String.Empty).Trim()

        ' Check if comboBox is empty
        If String.IsNullOrWhiteSpace(technology) Then
            comboBox.BackColor = Color.LightCoral
            isValid = False
        Else
            comboBox.BackColor = SystemColors.Window
        End If

        ' Reset all textboxes to normal color first
        For Each txt In textBoxes
            If txt IsNot Nothing Then
                txt.BackColor = SystemColors.Window
            End If
        Next

        ' Get the specific textboxes for this channel
        Dim mccTb As TextBox = Nothing
        Dim mncTb As TextBox = Nothing
        Dim earfcnTb As TextBox = Nothing
        Dim bsicTb As TextBox = Nothing

        ' Identify the textboxes based on channel
        Select Case channel
            Case 1
                mccTb = TextBox1
                mncTb = TextBox2
                earfcnTb = TextBox3
                bsicTb = TextBox97
            Case 2
                mccTb = TextBox101
                mncTb = TextBox100
                earfcnTb = TextBox99
                bsicTb = TextBox98
            Case 3
                mccTb = TextBox105
                mncTb = TextBox104
                earfcnTb = TextBox103
                bsicTb = TextBox102
            Case 4
                mccTb = TextBox109
                mncTb = TextBox108
                earfcnTb = TextBox107
                bsicTb = TextBox106
            Case 5
                mccTb = TextBox113
                mncTb = TextBox112
                earfcnTb = TextBox111
            Case 6
                mccTb = TextBox117
                mncTb = TextBox116
                earfcnTb = TextBox115
            Case 7
                mccTb = TextBox121
                mncTb = TextBox120
                earfcnTb = TextBox119
            Case 8
                mccTb = TextBox125
                mncTb = TextBox124
                earfcnTb = TextBox123
            Case 9
                Console.WriteLine("Validationg channel " & channel)
                mccTb = TextBox129
                mncTb = TextBox128
                earfcnTb = TextBox127 ' This is EARFCN9 for CH9
            Case 10
                ' CH10 uses the same controls as CH9
                Console.WriteLine("Validationg channel " & channel)
                mccTb = TextBox129
                mncTb = TextBox128
                earfcnTb = TextBox126
            Case 11
                mccTb = TextBox133
                mncTb = TextBox132
                earfcnTb = TextBox131
            Case 12
                mccTb = TextBox137
                mncTb = TextBox136
                earfcnTb = TextBox135
            Case 13
                mccTb = TextBox141
                mncTb = TextBox140
                earfcnTb = TextBox139
            Case 14
                mccTb = TextBox145
                mncTb = TextBox144
                earfcnTb = TextBox143
        End Select

        ' Validate required fields based on channel
        If mccTb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(mccTb.Text) Then
            mccTb.BackColor = Color.LightCoral
            isValid = False
        End If

        If mncTb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(mncTb.Text) Then
            mncTb.BackColor = Color.LightCoral
            isValid = False
        End If

        ' Validate technology-specific requirements
        Select Case channel
            Case 1 To 4, 7, 8, 11 To 14 ' Channels that require EARFCN and BSIC
                If earfcnTb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(earfcnTb.Text) Then
                    earfcnTb.BackColor = Color.LightCoral
                    isValid = False
                End If
                If bsicTb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(bsicTb.Text) Then
                    bsicTb.BackColor = Color.LightCoral
                    isValid = False
                End If

            Case 5, 6, 9, 10 ' Channels that only require EARFCN
                If earfcnTb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(earfcnTb.Text) Then
                    earfcnTb.BackColor = Color.LightCoral
                    isValid = False
                End If
        End Select

        ' Additional validation for specific values
        If isValid Then
            ' Validate MCC format (3 digits)
            If mccTb IsNot Nothing AndAlso Not String.IsNullOrEmpty(mccTb.Text) Then
                If mccTb.Text.Length <> 3 OrElse Not IsNumeric(mccTb.Text) Then
                    isValid = False
                End If
            End If

            ' Validate MNC format (2-3 digits)
            If mncTb IsNot Nothing AndAlso Not String.IsNullOrEmpty(mncTb.Text) Then
                If mncTb.Text.Length < 2 OrElse mncTb.Text.Length > 3 OrElse Not IsNumeric(mncTb.Text) Then
                    isValid = False
                End If
            End If

            ' Validate EARFCN format (0-65535)
            If earfcnTb IsNot Nothing AndAlso Not String.IsNullOrEmpty(earfcnTb.Text) Then
                Dim earfcnValue As Integer
                If Integer.TryParse(earfcnTb.Text, earfcnValue) Then
                    If earfcnValue < 0 OrElse earfcnValue > 65535 Then
                        isValid = False
                    End If
                Else
                    earfcnTb.BackColor = Color.LightCoral
                    isValid = False
                End If
            End If

            ' Validate BSIC format (0-63)
            If bsicTb IsNot Nothing AndAlso Not String.IsNullOrEmpty(bsicTb.Text) Then
                Dim bsicValue As Integer
                If Integer.TryParse(bsicTb.Text, bsicValue) Then
                    If bsicValue < 0 OrElse bsicValue > 63 Then
                        isValid = False
                    End If
                Else
                    isValid = False
                End If
            End If
        End If

        Return isValid
    End Function


    ' Repeat the StoreOriginalValue calls for all PopulateChannel methods
    Private Sub StoreOriginalValue(key As String, value As String)
        If originalValues.ContainsKey(key) Then
            originalValues(key) = value
        Else
            originalValues.Add(key, value)
        End If
    End Sub

    Private Sub GroupBox1_Enter(sender As Object, e As EventArgs)

    End Sub

    Private Sub TabPage3_Click(sender As Object, e As EventArgs)

    End Sub

    Private Sub TabPage1_Click(sender As Object, e As EventArgs) Handles TabPage1.Click

    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        OpenForm6()
    End Sub

    Private Sub Chart1_Click(sender As Object, e As EventArgs) Handles Chart1.Click

    End Sub

    Private Sub DataGridView1_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView1.CellContentClick

    End Sub

    Private Sub DataGridView4_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView4.CellContentClick

    End Sub

    Private Sub Button75_Click(sender As Object, e As EventArgs) Handles Button75.Click
        Dim f As New Formblacklist(selectedSchema)
        f.ShowDialog()
    End Sub

    Private Sub Button78_Click(sender As Object, e As EventArgs) Handles Button78.Click
        Dim f As New Form3(selectedSchema)
        f.ShowDialog()
    End Sub

    Private Sub Button73_Click(sender As Object, e As EventArgs) Handles Button73.Click
        Form4.Show()
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        OpenForm5()
    End Sub

    Private Sub OpenForm5()
        For Each f As Form In Application.OpenForms
            If TypeOf f Is Form5 Then
                f.BringToFront()
                f.Focus()
                Return
            End If
        Next

        If operatorFilter Is Nothing OrElse operatorFilter.Count = 0 Then
            Using f5 As New Form5()
                f5.ShowDialog(Me)
            End Using
        Else
            Using f5 As New Form5(operatorFilter)
                f5.ShowDialog(Me)
            End Using
        End If

    End Sub

    Private Sub OpenForm6()
        For Each f As Form In Application.OpenForms
            If TypeOf f Is Form6 Then
                f.BringToFront()
                f.Focus()
                Return
            End If
        Next

        If operatorFilter2 Is Nothing OrElse operatorFilter2.Count = 0 Then
            Using f6 As New Form6()
                f6.ShowDialog(Me)
            End Using
        Else
            Using f6 As New Form6(operatorFilter2)
                f6.ShowDialog(Me)
            End Using
        End If

    End Sub


    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Form7.Show()
    End Sub

    Private Sub Button70_Click(sender As Object, e As EventArgs) Handles Button70.Click
        Form9.Show()
    End Sub

    ' Helper method to parse textbox values
    Private Function ParseInteger(text As String) As Integer?
        If String.IsNullOrEmpty(text) Then Return Nothing
        Dim result As Integer
        If Integer.TryParse(text, result) Then Return result
        Return Nothing
    End Function

    ' Helper function to check if all fields are filled
    Private Function AreFieldsFilled(ParamArray fields() As String) As Boolean
        For Each field As String In fields
            If String.IsNullOrWhiteSpace(field) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If
        Next
        Return True
    End Function


    ' Event handlers for all Stored to CHx buttons
    Private Sub Button37_Click(sender As Object, e As EventArgs) Handles Button37.Click
        If AreFieldsFilled(ComboBox12.Text, TextBox1.Text, TextBox2.Text, TextBox3.Text, TextBox97.Text) Then
            SaveBaseStation(1, ComboBox12.Text, TextBox1.Text, TextBox2.Text, TextBox3.Text, TextBox97.Text)
        End If
    End Sub

    Private Sub Button38_Click(sender As Object, e As EventArgs) Handles Button38.Click
        If AreFieldsFilled(ComboBox13.Text, TextBox101.Text, TextBox100.Text, TextBox99.Text, TextBox98.Text) Then
            SaveBaseStation(2, ComboBox13.Text, TextBox101.Text, TextBox100.Text, TextBox99.Text, TextBox98.Text)
        End If
    End Sub

    Private Sub Button39_Click(sender As Object, e As EventArgs) Handles Button39.Click
        If AreFieldsFilled(ComboBox14.Text, TextBox105.Text, TextBox104.Text, TextBox103.Text, TextBox102.Text) Then
            SaveBaseStation(3, ComboBox14.Text, TextBox105.Text, TextBox104.Text, TextBox103.Text, TextBox102.Text)
        End If
    End Sub

    Private Sub Button40_Click(sender As Object, e As EventArgs) Handles Button40.Click
        If AreFieldsFilled(ComboBox15.Text, TextBox109.Text, TextBox108.Text, TextBox107.Text, TextBox106.Text) Then
            SaveBaseStation(4, ComboBox15.Text, TextBox109.Text, TextBox108.Text, TextBox107.Text, TextBox106.Text)
        End If
    End Sub

    Private Sub Button41_Click(sender As Object, e As EventArgs) Handles Button41.Click
        If AreFieldsFilled(ComboBox16.Text, TextBox113.Text, TextBox112.Text, TextBox111.Text) Then
            SaveBaseStation(5, ComboBox16.Text, TextBox113.Text, TextBox112.Text, TextBox111.Text)
        End If
    End Sub

    Private Sub Button42_Click(sender As Object, e As EventArgs) Handles Button42.Click
        If AreFieldsFilled(ComboBox17.Text, TextBox117.Text, TextBox116.Text, TextBox115.Text) Then
            SaveBaseStation(6, ComboBox17.Text, TextBox117.Text, TextBox116.Text, TextBox115.Text)
        End If
    End Sub

    Private Sub Button43_Click(sender As Object, e As EventArgs) Handles Button43.Click
        If AreFieldsFilled(ComboBox18.Text, TextBox121.Text, TextBox120.Text, TextBox119.Text) Then
            SaveBaseStation(7, ComboBox18.Text, TextBox121.Text, TextBox120.Text, TextBox119.Text)
        End If
    End Sub

    Private Sub Button44_Click(sender As Object, e As EventArgs) Handles Button44.Click
        If AreFieldsFilled(ComboBox19.Text, TextBox125.Text, TextBox124.Text, TextBox123.Text) Then
            SaveBaseStation(8, ComboBox19.Text, TextBox125.Text, TextBox124.Text, TextBox123.Text)
        End If
    End Sub

    Private Sub Button45_Click(sender As Object, e As EventArgs) Handles Button45.Click
        If AreFieldsFilled(ComboBox20.Text, TextBox129.Text, TextBox128.Text, TextBox127.Text, TextBox126.Text) Then
            SaveBaseStation(9, ComboBox20.Text, TextBox129.Text, TextBox128.Text, TextBox127.Text, Nothing, TextBox126.Text)
        End If
    End Sub

    Private Sub Button46_Click(sender As Object, e As EventArgs) Handles Button46.Click
        If AreFieldsFilled(ComboBox21.Text, TextBox133.Text, TextBox132.Text, TextBox131.Text) Then
            SaveBaseStation(11, ComboBox21.Text, TextBox133.Text, TextBox132.Text, TextBox131.Text)
        End If
    End Sub

    Private Sub Button47_Click(sender As Object, e As EventArgs) Handles Button47.Click
        If AreFieldsFilled(ComboBox22.Text, TextBox137.Text, TextBox136.Text, TextBox135.Text) Then
            SaveBaseStation(12, ComboBox22.Text, TextBox137.Text, TextBox136.Text, TextBox135.Text)
        End If
    End Sub

    Private Sub Button48_Click(sender As Object, e As EventArgs) Handles Button48.Click
        If AreFieldsFilled(ComboBox23.Text, TextBox141.Text, TextBox140.Text, TextBox139.Text) Then
            SaveBaseStation(13, ComboBox23.Text, TextBox141.Text, TextBox140.Text, TextBox139.Text)
        End If
    End Sub

    Private Sub Button49_Click(sender As Object, e As EventArgs) Handles Button49.Click
        If AreFieldsFilled(ComboBox24.Text, TextBox145.Text, TextBox144.Text, TextBox143.Text) Then
            SaveBaseStation(14, ComboBox24.Text, TextBox145.Text, TextBox144.Text, TextBox143.Text)
        End If
    End Sub


    ' Add this method to handle the TextChanged event for all textboxes
    Private Sub TextBox_TextChanged(sender As Object, e As EventArgs)
        Dim txt As TextBox = CType(sender, TextBox)
        Dim channel As Integer = GetChannelFromTextBoxName(txt.Name)

        If channel > 0 Then
            ' Get all textboxes for this channel and validate
            Dim textBoxes = GetTextboxesForChannel(channel)
            Dim comboBox = GetComboBoxForChannel(channel)
            ValidateChannelFields(channel, textBoxes, comboBox)
        End If
    End Sub

    Private Function GetChannelFromTextBoxName(name As String) As Integer
        ' Extract channel number from textbox name
        Select Case True
            Case name = "TextBox1" : Return 1
            Case name = "TextBox2" : Return 1
            Case name = "TextBox3" : Return 1
            Case name = "TextBox97" : Return 1

            Case name = "TextBox101" : Return 2
            Case name = "TextBox100" : Return 2
            Case name = "TextBox99" : Return 2
            Case name = "TextBox98" : Return 2

            Case name = "TextBox105" : Return 3
            Case name = "TextBox104" : Return 3
            Case name = "TextBox103" : Return 3
            Case name = "TextBox102" : Return 3

            Case name = "TextBox109" : Return 4
            Case name = "TextBox108" : Return 4
            Case name = "TextBox107" : Return 4
            Case name = "TextBox106" : Return 4

            Case name = "TextBox113" : Return 5
            Case name = "TextBox112" : Return 5
            Case name = "TextBox111" : Return 5

            Case name = "TextBox117" : Return 6
            Case name = "TextBox116" : Return 6
            Case name = "TextBox115" : Return 6

            Case name = "TextBox121" : Return 7
            Case name = "TextBox120" : Return 7
            Case name = "TextBox119" : Return 7

            Case name = "TextBox125" : Return 8
            Case name = "TextBox124" : Return 8
            Case name = "TextBox123" : Return 8

            Case name = "TextBox129" : Return 9
            Case name = "TextBox128" : Return 9
            Case name = "TextBox127" : Return 9
            Case name = "TextBox126" : Return 10

            Case name = "TextBox133" : Return 11
            Case name = "TextBox132" : Return 11
            Case name = "TextBox131" : Return 11

            Case name = "TextBox137" : Return 12
            Case name = "TextBox136" : Return 12
            Case name = "TextBox135" : Return 12

            Case name = "TextBox141" : Return 13
            Case name = "TextBox140" : Return 13
            Case name = "TextBox139" : Return 13

            Case name = "TextBox145" : Return 14
            Case name = "TextBox144" : Return 14
            Case name = "TextBox143" : Return 14

            Case Else : Return -1
        End Select
    End Function

    Private Function GetTextboxesForChannel(channel As Integer) As TextBox()
        Select Case channel
            Case 1 : Return {TextBox1, TextBox2, TextBox3, TextBox97}
            Case 2 : Return {TextBox101, TextBox100, TextBox99, TextBox98}
            Case 3 : Return {TextBox105, TextBox104, TextBox103, TextBox102}
            Case 4 : Return {TextBox109, TextBox108, TextBox107, TextBox106}
            Case 5 : Return {TextBox113, TextBox112, TextBox111}
            Case 6 : Return {TextBox117, TextBox116, TextBox115}
            Case 7 : Return {TextBox121, TextBox120, TextBox119}
            Case 8 : Return {TextBox125, TextBox124, TextBox123}
            Case 9 : Return {TextBox129, TextBox128, TextBox127}
            Case 10 : Return {TextBox129, TextBox128, TextBox126}
            Case 11 : Return {TextBox133, TextBox132, TextBox131}
            Case 12 : Return {TextBox137, TextBox136, TextBox135}
            Case 13 : Return {TextBox141, TextBox140, TextBox139}
            Case 14 : Return {TextBox145, TextBox144, TextBox143}
            Case Else : Return {}
        End Select
    End Function

    Private Function GetComboBoxForChannel(channel As Integer) As ComboBox
        Select Case channel
            Case 1 : Return ComboBox12
            Case 2 : Return ComboBox13
            Case 3 : Return ComboBox14
            Case 4 : Return ComboBox15
            Case 5 : Return ComboBox16
            Case 6 : Return ComboBox17
            Case 7 : Return ComboBox18
            Case 8 : Return ComboBox19
            Case 9 : Return ComboBox20
            Case 10 : Return ComboBox20
            Case 11 : Return ComboBox21
            Case 12 : Return ComboBox22
            Case 13 : Return ComboBox23
            Case 14 : Return ComboBox24
            Case Else : Return Nothing
        End Select
    End Function

    Public Sub LoadDataToGridViews()
        Try
            Dim dbHelper As New DatabaseHelper()
            LoadGSMData(dbHelper.GetGSMData())
            LoadWCDMAData(dbHelper.GetWCDMAData())
            LoadLTEData(dbHelper.GetLTEData())

        Catch ex As SqlException
            MessageBox.Show("Database error: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            MessageBox.Show("Error loading data: " & ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadGSMData(dataTable As DataTable)
        DataGridView1.DataSource = Nothing
        DataGridView1.AutoGenerateColumns = False
        DataGridView1.DataSource = dataTable

        DataGridView1.Columns("Column5").DataPropertyName = "ProviderName"
        DataGridView1.Columns("Column6").DataPropertyName = "plmn"
        DataGridView1.Columns("Column14").DataPropertyName = "mcc"
        DataGridView1.Columns("Column15").DataPropertyName = "mnc"
        DataGridView1.Columns("Column7").DataPropertyName = "band"
        DataGridView1.Columns("Column8").DataPropertyName = "rat"
        DataGridView1.Columns("Column9").DataPropertyName = "arfcn"
        DataGridView1.Columns("Column12").DataPropertyName = "lac"
        DataGridView1.Columns("Column13").DataPropertyName = "nb_cell"
        DataGridView1.Columns("Column10").DataPropertyName = "cell_id"
        DataGridView1.Columns("Column11").DataPropertyName = "bsic"

        ApplyFilterToDataGridViews()
    End Sub

    Private Sub LoadWCDMAData(dataTable As DataTable)
        DataGridView2.DataSource = Nothing
        DataGridView2.AutoGenerateColumns = False
        DataGridView2.DataSource = dataTable

        DataGridView2.Columns("Column16").DataPropertyName = "provider_name"
        DataGridView2.Columns("Column17").DataPropertyName = "plmn"
        DataGridView2.Columns("Column18").DataPropertyName = "mcc"
        DataGridView2.Columns("Column19").DataPropertyName = "mnc"
        DataGridView2.Columns("Column20").DataPropertyName = "band"
        DataGridView2.Columns("Column26").DataPropertyName = "psc"
        DataGridView2.Columns("Column21").DataPropertyName = "earfcn"
        DataGridView2.Columns("Column27").DataPropertyName = "nbsc"
        DataGridView2.Columns("Column24").DataPropertyName = "rat"
        DataGridView2.Columns("Column22").DataPropertyName = "lac"
        DataGridView2.Columns("Column23").DataPropertyName = "cell_id"
        DataGridView2.Columns("Column25").DataPropertyName = "rscp"

        ApplyFilterToDataGridViews()
    End Sub

    Private Sub LoadLTEData(dataTable As DataTable)
        DataGridView3.DataSource = Nothing
        DataGridView3.AutoGenerateColumns = False
        DataGridView3.DataSource = dataTable

        DataGridView3.Columns("Column28").DataPropertyName = "provider_name"
        DataGridView3.Columns("Column29").DataPropertyName = "plmn"
        DataGridView3.Columns("Column30").DataPropertyName = "mcc"
        DataGridView3.Columns("Column31").DataPropertyName = "mnc"
        DataGridView3.Columns("Column32").DataPropertyName = "band"
        DataGridView3.Columns("Column37").DataPropertyName = "pci"
        DataGridView3.Columns("Column38").DataPropertyName = "nb_earfcn"
        DataGridView3.Columns("Column34").DataPropertyName = "rat"
        DataGridView3.Columns("Column35").DataPropertyName = "lac"
        DataGridView3.Columns("Column33").DataPropertyName = "earfcn"
        DataGridView3.Columns("Column36").DataPropertyName = "rsrp"

        ApplyFilterToDataGridViews()
    End Sub

    Public Sub LoadBaseStationData()
        Try
            Dim baseStationHelper As New BaseStationHelper()

            For channel As Integer = 1 To 14
                Dim dataTable As DataTable = baseStationHelper.GetBaseStationByChannel(channel)
                If Me.InvokeRequired Then
                    Me.Invoke(Sub() LoadChannelData(channel, dataTable))
                Else
                    LoadChannelData(channel, dataTable)
                End If
            Next

        Catch ex As SqlException
            MessageBox.Show("Database error: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            MessageBox.Show("Error loading base station data: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadChannelData(channelNumber As Integer, dataTable As DataTable)
        If dataTable.Rows.Count > 0 Then
            Dim row As DataRow = dataTable.Rows(0) ' Get the most recent entry

            Select Case channelNumber
                Case 1
                    PopulateChannel1(row)
                Case 2
                    PopulateChannel2(row)
                Case 3
                    PopulateChannel3(row)
                Case 4
                    PopulateChannel4(row)
                Case 5
                    PopulateChannel5(row)
                Case 6
                    PopulateChannel6(row)
                Case 7
                    PopulateChannel7(row)
                Case 8
                    PopulateChannel8(row)
                Case 9
                    PopulateChannel9(row)
                Case 10
                    PopulateChannel10(row)
                Case 11
                    PopulateChannel11(row)
                Case 12
                    PopulateChannel12(row)
                Case 13
                    PopulateChannel13(row)
                Case 14
                    PopulateChannel14(row)
            End Select
        End If
    End Sub

    Private Sub LoadTaskingList()
        Try
            DataGridView8.Rows.Clear()

            Using conn As New SqlConnection(connectionString)
                conn.Open()

                Dim sql As String = "
                SELECT name AS SchemaName
                FROM sys.schemas
                WHERE name LIKE 'op_%'
                ORDER BY name
            "

                Using cmd As New SqlCommand(sql, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim schemaName As String = reader("SchemaName").ToString()
                            DataGridView8.Rows.Add(schemaName)
                        End While
                    End Using
                End Using
            End Using

        Catch ex As Exception
            MessageBox.Show("Error loading schema list: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Sub PopulateChannel1(row As DataRow)
        StoreOriginalValue("CH1_ComboBox", ComboBox12.Text)
        StoreOriginalValue("CH1_TextBox1", TextBox1.Text)
        StoreOriginalValue("CH1_TextBox2", TextBox2.Text)
        StoreOriginalValue("CH1_TextBox3", TextBox3.Text)
        StoreOriginalValue("CH1_TextBox97", TextBox97.Text)

        TextBox1.Text = GetSafeString(row("mcc")) ' MCC
        TextBox2.Text = GetSafeString(row("mnc")) ' MNC
        TextBox3.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox97.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox12.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox12.Text = "LTE - FDD"
            TextBox97.Enabled = False
        End If

        buttonStates(1) = False
        UpdateButtonState(1, True)
    End Sub

    Private Sub PopulateChannel2(row As DataRow)
        StoreOriginalValue("CH2_ComboBox", ComboBox13.Text)
        StoreOriginalValue("CH2_TextBox101", TextBox101.Text)
        StoreOriginalValue("CH2_TextBox100", TextBox100.Text)
        StoreOriginalValue("CH2_TextBox99", TextBox99.Text)
        StoreOriginalValue("CH2_TextBox98", TextBox98.Text)

        TextBox101.Text = GetSafeString(row("mcc")) ' MCC
        TextBox100.Text = GetSafeString(row("mnc")) ' MNC
        TextBox99.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox98.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox13.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox13.Text = "LTE - FDD"
            TextBox98.Enabled = False
        End If

        buttonStates(2) = False
        UpdateButtonState(2, True)
    End Sub

    Private Sub PopulateChannel3(row As DataRow)
        StoreOriginalValue("CH3_ComboBox", ComboBox14.Text)
        StoreOriginalValue("CH3_TextBox105", TextBox105.Text)
        StoreOriginalValue("CH3_TextBox104", TextBox104.Text)
        StoreOriginalValue("CH3_TextBox103", TextBox103.Text)
        StoreOriginalValue("CH3_TextBox102", TextBox102.Text)

        TextBox105.Text = GetSafeString(row("mcc")) ' MCC
        TextBox104.Text = GetSafeString(row("mnc")) ' MNC
        TextBox103.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox102.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox14.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox14.Text = "LTE - FDD"
            TextBox102.Enabled = False
        End If

        buttonStates(3) = False
        UpdateButtonState(3, True)
    End Sub

    Private Sub PopulateChannel4(row As DataRow)
        StoreOriginalValue("CH4_ComboBox", ComboBox15.Text)
        StoreOriginalValue("CH4_TextBox109", TextBox109.Text)
        StoreOriginalValue("CH4_TextBox108", TextBox108.Text)
        StoreOriginalValue("CH4_TextBox107", TextBox107.Text)
        StoreOriginalValue("CH4_TextBox106", TextBox106.Text)

        TextBox109.Text = GetSafeString(row("mcc")) ' MCC
        TextBox108.Text = GetSafeString(row("mnc")) ' MNC
        TextBox107.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox106.Text = GetSafeString(row("bsic"))

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox15.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox15.Text = "LTE - FDD"
            TextBox106.Enabled = False
        End If

        buttonStates(4) = False
        UpdateButtonState(4, True)
    End Sub

    Private Sub PopulateChannel5(row As DataRow)
        StoreOriginalValue("CH5_ComboBox", ComboBox16.Text)
        StoreOriginalValue("CH5_TextBox113", TextBox113.Text)
        StoreOriginalValue("CH5_TextBox112", TextBox112.Text)
        StoreOriginalValue("CH5_TextBox111", TextBox111.Text)

        ' CH5 2100MHz
        TextBox113.Text = GetSafeString(row("mcc")) ' MCC
        TextBox112.Text = GetSafeString(row("mnc")) ' MNC
        TextBox111.Text = GetSafeString(row("earfcn")) ' EARFCN

        If Convert.ToBoolean(row("is_lte")) Then
            ComboBox16.Text = "LTE - FDD"
        ElseIf Convert.ToBoolean(row("is_wcdma")) Then
            ComboBox16.Text = "WCDMA"
        End If

        buttonStates(5) = False
        UpdateButtonState(5, True)
    End Sub

    Private Sub PopulateChannel6(row As DataRow)
        StoreOriginalValue("CH6_ComboBox", ComboBox17.Text)
        StoreOriginalValue("CH6_TextBox117", TextBox117.Text)
        StoreOriginalValue("CH6_TextBox116", TextBox116.Text)
        StoreOriginalValue("CH6_TextBox115", TextBox115.Text)

        ' CH6 2100MHz
        TextBox117.Text = GetSafeString(row("mcc")) ' MCC
        TextBox116.Text = GetSafeString(row("mnc")) ' MNC
        TextBox115.Text = GetSafeString(row("earfcn")) ' EARFCN

        If Convert.ToBoolean(row("is_lte")) Then
            ComboBox17.Text = "LTE - FDD"
        ElseIf Convert.ToBoolean(row("is_wcdma")) Then
            ComboBox17.Text = "WCDMA"
        End If

        buttonStates(6) = False
        UpdateButtonState(6, True)
    End Sub

    Private Sub PopulateChannel7(row As DataRow)
        StoreOriginalValue("CH7_ComboBox", ComboBox18.Text)
        StoreOriginalValue("CH7_TextBox121", TextBox121.Text)
        StoreOriginalValue("CH7_TextBox120", TextBox120.Text)
        StoreOriginalValue("CH7_TextBox119", TextBox119.Text)

        ' CH7 850MHz
        TextBox121.Text = GetSafeString(row("mcc")) ' MCC
        TextBox120.Text = GetSafeString(row("mnc")) ' MNC
        TextBox119.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox18.Text = "LTE - FDD"

        buttonStates(7) = False
        UpdateButtonState(7, True)
    End Sub

    Private Sub PopulateChannel8(row As DataRow)
        StoreOriginalValue("CH8_ComboBox", ComboBox19.Text)
        StoreOriginalValue("CH8_TextBox125", TextBox125.Text)
        StoreOriginalValue("CH8_TextBox124", TextBox124.Text)
        StoreOriginalValue("CH8_TextBox123", TextBox123.Text)

        ' CH8 850MHz
        TextBox125.Text = GetSafeString(row("mcc")) ' MCC
        TextBox124.Text = GetSafeString(row("mnc")) ' MNC
        TextBox123.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox19.Text = "LTE - FDD"

        buttonStates(8) = False
        UpdateButtonState(8, True)
    End Sub

    Private Sub PopulateChannel9(row As DataRow)
        StoreOriginalValue("CH9_ComboBox", ComboBox20.Text)
        StoreOriginalValue("CH9_TextBox129", TextBox129.Text)
        StoreOriginalValue("CH9_TextBox128", TextBox128.Text)
        StoreOriginalValue("CH9_TextBox127", TextBox127.Text)

        ' CH9 2300MHz (TDD)
        TextBox129.Text = GetSafeString(row("mcc")) ' MCC
        TextBox128.Text = GetSafeString(row("mnc")) ' MNC
        TextBox127.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox20.Text = "LTE - TDD"

        buttonStates(9) = False
        UpdateButtonState(9, True)
    End Sub

    Private Sub PopulateChannel10(row As DataRow)
        StoreOriginalValue("CH9_ComboBox", ComboBox20.Text)
        StoreOriginalValue("CH9_TextBox129", TextBox129.Text)
        StoreOriginalValue("CH9_TextBox128", TextBox128.Text)
        StoreOriginalValue("CH9_TextBox126", TextBox126.Text)

        ' CH9 2300MHz (TDD)
        TextBox129.Text = GetSafeString(row("mcc")) ' MCC
        TextBox128.Text = GetSafeString(row("mnc")) ' MNC
        TextBox126.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox20.Text = "LTE - TDD"

        buttonStates(9) = False
        UpdateButtonState(9, True)
    End Sub

    Private Sub PopulateChannel11(row As DataRow)
        StoreOriginalValue("CH11_ComboBox", ComboBox21.Text)
        StoreOriginalValue("CH11_TextBox133", TextBox133.Text)
        StoreOriginalValue("CH11_TextBox132", TextBox132.Text)
        StoreOriginalValue("CH11_TextBox131", TextBox131.Text)

        ' CH11 700MHz
        TextBox133.Text = GetSafeString(row("mcc")) ' MCC
        TextBox132.Text = GetSafeString(row("mnc")) ' MNC
        TextBox131.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox21.Text = "LTE - FDD"

        buttonStates(11) = False
        UpdateButtonState(11, True)
    End Sub

    Private Sub PopulateChannel12(row As DataRow)
        StoreOriginalValue("CH12_ComboBox", ComboBox22.Text)
        StoreOriginalValue("CH12_TextBox137", TextBox137.Text)
        StoreOriginalValue("CH12_TextBox136", TextBox136.Text)
        StoreOriginalValue("CH12_TextBox135", TextBox135.Text)

        ' CH12 700MHz
        TextBox137.Text = GetSafeString(row("mcc")) ' MCC
        TextBox136.Text = GetSafeString(row("mnc")) ' MNC
        TextBox135.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox22.Text = "LTE - FDD"

        buttonStates(12) = False
        UpdateButtonState(12, True)
    End Sub

    Private Sub PopulateChannel13(row As DataRow)
        StoreOriginalValue("CH13_ComboBox", ComboBox23.Text)
        StoreOriginalValue("CH13_TextBox141", TextBox141.Text)
        StoreOriginalValue("CH13_TextBox140", TextBox140.Text)
        StoreOriginalValue("CH13_TextBox139", TextBox139.Text)

        ' CH13 2600MHz
        TextBox141.Text = GetSafeString(row("mcc")) ' MCC
        TextBox140.Text = GetSafeString(row("mnc")) ' MNC
        TextBox139.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox23.Text = "LTE - FDD"

        buttonStates(13) = False
        UpdateButtonState(13, True)
    End Sub

    Private Sub PopulateChannel14(row As DataRow)
        StoreOriginalValue("CH14_ComboBox", ComboBox24.Text)
        StoreOriginalValue("CH14_TextBox145", TextBox145.Text)
        StoreOriginalValue("CH14_TextBox144", TextBox144.Text)
        StoreOriginalValue("CH14_TextBox143", TextBox143.Text)

        ' CH14 2600MHz
        TextBox145.Text = GetSafeString(row("mcc")) ' MCC
        TextBox144.Text = GetSafeString(row("mnc")) ' MNC
        TextBox143.Text = GetSafeString(row("earfcn")) ' EARFCN

        ComboBox24.Text = "LTE - FDD"

        buttonStates(14) = False
        UpdateButtonState(14, True)
    End Sub

    ' Helper method to safely get string values
    Private Function GetSafeString(value As Object) As String
        If value Is DBNull.Value OrElse value Is Nothing Then
            Return String.Empty
        Else
            Return value.ToString()
        End If
    End Function

    Public Function GetGSMDataById(gsmId As Integer) As DataRow

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT * FROM gsm_cells WHERE gsm_id = @gsmId"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@gsmId", gsmId)

                Using adapter As New SqlDataAdapter(command)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    If dt.Rows.Count > 0 Then
                        Return dt.Rows(0)
                    End If
                End Using
            End Using
        End Using
        Return Nothing
    End Function

    Public Function GetWCDMADataById(wcdmaId As Integer) As DataRow
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT * FROM wcdma_cells WHERE wcdma_id = @wcdmaId"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@wcdmaId", wcdmaId)

                Using adapter As New SqlDataAdapter(command)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    If dt.Rows.Count > 0 Then
                        Return dt.Rows(0)
                    End If
                End Using
            End Using
        End Using
        Return Nothing
    End Function

    Public Function GetLTEDataById(lteId As Integer) As DataRow
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT * FROM lte_cells WHERE lte_id = @lteId"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@lteId", lteId)

                Using adapter As New SqlDataAdapter(command)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    If dt.Rows.Count > 0 Then
                        Return dt.Rows(0)
                    End If
                End Using
            End Using
        End Using
        Return Nothing
    End Function

    Private Sub AddInputConstraints()
        AddNumericConstraint(TextBox1, 3) ' CH1 MCC
        AddNumericConstraint(TextBox101, 3) ' CH2 MCC
        AddNumericConstraint(TextBox105, 3) ' CH3 MCC
        AddNumericConstraint(TextBox109, 3) ' CH4 MCC
        AddNumericConstraint(TextBox113, 3) ' CH5 MCC
        AddNumericConstraint(TextBox117, 3) ' CH6 MCC
        AddNumericConstraint(TextBox121, 3) ' CH7 MCC
        AddNumericConstraint(TextBox125, 3) ' CH8 MCC
        AddNumericConstraint(TextBox129, 3) ' CH9 MCC
        AddNumericConstraint(TextBox133, 3) ' CH11 MCC
        AddNumericConstraint(TextBox137, 3) ' CH12 MCC
        AddNumericConstraint(TextBox141, 3) ' CH13 MCC
        AddNumericConstraint(TextBox145, 3) ' CH14 MCC

        ' MNC fields (2-3 digits) - Complete all MNC fields
        AddNumericConstraint(TextBox2, 3) ' CH1 MNC
        AddNumericConstraint(TextBox100, 3) ' CH2 MNC
        AddNumericConstraint(TextBox104, 3) ' CH3 MNC
        AddNumericConstraint(TextBox108, 3) ' CH4 MNC
        AddNumericConstraint(TextBox112, 3) ' CH5 MNC
        AddNumericConstraint(TextBox116, 3) ' CH6 MNC
        AddNumericConstraint(TextBox120, 3) ' CH7 MNC
        AddNumericConstraint(TextBox124, 3) ' CH8 MNC
        AddNumericConstraint(TextBox128, 3) ' CH9 MNC
        AddNumericConstraint(TextBox132, 3) ' CH11 MNC
        AddNumericConstraint(TextBox136, 3) ' CH12 MNC
        AddNumericConstraint(TextBox140, 3) ' CH13 MNC
        AddNumericConstraint(TextBox144, 3) ' CH14 MNC

        ' EARFCN fields (0-65535) - Complete all EARFCN fields
        AddNumericConstraint(TextBox3, 5) ' CH1 EARFCN
        AddNumericConstraint(TextBox99, 5) ' CH2 EARFCN
        AddNumericConstraint(TextBox103, 5) ' CH3 EARFCN
        AddNumericConstraint(TextBox107, 5) ' CH4 EARFCN
        AddNumericConstraint(TextBox111, 5) ' CH5 EARFCN
        AddNumericConstraint(TextBox115, 5) ' CH6 EARFCN
        AddNumericConstraint(TextBox119, 5) ' CH7 EARFCN
        AddNumericConstraint(TextBox123, 5) ' CH8 EARFCN
        AddNumericConstraint(TextBox127, 5) ' CH9 EARFCN
        AddNumericConstraint(TextBox131, 5) ' CH11 EARFCN
        AddNumericConstraint(TextBox135, 5) ' CH12 EARFCN
        AddNumericConstraint(TextBox139, 5) ' CH13 EARFCN
        AddNumericConstraint(TextBox143, 5) ' CH14 EARFCN

        ' BSIC fields (0-63, 1 byte) - Complete all BSIC fields
        AddNumericConstraint(TextBox97, 2) ' CH1 BSIC
        AddNumericConstraint(TextBox98, 2) ' CH2 BSIC
        AddNumericConstraint(TextBox102, 2) ' CH3 BSIC
        AddNumericConstraint(TextBox106, 2) ' CH4 BSIC

        ' Additional EARFCN field for channel 9
        AddNumericConstraint(TextBox126, 5) ' CH9 EARFCN2
    End Sub

    Private Sub AddNumericConstraint(textBox As TextBox, maxLength As Integer)
        textBox.MaxLength = maxLength
        AddHandler textBox.KeyPress, AddressOf NumericKeyPressHandler
    End Sub

    Private Sub NumericKeyPressHandler(sender As Object, e As KeyPressEventArgs)
        ' Only allow digits and control characters (backspace, delete, etc.)
        If Not Char.IsDigit(e.KeyChar) AndAlso Not Char.IsControl(e.KeyChar) Then
            e.Handled = True
        End If
    End Sub

    ' Helper methods for conversion
    Private Function ConvertToInt(value As String) As Object
        If String.IsNullOrEmpty(value) Then Return DBNull.Value
        If Integer.TryParse(value, Nothing) Then Return Integer.Parse(value)
        Return DBNull.Value
    End Function

    Private Function ConvertToLong(value As String) As Object
        If String.IsNullOrEmpty(value) Then Return DBNull.Value
        If Long.TryParse(value, Nothing) Then Return Long.Parse(value)
        Return DBNull.Value
    End Function

    Private Function ConvertToByte(value As String) As Object
        If String.IsNullOrEmpty(value) Then Return DBNull.Value
        If Byte.TryParse(value, Nothing) Then Return Byte.Parse(value)
        Return DBNull.Value
    End Function

    Public Function GetBaseStationByChannel(channelNumber As Integer) As DataRow
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT TOP 1 * FROM base_stations WHERE channel_number = @ChannelNumber ORDER BY last_updated DESC"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ChannelNumber", channelNumber)


                Using adapter As New SqlDataAdapter(command)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    If dt.Rows.Count > 0 Then
                        Return dt.Rows(0)
                    End If
                End Using
            End Using
        End Using
        Return Nothing
    End Function

    Private Function CreateLabelAndComboBox(labelText As String, selectedValue As String, yPos As Integer, items As String()) As Control()
        Dim lbl As New Label() With {.Text = labelText, .Location = New Point(20, yPos + 3), .Width = 80}
        Dim cmb As New ComboBox() With {.Location = New Point(120, yPos), .Width = 100, .DropDownStyle = ComboBoxStyle.DropDownList}
        cmb.Items.AddRange(items)

        If Not String.IsNullOrEmpty(selectedValue) Then
            cmb.SelectedItem = selectedValue
        ElseIf cmb.Items.Count > 0 Then
            cmb.SelectedIndex = 0
        End If

        cmb.Tag = labelText.Replace(":", "")
        Return {lbl, cmb}
    End Function

    Private Function CreateLabelAndTextBox(labelText As String, textValue As String, yPos As Integer, maxLength As Integer) As Control()
        Dim lbl As New Label() With {.Text = labelText, .Location = New Point(20, yPos + 3), .Width = 80}
        Dim txt As New TextBox() With {.Text = textValue, .Location = New Point(120, yPos), .Width = 100, .MaxLength = maxLength, .Tag = labelText.Replace(":", "")}
        AddHandler txt.KeyPress, AddressOf NumericKeyPressHandler
        Return {lbl, txt}
    End Function

    Private Sub SaveBaseStation(channel As Integer, technology As String, mccText As String, mncText As String, earfcnText As String, Optional bsicText As String = Nothing, Optional earfcn2Text As String = Nothing)
        Try
            Dim mcc = ParseInteger(mccText)
            Dim mnc = ParseInteger(mncText)
            Dim earfcn = ParseInteger(earfcnText)
            Dim bsic = ParseInteger(bsicText)
            Dim earfcn2 = ParseInteger(earfcn2Text)

            Dim isGsm = technology.Contains("GSM")
            Dim isLte = technology.Contains("LTE")
            Dim isWcdma = technology.Contains("WCDMA")

            Dim existingRecordId As Integer? = GetBaseStationIdByChannel(channel)

            If existingRecordId.HasValue Then
                UpdateBaseStation(existingRecordId.Value, channel, technology, mcc, mnc, earfcn, bsic, earfcn2, isGsm, isLte, isWcdma)
            Else
                InsertBaseStation(channel, technology, mcc, mnc, earfcn, bsic, earfcn2, isGsm, isLte, isWcdma)
            End If

            MessageBox.Show($"Base station CH{channel} saved successfully!")

            buttonStates(channel) = False
            UpdateButtonState(channel, True)

            StoreOriginalValue($"CH{channel}_ComboBox", technology)
            StoreOriginalValue($"CH{channel}_TextBox1", mccText)
            StoreOriginalValue($"CH{channel}_TextBox2", mncText)
            StoreOriginalValue($"CH{channel}_TextBox3", earfcnText)

            If bsicText IsNot Nothing Then
                If channel = 1 Then
                    StoreOriginalValue($"CH{channel}_TextBox97", bsicText)
                ElseIf channel = 2 Then
                    StoreOriginalValue($"CH{channel}_TextBox98", bsicText)
                ElseIf channel = 3 Then
                    StoreOriginalValue($"CH{channel}_TextBox102", bsicText)
                ElseIf channel = 4 Then
                    StoreOriginalValue($"CH{channel}_TextBox106", bsicText)
                End If
            End If

            If earfcn2Text IsNot Nothing AndAlso channel = 9 Then
                StoreOriginalValue($"CH{channel}_TextBox126", earfcn2Text)
            End If

        Catch ex As Exception
            MessageBox.Show($"Error saving base station CH{channel}: {ex.Message}")
        End Try
    End Sub

    Private Function GetBaseStationIdByChannel(channelNumber As Integer) As Integer?
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT base_station_id FROM base_stations WHERE channel_number = @ChannelNumber"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ChannelNumber", channelNumber)

                Dim result = command.ExecuteScalar()
                If result IsNot Nothing AndAlso result IsNot DBNull.Value Then
                    Return Convert.ToInt32(result)
                End If
            End Using
        End Using
        Return Nothing
    End Function

    Private Sub UpdateBaseStation(baseStationId As Integer, channel As Integer, technology As String, mcc As Integer?, mnc As Integer?, earfcn As Integer?, bsic As Integer?, earfcn2 As Integer?, isGsm As Boolean, isLte As Boolean, isWcdma As Boolean)
        Using connection As New SqlConnection(connectionString)
            connection.Open()

            Dim query As String = "UPDATE base_stations SET
                            channel_number = @ChannelNumber,
                            is_gsm = @IsGsm,
                            is_lte = @IsLte,
                            is_wcdma = @IsWcdma,
                            earfcn = @Earfcn,
                            mcc = @Mcc,
                            mnc = @Mnc,
                            bsic = @Bsic,
                            band = @Band,
                            last_updated = SYSUTCDATETIME()
                            WHERE base_station_id = @BaseStationId"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@BaseStationId", baseStationId)
                command.Parameters.AddWithValue("@ChannelNumber", channel)
                command.Parameters.AddWithValue("@IsGsm", isGsm)
                command.Parameters.AddWithValue("@IsLte", isLte)
                command.Parameters.AddWithValue("@IsWcdma", isWcdma)
                command.Parameters.AddWithValue("@Earfcn", If(earfcn.HasValue, earfcn.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Mcc", If(mcc.HasValue, mcc.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Mnc", If(mnc.HasValue, mnc.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Bsic", If(bsic.HasValue, bsic.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Band", BandHelper.GetFrequencyBandByChannel(channel))

                command.ExecuteNonQuery()
            End Using

            If earfcn2.HasValue AndAlso channel = 9 Then
                Dim query2 As String = "UPDATE base_stations SET
                                channel_number = @ChannelNumber,
                                is_gsm = @IsGsm,
                                is_lte = @IsLte,
                                is_wcdma = @IsWcdma,
                                earfcn = @Earfcn,
                                mcc = @Mcc,
                                mnc = @Mnc,
                                bsic = @Bsic,
                                band = @Band,
                                last_updated = SYSUTCDATETIME()
                                WHERE channel_number = 10"

                Using command2 As New SqlCommand(query2, connection)
                    command2.Parameters.AddWithValue("@ChannelNumber", 10)
                    command2.Parameters.AddWithValue("@IsGsm", isGsm)
                    command2.Parameters.AddWithValue("@IsLte", isLte)
                    command2.Parameters.AddWithValue("@IsWcdma", isWcdma)
                    command2.Parameters.AddWithValue("@Earfcn", earfcn2.Value)
                    command2.Parameters.AddWithValue("@Mcc", If(mcc.HasValue, mcc.Value, DBNull.Value))
                    command2.Parameters.AddWithValue("@Mnc", If(mnc.HasValue, mnc.Value, DBNull.Value))
                    command2.Parameters.AddWithValue("@Bsic", If(bsic.HasValue, bsic.Value, DBNull.Value))
                    command2.Parameters.AddWithValue("@Band", BandHelper.GetFrequencyBandByChannel(10))

                    command2.ExecuteNonQuery()
                End Using
            End If
        End Using
    End Sub


    Private Sub InsertBaseStation(channel As Integer, technology As String, mcc As Integer?, mnc As Integer?, earfcn As Integer?, bsic As Integer?, earfcn2 As Integer?, isGsm As Boolean, isLte As Boolean, isWcdma As Boolean)
        Using connection As New SqlConnection(connectionString)
            connection.Open()

            Dim query As String = "INSERT INTO base_stations 
                               (channel_number, is_gsm, is_lte, is_wcdma, 
                                earfcn, mcc, mnc, bsic, band, last_updated)
                               VALUES 
                               (@ChannelNumber, @IsGsm, @IsLte, @IsWcdma, 
                                @Earfcn, @Mcc, @Mnc, @Bsic, @Band, SYSUTCDATETIME())"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ChannelNumber", channel)
                command.Parameters.AddWithValue("@IsGsm", isGsm)
                command.Parameters.AddWithValue("@IsLte", isLte)
                command.Parameters.AddWithValue("@IsWcdma", isWcdma)
                command.Parameters.AddWithValue("@Earfcn", If(earfcn.HasValue, earfcn.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Mcc", If(mcc.HasValue, mcc.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Mnc", If(mnc.HasValue, mnc.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Bsic", If(bsic.HasValue, bsic.Value, DBNull.Value))
                command.Parameters.AddWithValue("@Band", BandHelper.GetFrequencyBandByChannel(channel))

                command.ExecuteNonQuery()
            End Using

            If earfcn2.HasValue AndAlso channel = 9 Then
                Using command As New SqlCommand(query, connection)
                    command.Parameters.AddWithValue("@ChannelNumber", 10)
                    command.Parameters.AddWithValue("@IsGsm", isGsm)
                    command.Parameters.AddWithValue("@IsLte", isLte)
                    command.Parameters.AddWithValue("@IsWcdma", isWcdma)
                    command.Parameters.AddWithValue("@Earfcn", earfcn2.Value) ' Use earfcn2 here
                    command.Parameters.AddWithValue("@Mcc", If(mcc.HasValue, mcc.Value, DBNull.Value))
                    command.Parameters.AddWithValue("@Mnc", If(mnc.HasValue, mnc.Value, DBNull.Value))
                    command.Parameters.AddWithValue("@Bsic", If(bsic.HasValue, bsic.Value, DBNull.Value))
                    command.Parameters.AddWithValue("@Band", BandHelper.GetFrequencyBandByChannel(10)) ' Band for channel 10

                    command.ExecuteNonQuery()
                End Using
            End If
        End Using
    End Sub


    Private Sub AddAdvancedConstraints()
        ' BSIC validation (0-63)
        AddHandler TextBox97.Validating, Sub(s, ev) ValidateBSIC(TextBox97, ev)
        AddHandler TextBox98.Validating, Sub(s, ev) ValidateBSIC(TextBox98, ev)
        AddHandler TextBox102.Validating, Sub(s, ev) ValidateBSIC(TextBox102, ev)
        AddHandler TextBox106.Validating, Sub(s, ev) ValidateBSIC(TextBox106, ev)

        ' MCC validation (001-999)
        AddHandler TextBox1.Validating, Sub(s, ev) ValidateMCC(TextBox1, ev)
        AddHandler TextBox101.Validating, Sub(s, ev) ValidateMCC(TextBox101, ev)
        AddHandler TextBox105.Validating, Sub(s, ev) ValidateMCC(TextBox105, ev)
        AddHandler TextBox109.Validating, Sub(s, ev) ValidateMCC(TextBox109, ev)
        AddHandler TextBox113.Validating, Sub(s, ev) ValidateMCC(TextBox113, ev)
        AddHandler TextBox117.Validating, Sub(s, ev) ValidateMCC(TextBox117, ev)
        AddHandler TextBox121.Validating, Sub(s, ev) ValidateMCC(TextBox121, ev)
        AddHandler TextBox125.Validating, Sub(s, ev) ValidateMCC(TextBox125, ev)
        AddHandler TextBox129.Validating, Sub(s, ev) ValidateMCC(TextBox129, ev)
        AddHandler TextBox133.Validating, Sub(s, ev) ValidateMCC(TextBox133, ev)
        AddHandler TextBox137.Validating, Sub(s, ev) ValidateMCC(TextBox137, ev)
        AddHandler TextBox141.Validating, Sub(s, ev) ValidateMCC(TextBox141, ev)
        AddHandler TextBox145.Validating, Sub(s, ev) ValidateMCC(TextBox145, ev)

        ' MNC validation (00-999)
        AddHandler TextBox2.Validating, Sub(s, ev) ValidateMNC(TextBox2, ev)
        AddHandler TextBox100.Validating, Sub(s, ev) ValidateMNC(TextBox100, ev)
        AddHandler TextBox104.Validating, Sub(s, ev) ValidateMNC(TextBox104, ev)
        AddHandler TextBox108.Validating, Sub(s, ev) ValidateMNC(TextBox108, ev)
        AddHandler TextBox112.Validating, Sub(s, ev) ValidateMNC(TextBox112, ev)
        AddHandler TextBox116.Validating, Sub(s, ev) ValidateMNC(TextBox116, ev)
        AddHandler TextBox120.Validating, Sub(s, ev) ValidateMNC(TextBox120, ev)
        AddHandler TextBox124.Validating, Sub(s, ev) ValidateMNC(TextBox124, ev)
        AddHandler TextBox128.Validating, Sub(s, ev) ValidateMNC(TextBox128, ev)
        AddHandler TextBox132.Validating, Sub(s, ev) ValidateMNC(TextBox132, ev)
        AddHandler TextBox136.Validating, Sub(s, ev) ValidateMNC(TextBox136, ev)
        AddHandler TextBox140.Validating, Sub(s, ev) ValidateMNC(TextBox140, ev)
        AddHandler TextBox144.Validating, Sub(s, ev) ValidateMNC(TextBox144, ev)

        ' EARFCN validation (0-65535)
        AddHandler TextBox3.Validating, Sub(s, ev) ValidateEARFCN(TextBox3, ev)
        AddHandler TextBox99.Validating, Sub(s, ev) ValidateEARFCN(TextBox99, ev)
        AddHandler TextBox103.Validating, Sub(s, ev) ValidateEARFCN(TextBox103, ev)
        AddHandler TextBox107.Validating, Sub(s, ev) ValidateEARFCN(TextBox107, ev)
        AddHandler TextBox111.Validating, Sub(s, ev) ValidateEARFCN(TextBox111, ev)
        AddHandler TextBox115.Validating, Sub(s, ev) ValidateEARFCN(TextBox115, ev)
        AddHandler TextBox119.Validating, Sub(s, ev) ValidateEARFCN(TextBox119, ev)
        AddHandler TextBox123.Validating, Sub(s, ev) ValidateEARFCN(TextBox123, ev)
        AddHandler TextBox127.Validating, Sub(s, ev) ValidateEARFCN(TextBox127, ev)
        AddHandler TextBox131.Validating, Sub(s, ev) ValidateEARFCN(TextBox131, ev)
        AddHandler TextBox135.Validating, Sub(s, ev) ValidateEARFCN(TextBox135, ev)
        AddHandler TextBox139.Validating, Sub(s, ev) ValidateEARFCN(TextBox139, ev)
        AddHandler TextBox143.Validating, Sub(s, ev) ValidateEARFCN(TextBox143, ev)
        AddHandler TextBox126.Validating, Sub(s, ev) ValidateEARFCN(TextBox126, ev) ' CH9 EARFCN2
    End Sub

    Private Sub DataGridView8_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView8.CellClick
        If e.RowIndex >= 0 Then
            Dim row As DataGridViewRow = DataGridView8.Rows(e.RowIndex)
            If row.Cells("tableName").Value IsNot Nothing Then
                selectedSchema = row.Cells("tableName").Value.ToString()
                Console.WriteLine("Selected schema: " & selectedSchema)
            End If
        End If
    End Sub

    Private Sub ValidateEARFCN(textBox As TextBox, e As CancelEventArgs)
        If Not String.IsNullOrEmpty(textBox.Text) Then
            Dim value As Integer
            If Integer.TryParse(textBox.Text, value) Then
                If value < 0 OrElse value > 65535 Then
                    MessageBox.Show("EARFCN must be between 0 and 65535", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    e.Cancel = True
                End If
            Else
                MessageBox.Show("EARFCN must be a numeric value", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                e.Cancel = True
            End If
        End If
    End Sub

    Private Sub ValidateBSIC(textBox As TextBox, e As CancelEventArgs)
        If Not String.IsNullOrEmpty(textBox.Text) Then
            Dim value As Integer
            If Integer.TryParse(textBox.Text, value) Then
                If value < 0 OrElse value > 63 Then
                    MessageBox.Show("BSIC must be between 0 and 63", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    e.Cancel = True
                End If
            End If
        End If
    End Sub

    Private Sub ValidateMCC(textBox As TextBox, e As CancelEventArgs)
        If Not String.IsNullOrEmpty(textBox.Text) Then
            If textBox.Text.Length <> 3 Then
                MessageBox.Show("MCC must be 3 digits", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                e.Cancel = True
            End If
        End If
    End Sub

    Private Sub ValidateMNC(textBox As TextBox, e As CancelEventArgs)
        If Not String.IsNullOrEmpty(textBox.Text) Then
            If textBox.Text.Length < 2 OrElse textBox.Text.Length > 3 Then
                MessageBox.Show("MNC must be 2 or 3 digits", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                e.Cancel = True
            End If
        End If
    End Sub

    Private Sub StyleChannelAnalyzerComponents()
        StyleDataGridView(DataGridView1)
        StyleDataGridView(DataGridView2)
        StyleDataGridView(DataGridView3)
        StyleDataGridView(DataGridView4)
        StyleDataGridView(DataGridView5)
        StyleDataGridView(DataGridView6)
        StyleDataGridView(DataGridView7)
        StyleDataGridView(DataGridView8)
        StyleDataGridView(DataGridView9)
        StyleDataGridView(DataGridView10)

        StyleGroupBox(GroupBox1)
        StyleGroupBox(GroupBox28)
        StyleGroupBox(GroupBox29)
        StyleGroupBox(GroupBox30)
        StyleGroupBox(GroupBox31)


        StyleCheckedListBox(CheckedListBox1)

        StyleAnalyzerLabels()

        StyleButtonColors()

        StyleManualBaseStation(GroupBox31)
    End Sub

    Private Sub StyleManualBaseStation(groupBox As GroupBox)
        groupBox.BackColor = Color.Gray
        groupBox.ForeColor = Color.White
        groupBox.Padding = New Padding(20)
        groupBox.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)

        For Each control As Control In groupBox.Controls
            If TypeOf control Is GroupBox Then
                Dim subGroup As GroupBox = CType(control, GroupBox)
                subGroup.BackColor = Color.LightSteelBlue
                subGroup.ForeColor = Color.DarkBlue
            End If
        Next
    End Sub

    Private Sub StyleButtonColors()
        For Each btn As Button In {Button3, Button4, Button5}
            btn.BackColor = Color.Gray
            btn.ForeColor = Color.White
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0

            AddHandler btn.MouseEnter, AddressOf Button_Hover
            AddHandler btn.MouseLeave, AddressOf Button_Leave
        Next
    End Sub

    Private Sub Button_Hover(sender As Object, e As EventArgs)
        Dim btn As Button = DirectCast(sender, Button)
        btn.BackColor = Color.LightGray
        btn.ForeColor = Color.Black
    End Sub

    Private Sub Button_Leave(sender As Object, e As EventArgs)
        Dim btn As Button = DirectCast(sender, Button)
        btn.BackColor = Color.Gray
        btn.ForeColor = Color.White
    End Sub


    Private Sub StyleDataGridView(dgv As DataGridView)
        If dgv Is Nothing Then Return

        With dgv
            .BackgroundColor = Color.Gray
            .BorderStyle = BorderStyle.None
            .EnableHeadersVisualStyles = False
            .GridColor = Color.FromArgb(200, 200, 200)

            .ColumnHeadersDefaultCellStyle.BackColor = Color.Maroon
            .ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            .ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
            .ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            .ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing
            .ColumnHeadersHeight = 30
            .ScrollBars = ScrollBars.Both
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            .RowTemplate.Height = 25

            Dim widths As Integer() = {200, 200, 200, 200, 200, 200, 200, 200, 200, 200}
            For i As Integer = 0 To widths.Length - 1
                If i < .Columns.Count Then
                    .Columns(i).Width = widths(i)
                End If
            Next

            .RowHeadersDefaultCellStyle.BackColor = Color.Green
            .RowHeadersDefaultCellStyle.ForeColor = Color.Black
            .RowHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.0!, FontStyle.Bold)
            .RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing

            .DefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
            .DefaultCellStyle.ForeColor = Color.FromArgb(51, 51, 51)
            .DefaultCellStyle.Font = New Font("Segoe UI", 8.5!)
            .DefaultCellStyle.SelectionBackColor = Color.FromArgb(120, 180, 220)
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
            .ReadOnly = True
        End With
    End Sub

    Private Sub StyleGroupBox(gb As GroupBox)
        If gb Is Nothing Then Return

        With gb
            .ForeColor = Color.DarkGreen
            .Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
            .BackColor = Color.Transparent
        End With
    End Sub

    Private Sub StyleButton(btn As Button)
        If btn Is Nothing Then Return

        With btn
            .FlatStyle = FlatStyle.Flat
            .FlatAppearance.BorderSize = 0
            .BackColor = Color.Black
            .ForeColor = Color.White
            .Font = New Font("Segoe UI", 9.0!)
            .Cursor = Cursors.Hand
            .Padding = New Padding(5)

            .FlatAppearance.MouseOverBackColor = Color.FromArgb(71, 71, 96)
            .FlatAppearance.MouseDownBackColor = Color.FromArgb(41, 41, 61)
        End With
    End Sub

    Private Sub StyleCheckedListBox(clb As CheckedListBox)
        If clb Is Nothing Then Return

        With clb
            .BackColor = Color.FromArgb(240, 240, 240)
            .ForeColor = Color.FromArgb(51, 51, 51)
            .BorderStyle = BorderStyle.FixedSingle
            .Font = New Font("Segoe UI", 8.5!)
        End With
    End Sub

    Private Sub StyleAnalyzerLabels()
        Dim analyzerLabels As New List(Of Control)

        For Each ctrl As Control In GroupBox1.Controls
            If TypeOf ctrl Is Label Then analyzerLabels.Add(ctrl)
        Next

        For Each ctrl As Control In GroupBox28.Controls
            If TypeOf ctrl Is Label Then analyzerLabels.Add(ctrl)
        Next

        For Each ctrl As Control In GroupBox29.Controls
            If TypeOf ctrl Is Label Then analyzerLabels.Add(ctrl)
        Next

        For Each ctrl As Control In GroupBox30.Controls
            If TypeOf ctrl Is Label Then analyzerLabels.Add(ctrl)
        Next

        For Each lbl As Label In analyzerLabels
            lbl.ForeColor = Color.Gray
            lbl.Font = New Font("Segoe UI", 8.5!, FontStyle.Regular)
            lbl.BackColor = Color.Transparent
        Next
    End Sub

    Private Sub ApplyGradientBackground(control As Control, color1 As Color, color2 As Color)
        AddHandler control.Paint, Sub(sender As Object, e As PaintEventArgs)
                                      Dim rect As New Rectangle(0, 0, control.Width, control.Height)
                                      Using brush As New LinearGradientBrush(rect, color1, color2, LinearGradientMode.Vertical)
                                          e.Graphics.FillRectangle(brush, rect)
                                      End Using
                                  End Sub
        control.Invalidate()
    End Sub

    Private Sub StyleSpecificColumns()
        StyleColumn(DataGridView1, "Provider Name", Color.FromArgb(220, 240, 240))
        StyleColumn(DataGridView1, "PLMN", Color.FromArgb(240, 240, 220))
        StyleColumn(DataGridView1, "MCC", Color.FromArgb(240, 220, 220))
        StyleColumn(DataGridView1, "MNC", Color.FromArgb(220, 220, 240))

        StyleColumn(DataGridView2, "Provider Name", Color.FromArgb(220, 240, 240))
        StyleColumn(DataGridView2, "BAND", Color.FromArgb(240, 240, 220))
        StyleColumn(DataGridView2, "PSC", Color.FromArgb(220, 240, 220))

        StyleColumn(DataGridView3, "Provider Name", Color.FromArgb(220, 240, 240))
        StyleColumn(DataGridView3, "EARFCN", Color.FromArgb(240, 240, 220))
        StyleColumn(DataGridView3, "PCI", Color.FromArgb(220, 240, 220))
        StyleColumn(DataGridView3, "RSRP", Color.FromArgb(240, 220, 220))
    End Sub

    Private Sub StyleColumn(dgv As DataGridView, columnName As String, backColor As Color)
        If dgv.Columns.Contains(columnName) Then
            dgv.Columns(columnName).DefaultCellStyle.BackColor = backColor
            dgv.Columns(columnName).DefaultCellStyle.SelectionBackColor = Color.FromArgb(
            Math.Min(backColor.R + 20, 255),
            Math.Min(backColor.G + 20, 255),
            Math.Min(backColor.B + 20, 255))
        End If
    End Sub

    Private Sub CheckedListBox1_ItemCheck(sender As Object, e As ItemCheckEventArgs) Handles CheckedListBox1.ItemCheck
        Me.BeginInvoke(New Action(AddressOf ApplyFilter))
    End Sub

    Private Sub ApplyFilter()
        Dim selectedItems As New List(Of String)()
        For i As Integer = 0 To CheckedListBox1.Items.Count - 1
            If CheckedListBox1.GetItemChecked(i) Then
                selectedItems.Add(CheckedListBox1.Items(i).ToString())
            End If
        Next

        operatorFilter1 = selectedItems
        ApplyFilterToDataGridViews()
    End Sub

    Private Sub ApplyFilterToDataGridViews()
        If operatorFilter1 Is Nothing OrElse operatorFilter1.Count = 0 Then
            If DataGridView1.DataSource IsNot Nothing AndAlso TypeOf DataGridView1.DataSource Is DataTable Then
                CType(DataGridView1.DataSource, DataTable).DefaultView.RowFilter = ""
            End If
            If DataGridView2.DataSource IsNot Nothing AndAlso TypeOf DataGridView2.DataSource Is DataTable Then
                CType(DataGridView2.DataSource, DataTable).DefaultView.RowFilter = ""
            End If
            If DataGridView3.DataSource IsNot Nothing AndAlso TypeOf DataGridView3.DataSource Is DataTable Then
                CType(DataGridView3.DataSource, DataTable).DefaultView.RowFilter = ""
            End If
        Else
            FilterDataGridView(DataGridView1, operatorFilter1)
            FilterDataGridView(DataGridView2, operatorFilter1)
            FilterDataGridView(DataGridView3, operatorFilter1)
        End If
    End Sub

    Private Sub FilterDataGridView(dgv As DataGridView, filters As List(Of String))
        If dgv.DataSource IsNot Nothing AndAlso TypeOf dgv.DataSource Is DataTable Then
            Dim dt As DataTable = CType(dgv.DataSource, DataTable)
            Dim filterExpression As String = ""

            Dim providerColumnName As String = ""

            If dgv Is DataGridView1 Then
                providerColumnName = "ProviderName"
            ElseIf dgv Is DataGridView2 Then
                providerColumnName = "provider_name"
            ElseIf dgv Is DataGridView3 Then
                providerColumnName = "provider_name"
            End If

            If filters.Contains("All") Then
                dt.DefaultView.RowFilter = ""
                Return
            End If

            If Not String.IsNullOrEmpty(providerColumnName) Then
                For Each filter As String In filters
                    If Not String.IsNullOrEmpty(filterExpression) Then
                        filterExpression += " OR "
                    End If
                    filterExpression += $"{providerColumnName} LIKE '%{filter}%'"
                Next
            End If

            If Not String.IsNullOrEmpty(filterExpression) Then
                dt.DefaultView.RowFilter = filterExpression
            Else
                dt.DefaultView.RowFilter = ""
            End If
        End If
    End Sub


    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        StartCellOperation("192.168.1.90", Button6)
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        StartCellOperation("192.168.1.91", Button8)
    End Sub

    Private Sub Button11_Click(sender As Object, e As EventArgs) Handles Button11.Click
        StartCellOperation("192.168.1.92", Button11)
    End Sub

    Private Sub Button13_Click(sender As Object, e As EventArgs) Handles Button13.Click
        StartCellOperation("192.168.1.93", Button13)
    End Sub

    Private Sub Button15_Click(sender As Object, e As EventArgs) Handles Button15.Click
        StartCellOperation("192.168.1.94", Button15)
    End Sub

    Private Sub Button17_Click(sender As Object, e As EventArgs) Handles Button17.Click
        StartCellOperation("192.168.1.95", Button17)
    End Sub

    Private Sub Button19_Click(sender As Object, e As EventArgs) Handles Button19.Click
        StartCellOperation("192.168.1.96", Button19)
    End Sub

    Private Sub Button21_Click(sender As Object, e As EventArgs) Handles Button21.Click
        StartCellOperation("192.168.1.97", Button21)
    End Sub

    Private Sub Button31_Click(sender As Object, e As EventArgs) Handles Button31.Click
        StartCellOperation("192.168.1.98", Button31)
    End Sub

    Private Sub Button23_Click(sender As Object, e As EventArgs) Handles Button23.Click
        StartCellOperation("192.168.1.101", Button23)
    End Sub

    Private Sub Button25_Click(sender As Object, e As EventArgs) Handles Button25.Click
        StartCellOperation("192.168.1.102", Button25)
    End Sub

    Private Sub Button27_Click(sender As Object, e As EventArgs) Handles Button27.Click
        StartCellOperation("192.168.1.103", Button27)
    End Sub

    Private Sub Button29_Click(sender As Object, e As EventArgs) Handles Button29.Click
        StartCellOperation("192.168.1.104", Button29)
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        StopCellOperation("192.168.1.90", Button7)
    End Sub

    Private Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click
        StopCellOperation("192.168.1.91", Button9)
    End Sub

    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button12.Click
        StopCellOperation("192.168.1.92", Button12)
    End Sub

    Private Sub Button12_Click(sender As Object, e As EventArgs) Handles Button14.Click
        StopCellOperation("192.168.1.93", Button14)
    End Sub

    Private Sub Button14_Click(sender As Object, e As EventArgs) Handles Button16.Click
        StopCellOperation("192.168.1.94", Button16)
    End Sub

    Private Sub Button16_Click(sender As Object, e As EventArgs) Handles Button18.Click
        StopCellOperation("192.168.1.95", Button18)
    End Sub

    Private Sub Button18_Click(sender As Object, e As EventArgs) Handles Button20.Click
        StopCellOperation("192.168.1.96", Button20)
    End Sub

    Private Sub Button20_Click(sender As Object, e As EventArgs) Handles Button22.Click
        StopCellOperation("192.168.1.97", Button22)
    End Sub

    Private Sub Button30_Click(sender As Object, e As EventArgs) Handles Button32.Click
        StopCellOperation("192.168.1.98", Button32)
    End Sub

    Private Sub Button22_Click(sender As Object, e As EventArgs) Handles Button24.Click
        StopCellOperation("192.168.1.101", Button24)
    End Sub

    Private Sub Button24_Click(sender As Object, e As EventArgs) Handles Button26.Click
        StopCellOperation("192.168.1.102", Button26)
    End Sub

    Private Sub Button26_Click(sender As Object, e As EventArgs) Handles Button28.Click
        StopCellOperation("192.168.1.103", Button28)
    End Sub

    Private Sub Button28_Click(sender As Object, e As EventArgs) Handles Button30.Click
        StopCellOperation("192.168.1.104", Button30)
    End Sub

End Class
