VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "CCounter"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
' Collection class: events, default member, For Each, properties with parameters, Initialize / Terminate.
Option Explicit

Public Event Changed(ByVal Total As Long)

Private mItems As Collection
Private mLabels(1 To 5) As String
Private mTotal As Long

Private Sub Class_Initialize()
  Set mItems = New Collection
End Sub

Private Sub Class_Terminate()
  Set mItems = Nothing
End Sub

Public Sub Add(ByVal Value As Long)
  mItems.Add Value
  mTotal = mTotal + Value
  RaiseEvent Changed(mTotal)
End Sub

Public Property Get Total() As Long
  Total = mTotal
End Property

Public Property Get Count() As Long
  Count = mItems.Count
End Property

Public Property Get Item(ByVal Index As Variant) As Variant
Attribute Item.VB_UserMemId = 0
  Item = mItems(Index)
End Property

Public Property Get NewEnum() As IUnknown
Attribute NewEnum.VB_UserMemId = -4
  Set NewEnum = mItems.[_NewEnum]
End Property

Public Property Get Label(ByVal Index As Long) As String
  Label = mLabels(Index)
End Property

Public Property Let Label(ByVal Index As Long, ByVal Value As String)
  mLabels(Index) = Value
End Property

Friend Sub Reset()
  mTotal = 0
End Sub
