Attribute VB_Name = "modApp"
' Uses the referenced DLL: qualified and unqualified class names, an interface, a method called without parentheses.
Option Explicit

Public Sub Main()
  Debug.Print RunGroup(), Owners()
End Sub

Public Function RunGroup() As Double
  Dim c As Lib.CCircle
  Dim s As IShape
  Dim q As New CSquare
  Set c = New Lib.CCircle
  c.Radius = 2
  q.Side = 3
  Set s = q
  RunGroup = c.Area + s.Area
End Function

Public Function Owners() As String
  Dim c As CCircle
  Set c = New CCircle
  Owners = Version() & "+" & c.Owner
End Function
