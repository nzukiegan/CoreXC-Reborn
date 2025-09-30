Imports System.Device.Location
Imports System.Net
Imports System.Text.Json
Imports System.Globalization

Public Class LocationHelper
    Public Shared Function GetCurrentLocationFromIp() As (Latitude As Double, Longitude As Double)
        Dim ip As String = "192.168.1.99"
        Try
            Using client As New WebClient()
                client.Headers(HttpRequestHeader.UserAgent) = "MyApp/1.0"
                Dim url As String = $"http://ip-api.com/json/{ip}"
                Dim response As String = client.DownloadString(url)

                Using doc As JsonDocument = JsonDocument.Parse(response)
                    Dim root = doc.RootElement
                    If root.TryGetProperty("lat", Nothing) AndAlso root.TryGetProperty("lon", Nothing) Then
                        Dim lat = root.GetProperty("lat").GetDouble()
                        Dim lon = root.GetProperty("lon").GetDouble()
                        Return (lat, lon)
                    End If
                End Using
            End Using
        Catch

        End Try

        Return (0, 0)
    End Function

End Class
