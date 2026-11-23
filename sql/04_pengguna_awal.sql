insert into profiles(id,nama,role) select id,'Kepala Biro','kepala' from auth.users where email='kepala@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Operator Satu','operator' from auth.users where email='operator@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Petugas Logistik','logistik' from auth.users where email='logistik@bika.local' on conflict do nothing;
insert into profiles(id,nama,role) select id,'Agen Satu','agen' from auth.users where email='agen1@bika.local' on conflict do nothing;
insert into agen(profile_id,nama,level,spesialisasi) select id,'Agen Satu',5,'Umum' from profiles where role='agen' on conflict do nothing;
