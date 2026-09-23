Attribute VB_Name = "modMain"
' Showcase of the VB6 language features Vb6ToCSharp converts: startup, types, statements, errors, strings, files.
Option Explicit

#Const TRACE_ON = 1

' ---- API
Private Declare Function GetTickCount Lib "kernel32" () As Long
Private Declare Sub Sleep Lib "kernel32" (ByVal dwMilliseconds As Long)

' ---- constants and enums
Public Const APP_TITLE As String = "Showcase", MAX_ITEMS As Long = 10
Private Const MASK = &HFF00&
Private Const RATE = 0.25

Public Enum Shade
  shLight = 1
  shDark = -1
  shMixed = &H10
  [Very Dark]
End Enum

' ---- user-defined types
Public Type Point2
  X As Double
  Y As Double
End Type

Private Type Person
  Name As String * 20
  Age As Integer
  Scores(1 To 3) As Long
  Tags() As String
  Home As Point2
End Type

' ---- module-level variables
Public gCount As Long
Private mNames() As String
Private mGrid(2, 3) As Double
Private mLog As New Collection
Private mCode As String * 8

' ---- entry point
Public Sub Main()
  Dim ok As Boolean
  ok = RunAll()
#If TRACE_ON Then
  Debug.Print APP_TITLE; " done: "; ok
#End If
  frmMain.Show
End Sub

Public Function RunAll() As Boolean
  Dim total As Long
  total = Arithmetic(7, 2) + Strings() + Arrays() + Loops(5) + Selects(3)
  total = total + Errors() + GoSubs(3) + Udts() + Dates() + Files()
  total = total + Classes()
  RunAll = total <> 0
End Function

' ---- arithmetic, operators, conversions
Public Function Arithmetic(ByVal a As Long, ByVal b As Integer) As Long
  Dim d As Double, s As Single, c As Currency, i As Integer, n&, x#
  d = a / b
  i = a \ b
  n = a Mod b
  x = 2 ^ b
  s = d * RATE
  c = CCur(d) + 1
  If a > b And Not a = 0 Then n = n + 1
  If (a Or b) = 0 Or (a Xor b) <> 0 Then n = n - 1
  n = n + (MASK And &HFF)
  i = CInt(d) + Int(s) + Fix(-1.5)
  Arithmetic = n + i + CLng(x) + Sgn(-a) + Abs(-b)
End Function

' ---- strings
Public Function Strings() As Long
  Dim s$, t As String, u As String, p As Long
  s = "Hello, " & "World"
  t = UCase$(Left$(s, 5)) & LCase(Right(s, 5)) & Mid$(s, 3, 2)
  u = Trim$("  x  ") & LTrim(" y") & RTrim("z ") & Space(2) & String(3, "-")
  p = InStr(s, "World") + InStr(1, s, "o") + InStrRev(s, "o") + Len(t) + Asc("A")
  u = Replace(u, "-", "+") & Chr(65) & Format(3.5, "0.00") & CStr(p) & StrReverse("ab")
  Mid(s, 1, 1) = "J"
  LSet mCode = "AB"
  RSet t = "right"
  If s Like "J*" Then p = p + 1
  If StrComp(s, "jello, world", vbTextCompare) = 0 Then p = p + 1
  Dim parts() As String
  parts = Split("a,b,c", ",")
  u = Join(parts, ";") & vbCrLf & vbTab
  Strings = p + UBound(parts) - LBound(parts)
End Function

' ---- arrays
Public Function Arrays() As Long
  Dim i As Long, total As Long, v As Variant
  Dim fixed(1 To 5) As Long, zero(4) As Integer, dyn() As Long
  ReDim mNames(2)
  mNames(0) = "a"
  ReDim Preserve mNames(4)
  ReDim dyn(1 To 3)
  For i = 1 To 5
    fixed(i) = i * i
  Next i
  zero(0) = 3
  mGrid(1, 2) = 1.5
  For Each v In fixed
    total = total + v
  Next
  total = total + UBound(fixed) + LBound(fixed) + zero(0)
  Erase dyn
  Arrays = total
End Function

' ---- loops, With, labels
Public Function Loops(ByVal n As Long) As Long
  Dim i As Long, j As Long, k As Long
  For i = n To 1 Step -1
    For j = 0 To 10 Step 2
      If j > 4 Then Exit For
      k = k + j
    Next j, i
  Do While k > 100
    k = k - 10
  Loop
  Do
    k = k + 1
  Loop Until k Mod 7 = 0
  Do Until k < 5
    k = k \ 2
  Loop
  While k < 20
    k = k + 3
  Wend
  With mLog
    .Add "loop"
    .Add CStr(k)
  End With
  If k = 0 Then GoTo Done
  k = k + 1
Done:
  Loops = k
End Function

' ---- Select Case
Public Function Selects(ByVal v As Long) As Long
  Dim s As String
  Select Case v
    Case 1, 2
      Selects = 10
    Case 3 To 5, Is > 100
      Selects = 20
    Case Else
      Selects = 30
  End Select
  s = "m"
  Select Case s
    Case "a" To "f"
      Selects = Selects + 1
    Case Is >= "x"
      Selects = Selects + 2
  End Select
  Select Case True
    Case v > 2
      Selects = Selects + 3
  End Select
End Function

' ---- error handling
Public Function Errors() As Long
  Dim n As Long
  On Error Resume Next
  n = 1 / 0
  If Err.Number <> 0 Then n = -Err.Number
  Err.Clear
  On Error GoTo 0
  Errors = n + Tolerant() + Retrying() + Raised()
End Function

Public Function Raised() As Long
  On Error GoTo EH
  Err.Raise 5, "Raised", "raised on purpose"
  Raised = -1
  Exit Function
EH:
  Raised = Err.Number
End Function

Private Function Tolerant() As Long
  On Error GoTo EH
10  Tolerant = CLng("x")
20  Tolerant = 5
ExitHere:
  Exit Function
EH:
  Debug.Print "Error " & Err.Number & " at " & Erl
  Resume ExitHere
End Function

Private Function Retrying() As Long
  Static attempts As Long
  On Error GoTo Retry
  attempts = 0
  Retrying = 10 \ attempts
  Exit Function
Retry:
  attempts = attempts + 1
  If attempts < 3 Then Resume
  Resume Next
End Function

' ---- GoSub / computed jumps
Public Function GoSubs(ByVal n As Long) As Long
  Dim acc As Long
  GoSub AddOne
  On n Mod 2 + 1 GoSub AddOne, AddTwo
  On n GoTo L1, L2, L3
L1:
  acc = acc + 100
L2:
  acc = acc + 10
L3:
  GoSubs = acc
  Exit Function
AddOne:
  acc = acc + 1
  Return
AddTwo:
  acc = acc + 2
  Return
End Function

' ---- user-defined types
Public Function Udts() As Long
  Dim p As Person, q As Person, pts(1) As Point2
  p.Name = "Ada"
  p.Age = 36
  p.Scores(1) = 90
  p.Home.X = 1.5
  pts(0).Y = 2
  q = p
  q.Age = 1
  Udts = p.Age + q.Age + Len(Trim(p.Name))
End Function

' ---- dates
Public Function Dates() As Long
  Dim d As Date, t As Date
  d = #1/15/2024#
  t = #10:30:00 PM#
  d = DateAdd("d", 1, d)
  Dates = Year(d) + Month(d) + Day(d) + DateDiff("d", #1/1/2024#, d) + Hour(t) + Weekday(Now)
End Function

' ---- file I/O
Public Function Files() As Long
  Dim f As Integer, path As String, s As String, n As Long
  path = Environ("TEMP") & "\showcase.txt"
  f = FreeFile
  Open path For Output As #f
  Print #f, "line one"
  Write #f, 1, "two"
  Close #f
  Open path For Append As #f
  Print #f, "a"; "b"
  Close #f
  Open path For Input As #f
  Line Input #f, s
  Do While Not EOF(f)
    Line Input #f, s
    n = n + 1
  Loop
  Close #f
  Kill path
  Files = n + Len(s)
End Function

' ---- classes
Public Function Classes() As Long
  Dim c As New CCounter, w As CWatcher, shape As IShape, ring As CCircle, it As Variant
  c.Add 2
  c.Add 3
  c.Label(1) = "first"
  Set w = New CWatcher
  w.Watch c
  c.Add 5
  Set ring = New CCircle
  ring.Radius = 2
  Set shape = ring
  For Each it In c
    Classes = Classes + it
  Next
  If TypeOf shape Is CCircle Then Classes = Classes + 1
  Classes = Classes + c.Total + CLng(shape.Area) + c(1) + w.Changes + Len(c.Label(1)) + CApp.Version
  Set c = Nothing
End Function

' ---- procedures: optional, ParamArray, ByRef
Public Function Sum(ParamArray values() As Variant) As Long
  Dim v As Variant
  For Each v In values
    Sum = Sum + v
  Next
End Function

Public Sub Swap(ByRef a As Long, ByRef b As Long)
  Dim t As Long
  t = a
  a = b
  b = t
End Sub

Public Function Greet(Optional ByVal who As String = "you") As String
  Greet = "Hi " & who
End Function
