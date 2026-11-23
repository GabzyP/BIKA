Imports MaterialSkin.Controls
Imports System.Text.Json.Nodes

Public Class LogistikPanel
    Inherits UserControl
    Private ReadOnly gReq As DataGridView = MakeGrid()
    Private ReadOnly gStok As DataGridView = MakeGrid()
    Private ReadOnly gMut As DataGridView = MakeGrid()
    Private ReadOnly lblInfo As Label = Lbl("")

    Public Sub New()
        Dock = DockStyle.Fill
        Dim t = NewTabs()
        Dim b1 = Bar()
        b1.Controls.Add(Btn("SEGARKAN", AddressOf ReloadAll, True))
        b1.Controls.Add(Btn("PROSES / SERAHKAN PERMINTAAN", AddressOf Serahkan))
        Dim p1 As New Panel With {.Dock = DockStyle.Fill}
        p1.Controls.Add(gReq) : p1.Controls.Add(b1)
        Dim b2 = Bar()
        b2.Controls.Add(Btn("STOK MASUK", AddressOf StokMasuk))
        b2.Controls.Add(Btn("KOREKSI STOK", AddressOf Koreksi, True))
        b2.Controls.Add(lblInfo)
        Dim p2 As New Panel With {.Dock = DockStyle.Fill}
        p2.Controls.Add(gStok) : p2.Controls.Add(b2)
        Dim b3 = Bar()
        b3.Controls.Add(Btn("EKSPOR CSV", Function() CsvService.Export(gMut, "mutasi_stok"), True))
        Dim p3 As New Panel With {.Dock = DockStyle.Fill}
        p3.Controls.Add(gMut) : p3.Controls.Add(b3)
        t.tabs.TabPages.Add(TabPageOf("Permintaan Agen", p1))
        t.tabs.TabPages.Add(TabPageOf("Stok Alat & Perlengkapan", p2))
        t.tabs.TabPages.Add(TabPageOf("Riwayat Mutasi", p3))
        Mount(Me, t.tabs, t.selector)
        AddHandler Me.Load, Async Sub(s, e) Await Guard(AddressOf ReloadAll)
    End Sub

    Public Async Function ReloadAll() As Task
        Bind(gReq, Await ListAsync("permintaan"))
        Dim st = Await ListAsync("stok")
        Bind(gStok, st)
        For Each r As DataGridViewRow In gStok.Rows
            If CBool(r.Cells("menipis").Value) Then r.DefaultCellStyle.BackColor = Color.LemonChiffon
        Next
        lblInfo.Text = $"Item menipis: {st.Select("menipis = true").Length} (disorot kuning)"
        Bind(gMut, Await ListAsync("mutasi"))
    End Function

    Private Async Function Serahkan() As Task
        Dim r = SelRow(gReq)
        If CellText(r, "status") <> "menunggu" Then Throw New BikaException("Permintaan ini sudah diproses.")
        Dim misi = Convert.ToInt32(r("misi_id"))
        Dim dt = Await ListAsync("permintaan_item", "misi_id", misi)
        dt.Columns.Add("serahkan", GetType(Integer))
        For Each row As DataRow In dt.Rows
            row("serahkan") = Math.Min(Convert.ToInt32(row("diminta")), Convert.ToInt32(row("tersedia")))
        Next
        Using d As New DlgForm($"Penyerahan - {CellText(r, "nomor")} ({CellText(r, "agen")})", 900, 640)
            Dim gi = MakeGrid(True)
            gi.Size = New Size(840, 440)
            d.Body.Controls.Add(Lbl("Jumlah SERAHKAN diisi otomatis sesuai stok. Stok tidak akan menjadi negatif; kekurangan dicatat sebagai risiko."))
            d.Body.Controls.Add(gi)
            Bind(gi, dt)
            For Each c As DataGridViewColumn In gi.Columns
                c.ReadOnly = (c.Name <> "serahkan")
            Next
            d.OkAction = Async Function()
                             Dim arr As New List(Of Object)
                             For Each rw As DataGridViewRow In gi.Rows
                                 arr.Add(New With {.item_id = Convert.ToInt32(rw.Cells("item_id").Value), .jumlah = Convert.ToInt32(rw.Cells("serahkan").Value)})
                             Next
                             Dim res = Await Rpc("api_serahkan", "p_misi", misi, "p_items", arr)
                             Dim sb As New Text.StringBuilder("Penyerahan tercatat. Misi berstatus Exorcise.")
                             For Each k In res("kekurangan").AsArray()
                                 sb.AppendLine().Append($"KEKURANGAN: {k("item")} diminta {k("diminta")}, diserahkan {k("diserahkan")}")
                             Next
                             If res("risiko_k3") IsNot Nothing Then sb.AppendLine().Append("RISIKO K3: " & res("risiko_k3").ToString())
                             Info(sb.ToString())
                         End Function
            If d.ShowDialog() = DialogResult.OK Then Await ReloadAll()
        End Using
    End Function

    Private Async Function StokMasuk() As Task
        Dim r = SelRow(gStok)
        Dim a = Ask("Stok masuk: " & CellText(r, "nama"), "Jumlah masuk", "Alasan / sumber")
        If a Is Nothing Then Return
        Await Rpc("api_stok_masuk", "p_item", Convert.ToInt32(r("id")), "p_jumlah", CInt(Num(a(0), "Jumlah")), "p_alasan", a(1))
        Await ReloadAll()
    End Function

    Private Async Function Koreksi() As Task
        Dim r = SelRow(gStok)
        Dim a = Ask($"Koreksi stok: {CellText(r, "nama")} (sekarang {CellText(r, "stok_tersedia")})", "Stok tersedia yang benar", "Alasan koreksi (wajib)")
        If a Is Nothing Then Return
        Await Rpc("api_stok_koreksi", "p_item", Convert.ToInt32(r("id")), "p_baru", CInt(Num(a(0), "Stok")), "p_alasan", a(1))
        Await ReloadAll()
    End Function
End Class
