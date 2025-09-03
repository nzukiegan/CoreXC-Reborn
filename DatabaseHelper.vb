' Add these imports at the top of your file
Imports System.Configuration
Imports System.Data.SqlClient
Imports System.Windows.Forms.VisualStyles.VisualStyleElement
Imports Microsoft.SqlServer

' Database helper class
Public Class DatabaseHelper
    Private ReadOnly connectionString As String = $"Server=(localdb)\MSSQLLocalDB;Database=CoreXCDb1;Integrated Security=true;"

    Public Function SaveBaseStation(baseStation As BaseStation) As Boolean
        Try
            Using connection As New SqlConnection(connectionString)
                connection.Open()

                ' First check if a record already exists for this channel
                Dim checkQuery As String = "SELECT COUNT(*) FROM base_stations WHERE channel_number = @channel_number"
                Dim recordExists As Boolean = False

                Using checkCommand As New SqlCommand(checkQuery, connection)
                    checkCommand.Parameters.AddWithValue("@channel_number", baseStation.ChannelNumber)
                    recordExists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0
                End Using

                If recordExists Then
                    ' UPDATE existing record
                    Dim updateQuery As String = "UPDATE base_stations SET " &
                                         "is_gsm = @is_gsm, " &
                                         "is_lte = @is_lte, " &
                                         "is_wcdma = @is_wcdma, " &
                                         "earfcn = @earfcn, " &
                                         "mcc = @mcc, " &
                                         "bsic = @bsic, " &
                                         "mnc = @mnc, " &
                                         "cid = @cid, " &
                                         "lac = @lac, " &
                                         "band = @band, " &
                                         "last_updated = SYSUTCDATETIME() " &
                                         "WHERE channel_number = @channel_number"

                    Using updateCommand As New SqlCommand(updateQuery, connection)
                        updateCommand.Parameters.AddWithValue("@channel_number", baseStation.ChannelNumber)
                        updateCommand.Parameters.AddWithValue("@is_gsm", baseStation.IsGsm)
                        updateCommand.Parameters.AddWithValue("@is_lte", baseStation.IsLte)
                        updateCommand.Parameters.AddWithValue("@is_wcdma", baseStation.IsWcdma)
                        updateCommand.Parameters.AddWithValue("@earfcn", If(baseStation.Earfcn.HasValue, baseStation.Earfcn, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@mcc", If(baseStation.Mcc.HasValue, baseStation.Mcc, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@bsic", If(baseStation.Bsic.HasValue, baseStation.Bsic, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@mnc", If(baseStation.Mnc.HasValue, baseStation.Mnc, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@cid", If(baseStation.Cid.HasValue, baseStation.Cid, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@lac", If(baseStation.Lac.HasValue, baseStation.Lac, DBNull.Value))
                        updateCommand.Parameters.AddWithValue("@band", If(baseStation.Band.HasValue, baseStation.Band, DBNull.Value))

                        Dim result = updateCommand.ExecuteNonQuery()
                        Return result > 0
                    End Using
                Else
                    ' INSERT new record
                    Dim insertQuery As String = "INSERT INTO base_stations (channel_number, is_gsm, is_lte, is_wcdma, " &
                                         "earfcn, mcc, bsic, mnc, cid, lac, band, last_updated) " &
                                         "VALUES (@channel_number, @is_gsm, @is_lte, @is_wcdma, " &
                                         "@earfcn, @mcc, @bsic, @mnc, @cid, @lac, @band, SYSUTCDATETIME())"

                    Using insertCommand As New SqlCommand(insertQuery, connection)
                        insertCommand.Parameters.AddWithValue("@channel_number", baseStation.ChannelNumber)
                        insertCommand.Parameters.AddWithValue("@is_gsm", baseStation.IsGsm)
                        insertCommand.Parameters.AddWithValue("@is_lte", baseStation.IsLte)
                        insertCommand.Parameters.AddWithValue("@is_wcdma", baseStation.IsWcdma)
                        insertCommand.Parameters.AddWithValue("@earfcn", If(baseStation.Earfcn.HasValue, baseStation.Earfcn, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@mcc", If(baseStation.Mcc.HasValue, baseStation.Mcc, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@bsic", If(baseStation.Bsic.HasValue, baseStation.Bsic, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@mnc", If(baseStation.Mnc.HasValue, baseStation.Mnc, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@cid", If(baseStation.Cid.HasValue, baseStation.Cid, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@lac", If(baseStation.Lac.HasValue, baseStation.Lac, DBNull.Value))
                        insertCommand.Parameters.AddWithValue("@band", If(baseStation.Band.HasValue, baseStation.Band, DBNull.Value))

                        Dim result = insertCommand.ExecuteNonQuery()
                        Return result > 0
                    End Using
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Database error: {ex.Message}")
            Return False
        End Try
    End Function

    ' Method to get GSM data
    Public Function GetGSMData() As DataTable
        Dim dt As New DataTable()

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT gsm_id, ProviderName, plmn, mcc, mnc, band, rat, arfcn, lac, nb_cell, cell_id, bsic FROM gsm_cells ORDER BY Timestamp DESC"

            Using command As New SqlCommand(query, connection)
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(dt)
                End Using
            End Using
        End Using

        Return dt
    End Function

    ' Method to get WCDMA data
    Public Function GetWCDMAData() As DataTable
        Dim dt As New DataTable()

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT wcdma_id, provider_name, plmn, mcc, mnc, band, psc, earfcn, nbsc, rat, lac, cell_id, rscp FROM wcdma_cells ORDER BY Timestamp DESC"

            Using command As New SqlCommand(query, connection)
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(dt)
                End Using
            End Using
        End Using

        Return dt
    End Function

    ' Method to get LTE data
    Public Function GetLTEData() As DataTable
        Dim dt As New DataTable()

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT lte_id, provider_name, plmn, mcc, mnc, band, nb_earfcn, rat, lac, earfcn, rsrp FROM lte_cells ORDER BY Timestamp DESC"

            Using command As New SqlCommand(query, connection)
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(dt)
                End Using
            End Using
        End Using

        Return dt
    End Function
End Class

' BaseStation class
Public Class BaseStation
    Public Property ChannelNumber As Integer
    Public Property BaseStationName As String
    Public Property IsGsm As Boolean
    Public Property IsLte As Boolean
    Public Property IsWcdma As Boolean
    Public Property FrequencyMhz As Double
    Public Property Earfcn As Integer?
    Public Property Mcc As Integer?
    Public Property Bsic As Integer?
    Public Property Mnc As Integer?
    Public Property Cid As Integer?
    Public Property Count As Integer?
    Public Property Lac As Integer?
    Public Property Name As String
    Public Property Status As String
    Public Property Band As Integer?
End Class

' Band frequency mapping
Public Class BandHelper
    Public Shared Function GetFrequencyBandByChannel(channel As Integer) As Double
        Select Case channel
            Case 1, 2 : Return 900
            Case 3, 4 : Return 1800
            Case 5, 6 : Return 2100
            Case 7, 8 : Return 850
            Case 9, 10 : Return 2300
            Case 11, 12 : Return 700
            Case 13, 14 : Return 2600
            Case Else : Return 0
        End Select
    End Function

    Public Shared Function GetBandIdByChannel(channel As Integer) As Integer?
        Select Case channel
            Case 1, 2 : Return 8  ' B8
            Case 3, 4 : Return 3  ' B3
            Case 5, 6 : Return 1  ' B1
            Case 7, 8 : Return 5  ' B5
            Case 9, 10 : Return 40 ' B40
            Case 11, 12 : Return 28 ' B28
            Case 13, 14 : Return 7  ' B7
            Case Else : Return Nothing
        End Select
    End Function
End Class