Imports System.IO
Imports System.Text.Json

Public Module AppConfig
    Public Url As String
    Public AnonKey As String

    Public Sub Load()
        Dim p = Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        If Not File.Exists(p) Then Throw New FileNotFoundException("appsettings.json tidak ditemukan di folder aplikasi.")
        Using doc = JsonDocument.Parse(File.ReadAllText(p))
            Url = doc.RootElement.GetProperty("SupabaseUrl").GetString().TrimEnd("/"c)
            AnonKey = doc.RootElement.GetProperty("SupabaseAnonKey").GetString()
        End Using
        If Url.Contains("GANTI") OrElse AnonKey.Contains("GANTI") Then Throw New InvalidOperationException("appsettings.json belum diisi URL/anon key Supabase.")
    End Sub
End Module
