Imports CsvHelper
Imports System.Globalization
Imports System.IO

Public Module CsvService
    Public Function Export(g As DataGridView, nama As String) As Task
        If g.DataSource Is Nothing OrElse g.Rows.Count = 0 Then Throw New BikaException("Tidak ada data untuk diekspor.")
        Using sfd As New SaveFileDialog With {.Filter = "CSV|*.csv", .FileName = $"{nama}_{DateTime.Now:yyyyMMdd_HHmm}.csv"}
            If sfd.ShowDialog() <> DialogResult.OK Then Return Task.CompletedTask
            Using w As New StreamWriter(sfd.FileName, False, New Text.UTF8Encoding(True))
                Using csv = CsvWriter.Create(w, CsvWriterOptions.Default, True)
                    For Each c As DataGridViewColumn In g.Columns
                        csv.WriteField(c.Name)
                    Next
                    csv.EndRecord()
                    For Each r As DataGridViewRow In g.Rows
                        For Each c As DataGridViewColumn In g.Columns
                            csv.WriteField(r.Cells(c.Index).Value)
                        Next
                        csv.EndRecord()
                    Next
                End Using
            End Using
            Info("Berkas disimpan: " & sfd.FileName)
        End Using
        Return Task.CompletedTask
    End Function
End Module
