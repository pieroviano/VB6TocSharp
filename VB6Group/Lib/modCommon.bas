Attribute VB_Name = "modCommon"
' Standard module of the DLL: same name as the App's, private to each project.
Option Explicit

Public Function Version() As String
  Version = "Lib"
End Function

Public Function Round3(ByVal x As Double) As Double
  Round3 = Round(x, 3)
End Function
