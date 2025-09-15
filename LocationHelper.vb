Imports System.Device.Location
Imports System.Net
Imports System.Text.Json
Imports System.Globalization

Public Class LocationHelper
    Private Shared Function ParseJsonNumberOrString(el As JsonElement) As Double
        Try
            If el.ValueKind = JsonValueKind.Number Then
                Return el.GetDouble()
            ElseIf el.ValueKind = JsonValueKind.String Then
                Dim s = el.GetString()
                If String.IsNullOrWhiteSpace(s) Then Return Double.NaN
                s = s.Replace(","c, "."c).Trim()
                Dim d As Double
                If Double.TryParse(s, NumberStyles.Float Or NumberStyles.AllowThousands, CultureInfo.InvariantCulture, d) Then
                    Return d
                End If
            End If
        Catch
        End Try
        Return Double.NaN
    End Function

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
        Catch

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
                        Dim lat = ParseJsonNumberOrString(latEl)
                        Dim lon = ParseJsonNumberOrString(lonEl)
                        If Not Double.IsNaN(lat) AndAlso Not Double.IsNaN(lon) Then
                            Return (lat, lon)
                        End If
                    End If
                End Using
            End Using
        Catch

        End Try

        Return (0, 0)
    End Function
End Class
