Imports System
Imports System.Collections.Generic

Public Module EarfcnHelpers

    Private Structure BandParams
        Public FdlLow As Double
        Public NoffDl As Integer
        Public FulLow As Double
        Public NoffUl As Integer
        Public IsTdd As Boolean
    End Structure

    Private ReadOnly bandParams1 As New Dictionary(Of Integer, BandParams) From {
        {1, New BandParams With {.FdlLow = 2110, .NoffDl = 0, .FulLow = 1920, .NoffUl = 18000, .IsTdd = False}},
        {2, New BandParams With {.FdlLow = 1930, .NoffDl = 600, .FulLow = 1850, .NoffUl = 18600, .IsTdd = False}},
        {3, New BandParams With {.FdlLow = 1805, .NoffDl = 1200, .FulLow = 1710, .NoffUl = 19200, .IsTdd = False}},
        {4, New BandParams With {.FdlLow = 2110, .NoffDl = 1950, .FulLow = 1710, .NoffUl = 19950, .IsTdd = False}},
        {5, New BandParams With {.FdlLow = 869, .NoffDl = 2400, .FulLow = 824, .NoffUl = 20400, .IsTdd = False}},
        {7, New BandParams With {.FdlLow = 2620, .NoffDl = 2750, .FulLow = 2500, .NoffUl = 20750, .IsTdd = False}},
        {8, New BandParams With {.FdlLow = 925, .NoffDl = 3450, .FulLow = 880, .NoffUl = 21450, .IsTdd = False}},
        {20, New BandParams With {.FdlLow = 791, .NoffDl = 6150, .FulLow = 832, .NoffUl = 24150, .IsTdd = False}},
        {28, New BandParams With {.FdlLow = 758, .NoffDl = 9210, .FulLow = 703, .NoffUl = 27210, .IsTdd = False}},
        {38, New BandParams With {.FdlLow = 2570, .NoffDl = 37750, .FulLow = 2570, .NoffUl = 37750, .IsTdd = True}},
        {40, New BandParams With {.FdlLow = 2300, .NoffDl = 38650, .FulLow = 2300, .NoffUl = 38650, .IsTdd = True}},
        {41, New BandParams With {.FdlLow = 2496, .NoffDl = 39650, .FulLow = 2496, .NoffUl = 39650, .IsTdd = True}}
    }

    Public Function GetUlChannelFromDl(band As Integer, dlChannel As Integer) As Integer
        If Not bandParams1.ContainsKey(band) Then
            Return -1
        End If

        Dim p = bandParams1(band)

        If p.IsTdd Then
            Return dlChannel
        Else
            Dim ulChannel As Integer = (dlChannel - p.NoffDl) + p.NoffUl
            Return ulChannel
        End If
    End Function


    Public Function ComputeDlUlFrequency(band As Integer, earfcn As Integer) As (dlFreq As Double, ulFreq As Double)
        If Not bandParams1.ContainsKey(band) Then
            Return (0.0, 0.0)
        End If

        Dim p = bandParams1(band)

        Dim fdl As Double = p.FdlLow + 0.1 * (earfcn - p.NoffDl)

        Dim ful As Double
        If p.IsTdd Then
            ful = fdl
        Else
            ful = p.FulLow + 0.1 * (earfcn - p.NoffUl)
        End If

        Return (Math.Round(fdl, 4), Math.Round(ful, 4))
    End Function


End Module
