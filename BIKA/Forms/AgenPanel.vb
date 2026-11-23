Imports MaterialSkin.Controls
Imports System.Text.Json.Nodes

Public Class AgenPanel
    Inherits UserControl
    Private ReadOnly g As DataGridView = MakeGrid()

    Public Sub New()
        Dock = DockStyle.Fill
        Dim b = Bar()
        b.Controls.Add(Btn("SEGARKAN", AddressOf Reload, True))
        b.Controls.Add(Btn("TERIMA MISI", Function() Terima(True)))
        b.Controls.Add(Btn("TOLAK MISI", Function() Terima(False), True))
        b.Controls.Add(Btn("MINTA ALAT & PERLENGKAPAN", AddressOf Minta))
        b.Controls.Add(Btn("JALANKAN SIMULASI & LAPOR HASIL", AddressOf Simulasi))
        Controls.Add(g)
        Controls.Add(b)
        AddHandler Me.Load, Async Sub(s, e) Await Guard(AddressOf Reload)
    End Sub

    Private Async Function Reload() As Task
        Bind(g, Await ListAsync("misi_agen"))
    End Function

    Private Async Function Terima(ok As Boolean) As Task
        Dim r = SelRow(g)
        Dim alasan As String = ""
        If Not ok Then
            Dim a = Ask("Tolak misi", "Alasan penolakan")
            If a Is Nothing Then Return
            alasan = a(0)
        End If
        Await Rpc("api_terima", "p_misi", Convert.ToInt32(r("id")), "p_terima", ok, "p_alasan", alasan)
        Await Reload()
    End Function

    Private Async Function Minta() As Task
        Dim r = SelRow(g)
        Dim id = Convert.ToInt32(r("id"))
        If CellText(r, "status") <> "Salvation" Then Throw New BikaException("Terima misi terlebih dahulu (status harus Salvation) sebelum meminta perlengkapan.")
        Dim items = Await ListAsync("item")
        items.Columns.Add("minta", GetType(Integer))
        Dim an = Await ListAsync("analisis", "misi_id", id)
        Dim k3 = JsonNode.Parse(an.Rows(0)("k3_cek").ToString()).AsArray()
        For Each row As DataRow In items.Rows
            row("minta") = 0
            For Each k In k3
                If k("item_id") IsNot Nothing AndAlso Convert.ToInt32(row("id")) = CInt(k("item_id").ToString()) Then row("minta") = CInt(k("qty").ToString())
            Next
        Next
        Using d As New DlgForm($"Permintaan logistik - misi #{id}", 900, 640)
            Dim gi = MakeGrid(True)
            d.Body.Controls.Add(Lbl("Syarat K3 sudah terisi. Isi kolom MINTA untuk item tambahan. Meteran Segel otomatis ditambahkan (1 per misi)."))
            gi.Size = New Size(840, 440)
            d.Body.Controls.Add(gi)
            Bind(gi, New DataView(items, "", "jenis, nama", DataViewRowState.CurrentRows).ToTable())
            For Each c As DataGridViewColumn In gi.Columns
                c.ReadOnly = (c.Name <> "minta")
            Next
            d.OkAction = Async Function()
                             Dim arr As New List(Of Object)
                             For Each rw As DataGridViewRow In gi.Rows
                                 Dim q = If(IsDBNull(rw.Cells("minta").Value), 0, Convert.ToInt32(rw.Cells("minta").Value))
                                 If q < 0 Then Throw New BikaException("Jumlah tidak boleh negatif.")
                                 If q > 0 Then arr.Add(New With {.item_id = Convert.ToInt32(rw.Cells("id").Value), .jumlah = q})
                             Next
                             Await Rpc("api_minta", "p_misi", id, "p_items", arr)
                         End Function
            If d.ShowDialog() = DialogResult.OK Then
                Info("Permintaan dikirim ke Petugas Logistik.")
                Await Reload()
            End If
        End Using
    End Function

    Private Async Function Simulasi() As Task
        Dim r = SelRow(g)
        If CellText(r, "status") <> "Exorcise" Then Throw New BikaException("Simulasi hanya tersedia setelah perlengkapan diserahkan (status Exorcise).")
        Dim id = Convert.ToInt32(r("id"))
        Dim items = Await ListAsync("item_misi", "misi_id", id)
        Dim cfg = Await ListAsync("config")
        Dim susut As Double = 1.0
        For Each row As DataRow In cfg.Rows
            If row("kunci").ToString() = "segel_susut_per_detik" Then susut = Convert.ToDouble(row("nilai"))
        Next
        Using f As New SimulasiForm(id, Convert.ToInt32(r("tier")), CellText(r, "dugaan_entitas"), CellText(r, "kelas"), items, susut)
            f.ShowDialog(Me)
        End Using
        Await Reload()
    End Function
End Class
