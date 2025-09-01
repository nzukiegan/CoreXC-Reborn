Imports System.Data.SqlClient

Public Class BaseStationHelper
    Private connectionString As String = $"Server=localhost;Database=CoreXCDb;Trusted_Connection=True;"

    ' Method to get base station data by channel number
    Public Function GetBaseStationByChannel(channelNumber As Integer) As DataTable
        Dim dt As New DataTable()

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT * FROM base_stations WHERE channel_number = @ChannelNumber ORDER BY last_updated DESC"

            Using command As New SqlCommand(query, connection)
                command.Parameters.AddWithValue("@ChannelNumber", channelNumber)

                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(dt)
                End Using
            End Using
        End Using

        Return dt
    End Function

    ' Method to get all base stations
    Public Function GetAllBaseStations() As DataTable
        Dim dt As New DataTable()

        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Dim query As String = "SELECT * FROM base_stations ORDER BY channel_number, last_updated DESC"

            Using command As New SqlCommand(query, connection)
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(dt)
                End Using
            End Using
        End Using

        Return dt
    End Function
End Class