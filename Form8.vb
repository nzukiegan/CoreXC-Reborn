Imports GMap.NET
Imports GMap.NET.MapProviders
Imports GMap.NET.WindowsForms
Imports GMap.NET.WindowsForms.Markers

Public Class Form8
    Private _latitude As Double
    Private _longitude As Double
    Private gmap As GMapControl

    Public Sub New(lat As String, lon As String)
        InitializeComponent()
        Double.TryParse(lat, _latitude)
        Double.TryParse(lon, _longitude)
    End Sub

    Private Sub MapForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        gmap = New GMapControl()
        gmap.Dock = DockStyle.Fill
        Me.Controls.Add(gmap)

        GMaps.Instance.Mode = AccessMode.CacheOnly
        gmap.MapProvider = GMapProviders.GoogleMap
        gmap.MinZoom = 1
        gmap.MaxZoom = 20
        gmap.Zoom = 15

        gmap.Position = New PointLatLng(_latitude, _longitude)

        Dim markers = New GMapOverlay("markers")
        Dim marker As New GMarkerGoogle(
            New PointLatLng(_latitude, _longitude),
            GMarkerGoogleType.red_dot
        )
        markers.Markers.Add(marker)
        gmap.Overlays.Add(markers)
    End Sub
End Class
