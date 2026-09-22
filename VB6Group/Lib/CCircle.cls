VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "CCircle"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
' Public class of the ActiveX DLL: uses the DLL's private class and standard module.
Option Explicit

Private mRadius As Double

Public Property Get Radius() As Double
  Radius = mRadius
End Property

Public Property Let Radius(ByVal Value As Double)
  mRadius = Value
End Property

Public Function Area() As Double
  Dim h As New CHelper
  Area = Round3(h.Twice(1.570795) * mRadius * mRadius)
End Function

Public Property Get Owner() As String
  Owner = Version()
End Property
