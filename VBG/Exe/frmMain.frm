VERSION 5.00
Begin VB.Form frmMain
   BorderStyle     =   3  'Fixed Dialog
   Caption         =   "Group"
   ClientHeight    =   1575
   ClientLeft      =   45
   ClientTop       =   375
   ClientWidth     =   4320
   LinkTopic       =   "Form1"
   MaxButton       =   0   'False
   MinButton       =   0   'False
   ScaleHeight     =   1575
   ScaleWidth      =   4320
   StartUpPosition =   2  'CenterScreen
   Begin VB.CommandButton cmdClose
      Cancel          =   -1  'True
      Caption         =   "&Close"
      Height          =   375
      Left            =   2880
      TabIndex        =   1
      Top             =   1080
      Width           =   1215
   End
   Begin VB.CommandButton cmdRun
      Caption         =   "&Run"
      Default         =   -1  'True
      Height          =   375
      Left            =   1560
      TabIndex        =   0
      Top             =   1080
      Width           =   1215
   End
   Begin VB.Label lblResult
      Caption         =   "Result"
      Height          =   255
      Left            =   120
      TabIndex        =   2
      Top             =   240
      Width           =   3975
   End
End
Attribute VB_Name = "frmMain"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' Form of the EXE: calls into the referenced DLL.
Option Explicit

Private Sub Form_Load()
  Me.Caption = APP_TITLE
End Sub

Private Sub cmdRun_Click()
  Dim c As New Lib.CCircle
  c.Radius = 10
  lblResult.Caption = Owners() & " " & CStr(CLng(c.Area))
End Sub

Private Sub cmdClose_Click()
  Unload Me
End Sub
