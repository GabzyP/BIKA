-- Konfigurasi awal (nilai default; ubah lewat menu Master Kepala Biro)
insert into konfigurasi(kunci,nilai,ket) values
 ('uang_muka_persen',50,'Uang muka (% dari estimasi awal)'),
 ('biaya_analisis',150000,'Biaya analisis bahaya (Rp)'),
 ('darurat_ambang',8,'Kepanikan >= nilai ini memicu biaya ekspres/darurat'),
 ('darurat_persen',25,'Biaya darurat (% dari tarif dasar)'),
 ('jasa_agen_persen',40,'Honor agen (% dari tarif dasar jasa)'),
 ('penalti_gagal_persen',50,'Potongan jasa bila misi gagal (Death) (%)'),
 ('penalti_segel_persen',20,'Potongan jasa bila meteran segel mencapai nol (%)'),
 ('peluang_unknown_berhasil',55,'Peluang berhasil entitas Unknown (%)'),
 ('peluang_unknown_jinak',40,'Peluang entitas Unknown tertangkap bersifat jinak (%)'),
 ('sel_total',6,'Jumlah sel karantina'),
 ('segel_susut_per_detik',1.0,'Laju susut meteran segel pada simulasi (poin/detik, dikali faktor tier)'),
 ('segel_sel_susut_per_menit',2,'Laju susut segel sel karantina (% per menit)'),
 ('terbengkalai_menit',15,'Misi tanpa kemajuan lebih dari N menit dianggap terbengkalai');

insert into faktor_skor(kategori,kode,label,poin) values
 ('gejala','suara','Suara langkah kaki / ketukan aneh',10),
 ('gejala','bau','Bau melati, kemenyan, atau anyir tiba-tiba',15),
 ('gejala','elektronik','Lampu kedip / WiFi mati tiap jam 2 pagi',20),
 ('gejala','poltergeist','Benda melayang / pintu terbanting sendiri',30),
 ('gejala','penampakan','Penampakan wujud fisik secara jelas',40),
 ('gejala','kesurupan','Ada korban kesurupan atau serangan fisik',60),
 ('entitas','bayangan','Kelas Bayangan / Penunggu Pasif',10),
 ('entitas','unknown','Belum Teridentifikasi (Misterius)',15),
 ('entitas','poltergeist','Kelas Poltergeist (Pengganggu Objek)',25),
 ('entitas','jin','Kelas Jin Teritorial / Agresif',40),
 ('entitas','kuno','Kelas Anomali Kuno / Kutukan Berat',55),
 ('properti','kecil','Kamar Kos / Rumah Tipe Kecil',5),
 ('properti','bertingkat','Ruko / Rumah Bertingkat',15),
 ('properti','gudang','Gudang / Pabrik / Gedung Kosong',25);

insert into tier_config(tier,min_skor,max_skor,label,tarif,level_min,rehab_menit,k3) values
 (1,0,35,'Gangguan Ringan (Usil)',500000,1,2,'[{"kode":"GARAM","qty":1}]'),
 (2,36,65,'Gangguan Sedang (Waspada)',1000000,2,5,'[{"kode":"EMF","qty":1}]'),
 (3,66,95,'Gangguan Berat (Siaga)',1750000,3,10,'[{"kode":"GARAM","qty":2},{"kode":"KEMENYAN","qty":1}]'),
 (4,96,130,'Darurat Taktikal (Berbahaya)',3000000,4,20,'[{"kode":"KONTAINER","qty":1}]'),
 (5,131,1000000,'Bencana Supranatural (Kritis)',5000000,5,30,'[{"kode":"GARAM","qty":2},{"kode":"EMF","qty":1},{"kode":"KEMENYAN","qty":1},{"kode":"KONTAINER","qty":1}]');

insert into katalog_anomali(nama,kelas,ciri) values
 ('Penunggu Dapur Pasif','bayangan','Bayangan diam, tidak agresif'),
 ('Bayangan Lorong Kos','bayangan','Langkah kaki malam hari'),
 ('Entitas Belum Terklasifikasi','unknown','Ciri tidak konsisten / belum terdata'),
 ('Pengganggu Objek Rumah Tangga','poltergeist','Benda berpindah, pintu terbanting'),
 ('Jin Penjaga Wilayah Gudang','jin','Teritorial, menyerang penghuni'),
 ('Kutukan Kuno Bangunan Tua','kuno','Kutukan turun-temurun, berat');

-- Item master: alat pakai ulang (30) + 3 alat K3 tambahan; perlengkapan habis pakai (30)
insert into item(nama,jenis,satuan,harga,biaya_pakai,stok_tersedia,ambang_min)
select n,'alat','buah',10000000,75000,3,1 from unnest(array[
 'Relikui Kayu Salib Sejati','Tombak Longinus','Salib Medali Santo Benediktus','Relikui Rantai Santo Petrus','Paku Suci Konstantinus','Sangkakala Tanduk Yobel',
 'Pedupaan Thurible Bizantium','Cawan Suci Perjamuan Kudus','Tongkat Gembala Santo Patrik','Lonceng Altar Sanctus Vatikan','Relikui Jubah Suci Trier','Cermin Perak Speculum Veritatis',
 'Perisai Salib Ksatria Templar','Lentera Katakomba Roma','Tabut Perjanjian Konsekrasi','Tongkat Harun','Rosario Kayu Zaitun Yerusalem','Relikui Kerudung Veronika',
 'Salib Patriarkat Yerusalem','Kandelabr Bait Suci','Aspersorium Perak Vatikan','Tasbih Kayu Kokka 99','Mangkuk Tembaga Rajah Syifa','Bilah Baja Damaskus Berukir',
 'Cincin Segel Sulaiman','Tongkat Kayu Zaitun Baitul Maqdis','Lentera Kuningan Masjidil Aqsa','Bejana Tembaga Air Zamzam','Cermin Perunggu Andalusia','Astrolab Kuningan Bagdad']) n;
insert into item(nama,jenis,satuan,harga,biaya_pakai,stok_tersedia,ambang_min,kode_k3,aturan) values
 ('EMF Meter','alat','buah',2500000,50000,3,1,'EMF','Syarat K3 Tier 2'),
 ('Kontainer Penahan','alat','buah',15000000,150000,3,1,'KONTAINER','Syarat K3 Tier 4'),
 ('Meteran Segel','alat','buah',1500000,25000,10,2,'METERAN','Wajib 1 per misi');
insert into item(nama,jenis,satuan,harga,biaya_pakai,stok_tersedia,ambang_min)
select n,'perlengkapan','unit',30000,30000,25,5 from unnest(array[
 'Air Suci Sungai Yordan','Garam Eksorsisme Rituale Romanum','Dupa Kemenyan Frankincense','Minyak Urapan Mur dan Narwastu','Lilin Paskah Lebah Murni','Kapur Epifani Konsekrasi',
 'Anggur Perjamuan Ekaristi','Abu Rabu Konsekrasi','Daun Palma Minggu Palma','Minyak Krisma Kudus','Hosti Tidak Beragi Konsekrasi','Air Suci Lourdes','Tanah Suci Golgota',
 'Getah Kemenyan Olibanum','Minyak Santo Benediktus','Benang Merah Segel Yerikho','Lilin Novena Putih','Air Suci Basilika Santo Petrus','Kelopak Mawar Santa Theresia',
 'Perkamen Doa Segel Vellum','Garam Laut Mati Konsekrasi','Air Zamzam Mekah','Serbuk Daun Bidara Sidr','Kayu Gaharu Oud','Minyak Kasturi Kijang Murni','Buhur Kemenyan Arab',
 'Air Hujan Malam Lailatul Qadar','Minyak Zaitun Bukit Tursina','Garam Bukit Ruqyah','Tanah Suci Lembah Bakka']) n;
-- Pemetaan syarat K3 (Kepala Biro dapat mengubah kolom kode_k3 di menu Master > item)
update item set kode_k3='GARAM' where nama='Garam Eksorsisme Rituale Romanum';
update item set kode_k3='KEMENYAN' where nama='Dupa Kemenyan Frankincense';
update item set kode_k3='SEGEL_REHAB', aturan='Penjaga segel sel/rehabilitasi' where nama='Benang Merah Segel Yerikho';
-- Stok awal tercatat sebagai transaksi
insert into stok_transaksi(item_id,jenis,jumlah,sebelum,sesudah,alasan) select id,'masuk',stok_tersedia,0,stok_tersedia,'Stok awal' from item;
