Imports System.Device.Location
Imports System.Net
Imports System.Text.Json

Public Class LocationHelper
    Public Shared Function GetCurrentLocation() As (Latitude As Double, Longitude As Double)
        Try
            Using watcher As New GeoCoordinateWatcher(GeoPositionAccuracy.Default)
                If watcher.TryStart(False, TimeSpan.FromSeconds(5)) Then
                    Dim coord = watcher.Position.Location
                    If coord IsNot Nothing AndAlso Not coord.IsUnknown Then
                        Return (coord.Latitude, coord.Longitude)
                    End If
                End If
            End Using
        Catch ex As Exception
        End Try

        Try
            Using client As New WebClient()
                client.Headers(HttpRequestHeader.UserAgent) = "MyApp/1.0"
                Dim response As String = client.DownloadString("http://ip-api.com/json/")
                Using doc As JsonDocument = JsonDocument.Parse(response)
                    Dim root = doc.RootElement
                    Dim latEl As JsonElement
                    Dim lonEl As JsonElement
                    If root.TryGetProperty("lat", latEl) AndAlso root.TryGetProperty("lon", lonEl) Then
                        Return (latEl.GetDouble(), lonEl.GetDouble())
                    End If
                End Using
            End Using
        Catch ex As Exception
        End Try

        Return (0, 0)
    End Function
End Class
