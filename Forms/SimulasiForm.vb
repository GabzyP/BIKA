Imports MaterialSkin
Imports MaterialSkin.Controls

''' <summary>Simulasi misi: progress perlawanan, timer, meteran segel otomatis menurun. TIDAK ADA tombol jeda; bisa diulang.
''' Pemakaian di simulasi bersifat virtual (dibatasi jumlah yang diserahkan); stok/biaya baru berubah saat percobaan FINAL dilaporkan.</summary>
Public Class SimulasiForm
    Inherits MaterialForm
    Private ReadOnly misiId As Integer, tier As Integer, kelas As String, susutBase As Double
    Private ReadOnly items As DataTable
    Private ReadOnly pbRes As New ProgressBar With {.Width = 520, .Height = 24, .Maximum = 100}
    Private ReadOnly pbSegel As New ProgressBar With {.Width = 520, .Height = 24, .Maximum = 100}
    Private ReadOnly lblWaktu As Label = Lbl("Waktu: -", True)
    Private ReadOnly lblStatus As Label = Lbl("Tekan MULAI untuk menjalankan simulasi.")
    Private ReadOnly lstLog As New ListBox With {.Width = 520, .Height = 170}
    Private ReadOnly flAksi As New FlowLayoutPanel With {.Width = 880, .Height = 150, .AutoScroll = True}
    Private ReadOnly tm As New Timer With {.Interval = 1000}
    Private ReadOnly rnd As New Random()
    Private ReadOnly sisa As New Dictionary(Of Integer, Integer)
    Private ReadOnly pakai As New Dictionary(Of Integer, Integer)
    Private ReadOnly cooldown As New Dictionary(Of Button, Integer)
    Private resist As Double, segel As Double, sisaDetik As Integer, durasi As Integer, waktuMulai As DateTime
    Private running As Boolean, segelNol As Boolean
    Private ReadOnly btnMulai As MaterialButton, btnFinal As MaterialButton
    Private lastAttempt As Dictionary(Of String, Object), lastId As Integer

    Public Sub New(misi As Integer, tierMisi As Integer, dugaan As String, kelasEntitas As String, itm As DataTable, susut As Double)
        misiId = misi : tier = tierMisi : kelas = kelasEntitas : items = itm : susutBase = susut
        Text = $"Simulasi Misi #{misi} - Tier {tier} - {dugaan}"
        Width = 940 : Height = 760
        StartPosition = FormStartPosition.CenterParent
        Sizable = False
        MaterialSkinManager.Instance.AddFormToManage(Me)
        btnMulai = Btn("MULAI / ULANGI SIMULASI", AddressOf Mulai)
        btnFinal = Btn("JADIKAN PERCOBAAN FINAL & LAPOR HASIL", AddressOf Finalkan)
        btnFinal.Enabled = False
        Dim p As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Padding = New Padding(10)}
        p.Controls.AddRange(New Control() {lblWaktu, Lbl("Perlawanan entitas (turun = menang):"), pbRes, Lbl("Meteran Segel (turun otomatis):"), pbSegel, lblStatus, Lbl("Aksi (alat: cooldown; perlengkapan: habis pakai & menambah segel):", True), flAksi, lstLog})
        Dim bb As New FlowLayoutPanel With {.AutoSize = True}
        bb.Controls.Add(btnMulai)
        bb.Controls.Add(btnFinal)
        p.Controls.Add(bb)
        Controls.Add(p)
        AddHandler tm.Tick, AddressOf Tick
        AddHandler Me.FormClosing, Sub(s, e)
                                       tm.Stop()
                                   End Sub
        BuildActions()
    End Sub

    Private Sub BuildActions()
        flAksi.Controls.Clear()
        For Each r As DataRow In items.Rows
            If r("kode_k3").ToString() = "METERAN" Then Continue For
            Dim idItem = Convert.ToInt32(r("item_id"))
            Dim jenis = r("jenis").ToString()
            Dim b As New Button With {.AutoSize = True, .Tag = idItem, .Margin = New Padding(3), .Enabled = False}
            b.Name = jenis
            AddHandler b.Click, Sub(s, e) Aksi(b, r("nama").ToString())
            flAksi.Controls.Add(b)
        Next
        RefreshLabels()
    End Sub

    Private Sub RefreshLabels()
        For Each b As Button In flAksi.Controls
            Dim idItem = CInt(b.Tag)
            Dim nama = items.Select("item_id=" & idItem)(0)("nama").ToString()
            If b.Name = "alat" Then b.Text = nama & " (alat)" Else b.Text = $"{nama} (sisa {If(sisa.ContainsKey(idItem), sisa(idItem), 0)})"
        Next
    End Sub

    Private Async Function Mulai() As Task
        If running Then
            Throw New BikaException("Simulasi tidak dapat dijeda. Tunggu hingga selesai, lalu ulangi.")
        End If
        durasi = 40 + tier * 20 : sisaDetik = durasi : resist = 100 : segel = 100 : segelNol = False : running = True
        sisa.Clear() : pakai.Clear() : cooldown.Clear() : lstLog.Items.Clear()
        For Each r As DataRow In items.Rows
            sisa(Convert.ToInt32(r("item_id"))) = Convert.ToInt32(r("diserahkan"))
        Next
        waktuMulai = DateTime.Now
        For Each b As Button In flAksi.Controls
            b.Enabled = True
        Next
        btnFinal.Enabled = False
        lblStatus.Text = "Simulasi berjalan..."
        RefreshLabels() : UpdateBars()
        tm.Start()
        Await Task.CompletedTask
    End Function

    Private Sub Aksi(b As Button, nama As String)
        If Not running Then Return
        Dim idItem = CInt(b.Tag)
        If b.Name = "alat" Then
            Dim d = 6 + rnd.Next(0, 7)
            resist -= d : lstLog.Items.Insert(0, $"{nama}: perlawanan -{d}")
            b.Enabled = False : cooldown(b) = 2
        Else
            If Not sisa.ContainsKey(idItem) OrElse sisa(idItem) <= 0 Then
                lstLog.Items.Insert(0, $"{nama}: stok yang diserahkan habis.")
                Return
            End If
            sisa(idItem) -= 1 : pakai(idItem) = If(pakai.ContainsKey(idItem), pakai(idItem) + 1, 1)
            Dim d = 15 + rnd.Next(0, 11)
            resist -= d : segel = Math.Min(100, segel + 8)
            lstLog.Items.Insert(0, $"{nama}: perlawanan -{d}, segel +8")
            RefreshLabels()
        End If
        UpdateBars()
        If resist <= 0 Then Selesai(True)
    End Sub

    Private Sub Tick(s As Object, e As EventArgs)
        If Not running Then Return
        sisaDetik -= 1
        resist = Math.Min(100, resist + 0.5 + 0.2 * tier)
        segel = Math.Max(0, segel - susutBase * (0.3 + 0.1 * tier))
        If segel <= 0 AndAlso Not segelNol Then
            segelNol = True
            lstLog.Items.Insert(0, "!! METERAN SEGEL HABIS - risiko meningkat, jasa agen akan dipotong sesuai aturan (misi tetap berlangsung)")
            lblStatus.Text = "PERINGATAN: segel habis! Bayaran jasa dipotong."
        End If
        For Each b As Button In New List(Of Button)(cooldown.Keys)
            cooldown(b) -= 1
            If cooldown(b) <= 0 Then
                b.Enabled = True
                cooldown.Remove(b)
            End If
        Next
        UpdateBars()
        If sisaDetik <= 0 Then Selesai(resist <= 0)
    End Sub

    Private Sub UpdateBars()
        pbRes.Value = CInt(Math.Max(0, Math.Min(100, resist)))
        pbSegel.Value = CInt(Math.Max(0, Math.Min(100, segel)))
        lblWaktu.Text = $"Waktu tersisa: {Math.Max(0, sisaDetik)} dtk dari {durasi} dtk"
    End Sub

    Private Async Sub Selesai(menang As Boolean)
        running = False
        tm.Stop()
        For Each b As Button In flAksi.Controls
            b.Enabled = False
        Next
        UpdateBars()
        lblStatus.Text = If(menang, "ENTITAS DIKALAHKAN (percobaan berhasil).", "WAKTU HABIS - misi gagal pada percobaan ini.")
        Dim lst As New List(Of Object)
        For Each kv In pakai
            lst.Add(New With {.item_id = kv.Key, .jumlah = kv.Value})
        Next
        lastAttempt = New Dictionary(Of String, Object) From {{"berhasil", menang}, {"durasi", CInt((DateTime.Now - waktuMulai).TotalSeconds)}, {"segel_akhir", CInt(Math.Max(0, segel))},
            {"resistensi_akhir", CInt(Math.Max(0, resist))}, {"segel_nol", segelNol}, {"pakai", lst}}
        Await Guard(Async Function()
                        lastId = CInt((Await Rpc("api_simpan_percobaan", "p_misi", misiId, "p_data", lastAttempt)).ToString())
                        lstLog.Items.Insert(0, $"Percobaan #{lastId} tersimpan.")
                        btnFinal.Enabled = True
                    End Function)
    End Sub

    Private Async Function Finalkan() As Task
        If lastAttempt Is Nothing OrElse running Then Throw New BikaException("Selesaikan satu percobaan terlebih dahulu.")
        Using d As New ResultDialog(misiId, kelas, lastId, lastAttempt, items)
            If d.ShowDialog(Me) = DialogResult.OK Then
                Info("Hasil misi tersimpan. Status, stok, invoice, dan karantina/koleksi telah diperbarui.")
                Close()
            End If
        End Using
        Await Task.CompletedTask
    End Function
End Class
