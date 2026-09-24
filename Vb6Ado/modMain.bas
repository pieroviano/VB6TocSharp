Attribute VB_Name = "modMain"
' ADO against SQL Server LocalDB: create a GUID-named database, insert a GUID string,
' read the last row back, show it and drop the database again.
Option Explicit

Private Type GUID
  Data1 As Long
  Data2 As Integer
  Data3 As Integer
  Data4(0 To 7) As Byte
End Type

' ---- API
Private Declare Function CoCreateGuid Lib "ole32.dll" (ByRef pguid As GUID) As Long

' ---- constants
Public Const APP_TITLE As String = "Vbb6Ado"
Private Const INSTANCE_NAME As String = "(localdb)\MSSQLLocalDB"
Private Const MASTER_DB As String = "master"
Private Const TABLE_NAME As String = "Items"

' ---- entry point
Public Sub Main()
  Dim dbName As String
  Dim newValue As String
  Dim lastValue As String
  Dim failure As String

  dbName = NewGuid()
  newValue = NewGuid()

  On Error GoTo Failed
  CreateDatabase dbName
  On Error GoTo Cleanup
  CreateTable dbName
  InsertValue dbName, newValue
  lastValue = ReadLastValue(dbName)
  MsgBox "Last inserted value:" & vbCrLf & lastValue, vbInformation, APP_TITLE

Cleanup:
  If Err.Number <> 0 Then failure = Err.Description
  On Error Resume Next
  DropDatabase dbName
  On Error GoTo 0
  If Len(failure) > 0 Then MsgBox failure, vbCritical, APP_TITLE
  Exit Sub

Failed:
  MsgBox Err.Description, vbCritical, APP_TITLE
End Sub

' ---- database steps
Private Sub CreateDatabase(ByVal dbName As String)
  ExecuteSql MASTER_DB, "CREATE DATABASE " & QuoteName(dbName)
End Sub

Private Sub CreateTable(ByVal dbName As String)
  ExecuteSql dbName, "CREATE TABLE " & QuoteName(TABLE_NAME) & " (" & _
    "[Id] INT IDENTITY(1, 1) NOT NULL PRIMARY KEY, " & _
    "[Text] NVARCHAR(50) NOT NULL)"
End Sub

Private Sub InsertValue(ByVal dbName As String, ByVal text As String)
  Dim cn As ADODB.Connection
  Dim cmd As ADODB.Command

  Set cn = OpenConnection(dbName)
  Set cmd = New ADODB.Command
  Set cmd.ActiveConnection = cn
  cmd.CommandType = adCmdText
  cmd.CommandText = "INSERT INTO " & QuoteName(TABLE_NAME) & " ([Text]) VALUES (?)"
  cmd.Parameters.Append cmd.CreateParameter("Text", adVarWChar, adParamInput, 50, text)
  cmd.Execute , , adExecuteNoRecords
  Set cmd = Nothing
  CloseConnection cn
End Sub

Private Function ReadLastValue(ByVal dbName As String) As String
  Dim cn As ADODB.Connection
  Dim rs As ADODB.Recordset

  Set cn = OpenConnection(dbName)
  Set rs = New ADODB.Recordset
  rs.Open "SELECT TOP 1 [Text] FROM " & QuoteName(TABLE_NAME) & " ORDER BY [Id] DESC", _
    cn, adOpenForwardOnly, adLockReadOnly, adCmdText
  If Not rs.EOF Then ReadLastValue = CStr(rs.Fields("Text").Value)
  rs.Close
  Set rs = Nothing
  CloseConnection cn
End Function

Private Sub DropDatabase(ByVal dbName As String)
  ExecuteSql MASTER_DB, "ALTER DATABASE " & QuoteName(dbName) & " SET SINGLE_USER WITH ROLLBACK IMMEDIATE"
  ExecuteSql MASTER_DB, "DROP DATABASE " & QuoteName(dbName)
End Sub

' ---- ADO plumbing
Private Function OpenConnection(ByVal dbName As String) As ADODB.Connection
  Dim cn As ADODB.Connection

  Set cn = New ADODB.Connection
  ' OLE DB Services=-2 disables pooling, so the database can be dropped right after use.
  cn.ConnectionString = "Provider=MSOLEDBSQL;Data Source=" & INSTANCE_NAME & _
    ";Initial Catalog=" & dbName & ";Integrated Security=SSPI;OLE DB Services=-2;"
  cn.Open
  Set OpenConnection = cn
End Function

Private Sub CloseConnection(ByRef cn As ADODB.Connection)
  If cn Is Nothing Then Exit Sub
  If cn.State <> adStateClosed Then cn.Close
  Set cn = Nothing
End Sub

Private Sub ExecuteSql(ByVal dbName As String, ByVal sql As String)
  Dim cn As ADODB.Connection

  Set cn = OpenConnection(dbName)
  cn.Execute sql, , adExecuteNoRecords
  CloseConnection cn
End Sub

' ---- helpers
Private Function QuoteName(ByVal name As String) As String
  QuoteName = "[" & Replace(name, "]", "]]") & "]"
End Function

Private Function NewGuid() As String
  Dim id As GUID
  Dim tail As String
  Dim i As Long

  If CoCreateGuid(id) <> 0 Then
    Err.Raise vbObjectError + 1, APP_TITLE, "CoCreateGuid failed."
  End If
  For i = 0 To 7
    tail = tail & HexPad(id.Data4(i), 2)
    If i = 1 Then tail = tail & "-"
  Next i
  NewGuid = LCase$(HexPad(id.Data1, 8) & "-" & _
    HexPad(id.Data2 And &HFFFF&, 4) & "-" & _
    HexPad(id.Data3 And &HFFFF&, 4) & "-" & tail)
End Function

Private Function HexPad(ByVal value As Long, ByVal digits As Long) As String
  HexPad = Right$(String$(digits, "0") & Hex$(value), digits)
End Function
