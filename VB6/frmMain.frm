VERSION 5.00
Begin VB.Form frmMain
   BorderStyle     =   3  'Fixed Dialog
   Caption         =   "Showcase"
   ClientHeight    =   4455
   ClientLeft      =   45
   ClientTop       =   375
   ClientWidth     =   6120
   LinkTopic       =   "Form1"
   MaxButton       =   0   'False
   MinButton       =   0   'False
   ScaleHeight     =   4455
   ScaleWidth      =   6120
   StartUpPosition =   2  'CenterScreen
   Begin VB.Timer tmrTick
      Enabled         =   0   'False
      Interval        =   1000
      Left            =   5520
      Top             =   3840
   End
   Begin VB.ComboBox cboShade
      Height          =   345
      Left            =   120
      Style           =   2  'Dropdown List
      TabIndex        =   7
      Top             =   1260
      Width           =   2415
   End
   Begin VB.ListBox lstLog
      Height          =   1635
      Left            =   2760
      TabIndex        =   6
      Top             =   120
      Width           =   3255
   End
   Begin VB.Frame fraMode
      Caption         =   "Mode"
      Height          =   975
      Left            =   120
      TabIndex        =   3
      Top             =   1845
      Width           =   2415
      Begin VB.OptionButton optB
         Caption         =   "Detailed"
         Height          =   255
         Left            =   120
         TabIndex        =   5
         Top             =   600
         Width           =   2055
      End
      Begin VB.OptionButton optA
         Caption         =   "Simple"
         Height          =   255
         Left            =   120
         TabIndex        =   4
         Top             =   240
         Value           =   -1  'True
         Width           =   2055
      End
   End
   Begin VB.TextBox txtName
      Height          =   345
      Left            =   120
      TabIndex        =   0
      Text            =   "World"
      Top             =   600
      Width           =   2415
   End
   Begin VB.CommandButton cmdClose
      Cancel          =   -1  'True
      Caption         =   "&Close"
      Height          =   375
      Left            =   4680
      TabIndex        =   2
      Top             =   3780
      Width           =   1215
   End
   Begin VB.CommandButton cmdRun
      Caption         =   "&Run"
      Default         =   -1  'True
      Height          =   375
      Left            =   3360
      TabIndex        =   1
      Top             =   3780
      Width           =   1215
   End
   Begin VB.Label lblResult
      Caption         =   "Result"
      Height          =   255
      Left            =   120
      TabIndex        =   8
      Top             =   3060
      Width           =   5895
   End
   Begin VB.Label lblName
      Caption         =   "Name:"
      Height          =   255
      Left            =   120
      TabIndex        =   9
      Top             =   240
      Width           =   2415
   End
End
Attribute VB_Name = "frmMain"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' Form: intrinsic controls, events, control properties and methods used from code.
Option Explicit

Private mRuns As Long

Private Sub Form_Load()
  Me.Caption = APP_TITLE
  cboShade.AddItem "Light"
  cboShade.AddItem "Dark"
  cboShade.ListIndex = 0
  optA.Value = True
  txtName.Text = Greet("VB6")
End Sub

Private Sub cmdRun_Click()
  mRuns = mRuns + 1
  If RunAll() Then lstLog.AddItem "Run " & mRuns
  lblResult.Caption = CStr(Legacy() + Pragmas())
  If optB.Value Then lstLog.AddItem txtName.Text
  tmrTick.Enabled = True
  cmdClose.Enabled = True
End Sub

Private Sub cmdClose_Click()
  Unload Me
End Sub

Private Sub txtName_Change()
  cmdRun.Enabled = Len(txtName.Text) > 0
End Sub

Private Sub lstLog_Click()
  If lstLog.ListIndex >= 0 Then lblResult.Caption = lstLog.List(lstLog.ListIndex)
End Sub

Private Sub tmrTick_Timer()
  tmrTick.Enabled = False
  lblResult.Caption = lstLog.ListCount & " runs"
End Sub
