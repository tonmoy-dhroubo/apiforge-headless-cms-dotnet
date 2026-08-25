INSERT INTO roles (id,name,description) VALUES
 (1,'SUPER_ADMIN','Full system access'),(2,'ADMIN','Administrative access'),(3,'MODERATOR','Content moderation access'),(4,'REGISTERED','Default registered user'),(5,'PUBLIC','Unauthenticated access role') ON CONFLICT DO NOTHING;
INSERT INTO permissions (id,name,description) VALUES
 (1,'MANAGE_USERS','Create and manage users'),(2,'MANAGE_CONTENT_TYPES','Create and manage content types'),(3,'MANAGE_CONTENT','Create and manage content entries'),(4,'VIEW_CONTENT','Read content entries'),(5,'MANAGE_MEDIA','Upload and manage media'),(6,'MANAGE_PERMISSIONS','Configure permissions') ON CONFLICT DO NOTHING;
INSERT INTO users (id,username,email,password,firstname,lastname,enabled) VALUES
 (1,'super_admin','super_admin@apiforge.com',crypt('password123',gen_salt('bf')),'Super','Admin',TRUE),
 (2,'admin','admin@apiforge.com',crypt('password123',gen_salt('bf')),'System','Admin',TRUE),
 (3,'moderator','moderator@apiforge.com',crypt('password123',gen_salt('bf')),'Content','Moderator',TRUE),
 (4,'jane','jane.doe@apiforge.com',crypt('password123',gen_salt('bf')),'Jane','Doe',TRUE) ON CONFLICT DO NOTHING;
INSERT INTO user_roles(user_id,role_id) VALUES (1,1),(1,2),(2,2),(3,3),(4,4) ON CONFLICT DO NOTHING;
SELECT setval(pg_get_serial_sequence('users','id'),COALESCE((SELECT MAX(id) FROM users),1),true);
SELECT setval(pg_get_serial_sequence('roles','id'),COALESCE((SELECT MAX(id) FROM roles),1),true);
