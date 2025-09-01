Imports System.ComponentModel
Imports System.Data.SqlClient
Imports System.Net.NetworkInformation
Imports System.Threading
Imports System.Net
Imports System.Net.Sockets
Imports System.Windows.Forms.DataVisualization.Charting
Imports System.Windows.Forms
Imports System.Text

Public Class Form1

    Private ReadOnly connectionString As String = "Server=localhost;Database=CoreXCDb;Trusted_Connection=True;"
    Private buttonStates As New Dictionary(Of Integer, Boolean)()
    Private WithEvents pingTimer As System.Windows.Forms.Timer
    Private originalValues As New Dictionary(Of String, String)()
    Private editModeButtons As New Dictionary(Of Integer, Button)()
    Private udpClientLteWcdma As UdpClient
    Private udpClientGsm As UdpClient
    Private receivingThread As Thread
    Private isListening As Boolean = False

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ' Initialize edit mode buttons dictionary
            InitializeEditModeButtons()

            Dim dbInitializer As New DatabaseInitializer("localhost", "CoreXCDb")
            Await dbInitializer.EnsureDatabaseExistsAsync()
            Await dbInitializer.ApplySchemaAsync()
            disableAllBtns()
            LoadDataToGridViews()
            LoadBaseStationData()
            LoadBaseStationData1()
            SetupDataGridViewEvents()
            AddInputConstraints()
            AddAdvancedConstraints()
            SetupValidationEvents()
            LoadBlacklistData()
            LoadWhitelistData()
            MessageBox.Show("Database and schema ready!", "Success")
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
            ' Initial check
            Task.Run(Sub() UpdateButtonColors())
        Catch ex As Exception
            MessageBox.Show("Database setup failed: " & ex.StackTrace, "Error")
        End Try
    End Sub


    Private Sub pingTimer_Tick(sender As Object, e As EventArgs) Handles pingTimer.Tick
        ' Run ping checks in background to avoid UI freezing
        Task.Run(Sub() UpdateButtonColors())
    End Sub

    Private Function PingHost(ipAddress As String) As Boolean
        Try
            Dim ping As New Ping()
            Dim reply As PingReply = ping.Send(ipAddress, 1000) ' 1 second timeout

            Return reply.Status = IPStatus.Success
        Catch
            Return False
        End Try
    End Function

    Private Sub UpdateButtonColors()
        ' Dictionary to map button controls to their corresponding IP addresses
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
                Dim query As String = "SELECT imsi, imei FROM blacklist"
                Dim adapter As New SqlDataAdapter(query, connection)
                Dim table As New DataTable()
                adapter.Fill(table)

                DataGridView9.DataSource = table
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading blacklist: " & ex.Message)
        End Try
    End Sub

    Private Sub Button36_Click(sender As Object, e As EventArgs) Handles Button36.Click
        SearchIMSI()
    End Sub

    Private Sub SearchIMSI()
        Dim searchValue As String = TextBox96.Text.Trim()

        If String.IsNullOrEmpty(searchValue) Then
            MessageBox.Show("Please enter an IMSI to search")
            Return
        End If

        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT result_no, date_event, location_name, source, provider_name, mcc, mnc, imsi, imei, tmsi, guti, count, signal_level, time_advance, phone_model, event, longitude, latitude FROM scan_results WHERE imsi LIKE @imsi"

                Using cmd As New SqlCommand(query, conn)
                    cmd.Parameters.AddWithValue("@imsi", "%" + searchValue + "%")

                    Using adapter As New SqlDataAdapter(cmd)
                        Dim dt As New DataTable()
                        adapter.Fill(dt)

                        'Bind to DataGridView
                        DataGridView4.DataSource = dt

                        If dt.Rows.Count = 0 Then
                            MessageBox.Show("No results found for IMSI: " & searchValue)
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error searching IMSI: " & ex.Message)
        End Try
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        StartChannelAnalyzer()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        StopChannelAnalyzer()
        Thread.Sleep(1000)
        StartChannelAnalyzer()
    End Sub

    Private Sub StartChannelAnalyzer()
        Try
            udpClientLteWcdma = New UdpClient()
            udpClientGsm = New UdpClient()

            Dim lteWcdmaEndpoint As New IPEndPoint(IPAddress.Parse("192.168.1.99"), 9001)
            Dim gsmEndpoint As New IPEndPoint(IPAddress.Parse("192.168.1.100"), 9001)

            udpClientLteWcdma.Connect(lteWcdmaEndpoint)
            udpClientGsm.Connect(gsmEndpoint)

            Dim command As String = "StartSniffer"
            Dim commandBytes As Byte() = Encoding.ASCII.GetBytes(command)

            udpClientLteWcdma.Send(commandBytes, commandBytes.Length)
            udpClientGsm.Send(commandBytes, commandBytes.Length)

            isListening = True
            receivingThread = New Thread(AddressOf ReceiveData)
            receivingThread.IsBackground = True
            receivingThread.Start()

            PictureBox15.BackColor = Color.Green
            PictureBox16.BackColor = Color.Green
            PictureBox17.BackColor = Color.Green

            MessageBox.Show("Channel analyzer started successfully!")
        Catch ex As Exception
            MessageBox.Show("Error starting channel analyzer: " & ex.Message)
        End Try
    End Sub

    Private Sub StopChannelAnalyzer()
        Try
            isListening = False

            If receivingThread IsNot Nothing AndAlso receivingThread.IsAlive Then
                receivingThread.Join(1000)
            End If

            If udpClientLteWcdma IsNot Nothing Then
                udpClientLteWcdma.Close()
            End If

            If udpClientGsm IsNot Nothing Then
                udpClientGsm.Close()
            End If

            PictureBox15.BackColor = Color.Red
            PictureBox16.BackColor = Color.Red
            PictureBox17.BackColor = Color.Red

            MessageBox.Show("Channel analyzer stopped!")
        Catch ex As Exception
            MessageBox.Show("Error stopping channel analyzer: " & ex.Message)
        End Try
    End Sub

    Private Sub ReceiveData()
        While isListening
            Try
                ' Check if there's data available on LTE/WCDMA port
                If udpClientLteWcdma.Available > 0 Then
                    Dim remoteEP As IPEndPoint = Nothing
                    Dim receiveBytes As Byte() = udpClientLteWcdma.Receive(remoteEP)
                    Dim response As String = Encoding.ASCII.GetString(receiveBytes)

                    ProcessLteWcdmaData(response)
                End If

                ' Check if there's data available on GSM port
                If udpClientGsm.Available > 0 Then
                    Dim remoteEP As IPEndPoint = Nothing
                    Dim receiveBytes As Byte() = udpClientGsm.Receive(remoteEP)
                    Dim response As String = Encoding.ASCII.GetString(receiveBytes)

                    ProcessGsmData(response)
                End If

                ' Small delay to prevent CPU overuse
                Thread.Sleep(100)
            Catch ex As Exception
                ' Handle exceptions (e.g., socket closed)
                If isListening Then
                    ' Only log if we're supposed to be listening
                    ' You might want to add proper logging here
                    Debug.WriteLine("Error receiving data: " & ex.Message)
                End If
            End Try
        End While
    End Sub

    Private Sub ProcessLteWcdmaData(data As String)
        ' Parse the received data and determine if it's LTE or WCDMA
        ' This is a simplified example - you'll need to implement actual parsing logic
        ' based on your device's response format

        Try
            ' Split the data into lines for processing
            Dim lines As String() = data.Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)

            For Each line As String In lines
                If line.Contains("LTE") Then
                    ProcessLteData(line)
                ElseIf line.Contains("WCDMA") Or line.Contains("UMTS") Then
                    ProcessWcdmaData(line)
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("Error processing LTE/WCDMA data: " & ex.Message)
        End Try
    End Sub

    Private Sub ProcessGsmData(data As String)
        Try
            Dim lines As String() = data.Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)

            For Each line As String In lines
                If line.Contains("GSM") Then
                    Dim parts As String() = line.Split(","c)

                    If parts.Length >= 10 Then
                        Dim providerName As String = parts(0).Trim()
                        Dim plmn As String = parts(1).Trim()
                        Dim mcc As Integer = Integer.Parse(parts(2).Trim())
                        Dim mnc As Integer = Integer.Parse(parts(3).Trim())
                        Dim band As String = parts(4).Trim()
                        Dim arfcn As Integer = Integer.Parse(parts(5).Trim())
                        Dim lac As Integer = Integer.Parse(parts(6).Trim())
                        Dim nbCell As Integer = Integer.Parse(parts(7).Trim())
                        Dim cellId As Long = Long.Parse(parts(8).Trim())
                        Dim bsic As Byte = Byte.Parse(parts(9).Trim())

                        InsertGsmData(providerName, plmn, mcc, mnc, band, arfcn, lac, nbCell, cellId, bsic)

                        UpdateGsmDataGridView(providerName, plmn, mcc, mnc, band, arfcn, lac, nbCell, cellId, bsic)
                    End If
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("Error processing GSM data: " & ex.Message)
        End Try
    End Sub

    Private Sub ProcessLteData(line As String)
        Try
            Dim parts As String() = line.Split(","c)

            If parts.Length >= 12 Then
                Dim providerName As String = parts(0).Trim()
                Dim plmn As String = parts(1).Trim()
                Dim mcc As Integer = Integer.Parse(parts(2).Trim())
                Dim mnc As Integer = Integer.Parse(parts(3).Trim())
                Dim band As String = parts(4).Trim()
                Dim pci As Integer = Integer.Parse(parts(5).Trim())
                Dim nbEarfcn As Integer = Integer.Parse(parts(6).Trim())
                Dim nbsc As Integer = Integer.Parse(parts(7).Trim())
                Dim lac As Integer = Integer.Parse(parts(8).Trim())
                Dim cellId As Long = Long.Parse(parts(9).Trim())
                Dim rsrp As Double = Double.Parse(parts(10).Trim())

                InsertLteData(providerName, plmn, mcc, mnc, band, pci, nbEarfcn, nbsc, lac, cellId, rsrp)

                UpdateLteDataGridView(providerName, plmn, mcc, mnc, band, pci, nbEarfcn, nbsc, lac, cellId, rsrp)
            End If
        Catch ex As Exception
            Debug.WriteLine("Error processing LTE data: " & ex.Message)
        End Try
    End Sub

    Private Sub ProcessWcdmaData(line As String)
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
                             band As String, arfcn As Integer, lac As Integer, nbCell As Integer,
                             cellId As Long, bsic As Byte)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "INSERT INTO gsm_cells (ProviderName, plmn, mcc, mnc, band, arfcn, lac, nb_cell, cell_id, bsic) " &
                                 "VALUES (@ProviderName, @plmn, @mcc, @mnc, @band, @arfcn, @lac, @nbCell, @cellId, @bsic)"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ProviderName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@mcc", mcc)
                command.Parameters.AddWithValue("@mnc", mnc)
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@arfcn", arfcn)
                command.Parameters.AddWithValue("@lac", lac)
                command.Parameters.AddWithValue("@nbCell", nbCell)
                command.Parameters.AddWithValue("@cellId", cellId)
                command.Parameters.AddWithValue("@bsic", bsic)

                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Sub InsertLteData(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                             band As String, pci As Integer, nbEarfcn As Integer, nbsc As Integer,
                             lac As Integer, cellId As Long, rsrp As Double)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "INSERT INTO lte_cells (provider_name, plmn, mcc, mnc, band, pci, nb_earfcn, nbsc, lac, cell_id, rsrp) " &
                                 "VALUES (@providerName, @plmn, @mcc, @mnc, @band, @pci, @nbEarfcn, @nbsc, @lac, @cellId, @rsrp)"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@providerName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@mcc", mcc)
                command.Parameters.AddWithValue("@mnc", mnc)
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@pci", pci)
                command.Parameters.AddWithValue("@nbEarfcn", nbEarfcn)
                command.Parameters.AddWithValue("@nbsc", nbsc)
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
                                     band As String, arfcn As Integer, lac As Integer, nbCell As Integer,
                                     cellId As Long, bsic As Byte)
        If DataGridView1.InvokeRequired Then
            DataGridView1.Invoke(Sub() UpdateGsmDataGridView(providerName, plmn, mcc, mnc, band, arfcn, lac, nbCell, cellId, bsic))
        Else
            Dim rowIndex As Integer = -1

            For i As Integer = 0 To DataGridView1.Rows.Count - 1
                If DataGridView1.Rows(i).Cells("Column10").Value.ToString() = cellId.ToString() Then
                    rowIndex = i
                    Exit For
                End If
            Next

            If rowIndex = -1 Then
                rowIndex = DataGridView1.Rows.Add()
            End If

            DataGridView1.Rows(rowIndex).Cells("Column5").Value = providerName
            DataGridView1.Rows(rowIndex).Cells("Column6").Value = plmn
            DataGridView1.Rows(rowIndex).Cells("Column14").Value = mcc
            DataGridView1.Rows(rowIndex).Cells("Column15").Value = mnc
            DataGridView1.Rows(rowIndex).Cells("Column7").Value = band
            DataGridView1.Rows(rowIndex).Cells("Column9").Value = arfcn
            DataGridView1.Rows(rowIndex).Cells("Column12").Value = lac
            DataGridView1.Rows(rowIndex).Cells("Column13").Value = nbCell
            DataGridView1.Rows(rowIndex).Cells("Column10").Value = cellId
            DataGridView1.Rows(rowIndex).Cells("Column11").Value = bsic
        End If
    End Sub

    Private Sub UpdateLteDataGridView(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                                  band As String, pci As Integer, nbEarfcn As Integer, nbsc As Integer,
                                  lac As Integer, cellId As Long, rsrp As Double)

        If DataGridView3.InvokeRequired Then
            DataGridView3.Invoke(Sub() UpdateLteDataGridView(providerName, plmn, mcc, mnc, band, pci, nbEarfcn, nbsc, lac, cellId, rsrp))
        Else
            Dim rowIndex As Integer = -1

            ' Search existing row by CellId
            For i As Integer = 0 To DataGridView3.Rows.Count - 1
                If DataGridView3.Rows(i).Cells("Column10").Value IsNot Nothing AndAlso
               DataGridView3.Rows(i).Cells("Column10").Value.ToString() = cellId.ToString() Then
                    rowIndex = i
                    Exit For
                End If
            Next

            ' Add new row if not found
            If rowIndex = -1 Then
                rowIndex = DataGridView3.Rows.Add()
            End If

            ' Update LTE row values
            DataGridView3.Rows(rowIndex).Cells("Column5").Value = providerName
            DataGridView3.Rows(rowIndex).Cells("Column6").Value = plmn
            DataGridView3.Rows(rowIndex).Cells("Column14").Value = mcc
            DataGridView3.Rows(rowIndex).Cells("Column15").Value = mnc
            DataGridView3.Rows(rowIndex).Cells("Column7").Value = band
            DataGridView3.Rows(rowIndex).Cells("Column8").Value = pci
            DataGridView3.Rows(rowIndex).Cells("Column9").Value = nbEarfcn
            DataGridView3.Rows(rowIndex).Cells("Column11").Value = nbsc
            DataGridView3.Rows(rowIndex).Cells("Column12").Value = lac
            DataGridView3.Rows(rowIndex).Cells("Column10").Value = cellId
            DataGridView3.Rows(rowIndex).Cells("Column13").Value = rsrp
        End If
    End Sub

    Private Sub UpdateWcdmaDataGridView(providerName As String, plmn As String, mcc As Integer, mnc As Integer,
                                    band As String, psc As Integer, earfcn As Integer, nbsc As Integer,
                                    lac As Integer, cellId As Long, rscp As Double)

        If DataGridView2.InvokeRequired Then
            DataGridView2.Invoke(Sub() UpdateWcdmaDataGridView(providerName, plmn, mcc, mnc, band, psc, earfcn, nbsc, lac, cellId, rscp))
        Else
            Dim rowIndex As Integer = -1

            ' Search existing row by CellId
            For i As Integer = 0 To DataGridView2.Rows.Count - 1
                If DataGridView2.Rows(i).Cells("Column10").Value IsNot Nothing AndAlso
               DataGridView2.Rows(i).Cells("Column10").Value.ToString() = cellId.ToString() Then
                    rowIndex = i
                    Exit For
                End If
            Next

            ' Add new row if not found
            If rowIndex = -1 Then
                rowIndex = DataGridView2.Rows.Add()
            End If

            ' Update WCDMA row values
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
    End Sub


    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        StopChannelAnalyzer()
    End Sub

    Private Sub TabPage3_Enter(sender As Object, e As EventArgs) Handles TabPage3.Enter
        LoadScanResults()
        LoadChartData()
    End Sub

    Private Sub Button34_Click(sender As Object, e As EventArgs) Handles Button34.Click
        DataGridView4.DataSource = Nothing
        DataGridView4.Rows.Clear()
    End Sub

    Private Sub LoadScanResults()
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT result_no, date_event, location_name, source, provider_name, mcc, mnc, imsi, imei, tmsi, guti, count, signal_level, time_advance, phone_model, event, longitude, latitude FROM scan_results"

                Using cmd As New SqlCommand(query, conn)
                    Using adapter As New SqlDataAdapter(cmd)
                        Dim dt As New DataTable()
                        adapter.Fill(dt)

                        'Bind to DataGridView
                        DataGridView4.DataSource = dt
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading scan results: " & ex.Message)
        End Try
    End Sub

    Private Sub LoadChartData()
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT provider_name, COUNT(*) as scan_count FROM scan_results GROUP BY provider_name"

                Using cmd As New SqlCommand(query, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        Chart1.Series("Series1").Points.Clear()

                        While reader.Read()
                            Dim providerName As String = reader("provider_name").ToString()
                            Dim scanCount As Integer = Convert.ToInt32(reader("scan_count"))

                            Chart1.Series("Series1").Points.AddXY(providerName, scanCount)
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading chart data: " & ex.Message)
        End Try
    End Sub

    Private Sub LoadWhitelistData()
        Try
            Using connection As New SqlConnection(connectionString)
                Dim query As String = "SELECT imsi FROM whitelist"
                Dim adapter As New SqlDataAdapter(query, connection)
                Dim table As New DataTable()
                adapter.Fill(table)

                DataGridView10.DataSource = table
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading whitelist: " & ex.Message)
        End Try
    End Sub


    Private Sub LoadBaseStationData1()
        Try
            ' Load data for all base station channels
            LoadBaseStationChannel(1, TextBox4, TextBox5, TextBox6, TextBox7, TextBox9, TextBox8, TextBox40, ComboBox12)
            LoadBaseStationChannel(2, TextBox15, TextBox14, TextBox12, TextBox13, TextBox10, TextBox11, TextBox41, ComboBox13)
            LoadBaseStationChannel(3, TextBox21, TextBox20, TextBox18, TextBox19, TextBox16, TextBox17, TextBox42, ComboBox14)
            LoadBaseStationChannel(4, TextBox27, TextBox26, TextBox24, TextBox25, TextBox22, TextBox23, TextBox43, ComboBox15)
            LoadBaseStationChannel(5, TextBox33, TextBox32, TextBox30, TextBox31, TextBox28, TextBox29, TextBox44, ComboBox16)
            LoadBaseStationChannel(6, TextBox39, TextBox38, TextBox36, TextBox37, TextBox34, TextBox35, TextBox45, ComboBox17)
            LoadBaseStationChannel(7, TextBox52, TextBox51, TextBox49, TextBox50, TextBox47, TextBox48, TextBox46, ComboBox18)
            LoadBaseStationChannel(8, TextBox59, TextBox58, TextBox56, TextBox57, TextBox54, TextBox55, TextBox53, ComboBox19)

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

            LoadBaseStationChannel(11, TextBox65, TextBox66, TextBox63, TextBox64, TextBox61, TextBox62, TextBox60, ComboBox21)
            LoadBaseStationChannel(12, TextBox72, TextBox73, TextBox70, TextBox71, TextBox68, TextBox69, TextBox67, ComboBox22)
            LoadBaseStationChannel(13, TextBox79, TextBox80, TextBox77, TextBox78, TextBox75, TextBox76, TextBox74, ComboBox23)
            LoadBaseStationChannel(14, TextBox86, TextBox87, TextBox84, TextBox85, TextBox82, TextBox83, TextBox81, ComboBox24)

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
                        ' Populate text fields
                        txtMCC.Text = If(reader("mcc") IsNot DBNull.Value, reader("mcc").ToString(), "")
                        txtMNC.Text = If(reader("mnc") IsNot DBNull.Value, reader("mnc").ToString(), "")
                        txtCID.Text = If(reader("cid") IsNot DBNull.Value, reader("cid").ToString(), "")
                        txtLAC.Text = If(reader("lac") IsNot DBNull.Value, reader("lac").ToString(), "")
                        txtCount.Text = If(reader("count") IsNot DBNull.Value, reader("count").ToString(), "")
                        If txtEarfcn IsNot Nothing AndAlso reader("earfcn") IsNot DBNull.Value Then
                            txtEarfcn.Text = reader("earfcn").ToString()
                        End If

                        ' Set technology type
                        If reader("is_gsm") IsNot DBNull.Value AndAlso CBool(reader("is_gsm")) Then
                            txtTechnology.Text = "GSM"
                        ElseIf reader("is_lte") IsNot DBNull.Value AndAlso CBool(reader("is_lte")) Then
                            txtTechnology.Text = "LTE"
                        ElseIf reader("is_wcdma") IsNot DBNull.Value AndAlso CBool(reader("is_wcdma")) Then
                            txtTechnology.Text = "WCDMA"
                        Else
                            txtTechnology.Text = "Unknown"
                        End If

                        ' Set band in combobox if available
                        If reader("band") IsNot DBNull.Value Then
                            cmbBand.SelectedItem = reader("band").ToString()
                        End If
                    Else
                        ' Clear fields if no data found
                        ClearTextBoxes(txtMCC, txtMNC, txtCID, txtLAC, txtCount, txtEarfcn, txtTechnology)
                    End If
                End Using
            End Using
        End Using
    End Sub

    Private Sub LoadChannels9And10()
        Using connection As New SqlConnection(connectionString)
            connection.Open()

            ' Get data for channel 9
            Dim query9 As String = "SELECT earfcn FROM base_stations WHERE channel_number = 9"
            Using command9 As New SqlCommand(query9, connection)
                Dim earfcn9 As Object = command9.ExecuteScalar()
                If earfcn9 IsNot Nothing AndAlso earfcn9 IsNot DBNull.Value Then
                    TextBox90.Text = earfcn9.ToString()
                Else
                    TextBox90.Text = ""
                End If
            End Using

            ' Get data for channel 10
            Dim query10 As String = "SELECT earfcn FROM base_stations WHERE channel_number = 10"
            Using command10 As New SqlCommand(query10, connection)
                Dim earfcn10 As Object = command10.ExecuteScalar()
                If earfcn10 IsNot Nothing AndAlso earfcn10 IsNot DBNull.Value Then
                    TextBox95.Text = earfcn10.ToString()
                Else
                    TextBox95.Text = ""
                End If
            End Using

            ' Load other common data for channel 9 (using channel 9 as reference)
            LoadBaseStationChannel(9, TextBox94, TextBox93, TextBox91, TextBox92, TextBox89, Nothing, TextBox88, ComboBox20)
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

            ' Enable button only if valid AND has changes
            button.Enabled = isValid AndAlso hasChanges

            Console.WriteLine(isValid AndAlso hasChanges)

            ' Update button text based on state
            If hasChanges Then
                If channel = 9 Then
                    button.Text = $"Save changes to CH9 and CH10"
                Else
                    button.Text = $"Save changes to CH{channel}"
                End If
                button.BackColor = Color.LightGreen ' Visual indicator
            Else
                If channel = 9 Then
                    button.Text = $"Stored to CH9 and CH10"
                Else
                    button.Text = $"Stored to CH{channel}"
                End If
                button.BackColor = SystemColors.Control ' Default color
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

    End Sub

    Private Sub Chart1_Click(sender As Object, e As EventArgs) Handles Chart1.Click

    End Sub

    Private Sub DataGridView1_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView1.CellContentClick

    End Sub

    Private Sub DataGridView4_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView4.CellContentClick

    End Sub

    Private Sub Button75_Click(sender As Object, e As EventArgs) Handles Button75.Click
        Formblacklist.Show()
    End Sub

    Private Sub Button78_Click(sender As Object, e As EventArgs) Handles Button78.Click
        Form3.Show()
    End Sub

    Private Sub Button73_Click(sender As Object, e As EventArgs) Handles Button73.Click
        Form4.Show()
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click

    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click

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

        ' Map the columns if needed (optional, since DataPropertyName should handle it)
        DataGridView1.Columns("ColumnIv").DataPropertyName = "gsm_id"
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
    End Sub

    Private Sub LoadWCDMAData(dataTable As DataTable)
        DataGridView2.DataSource = Nothing
        DataGridView2.AutoGenerateColumns = False
        DataGridView2.DataSource = dataTable

        ' Map the columns
        DataGridView2.Columns("ColumnIv2").DataPropertyName = "wcdma_id"
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
    End Sub

    Private Sub LoadLTEData(dataTable As DataTable)
        DataGridView3.DataSource = Nothing
        DataGridView3.AutoGenerateColumns = False
        DataGridView3.DataSource = dataTable

        ' Map the columns
        DataGridView3.Columns("ColumnIv3").DataPropertyName = "lte_id"
        DataGridView3.Columns("Column28").DataPropertyName = "provider_name"
        DataGridView3.Columns("Column29").DataPropertyName = "plmn"
        DataGridView3.Columns("Column30").DataPropertyName = "mcc"
        DataGridView3.Columns("Column31").DataPropertyName = "mnc"
        DataGridView3.Columns("Column32").DataPropertyName = "band"
        DataGridView3.Columns("Column37").DataPropertyName = "pci"
        DataGridView3.Columns("Column38").DataPropertyName = "nb_earfcn"
        DataGridView3.Columns("Column34").DataPropertyName = "rat"
        DataGridView3.Columns("Column35").DataPropertyName = "lac"
        DataGridView3.Columns("Column33").DataPropertyName = "cell_id" ' Note: This might need adjustment
        DataGridView3.Columns("Column36").DataPropertyName = "rsrp"
    End Sub

    Public Sub LoadBaseStationData()
        Try
            Dim baseStationHelper As New BaseStationHelper()

            For channel As Integer = 1 To 14
                Dim dataTable As DataTable = baseStationHelper.GetBaseStationByChannel(channel)
                LoadChannelData(channel, dataTable)
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

    Private Sub PopulateChannel1(row As DataRow)
        ' Store original values
        StoreOriginalValue("CH1_ComboBox", ComboBox12.Text)
        StoreOriginalValue("CH1_TextBox1", TextBox1.Text)
        StoreOriginalValue("CH1_TextBox2", TextBox2.Text)
        StoreOriginalValue("CH1_TextBox3", TextBox3.Text)
        StoreOriginalValue("CH1_TextBox97", TextBox97.Text)

        ' CH1 900MHz
        TextBox1.Text = GetSafeString(row("mcc")) ' MCC
        TextBox2.Text = GetSafeString(row("mnc")) ' MNC
        TextBox3.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox97.Text = GetSafeString(row("bsic")) ' BSIC

        ' Set technology combo box
        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox12.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox12.Text = "LTE - FDD"
        End If

        ' Set initial button state (no changes, disabled)
        buttonStates(1) = False
        UpdateButtonState(1, True)
    End Sub

    Private Sub PopulateChannel2(row As DataRow)
        StoreOriginalValue("CH2_ComboBox", ComboBox13.Text)
        StoreOriginalValue("CH2_TextBox101", TextBox101.Text)
        StoreOriginalValue("CH2_TextBox100", TextBox100.Text)
        StoreOriginalValue("CH2_TextBox99", TextBox99.Text)
        StoreOriginalValue("CH2_TextBox98", TextBox98.Text)

        ' CH2 900MHz
        TextBox101.Text = GetSafeString(row("mcc")) ' MCC
        TextBox100.Text = GetSafeString(row("mnc")) ' MNC
        TextBox99.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox98.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox13.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox13.Text = "LTE - FDD"
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

        ' CH3 1800MHz
        TextBox105.Text = GetSafeString(row("mcc")) ' MCC
        TextBox104.Text = GetSafeString(row("mnc")) ' MNC
        TextBox103.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox102.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox14.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox14.Text = "LTE - FDD"
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

        ' CH4 1800MHz
        TextBox109.Text = GetSafeString(row("mcc")) ' MCC
        TextBox108.Text = GetSafeString(row("mnc")) ' MNC
        TextBox107.Text = GetSafeString(row("earfcn")) ' EARFCN
        TextBox106.Text = GetSafeString(row("bsic")) ' BSIC

        If Convert.ToBoolean(row("is_gsm")) Then
            ComboBox15.Text = "GSM"
        ElseIf Convert.ToBoolean(row("is_lte")) Then
            ComboBox15.Text = "LTE - FDD"
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

    ' Add these event handlers to your form load
    Private Sub SetupDataGridViewEvents()
        AddHandler DataGridView1.CellDoubleClick, AddressOf GSM_CellDoubleClick
        AddHandler DataGridView2.CellDoubleClick, AddressOf WCDMA_CellDoubleClick
        AddHandler DataGridView3.CellDoubleClick, AddressOf LTE_CellDoubleClick
    End Sub

    ' GSM double-click handler
    Private Sub GSM_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 AndAlso e.RowIndex < DataGridView1.Rows.Count - 1 Then
            Dim gsmId As Integer = Convert.ToInt32(DataGridView1.Rows(e.RowIndex).Cells("ColumnIv").Value)
            Console.WriteLine("Gsm cell id " & gsmId)
            OpenGSMEditForm(gsmId)
        End If
    End Sub

    ' WCDMA double-click handler
    Private Sub WCDMA_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 AndAlso e.RowIndex < DataGridView2.Rows.Count - 1 Then
            Dim wcdmaId As Integer = Convert.ToInt32(DataGridView2.Rows(e.RowIndex).Cells("ColumnIv2").Value)
            OpenWCDMAEditForm(wcdmaId)
        End If
    End Sub

    ' LTE double-click handler
    Private Sub LTE_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 AndAlso e.RowIndex < DataGridView3.Rows.Count - 1 Then
            Dim lteId As Integer = Convert.ToInt32(DataGridView3.Rows(e.RowIndex).Cells("ColumnIv3").Value)
            OpenLTEEditForm(lteId)
        End If
    End Sub

    '================= GSM =====================
    Private Sub OpenGSMEditForm(gsmId As Integer)
        Console.WriteLine("Open gsm form called with gsm id " & gsmId)
        Dim editForm As New Form()
        editForm.Text = "Edit GSM Cell"
        editForm.Size = New Size(450, 550)

        Dim gsmData As DataRow = GetGSMDataById(gsmId)
        If gsmData Is Nothing Then Return

        ' Controls
        Dim txtProviderName As New TextBox() With {.Text = GetSafeString(gsmData("ProviderName")), .Location = New Point(150, 20), .Width = 200}
        Dim txtPlmn As New TextBox() With {.Text = GetSafeString(gsmData("plmn")), .Location = New Point(150, 50), .Width = 100}
        Dim txtMcc As New TextBox() With {.Text = gsmData("mcc").ToString(), .Location = New Point(150, 80), .Width = 80}
        Dim txtMnc As New TextBox() With {.Text = gsmData("mnc").ToString(), .Location = New Point(150, 110), .Width = 80}
        Dim txtBand As New TextBox() With {.Text = GetSafeString(gsmData("band")), .Location = New Point(150, 140), .Width = 100}
        Dim txtArfcn As New TextBox() With {.Text = gsmData("arfcn").ToString(), .Location = New Point(150, 170), .Width = 100}
        Dim txtLac As New TextBox() With {.Text = gsmData("lac").ToString(), .Location = New Point(150, 200), .Width = 100}
        Dim txtNbCell As New TextBox() With {.Text = gsmData("nb_cell").ToString(), .Location = New Point(150, 230), .Width = 100}
        Dim txtCellId As New TextBox() With {.Text = gsmData("cell_id").ToString(), .Location = New Point(150, 260), .Width = 150}
        Dim txtBsic As New TextBox() With {.Text = gsmData("bsic").ToString(), .Location = New Point(150, 290), .Width = 80}

        Dim btnSave As New Button() With {.Text = "Save Changes", .Location = New Point(150, 400), .Width = 120}
        AddHandler btnSave.Click, Sub(s, ev)
                                      SaveGSMChanges(gsmId, txtProviderName.Text, txtPlmn.Text, txtMcc.Text, txtMnc.Text, txtBand.Text, txtArfcn.Text, txtLac.Text, txtNbCell.Text, txtCellId.Text, txtBsic.Text)
                                      editForm.Close()
                                  End Sub

        editForm.Controls.AddRange({
            New Label() With {.Text = "Provider Name:", .Location = New Point(20, 23)},
            txtProviderName,
            New Label() With {.Text = "PLMN:", .Location = New Point(20, 53)},
            txtPlmn,
            New Label() With {.Text = "MCC:", .Location = New Point(20, 83)},
            txtMcc,
            New Label() With {.Text = "MNC:", .Location = New Point(20, 113)},
            txtMnc,
            New Label() With {.Text = "Band:", .Location = New Point(20, 143)},
            txtBand,
            New Label() With {.Text = "ARFCN:", .Location = New Point(20, 173)},
            txtArfcn,
            New Label() With {.Text = "LAC:", .Location = New Point(20, 203)},
            txtLac,
            New Label() With {.Text = "NB Cell:", .Location = New Point(20, 233)},
            txtNbCell,
            New Label() With {.Text = "Cell ID:", .Location = New Point(20, 263)},
            txtCellId,
            New Label() With {.Text = "BSIC:", .Location = New Point(20, 293)},
            txtBsic,
            btnSave
        })

        Console.WriteLine("Showing GSM edit form for gsm id " & gsmId)
        editForm.ShowDialog()
    End Sub

    Private Sub SaveGSMChanges(gsmId As Integer, providerName As String, plmn As String, mcc As String, mnc As String, band As String, arfcn As String, lac As String, nbCell As String, cellId As String, bsic As String)
        Using con As New SqlConnection(connectionString)
            con.Open()
            Dim query As String = "UPDATE gsm_cells SET ProviderName=@ProviderName, plmn=@plmn, mcc=@mcc, mnc=@mnc, band=@band, arfcn=@arfcn, lac=@lac, nb_cell=@nb_cell, cell_id=@cell_id, bsic=@bsic WHERE gsm_id=@gsm_id"
            Using cmd As New SqlCommand(query, con)
                cmd.Parameters.AddWithValue("@gsm_id", gsmId)
                cmd.Parameters.AddWithValue("@ProviderName", providerName)
                cmd.Parameters.AddWithValue("@plmn", plmn)
                cmd.Parameters.AddWithValue("@mcc", mcc)
                cmd.Parameters.AddWithValue("@mnc", mnc)
                cmd.Parameters.AddWithValue("@band", band)
                cmd.Parameters.AddWithValue("@arfcn", arfcn)
                cmd.Parameters.AddWithValue("@lac", lac)
                cmd.Parameters.AddWithValue("@nb_cell", nbCell)
                cmd.Parameters.AddWithValue("@cell_id", cellId)
                cmd.Parameters.AddWithValue("@bsic", bsic)
                cmd.ExecuteNonQuery()
            End Using
        End Using
        Dim dbHelper As New DatabaseHelper()
        LoadGSMData(dbHelper.GetGSMData())
    End Sub

    '================= WCDMA =====================
    Private Sub OpenWCDMAEditForm(wcdmaId As Integer)
        Console.WriteLine("Showing WCDMA edit form for id " & wcdmaId)
        Dim editForm As New Form()
        editForm.Text = "Edit WCDMA Cell"
        editForm.Size = New Size(500, 550)

        Dim wcdmaData As DataRow = GetWCDMADataById(wcdmaId)
        If wcdmaData Is Nothing Then Return

        Dim y As Integer = 20
        Dim labelX As Integer = 20
        Dim textX As Integer = 150
        Dim spacing As Integer = 30

        ' Helper function: label + textbox
        Dim controls As New List(Of Control)
        Dim addField = Sub(caption As String, value As String, width As Integer)
                           Dim lbl As New Label() With {.Text = caption, .Location = New Point(labelX, y + 3), .AutoSize = True}
                           Dim txt As New TextBox() With {.Text = value, .Location = New Point(textX, y), .Width = width}
                           controls.Add(lbl)
                           controls.Add(txt)
                           y += spacing
                       End Sub

        ' Add fields (manually pass width)
        addField("Provider Name", GetSafeString(wcdmaData("provider_name")), 200)
        addField("PLMN", GetSafeString(wcdmaData("plmn")), 100)
        addField("MCC", wcdmaData("mcc").ToString(), 80)
        addField("MNC", wcdmaData("mnc").ToString(), 80)
        addField("Band", GetSafeString(wcdmaData("band")), 100)
        addField("PSC", wcdmaData("psc").ToString(), 100)
        addField("EARFCN", wcdmaData("earfcn").ToString(), 100)
        addField("NBSC", wcdmaData("nbsc").ToString(), 100)
        addField("LAC", wcdmaData("lac").ToString(), 100)
        addField("Cell ID", wcdmaData("cell_id").ToString(), 150)
        addField("RSCP", wcdmaData("rscp").ToString(), 100)

        ' Extract the textboxes
        Dim txtProviderName As TextBox = CType(controls(1), TextBox)
        Dim txtPlmn As TextBox = CType(controls(3), TextBox)
        Dim txtMcc As TextBox = CType(controls(5), TextBox)
        Dim txtMnc As TextBox = CType(controls(7), TextBox)
        Dim txtBand As TextBox = CType(controls(9), TextBox)
        Dim txtPsc As TextBox = CType(controls(11), TextBox)
        Dim txtEarfcn As TextBox = CType(controls(13), TextBox)
        Dim txtNbsc As TextBox = CType(controls(15), TextBox)
        Dim txtLac As TextBox = CType(controls(17), TextBox)
        Dim txtCellId As TextBox = CType(controls(19), TextBox)
        Dim txtRscp As TextBox = CType(controls(21), TextBox)

        ' Save button
        Dim btnSave As New Button() With {.Text = "Save Changes", .Location = New Point(150, y + 20), .Width = 120}
        AddHandler btnSave.Click, Sub(s, ev)
                                      SaveWCDMAChanges(wcdmaId,
                                                   txtProviderName.Text,
                                                   txtPlmn.Text,
                                                   txtMcc.Text,
                                                   txtMnc.Text,
                                                   txtBand.Text,
                                                   txtPsc.Text,
                                                   txtEarfcn.Text,
                                                   txtNbsc.Text,
                                                   txtLac.Text,
                                                   txtCellId.Text,
                                                   txtRscp.Text)
                                      editForm.Close()
                                  End Sub

        ' Add controls to form
        editForm.Controls.AddRange(controls.ToArray())
        editForm.Controls.Add(btnSave)

        Console.WriteLine("Opening dialog for WCDMA edit form")
        editForm.ShowDialog()
    End Sub



    Private Sub SaveWCDMAChanges(wcdmaId As Integer, providerName As String, plmn As String, mcc As String, mnc As String, band As String, psc As String, earfcn As String, nbsc As String, lac As String, cellId As String, rscp As String)
        Using con As New SqlConnection(connectionString)
            con.Open()
            Dim query As String = "UPDATE wcdma_cells SET provider_name=@provider_name, plmn=@plmn, mcc=@mcc, mnc=@mnc, band=@band, psc=@psc, earfcn=@earfcn, nbsc=@nbsc, lac=@lac, cell_id=@cell_id, rscp=@rscp WHERE wcdma_id=@wcdma_id"
            Using cmd As New SqlCommand(query, con)
                cmd.Parameters.AddWithValue("@wcdma_id", wcdmaId)
                cmd.Parameters.AddWithValue("@provider_name", providerName)
                cmd.Parameters.AddWithValue("@plmn", plmn)
                cmd.Parameters.AddWithValue("@mcc", mcc)
                cmd.Parameters.AddWithValue("@mnc", mnc)
                cmd.Parameters.AddWithValue("@band", band)
                cmd.Parameters.AddWithValue("@psc", psc)
                cmd.Parameters.AddWithValue("@earfcn", earfcn)
                cmd.Parameters.AddWithValue("@nbsc", nbsc)
                cmd.Parameters.AddWithValue("@lac", lac)
                cmd.Parameters.AddWithValue("@cell_id", cellId)
                cmd.Parameters.AddWithValue("@rscp", rscp)
                cmd.ExecuteNonQuery()
            End Using
        End Using
        Dim dbHelper As New DatabaseHelper()
        LoadWCDMAData(dbHelper.GetWCDMAData())
    End Sub

    Private Sub OpenLTEEditForm(lteId As Integer)
        Console.WriteLine("Showing LTE edit form for id " & lteId)
        Dim editForm As New Form()
        editForm.Text = "Edit LTE Cell"
        editForm.Size = New Size(500, 650)

        ' Retrieve LTE data
        Dim lteData As DataRow = GetLTEDataById(lteId)
        If lteData Is Nothing Then Return

        Dim y As Integer = 20
        Dim labelX As Integer = 20
        Dim textX As Integer = 150
        Dim spacing As Integer = 30

        ' Helper function to add a label + textbox
        Dim controls As New List(Of Control)
        Dim addField = Function(caption As String, value As String, width As Integer) As TextBox
                           Dim lbl As New Label() With {.Text = caption, .Location = New Point(labelX, y + 3), .AutoSize = True}
                           Dim txt As New TextBox() With {.Text = value, .Location = New Point(textX, y), .Width = width}
                           controls.Add(lbl)
                           controls.Add(txt)
                           y += spacing
                           Return txt
                       End Function

        ' Add fields
        Dim txtProviderName As TextBox = addField("Provider Name", GetSafeString(lteData("provider_name")), 200)
        Dim txtPlmn As TextBox = addField("PLMN", GetSafeString(lteData("plmn")), 100)
        Dim txtMcc As TextBox = addField("MCC", lteData("mcc").ToString(), 80)
        Dim txtMnc As TextBox = addField("MNC", lteData("mnc").ToString(), 80)
        Dim txtBand As TextBox = addField("Band", GetSafeString(lteData("band")), 100)
        Dim txtPci As TextBox = addField("PCI", lteData("pci").ToString(), 100)
        Dim txtNbEarfcn As TextBox = addField("NB EARFCN", lteData("nb_earfcn").ToString(), 100)
        Dim txtNbsc As TextBox = addField("NBSC", lteData("nbsc").ToString(), 100)
        Dim txtRat As TextBox = addField("RAT", GetSafeString(lteData("rat")), 80)
        Dim txtLac As TextBox = addField("LAC", lteData("lac").ToString(), 100)
        Dim txtCellId As TextBox = addField("Cell ID", lteData("cell_id").ToString(), 150)
        Dim txtRsrp As TextBox = addField("RSRP", lteData("rsrp").ToString(), 100)

        ' Save button
        Dim btnSave As New Button() With {.Text = "Save Changes", .Location = New Point(150, y + 20), .Width = 120}
        AddHandler btnSave.Click, Sub(s, ev)
                                      SaveLTEChanges(lteId,
                                                 txtProviderName.Text,
                                                 txtPlmn.Text,
                                                 txtMcc.Text,
                                                 txtMnc.Text,
                                                 txtBand.Text,
                                                 txtPci.Text,
                                                 txtNbEarfcn.Text,
                                                 txtNbsc.Text,
                                                 txtRat.Text,
                                                 txtLac.Text,
                                                 txtCellId.Text,
                                                 txtRsrp.Text)
                                      editForm.Close()
                                  End Sub

        ' Add all controls
        editForm.Controls.AddRange(controls.ToArray())
        editForm.Controls.Add(btnSave)

        Console.WriteLine("Opening dialog for LTE edit form")
        editForm.ShowDialog()
    End Sub

    Private Sub SaveLTEChanges(lteId As Integer,
                           providerName As String,
                           plmn As String,
                           mcc As String,
                           mnc As String,
                           band As String,
                           pci As String,
                           nbEarfcn As String,
                           nbsc As String,
                           rat As String,
                           lac As String,
                           cellId As String,
                           rsrp As String)

        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()
                Dim query As String = "UPDATE lte_cells 
                                   SET provider_name = @providerName,
                                       plmn = @plmn,
                                       mcc = @mcc,
                                       mnc = @mnc,
                                       band = @band,
                                       pci = @pci,
                                       nb_earfcn = @nbEarfcn,
                                       nbsc = @nbsc,
                                       rat = @rat,
                                       lac = @lac,
                                       cell_id = @cellId,
                                       rsrp = @rsrp
                                   WHERE lte_id = @lteId"

                Using command As New SqlCommand(query, connection)
                    command.Parameters.AddWithValue("@providerName", providerName)
                    command.Parameters.AddWithValue("@plmn", plmn)
                    command.Parameters.AddWithValue("@mcc", Integer.Parse(mcc))
                    command.Parameters.AddWithValue("@mnc", Integer.Parse(mnc))
                    command.Parameters.AddWithValue("@band", band)
                    command.Parameters.AddWithValue("@pci", Integer.Parse(pci))
                    command.Parameters.AddWithValue("@nbEarfcn", Integer.Parse(nbEarfcn))
                    command.Parameters.AddWithValue("@nbsc", Integer.Parse(nbsc))
                    command.Parameters.AddWithValue("@rat", rat)
                    command.Parameters.AddWithValue("@lac", Integer.Parse(lac))
                    command.Parameters.AddWithValue("@cellId", Long.Parse(cellId)) ' BIGINT
                    command.Parameters.AddWithValue("@rsrp", Double.Parse(rsrp))
                    command.Parameters.AddWithValue("@lteId", lteId)

                    command.ExecuteNonQuery()
                End Using
            End Using
            Dim dbHelper As New DatabaseHelper()
            LoadLTEData(dbHelper.GetLTEData())
            MessageBox.Show("LTE cell updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show("Error updating LTE cell: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


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


    Public Sub SaveGSMChanges(gsmId As Integer, providerName As String, plmn As String, mcc As String, mnc As String, band As String, arfcn As String, lac As String, cellId As String, bsic As String)
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "UPDATE gsm_cells SET ProviderName = @ProviderName, plmn = @plmn, mcc = @mcc, " &
                                 "mnc = @mnc, band = @band, arfcn = @arfcn, lac = @lac, cell_id = @cell_id, " &
                                 "bsic = @bsic, Timestamp = SYSUTCDATETIME() WHERE gsm_id = @gsmId"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@gsmId", gsmId)
                command.Parameters.AddWithValue("@ProviderName", providerName)
                command.Parameters.AddWithValue("@plmn", plmn)
                command.Parameters.AddWithValue("@mcc", ConvertToInt(mcc))
                command.Parameters.AddWithValue("@mnc", ConvertToInt(mnc))
                command.Parameters.AddWithValue("@band", band)
                command.Parameters.AddWithValue("@arfcn", ConvertToInt(arfcn))
                command.Parameters.AddWithValue("@lac", ConvertToInt(lac))
                command.Parameters.AddWithValue("@cell_id", ConvertToLong(cellId))
                command.Parameters.AddWithValue("@bsic", ConvertToByte(bsic))

                command.ExecuteNonQuery()
            End Using
        End Using

        MessageBox.Show("GSM cell updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Dim dbHelper As New DatabaseHelper()
        LoadGSMData(dbHelper.GetGSMData()) ' Refresh the grid
    End Sub

    Private Sub AddInputConstraints()
        ' MCC fields (3 digits) - Complete all MCC fields
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

    ' Modify the SaveBaseStation method to update button state after save
    Private Sub SaveBaseStation(channel As Integer, technology As String, mccText As String, mncText As String, earfcnText As String, Optional bsicText As String = Nothing, Optional earfcn2Text As String = Nothing)
        Try
            Dim mcc = ParseInteger(mccText)
            Dim mnc = ParseInteger(mncText)
            Dim earfcn = ParseInteger(earfcnText)
            Dim bsic = ParseInteger(bsicText)
            Dim earfcn2 = ParseInteger(earfcn2Text)

            ' Determine technology type
            Dim isGsm = technology.Contains("GSM")
            Dim isLte = technology.Contains("LTE")
            Dim isWcdma = technology.Contains("WCDMA")

            ' Check if record already exists for this channel
            Dim existingRecordId As Integer? = GetBaseStationIdByChannel(channel)

            If existingRecordId.HasValue Then
                ' UPDATE existing record
                UpdateBaseStation(existingRecordId.Value, channel, technology, mcc, mnc, earfcn, bsic, earfcn2, isGsm, isLte, isWcdma)
            Else
                ' INSERT new record
                InsertBaseStation(channel, technology, mcc, mnc, earfcn, bsic, earfcn2, isGsm, isLte, isWcdma)
            End If

            MessageBox.Show($"Base station CH{channel} saved successfully!")

            ' Reset button state after successful save
            buttonStates(channel) = False ' No changes
            UpdateButtonState(channel, True) ' Update button text and disable it

            ' Update original values
            StoreOriginalValue($"CH{channel}_ComboBox", technology)
            StoreOriginalValue($"CH{channel}_TextBox1", mccText)
            StoreOriginalValue($"CH{channel}_TextBox2", mncText)
            StoreOriginalValue($"CH{channel}_TextBox3", earfcnText)

            ' Store BSIC if available (for GSM channels)
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

            ' Store EARFCN2 if available (for channel 9)
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

            ' --- First update the original channel ---
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

            ' --- If channel = 9 and earfcn2 is available, also update channel 10 ---
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
End Class
