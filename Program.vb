Imports MaterialSkin

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Dim sm = MaterialSkinManager.Instance
        sm.Theme = MaterialSkinManager.Themes.LIGHT
        sm.ColorScheme = New ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.Teal400, TextShade.WHITE)
        Try
            AppConfig.Load()
            Db.InitAsync().GetAwaiter().GetResult()
        Catch ex As Exception
            MessageBox.Show("Gagal menginisialisasi aplikasi: " & ex.Message & vbCrLf & "Periksa appsettings.json dan koneksi internet.", "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try
        Do
            Using lf As New LoginForm()
                If lf.ShowDialog() <> DialogResult.OK Then Exit Do
            End Using
            Dim mf As New MainForm()
            Application.Run(mf)
            If Not mf.Logout Then Exit Do
        Loop
    End Sub
End Module
