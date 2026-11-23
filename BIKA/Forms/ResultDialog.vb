Imports MaterialSkin.Controls

Public Class ResultDialog
    Inherits DlgForm

    Public Sub New(misiId As Integer, kelas As String, percobaanId As Integer, att As Dictionary(Of String, Object), items As DataTable)
        MyBase.New($"Laporan hasil misi #{misiId} (percobaan #{percobaanId} sebagai FINAL)", 640, 720)
        Dim menang = CBool(att("berhasil"))
        Dim chkOk As New MaterialCheckbox With {.Text = "Misi berhasil (entitas dikalahkan)", .Checked = menang, .AutoSize = True}
        Dim tLawan = Txt("Entitas yang dilawan", 580)
        Dim chkTangkap As New MaterialCheckbox With {.Text = "Entitas berhasil ditangkap", .AutoSize = True}
        Dim tNama = Txt("Nama entitas yang ditangkap", 580)
        Dim cSifat = Combo("Klasifikasi entitas tertangkap", 580)
        cSifat.Items.AddRange({"jinak (masuk koleksi Kepala Biro)", "jahat (masuk karantina)"}) : cSifat.SelectedIndex = 1
        Dim cKond = Combo("Kondisi agen setelah misi", 580)
        cKond.Items.AddRange({"Sehat", "Luka Ringan", "Luka Berat", "Trauma Berat"}) : cKond.SelectedIndex = 0
        Dim tCat = Txt("Catatan lapangan", 580)
        Dim lblRingkas As New Label With {.AutoSize = True, .MaximumSize = New Size(580, 0)}
        Dim sb As New Text.StringBuilder("Pemakaian perlengkapan pada percobaan final (sisa otomatis dikembalikan ke stok):" & vbCrLf)
        Dim pk = CType(att("pakai"), List(Of Object))
        If pk.Count = 0 Then sb.AppendLine("  (tidak ada)")
        For Each o In pk
            Dim id = CInt(o.GetType().GetProperty("item_id").GetValue(o))
            sb.AppendLine($"  - {items.Select("item_id=" & id)(0)("nama")} x{o.GetType().GetProperty("jumlah").GetValue(o)}")
        Next
        sb.AppendLine($"Segel akhir: {att("segel_akhir")}%" & If(CBool(att("segel_nol")), "  (SEGEL HABIS: jasa agen dipotong)", ""))
        If kelas = "unknown" Then sb.AppendLine(vbCrLf & "Entitas UNKNOWN: hasil akhir (berhasil/gagal, jinak/jahat) ditentukan acak oleh sistem sesuai peluang konfigurasi dan dicatat untuk audit.")
        If Not menang Then sb.AppendLine(vbCrLf & "Percobaan ini gagal: status akan menjadi Death dan jasa agen dipotong.")
        lblRingkas.Text = sb.ToString()
        Body.Controls.AddRange(New Control() {chkOk, tLawan, chkTangkap, tNama, cSifat, cKond, tCat, lblRingkas})
        OkAction = Async Function()
                       If tLawan.Text.Trim() = "" Then Throw New BikaException("Isi entitas yang dilawan.")
                       Dim h As New Dictionary(Of String, Object) From {{"percobaan_id", percobaanId}, {"berhasil", chkOk.Checked}, {"entitas_dilawan", tLawan.Text.Trim()},
                           {"ditangkap", chkTangkap.Checked}, {"nama_ditangkap", tNama.Text.Trim()}, {"sifat", If(cSifat.SelectedIndex = 0, "jinak", "jahat")},
                           {"kondisi_agen", cKond.Text}, {"catatan", tCat.Text}, {"segel_nol", att("segel_nol")}, {"pakai", att("pakai")}}
                       If chkOk.Checked <> menang Then Throw New BikaException("Hasil yang dilaporkan harus sesuai hasil percobaan final (" & If(menang, "berhasil", "gagal") & "). Ulangi simulasi bila perlu.")
                       Dim r = Await Rpc("api_selesai", "p_misi", misiId, "p_hasil", h)
                       If kelas = "unknown" Then Info("Hasil undian sistem: status akhir " & r("status").ToString())
                   End Function
    End Sub
End Class
