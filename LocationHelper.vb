Imports System.Device.Location
Imports System.Net
Imports System.Text.Json

Public Class LocationHelper
    Public Shared Function GetCurrentLocation() As (Latitude As Double, Longitude As Double)
        Try
            Dim watcher As New GeoCoordinateWatcher(GeoPositionAccuracy.Default)
            watcher.TryStart(False, TimeSpan.FromSeconds(3))

            Dim coord = watcher.Position.Location
            If coord IsNot Nothing AndAlso Not coord.IsUnknown Then
                Return (coord.Latitude, coord.Longitude)
            End If
        Catch

        End Try

        Try
            Dim client As New WebClient()
            Dim response As String = client.DownloadString("http://ip-api.com/json/")
            Dim doc = JsonDocument.Parse(response)

            Dim lat = doc.RootElement.GetProperty("lat").GetDouble()
            Dim lon = doc.RootElement.GetProperty("lon").GetDouble()

            Return (lat, lon)
        Catch
            ' If everything fails, return 0,0
            Return (0, 0)
        End Try
    End Function
End Class
