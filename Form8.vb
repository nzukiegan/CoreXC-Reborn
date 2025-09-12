Imports GMap.NET
Imports GMap.NET.MapProviders
Imports GMap.NET.WindowsForms
Imports GMap.NET.WindowsForms.Markers
Imports GMap.NET.WindowsForms.ToolTips

Public Class Form8
    Private _latitude As Double
    Private _longitude As Double
    Private _destLat As Double
    Private _destLon As Double
    Private gmap As GMapControl

    Public Sub New(lat As String, lon As String, destLat As String, destLon As String)
        InitializeComponent()
        Double.TryParse(lat, _latitude)
        Double.TryParse(lon, _longitude)
        Double.TryParse(destLat, _destLat)
        Double.TryParse(destLon, _destLon)
    End Sub

    Private Sub MapForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        gmap = New GMapControl()
        gmap.Dock = DockStyle.Fill
        Me.Controls.Add(gmap)

        GMaps.Instance.Mode = AccessMode.ServerAndCache
        gmap.MapProvider = GMapProviders.GoogleMap
        gmap.MinZoom = 1
        gmap.MaxZoom = 20
        gmap.Zoom = 12
        gmap.Position = New PointLatLng(_latitude, _longitude)

        Dim markers = New GMapOverlay("markers")
        Dim startMarker As New GMarkerGoogle(New PointLatLng(_latitude, _longitude), GMarkerGoogleType.green_dot)
        Dim endMarker As New GMarkerGoogle(New PointLatLng(_destLat, _destLon), GMarkerGoogleType.red_dot)
        markers.Markers.Add(startMarker)
        markers.Markers.Add(endMarker)
        gmap.Overlays.Add(markers)

        Dim route As MapRoute = GMapProviders.GoogleMap.GetRoute(
            New PointLatLng(_latitude, _longitude),
            New PointLatLng(_destLat, _destLon),
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
End Class
