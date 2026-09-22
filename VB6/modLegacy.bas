Attribute VB_Name = "modLegacy"
' Legacy module: no Option Explicit, Option Base 1, Option Compare Text, DefType, VB Migration Partner pragmas.
'## AutoDispose Yes
Option Base 1
Option Compare Text
DefLng I-N
DefStr S

Public Function Legacy() As Long
  Dim list(3) As Long
  list(1) = 7
  count = 2
  sName = "abc"
  If sName = "ABC" Then count = count + 1
  If InStr(sName, "B") > 0 Then count = count + 1
  Legacy = count + list(1) + LBound(list)
End Function

Public Function Pragmas() As Long
  Dim n As Long
  '## InsertStatement n = 40;
  '## ReplaceStatement n = n + 2;
  n = -1
  '## Note checked by hand after the conversion
  Pragmas = n
End Function
