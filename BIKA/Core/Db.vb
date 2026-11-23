Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Text.RegularExpressions

Public Class BikaException
    Inherits Exception
    Public Sub New(msg As String)
        MyBase.New(msg)
    End Sub
End Class

''' <summary>Akses data: login lewat package Supabase (anon key + JWT pengguna); semua operasi data lewat fungsi RPC api_* (hak akses dicek di server).</summary>
Public Module Db
    Public Client As Supabase.Client
    Private ReadOnly Http As New HttpClient With {.Timeout = TimeSpan.FromSeconds(30)}
    Public Role As String
    Public NamaUser As String
    Public AgenId As Integer

    Public Async Function InitAsync() As Task
        Dim opt As New Supabase.SupabaseOptions With {.AutoRefreshToken = True, .AutoConnectRealtime = False}
        Client = New Supabase.Client(AppConfig.Url, AppConfig.AnonKey, opt)
        Await Client.InitializeAsync()
    End Function

    Public Async Function LoginAsync(email As String, pass As String) As Task
        Try
            Await Client.Auth.SignIn(email, pass)
        Catch ex As HttpRequestException
            Throw New BikaException("Koneksi ke server gagal. Periksa internet Anda.")
        Catch ex As Exception
            If ex.Message.ToLower().Contains("invalid") OrElse ex.Message.Contains("400") Then Throw New BikaException("Email atau password salah.")
            Throw New BikaException("Login gagal: " & ex.Message)
        End Try
        Dim me1 = Await Rpc("api_me")
        Role = me1("role").ToString()
        NamaUser = me1("nama").ToString()
        AgenId = If(me1("agen_id") Is Nothing, 0, CInt(me1("agen_id").ToString()))
    End Function

    Public Async Function LogoutAsync() As Task
        Try
            Await Client.Auth.SignOut()
        Catch
        End Try
        Role = Nothing
    End Function

    Public Function Params(ParamArray kv() As Object) As Dictionary(Of String, Object)
        Dim d As New Dictionary(Of String, Object)
        For i = 0 To kv.Length - 2 Step 2
            d(CStr(kv(i))) = kv(i + 1)
        Next
        Return d
    End Function

    Public Async Function Rpc(fn As String, ParamArray kv() As Object) As Task(Of JsonNode)
        Dim session = Client.Auth.CurrentSession
        If session Is Nothing Then Throw New BikaException("Sesi berakhir. Silakan login ulang.")
        Dim req As New HttpRequestMessage(HttpMethod.Post, $"{AppConfig.Url}/rest/v1/rpc/{fn}")
        req.Headers.Add("apikey", AppConfig.AnonKey)
        req.Headers.Authorization = New Headers.AuthenticationHeaderValue("Bearer", session.AccessToken)
        req.Content = New StringContent(JsonSerializer.Serialize(Params(kv)), Encoding.UTF8, "application/json")
        Dim resp As HttpResponseMessage
        Try
            resp = Await Http.SendAsync(req)
        Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is TaskCanceledException
            Throw New BikaException("Koneksi ke server gagal atau timeout. Data belum tersimpan; coba lagi.")
        End Try
        Dim body = Await resp.Content.ReadAsStringAsync()
        If Not resp.IsSuccessStatusCode Then
            Dim msg = body
            Try
                msg = JsonNode.Parse(body)("message").ToString()
            Catch
            End Try
            If resp.StatusCode = Net.HttpStatusCode.Unauthorized Then msg = "Sesi berakhir. Silakan login ulang. (" & msg & ")"
            Throw New BikaException(msg)
        End If
        If String.IsNullOrWhiteSpace(body) Then Return Nothing
        Return JsonNode.Parse(body)
    End Function

    ''' <summary>Daftar/laporan: api_list(nama, args) menjadi DataTable bertipe.</summary>
    Public Async Function ListAsync(nama As String, ParamArray kv() As Object) As Task(Of DataTable)
        Dim n = Await Rpc("api_list", "p_nama", nama, "p_args", Params(kv))
        Return ToTable(n)
    End Function

    Public Function ToTable(node As JsonNode) As DataTable
        Dim dt As New DataTable()
        Dim arr = TryCast(node, JsonArray)
        If arr Is Nothing OrElse arr.Count = 0 Then Return dt
        Dim cols As New List(Of String)
        For Each row In arr
            For Each kv In row.AsObject()
                If Not cols.Contains(kv.Key) Then cols.Add(kv.Key)
            Next
        Next
        For Each c In cols
            Dim t As Type = GetType(String)
            For Each row In arr
                Dim v = row(c)
                If v Is Nothing Then Continue For
                Dim je = v.GetValue(Of JsonElement)()
                Select Case je.ValueKind
                    Case JsonValueKind.Number : t = GetType(Decimal)
                    Case JsonValueKind.True, JsonValueKind.False : t = GetType(Boolean)
                    Case JsonValueKind.String
                        If Regex.IsMatch(je.GetString(), "^\d{4}-\d\d-\d\dT") Then t = GetType(DateTime)
                End Select
                Exit For
            Next
            dt.Columns.Add(c, t)
        Next
        For Each row In arr
            Dim dr = dt.NewRow()
            For Each c In cols
                Dim v = row(c)
                If v Is Nothing Then
                    dr(c) = DBNull.Value
                    Continue For
                End If
                Dim je = v.GetValue(Of JsonElement)()
                Dim t = dt.Columns(c).DataType
                Try
                    If t Is GetType(Decimal) Then
                        dr(c) = je.GetDecimal()
                    ElseIf t Is GetType(Boolean) Then
                        dr(c) = je.GetBoolean()
                    ElseIf t Is GetType(DateTime) Then
                        dr(c) = DateTime.Parse(je.GetString(), Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind).ToLocalTime()
                    Else
                        dr(c) = If(je.ValueKind = JsonValueKind.String, je.GetString(), je.ToString())
                    End If
                Catch
                    dr(c) = DBNull.Value
                End Try
            Next
            dt.Rows.Add(dr)
        Next
        Return dt
    End Function

    Public Function Rp(v As Object) As String
        If v Is Nothing OrElse IsDBNull(v) Then Return "-"
        Return "Rp " & Convert.ToDecimal(v).ToString("N0", New Globalization.CultureInfo("id-ID"))
    End Function
End Module
