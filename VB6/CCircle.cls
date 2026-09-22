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
Attribute VB_Exposed = False
' Implements IShape.
Option Explicit
Implements IShape

Private mRadius As Double

Public Property Get Radius() As Double
  Radius = mRadius
End Property

Public Property Let Radius(ByVal Value As Double)
  mRadius = Value
End Property

Private Function IShape_Area() As Double
  IShape_Area = 3.14159 * mRadius ^ 2
End Function

Private Property Get IShape_Name() As String
  IShape_Name = "circle"
End Property
