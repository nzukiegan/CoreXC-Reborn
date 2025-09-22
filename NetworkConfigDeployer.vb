Imports Renci.SshNet
Imports Renci.SshNet.Common
Imports System.Xml.Linq
Imports System.IO

Public Class NetworkConfigDeployer

    Public Shared Sub ApplyNetworkConfiguration(host As String,
                                                username As String,
                                                password As String,
                                                keyPath As String,
                                                rat As String,
                                                mcc As String,
                                                mnc As String,
                                                earfcn As String,
                                                arfcn As String,
                                                pci As String,
                                                band As String)

        If String.IsNullOrWhiteSpace(host) Then Throw New ArgumentException("host required")
        If String.IsNullOrWhiteSpace(username) Then Throw New ArgumentException("username required")

        Dim fileMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"gsm", "cellPara_gsm9x.xml"},
            {"lte-fdd", "cellPara_lteFdd.xml"},
            {"lte-tdd", "cellPara_lteTdd.xml"}
        }

        If Not fileMap.ContainsKey(rat) Then
            Throw New ArgumentException("rat must be one of: gsm, lte-fdd, lte-tdd")
        End If

        Dim remoteDir As String = "/root/sec/cfg/inUse/"
        Dim remoteFileName = fileMap(rat)
        Dim remotePath = remoteDir.TrimEnd("/"c) & "/" & remoteFileName
        Dim localTemp As String = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() & ".xml")

        Dim connInfo As ConnectionInfo = Nothing
        If Not String.IsNullOrWhiteSpace(keyPath) AndAlso File.Exists(keyPath) Then
            Dim pkFile As PrivateKeyFile = Nothing
            Try
                pkFile = New PrivateKeyFile(keyPath)
            Catch ex As Exception
                Throw New InvalidOperationException($"Failed to load private key: {ex.Message}", ex)
            End Try
            connInfo = New ConnectionInfo(host, 9001, username, New AuthenticationMethod() {
                                            New PrivateKeyAuthenticationMethod(username, pkFile)
                                        })
        Else
            Dim pwd As String = If(password, "")
            connInfo = New ConnectionInfo(host, 9001, username, New AuthenticationMethod() {
                                            New PasswordAuthenticationMethod(username, pwd)
                                        })
        End If

        Dim ssh As New SshClient(connInfo)
        Dim sftp As New SftpClient(connInfo)

        Try
            Try
                ssh.Connect()
                Console.WriteLine("Ssh connected success")
            Catch authEx As SshAuthenticationException
                Throw New InvalidOperationException("Authentication failed. If device is key-based, pass keyPath. If it accepts empty password, pass password = """" (empty string).", authEx)
            End Try

            sftp.Connect()

            Try
                Dim bakCmd = $"cp ""{remotePath}"" ""{remotePath}.bak"""
                Dim res = ssh.RunCommand(bakCmd)
            Catch exBak As Exception
                Console.WriteLine(exBak.StackTrace)
            End Try

            Using fs As FileStream = File.OpenWrite(localTemp)
                sftp.DownloadFile(remotePath, fs)
            End Using

            Dim doc As XDocument = XDocument.Load(localTemp)
            Select Case rat.ToLowerInvariant()
                Case "gsm"
                    ApplyGsmChanges(doc, mcc, mnc, arfcn)
                Case "lte-fdd", "lte-tdd"
                    ApplyLteChanges(doc, mcc, mnc, earfcn, pci, band)
                Case Else
                    Throw New ArgumentException("Unsupported RAT")
            End Select

            doc.Save(localTemp)

            Using fs As FileStream = File.OpenRead(localTemp)
                sftp.UploadFile(fs, remotePath, True)
            End Using

        Finally
            If sftp IsNot Nothing AndAlso sftp.IsConnected Then sftp.Disconnect()
            If ssh IsNot Nothing AndAlso ssh.IsConnected Then ssh.Disconnect()

            Try
                If File.Exists(localTemp) Then File.Delete(localTemp)
            Catch : End Try
        End Try
    End Sub


    Private Shared Sub SetElementValue(parent As XElement, name As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Return
        Dim el As XElement = parent.Element(name)
        If el Is Nothing Then
            el = New XElement(name, value)
            parent.Add(el)
        Else
            el.Value = value
        End If
    End Sub

    Private Shared Sub ApplyGsmChanges(doc As XDocument, mcc As String, mnc As String, arfcn As String)
        Dim content As XElement = doc.Root
        If content Is Nothing Then Throw New InvalidOperationException("XML missing <content> root")

        Dim cellEl As XElement = content.Element("cell")
        If cellEl Is Nothing Then cellEl = content.Descendants("cell").FirstOrDefault()

        If cellEl Is Nothing Then
            If Not String.IsNullOrWhiteSpace(mcc) Then SetElementValue(content, "mcc", mcc)
            If Not String.IsNullOrWhiteSpace(mnc) Then SetElementValue(content, "mnc", mnc)
            Return
        End If

        Dim itemEl As XElement = cellEl.Element("item")
        If itemEl Is Nothing Then itemEl = cellEl.Descendants("item").FirstOrDefault()

        If itemEl Is Nothing Then
            If Not String.IsNullOrWhiteSpace(mcc) Then SetElementValue(cellEl, "mcc", mcc)
            If Not String.IsNullOrWhiteSpace(mnc) Then SetElementValue(cellEl, "mnc", mnc)
        Else
            If Not String.IsNullOrWhiteSpace(arfcn) Then
                Dim arfcnList As XElement = itemEl.Element("arfcnList")
                If arfcnList Is Nothing Then
                    arfcnList = New XElement("arfcnList")
                    itemEl.AddFirst(arfcnList)
                End If

                Dim firstArfcn As XElement = arfcnList.Element("arfcn")
                If firstArfcn Is Nothing Then
                    firstArfcn = New XElement("arfcn", arfcn)
                    arfcnList.Add(firstArfcn)
                Else
                    firstArfcn.Value = arfcn
                End If
            End If

            If Not String.IsNullOrWhiteSpace(mcc) Then SetElementValue(itemEl, "mcc", mcc)
            If Not String.IsNullOrWhiteSpace(mnc) Then SetElementValue(itemEl, "mnc", mnc)
        End If
    End Sub

    Private Shared Sub ApplyLteChanges(doc As XDocument, mcc As String, mnc As String, earfcn As String, pci As String, band As String)
        Dim content As XElement = doc.Root
        If content Is Nothing Then Throw New InvalidOperationException("XML missing <content> root")

        If Not String.IsNullOrWhiteSpace(band) Then SetElementValue(content, "band", band)
        If Not String.IsNullOrWhiteSpace(earfcn) Then
            SetElementValue(content, "erfcn", earfcn)
            SetElementValue(content, "earfcn", earfcn)
            SetElementValue(content, "listenErfcn", earfcn)
        End If

        If Not String.IsNullOrWhiteSpace(pci) Then
            SetElementValue(content, "pci", pci)
            SetElementValue(content, "listenPci", pci)
        End If

        If Not String.IsNullOrWhiteSpace(mcc) Then SetElementValue(content, "mcc", mcc)
        If Not String.IsNullOrWhiteSpace(mnc) Then SetElementValue(content, "mnc", mnc)
    End Sub

End Class
