VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "CWatcher"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
' Handles the events of a CCounter (WithEvents).
Option Explicit

Private WithEvents mCounter As CCounter
Private mChanges As Long

Public Sub Watch(ByVal Counter As CCounter)
  Set mCounter = Counter
End Sub

Public Property Get Changes() As Long
  Changes = mChanges
End Property

Private Sub mCounter_Changed(ByVal Total As Long)
  mChanges = mChanges + 1
End Sub
