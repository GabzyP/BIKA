-- 1) Buat 4 pengguna di Supabase Dashboard > Authentication > Users (centang Auto Confirm):
--    kepala@bika.local, operator@bika.local, logistik@bika.local, agen1@bika.local  (ganti password sendiri)
-- 2) Jalankan skrip ini untuk memberi peran.
insert into profiles(id,nama,role) select id,'Kepala Biro','kepala' from auth.users where email='kepala@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Operator Satu','operator' from auth.users where email='operator@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Petugas Logistik','logistik' from auth.users where email='logistik@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Agen Satu','agen' from auth.users where email='agen1@bika.local' on conflict do nothing;
insert into agen(profile_id,nama,level,spesialisasi) select id,'Agen Satu',5,'Umum' from profiles where role='agen' on conflict do nothing;
-- Agen tambahan: buat user auth lalu insert profiles (role 'agen') dan baris agen, atau lewat menu Master > agen (isi profile_id).
