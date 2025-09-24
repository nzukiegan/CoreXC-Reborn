Imports System.Net
Imports System.Net.Sockets
Imports System.Text

Public Class HeartbeatSender
    Private ReadOnly udp As UdpClient
    Private ReadOnly remoteEndPoint As IPEndPoint
    Private cts As Threading.CancellationTokenSource

    Public Sub New(udpClient As UdpClient, add As String)
        udp = udpClient
        remoteEndPoint = New IPEndPoint(IPAddress.Parse(add), 9001)
    End Sub

    Public Sub StartHeartbeat(intervalSeconds As Integer)
        cts = New Threading.CancellationTokenSource()
        Dim token = cts.Token

        Task.Run(Async Function()
                     Dim message As Byte() = Encoding.ASCII.GetBytes("heartbeat")

                     While Not token.IsCancellationRequested
                         Try
                             Await udp.SendAsync(message, message.Length, remoteEndPoint)
                             Console.WriteLine("Heartbeat sent to " & remoteEndPoint.ToString() &
                                               " at " & DateTime.Now.ToString("HH:mm:ss"))
                         Catch ex As Exception
                             Console.WriteLine("Failed to send heartbeat: " & ex.Message)
                         End Try

                         Await Task.Delay(intervalSeconds * 1000, token)
                     End While
                 End Function, token)
    End Sub

    Public Sub StopHeartbeat()
        If cts IsNot Nothing Then
            cts.Cancel()
            cts.Dispose()
        End If
    End Sub
End Class
